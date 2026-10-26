using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

[TestFixture]
public sealed class FunctionParameterParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/FunctionParameters.sus");

    [TestCase("public bool echo(bool value) { return value; }", 1, TestName = "Parser Should Retain One Typed Parameter")]
    [TestCase("public bool choose(bool first, bool second) { return second; }", 2, TestName = "Parser Should Retain Multiple Ordered Parameters")]
    [TestCase("public bool echo(bool @default) { return @default; }", 1, TestName = "Parser Should Preserve Escaped Parameter Name")]
    [TestCase("public bool echo() { return true; }", 0, TestName = "Parser Should Preserve Parameterless Function")]
    public void ParseShould_0(string source, int expectedCount)
    {
        (FunctionDeclarationSyntax declaration, IReadOnlyList<SushiDiagnostic> diagnostics) = Parse(source);

        declaration.Parameters.Should().HaveCount(expectedCount);
        declaration.ParameterSeparators.Should().HaveCount(Math.Max(0, expectedCount - 1));
        declaration.CloseParenthesisToken.IsSourceBacked.Should().BeTrue();
        declaration.Body.CloseBraceToken.IsSourceBacked.Should().BeTrue();
        diagnostics.Should().BeEmpty();
    }

    [TestCase("public bool echo(bool value) { return value; }", SyntaxType.IdentifierToken, TestName = "Parser Should Retain Ordinary Identifier Expression")]
    [TestCase("public bool echo(bool @default) { return @default; }", SyntaxType.EscapedIdentifierToken, TestName = "Parser Should Retain Escaped Identifier Expression")]
    public void ParseShould_1(string source, SyntaxType expectedTokenType)
    {
        (FunctionDeclarationSyntax declaration, IReadOnlyList<SushiDiagnostic> diagnostics) = Parse(source);
        ReturnStatementSyntax statement = declaration.Body.Statements.Should().ContainSingle().Which.Should().BeOfType<ReturnStatementSyntax>().Subject;
        IdentifierExpressionSyntax reference = statement.Expression.Should().BeOfType<IdentifierExpressionSyntax>().Subject;

        reference.IdentifierToken.Type.Should().Be(expectedTokenType);
        declaration.Parameters[0].NameToken.Type.Should().Be(expectedTokenType);
        diagnostics.Should().BeEmpty();
    }

    [TestCase("public bool echo(bool value,) { return value; }", TestName = "Parser Should Diagnose Trailing Comma")]
    [TestCase("public bool echo(bool) { return true; }", TestName = "Parser Should Diagnose Missing Parameter Name")]
    [TestCase("public bool echo(bool first bool second) { return first; }", TestName = "Parser Should Diagnose Missing Parameter Comma")]
    [TestCase("public bool echo(bool value { return value; }", TestName = "Parser Should Diagnose Missing Closing Parenthesis")]
    public void ParseShould_2(string source)
    {
        (FunctionDeclarationSyntax declaration, IReadOnlyList<SushiDiagnostic> diagnostics) = Parse(source);

        declaration.Body.CloseBraceToken.IsSourceBacked.Should().BeTrue();
        diagnostics.Should().Contain(item => item.IsType(ErrorType.ExpectedSyntax));
    }

    private static (FunctionDeclarationSyntax Declaration, IReadOnlyList<SushiDiagnostic> Diagnostics) Parse(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();
        LexerResult lexer = new SourceLexer().Lex(snapshot, reporter, CancellationToken.None);
        ParserResult parsed = SourceParser.ParseFunctionDeclaration(lexer, reporter, CancellationToken.None);

        FunctionDeclarationSyntax declaration = parsed.Tree.Root.Should().BeOfType<FunctionDeclarationSyntax>().Subject;
        return (declaration, reporter.ReportDiagnostics());
    }
}