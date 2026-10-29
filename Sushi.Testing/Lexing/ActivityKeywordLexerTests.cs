using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Testing.Lexing;

/// <summary>
/// Verifies lexical classification of the reserved words used by activity declarations and lifetimes.
/// </summary>
[TestFixture]
public class ActivityKeywordLexerTests
{
    private static readonly Uri testUri = new("file:///TestProject/ActivityKeywords.sus");

    [TestCase("activity", TestName = "Lex Should Recognize activity Reserved Keyword")]
    [TestCase("root", TestName = "Lex Should Recognize root Reserved Keyword")]
    [TestCase("entry", TestName = "Lex Should Recognize entry Reserved Keyword")]
    [TestCase("stop", TestName = "Lex Should Recognize stop Reserved Keyword")]
    [TestCase("task", TestName = "Lex Should Recognize task Reserved Keyword")]
    [TestCase("persistent", TestName = "Lex Should Recognize persistent Reserved Keyword")]
    public void LexShould_0(string keyword)
    {
        (LexerResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = Lex(keyword);

        result.Tokens
            .Should()
            .ContainSingle()
            .Which.Type
            .Should()
            .Be(LexTokenType.Keyword);

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("activity", TestName = "Lex Should Allow Escaping activity Reserved Keyword")]
    [TestCase("root", TestName = "Lex Should Allow Escaping root Reserved Keyword")]
    [TestCase("entry", TestName = "Lex Should Allow Escaping entry Reserved Keyword")]
    [TestCase("stop", TestName = "Lex Should Allow Escaping stop Reserved Keyword")]
    [TestCase("task", TestName = "Lex Should Allow Escaping task Reserved Keyword")]
    [TestCase("persistent", TestName = "Lex Should Allow Escaping persistent Reserved Keyword")]
    public void LexShould_1(string keyword)
    {
        (LexerResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = Lex($"@{keyword}");

        result.Tokens
            .Should()
            .ContainSingle()
            .Which.Type
            .Should()
            .Be(LexTokenType.EscapedIdentifier);

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("activities", TestName = "Lex Should Not Reserve Identifier Containing Or Resembling activity Keyword")]
    [TestCase("rooted", TestName = "Lex Should Not Reserve Identifier Containing Or Resembling root Keyword")]
    [TestCase("entryPoint", TestName = "Lex Should Not Reserve Identifier Containing Or Resembling entry Keyword")]
    [TestCase("stopping", TestName = "Lex Should Not Reserve Identifier Containing Or Resembling stop Keyword")]
    [TestCase("task1", TestName = "Lex Should Not Reserve Identifier Containing Or Resembling task Keyword")]
    [TestCase("persistentActivity", TestName = "Lex Should Not Reserve Identifier Containing Or Resembling persistent Keyword")]
    [TestCase("Task", TestName = "Lex Should Not Reserve Capitalized Version Of task Keyword")]
    [TestCase("completion", TestName = "Lex Should Not Reserve completion Keyword")]
    public void LexShould_2(string identifier)
    {
        (LexerResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = Lex(identifier);

        result.Tokens
            .Should()
            .ContainSingle()
            .Which.Type
            .Should()
            .Be(LexTokenType.Identifier);

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("activity", "root", TestName = "Lex Should Require Separation Between Adjacent activity And root Keywords")]
    public void LexShould_3(string first, string second)
    {
        (LexerResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = Lex($"{first} @{second}");

        result.Tokens
            .Where(token => token.Type is not LexTokenType.Whitespace)
            .Select(token => token.Type)
            .Should()
            .Equal(LexTokenType.Keyword, LexTokenType.EscapedIdentifier);

        diagnostics
            .Should()
            .BeEmpty();
    }

    private static (LexerResult Result, IReadOnlyList<SushiDiagnostic> Diagnostics) Lex(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);

        IDiagnosticReporter reporter = new DiagnosticReporter();
        LexerResult result = new SourceLexer().Lex(snapshot, reporter, CancellationToken.None);

        return (result, reporter.ReportDiagnostics());
    }
}