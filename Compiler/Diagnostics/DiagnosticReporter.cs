using System.Collections.ObjectModel;
using Sushi.Source;

namespace Sushi.Diagnostics;

/// <inheritdoc />
public sealed class DiagnosticReporter : IDiagnosticReporter
{
    private readonly List<SushiDiagnostic> diagnostics = [];

    private static readonly Dictionary<ErrorType, string> errorMessages = new(){
        [ErrorType.InvalidUtf8] = "Malformed UTF-8 source sequence.",
        [ErrorType.TomlSyntaxError] = "Syntax error in Toml source.",
        [ErrorType.TomlMissingRequiredKey] = "Required configuration key is missing.",
        [ErrorType.TomlInvalidValueType] = "Configuration key has an invalid value type.",
        [ErrorType.TomlEmptyProjectName] = "Required project name is empty.",
        [ErrorType.TomlMissingBuildTargets] = "Project configuration must define at least one build target.",
        [ErrorType.TomlUnknownDefaultBuildTarget] = "Default build target is not declared in \"build.targets\".",
        [ErrorType.UnterminatedBlockComment] = "Unterminated block comment.",
        [ErrorType.InvalidDigitSeparator] = "Integer digit separator must occur between two digits valid for the literal's radix.",
        [ErrorType.MissingRadixDigit] = "Radix-prefixed integer literal requires at least one digit.",
        [ErrorType.InvalidRadixDigit] = "Digit is not valid in an integer literal of the specified radix.",
        [ErrorType.MissingLexicalSeparation] = "Separation is required between adjacent lexical elements.",
        [ErrorType.RequiredSyntacticAdjacency] = "Syntactic adjacency is required for certain lexical elements.",
        [ErrorType.ExpectedSyntax] = "Syntax expected.",
        [ErrorType.IntegerConstantOutOfRange] = "Integer constant is not representable as int32.",
        [ErrorType.MissingPackageDeclaration] = "A Sushi source file must declare exactly one package.",
        [ErrorType.DuplicatePackageDeclaration] = "A Sushi source file may declare only one package.",
        [ErrorType.MissingNamespaceDeclaration] = "A Sushi source file must declare exactly one namespace.",
        [ErrorType.DuplicateNamespaceDeclaration] = "A Sushi source file may declare only one namespace."
    };

    private static readonly Dictionary<WarningType, string> warningMessages = new(){
        [WarningType.TomlSyntaxWarning] = "Syntax warning in Toml source."
    };

    /// <inheritdoc />
    public bool HasErrors() => this.diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    /// <inheritdoc />
    public IReadOnlyList<SushiDiagnostic> ReportDiagnostics() => new ReadOnlyCollection<SushiDiagnostic>([..this.diagnostics]);

    /// <inheritdoc />
    public void GenerateError(ErrorType type, SourceSpan sourceSpan)
    {
        if (!errorMessages.TryGetValue(type, out string? message))
        {
            throw new InvalidOperationException($"ErrorType \"{Enum.GetName(type)}\" does not have an associated error message.");
        }

        this.GenerateErrorWithCustomMessage(type, message, sourceSpan);     
    }

    /// <inheritdoc />
    public void GenerateErrorWithCustomMessage(ErrorType type, string message, SourceSpan sourceSpan)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException($"Type value {(int)type} is invalid for enum ErrorType.");
        }

        SushiDiagnostic diagnostic = new((int)type, message, DiagnosticSeverity.Error, sourceSpan);

        this.diagnostics.Add(diagnostic);   
    }

    /// <inheritdoc />
    public void GenerateWarning(WarningType type, SourceSpan sourceSpan)
    {
        if (!warningMessages.TryGetValue(type, out string? message))
        {
            throw new InvalidOperationException($"WarningType \"{Enum.GetName(type)}\" does not have an associated warning message.");
        }

        this.GenerateWarningWithCustomMessage(type, message, sourceSpan);    
    }

    /// <inheritdoc />
    public void GenerateWarningWithCustomMessage(WarningType type, string message, SourceSpan sourceSpan)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentException($"Type value {(int)type} is invalid for enum WarningType.");
        }

        SushiDiagnostic diagnostic = new((int)type, message, DiagnosticSeverity.Warning, sourceSpan);

        this.diagnostics.Add(diagnostic);   
    }

    /// <inheritdoc />
    public void CommitReporter(IDiagnosticReporter diagnosticReporter)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        foreach (SushiDiagnostic diagnostic in diagnosticReporter.ReportDiagnostics())
        {
            this.diagnostics.Add(diagnostic);
        }
    }
}