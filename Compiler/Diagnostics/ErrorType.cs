namespace Sushi.Diagnostics;

/// <summary>
/// Represents a type of error in Sushi.
/// </summary>
#pragma warning disable CA1008 // Enums should have zero value
public enum ErrorType
#pragma warning restore CA1008 // Enums should have zero value
{
    #region Core Errors
    InvalidUtf8 = 1,
    #endregion Core Errors
    #region Toml Errors
    TomlSyntaxError = 100,
    TomlMissingRequiredKey = 101,
    TomlInvalidValueType = 102,
    TomlEmptyProjectName = 103,
    TomlMissingBuildTargets = 104,
    TomlUnknownDefaultBuildTarget = 105,
    #endregion Toml Errors
    #region Syntax Errors
    UnterminatedBlockComment = 201,
    MissingRadixDigit = 202,
    InvalidRadixDigit = 203,
    InvalidDigitSeparator = 204,
    MissingLexicalSeparation = 205,
    RequiredSyntacticAdjacency = 206,
    ExpectedSyntax = 207,
    #endregion
    #region Semantic Errors
    IntegerConstantOutOfRange = 300,
    MissingPackageDeclaration = 301,
    DuplicatePackageDeclaration = 302,
    MissingNamespaceDeclaration = 303,
    DuplicateNamespaceDeclaration = 304,
    #endregion
}