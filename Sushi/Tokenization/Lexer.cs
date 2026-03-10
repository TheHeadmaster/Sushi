using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Serilog;
using Sushi.Diagnostics;
using Sushi.Diagnostics.Errors;

namespace Sushi.Tokenization;

/// <summary>
/// Handles the scanning and lexing of source files into tokens.
/// </summary>
public sealed partial class Lexer
{
    /// <summary>
    /// Lexes the source files (.sus) in the project path.
    /// </summary>
    /// <param name="projectPath">
    /// The project path.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task" /> that returns a <see cref="List{T}" /> of <see cref="TokenFile" /> objects.
    /// </returns>
    public async Task<List<TokenFile>> LexFiles([NotNull] string projectPath) => await Diag.MonitorAsync("Lexing", async () =>
    {
        List<Task<TokenFile>> fileTasks = [];

        foreach (string file in Directory.EnumerateFiles(projectPath, "*.sus", SearchOption.AllDirectories))
        {
            fileTasks.Add(Task.Run(() => LexFile(file)));
        }

        List<TokenFile> sourceFiles = [.. await Task.WhenAll(fileTasks)];

        int errorCount = sourceFiles.SelectMany(file => file.Messages).Count(message => message.Type is CompilerMessageType.Error);
        int warningCount = sourceFiles.SelectMany(file => file.Messages).Count(message => message.Type is CompilerMessageType.Warning);

        foreach (CompilerMessage? message in sourceFiles.SelectMany(file => file.Messages).OrderBy(x => x.Type))
        {
            await message.LogMessage();
        }

        Log.Information("{FileCount} files were lexed with {ErrorCount} syntax errors and {WarningCount} warnings.", sourceFiles.Count, errorCount, warningCount);

        if (errorCount > 0)
        {
            Program.Exit(ExitCode.LexingSyntaxError);
        }

        return sourceFiles;
    });

    /// <summary>
    /// Loads the specified file from disk and translates it into a <see cref="TokenFile"/>.
    /// </summary>
    /// <param name="sourceFilePath">
    /// The path of the file to convert.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns the converted <see cref="TokenFile"/>.
    /// </returns>
    private static async Task<TokenFile> LexFile([NotNull] string sourceFilePath)
    {
        string source = await File.ReadAllTextAsync(sourceFilePath);

        TokenFile file = new()
        {
            FileName = Path.GetFileName(sourceFilePath),
            FilePath = sourceFilePath,
            RawSourceCode = source
        };

        while (!file.LastIndexOfFileReached())
        {
            await ConsumeTokenWithHighestAffinity(file);
        }

        return file;
    }

    /// <summary>
    /// Attempts to consume the next token based on the remaining input, and returns the one with the highest affinity.
    /// If a valid token can't be generated, then an unknown token is generated and it consumes one char at a time until
    /// it can make a valid token.
    /// </summary>
    /// <param name="file">
    /// The file to get the next token for.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static async Task ConsumeTokenWithHighestAffinity([NotNull] TokenFile file)
    {
        string remainingInput = file.GetRemainingInput() ?? string.Empty;
        string tokenValue = string.Empty;
        bool handled = false;
        bool ignoreToken = false;
        TokenType type = TokenType.Unknown;

        if (IsComment(remainingInput, out string? comment))
        {
            ignoreToken = true;
            handled = true;
            tokenValue = comment;
        }
        else if (IsWhitespace(remainingInput, out string? whitespace))
        {
            ignoreToken = true;
            handled = true;
            tokenValue = whitespace;
        }
        else if (IsNewline(remainingInput, out string? newline))
        {
            ignoreToken = true;
            handled = true;
            tokenValue = newline;
        }
        else if (IsKeyword(remainingInput, out string? keyword, out TokenType? keywordType))
        {
            handled = true;
            tokenValue = keyword;
            type = keywordType.Value;
        }
        else if (IsIdentifier(remainingInput, out string? identifier))
        {
            handled = true;
            tokenValue = identifier;
            type = TokenType.Identifier;
        }
        else if (IsNumber(remainingInput, out string? number))
        {
            handled = true;
            tokenValue = number;
            type = TokenType.NumberLiteral;
        }
        else if (IsSymbol(file.Lookahead(1) ?? string.Empty, out string? symbol, out TokenType? symbolType))
        {
            handled = true;
            tokenValue = symbol;
            type = symbolType.Value;
        }

        if (!handled)
        {
            await AppendUnknownToken(file);
            return;
        }

        if (!ignoreToken)
        {
            file.Tokens.Add(new Token()
            {
                CurrentLine = file.GetCurrentLine() ?? string.Empty,
                LineNumber = file.GetLineNumber(),
                LinePosition = file.GetLinePosition(),
                Type = type,
                Value = tokenValue
            });
        }

        file.CurrentPosition += tokenValue.Length;
    }

    /// <summary>
    /// Returns whether the specified input can be consumed as an identifier.
    /// </summary>
    /// <param name="remainingInput">The remaining input of the source file.</param>
    /// <param name="identifier">The identifier that gets generated, if any.</param>
    /// <returns>
    /// True if the consumption was successful. False otherwise.
    /// </returns>
    private static bool IsIdentifier(string remainingInput, [NotNullWhen(true)] out string? identifier)
    {
        identifier = null;

        Match match = Identifier().Match(remainingInput);

        if (!match.Success)
        {
            return false;
        }

        if (!match.Value.StartsWith("@", StringComparison.InvariantCultureIgnoreCase) && Constants.ReservedKeywords.ContainsKey(match.Value))
        {
            return false;
        }

        identifier = match.Value.Replace("@", string.Empty, StringComparison.InvariantCultureIgnoreCase);
        return true;
    }

    /// <summary>
    /// Returns whether the specified input can be consumed as a number.
    /// </summary>
    /// <param name="remainingInput">The remaining input of the source file.</param>
    /// <param name="number">The number that gets generated, if any.</param>
    /// <returns>
    /// True if the consumption was successful. False otherwise.
    /// </returns>
    private static bool IsNumber(string remainingInput, [NotNullWhen(true)] out string? number)
    {
        number = null;

        Match match = NumberLiteral().Match(remainingInput);

        if (!match.Success)
        {
            return false;
        }

        number = match.Value;
        return true;
    }

    /// <summary>
    /// Returns whether the specified input can be consumed as a symbol.
    /// </summary>
    /// <param name="sample">The first few characters of the remaining input as a sample.</param>
    /// <param name="symbol">The symbol that gets generated, if any.</param>
    /// <param name="symbolType">The type of the symbol, if any.</param>
    /// <returns>
    /// True if the consumption was successful. False otherwise.
    /// </returns>
    private static bool IsSymbol(string sample, [NotNullWhen(true)] out string? symbol, [NotNullWhen(true)] out TokenType? symbolType)
    {
        symbol = null;
        symbolType = null;

        foreach ((string key, TokenType value) in Constants.Symbols.OrderByDescending(x => x.Key.Length))
        {
            if (key == sample || key == sample[0].ToString())
            {
                symbol = key;
                symbolType = value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether the specified input can be consumed as a keyword.
    /// </summary>
    /// <param name="remainingInput">The remaining input of the source file.</param>
    /// <param name="keyword">The keyword that gets generated, if any.</param>
    /// <param name="keywordType">The type of the keyword, if any.</param>
    /// <returns>
    /// True if the consumption was successful. False otherwise.
    /// </returns>
    private static bool IsKeyword(string remainingInput, [NotNullWhen(true)] out string? keyword, [NotNullWhen(true)] out TokenType? keywordType)
    {
        keyword = null;
        keywordType = null;

        Match match = Keyword().Match(remainingInput);

        if (!match.Success)
        {
            return false;
        }

        foreach ((string key, TokenType value) in Constants.ReservedKeywords)
        {
            if (match.Value.Equals(key, StringComparison.Ordinal))
            {
                keyword = key;
                keywordType = value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether the specified input can be consumed as a comment.
    /// </summary>
    /// <param name="remainingInput">The remaining input of the source file.</param>
    /// <param name="comment">The comment that gets generated, if any.</param>
    /// <returns>
    /// True if the consumption was successful. False otherwise.
    /// </returns>
    private static bool IsComment(string remainingInput, [NotNullWhen(true)] out string? comment)
    {
        // Lexes line comments (i.e. // This is a comment).
        if (remainingInput.StartsWith("//", StringComparison.InvariantCultureIgnoreCase))
        {
            comment = remainingInput.Split(Environment.NewLine)[0];

            return true;
        }

        // Lexes block comments (i.e. /* This is a comment */).
        if (remainingInput.StartsWith("/*", StringComparison.InvariantCultureIgnoreCase))
        {
            // Lazily gets the next closing */ and gets the content of that.
            comment = $"{remainingInput.Split("*/")[0]}*/";

            return true;
        }

        comment = null;
        return false;
    }

    /// <summary>
    /// Returns whether the specified input can be consumed as a newline.
    /// </summary>
    /// <param name="remainingInput">The remaining input of the source file.</param>
    /// <param name="newline">The newline that gets generated, if any.</param>
    /// <returns>
    /// True if the consumption was successful. False otherwise.
    /// </returns>
    private static bool IsNewline(string remainingInput, [NotNullWhen(true)] out string? newline)
    {
        // These statements are done this way because we don't know which operating system the file was written on,
        // which may be different from the one our compiler is running on. So let's catch the longest newline first
        // and then fall through the single char newlines.
        if (remainingInput.StartsWith("\r\n", StringComparison.InvariantCultureIgnoreCase))
        {
            newline = "\r\n";
            return true;
        }
        else if (remainingInput.StartsWith("\r", StringComparison.InvariantCultureIgnoreCase))
        {
            newline = "\r";
            return true;
        }
        else if (remainingInput.StartsWith("\n", StringComparison.InvariantCultureIgnoreCase))
        {
            newline = "\n";
            return true;
        }

        newline = null!;
        return false;
    }

    /// <summary>
    /// Returns whether the specified input can be consumed as whitespace.
    /// </summary>
    /// <param name="remainingInput">The remaining input of the source file.</param>
    /// <param name="value">The whitespace that gets generated, if any.</param>
    /// <returns>
    /// True if the consumption was successful. False otherwise.
    /// </returns>
    private static bool IsWhitespace(string remainingInput, [NotNullWhen(true)] out string? value)
    {
        value = null;
        Match match = LeadingWhitespace().Match(remainingInput ?? "");

        if (match.Success)
        {
            value = match.Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Appends an unknown token to the token list, or appends a character to an existing unknown token.
    /// </summary>
    /// <param name="file">
    /// The <see cref="TokenFile"/> being processed.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static Task AppendUnknownToken([NotNull] TokenFile file)
    {
        Token? lastToken = file.Tokens.LastOrDefault();
        if (lastToken is null || lastToken.Type is not TokenType.Unknown)
        {
            file.Messages.Add(new SyntaxError(file.GetCurrentLine() ?? string.Empty, file.GetLineNumber(), file.GetLinePosition()));
            file.Tokens.Add(new Token
            {
                Type = TokenType.Unknown,
                Value = file.GetNextChar()?.ToString() ?? string.Empty,
                LineNumber = file.GetLineNumber(),
                LinePosition = file.GetLinePosition()
            });
            file.CurrentPosition++;

            return Task.CompletedTask;
        }

        lastToken.Value += file.GetNextChar()?.ToString() ?? string.Empty;
        file.CurrentPosition++;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Matches valid identifier strings.
    /// </summary>
    /// <returns>
    /// The <see cref="Regex"/>.
    /// </returns>
    [GeneratedRegex(@"^@?[a-zA-Z][a-zA-Z0-9]*")]
    private static partial Regex Identifier();

    /// <summary>
    /// Matches valid number literal strings.
    /// </summary>
    /// <returns>
    /// The <see cref="Regex"/>.
    /// </returns>
    [GeneratedRegex(@"^[0-9]+(\.[0-9]+)?")]
    private static partial Regex NumberLiteral();

    /// <summary>
    /// Matches on whitespace characters at the beginning of a string.
    /// </summary>
    /// <returns>
    /// The <see cref="Regex"/>.
    /// </returns>
    [GeneratedRegex(@"^[\s-[\r\n]]+")]
    private static partial Regex LeadingWhitespace();

    /// <summary>
    /// Matches valid keyword strings.
    /// </summary>
    /// <returns>
    /// The <see cref="Regex"/>.
    /// </returns>
    [GeneratedRegex(@"^[a-z][a-z0-9]*")]
    private static partial Regex Keyword();
}
