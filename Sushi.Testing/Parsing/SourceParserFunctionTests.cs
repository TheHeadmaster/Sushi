using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

[TestFixture]
public class SourceParserFunctionTests
{
    private static readonly Uri testUri = new("file:///TestProject/Function.sus");

    [TestCase(TestName = "Parser Should Parse Minimum Function Declaration")]
    public void ParserShould_0()
    {
        const string source = "public int32 main() { return 42; }";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseFunctionDeclaration(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        FunctionDeclarationSyntax declaration = GetFunction(result);

        declaration.Type
            .Should()
            .Be(SyntaxType.FunctionDeclaration);

        declaration.AccessModifier.Type
            .Should()
            .Be(SyntaxType.AccessModifierKeyword);

        declaration.AccessModifier.IsMissing
            .Should()
            .BeFalse();

        declaration.ReturnType.Type
            .Should()
            .Be(SyntaxType.IdentifierToken);

        declaration.ReturnType.IsMissing
            .Should()
            .BeFalse();

        declaration.Name.Type
            .Should()
            .Be(SyntaxType.IdentifierToken);

        declaration.Name.IsMissing
            .Should()
            .BeFalse();

        declaration.OpenParenthesisToken.Type
            .Should()
            .Be(SyntaxType.OpenParenthesisToken);

        declaration.CloseParenthesisToken.Type
            .Should()
            .Be(SyntaxType.CloseParenthesisToken);

        declaration.Body.Type
            .Should()
            .Be(SyntaxType.Block);

        declaration.Body.OpenBraceToken.Type
            .Should()
            .Be(SyntaxType.OpenBraceToken);

        declaration.Body.CloseBraceToken.Type
            .Should()
            .Be(SyntaxType.CloseBraceToken);

        declaration.Body.Statements
            .Should()
            .ContainSingle();

        ReturnStatementSyntax returnStatement = declaration.Body.Statements[0]
            .Should()
            .BeOfType<ReturnStatementSyntax>()
            .Subject;

        returnStatement.ReturnKeyword.Type
            .Should()
            .Be(SyntaxType.ReturnKeyword);

        returnStatement.SemicolonToken.Type
            .Should()
            .Be(SyntaxType.SemicolonToken);
        
        IntegerLiteralExpressionSyntax expression = returnStatement.Expression
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        expression.Type
            .Should()
            .Be(SyntaxType.IntegerLiteralExpression);

        expression.LiteralToken.Type
            .Should()
            .Be(SyntaxType.IntegerLiteralToken);

        expression.LiteralToken.IsMissing
            .Should()
            .BeFalse();

        declaration.Span.Snapshot
            .Should()
            .BeSameAs(result.Tree.Snapshot);

        declaration.Span.Start
            .Should()
            .Be(0);

        declaration.Span.End
            .Should()
            .Be(source.Length);
    }

    [TestCase(TestName = "Parser Should Parse Void Function Declaration")]
    public void ParserShould_1()
    {
        const string source = "public void log() {}";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseFunctionDeclaration(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        FunctionDeclarationSyntax declaration = GetFunction(result);

        declaration.ReturnType.Type
            .Should()
            .Be(SyntaxType.VoidKeyword);

        declaration.Body.Statements
            .Should()
            .BeEmpty();

        declaration.Body.CloseBraceToken.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Accept Escaped Function Name")]
    public void ParserShould_2()
    {
        const string source = "public int32 @return() { return 42; }";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseFunctionDeclaration(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        FunctionDeclarationSyntax declaration = GetFunction(result);

        declaration.Name.Type
            .Should()
            .Be(SyntaxType.EscapedIdentifierToken);

        declaration.Name.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Accept Escaped Function Return Type")]
    public void ParserShould_3()
    {
        const string source = "public @int32 main() { return 42; }";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseFunctionDeclaration(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        FunctionDeclarationSyntax declaration = GetFunction(result);

        declaration.ReturnType.Type
            .Should()
            .Be(SyntaxType.EscapedIdentifierToken);

        declaration.ReturnType.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Recover Missing Return Semicolon")]
    public void ParserShould_4()
    {
        const string source = "public int32 main() { return 42 }";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseFunctionDeclaration(source, testUri);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        FunctionDeclarationSyntax declaration = GetFunction(result);

        ReturnStatementSyntax returnStatement = declaration.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<ReturnStatementSyntax>()
            .Subject;

        returnStatement.SemicolonToken.Type
            .Should()
            .Be(SyntaxType.SemicolonToken);

        returnStatement.SemicolonToken.IsMissing
            .Should()
            .BeTrue();

        declaration.Body.CloseBraceToken.IsMissing
            .Should()
            .BeFalse();

        declaration.Span.End
            .Should()
            .Be(source.Length);
    }

    [TestCase(TestName = "Parser Should Parse Function As Source File Declaration")]
    public void ParserShould_5()
    {
        const string source = 
        """
        package Example;
        namespace Example;

        public int32 main() {
            return 42;
        }
        """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseSourceFile(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        SourceFileSyntax sourceFile = GetSourceFile(result);

        sourceFile.PackageDeclarations
            .Should()
            .ContainSingle();

        sourceFile.NamespaceDeclarations
            .Should()
            .ContainSingle();

        sourceFile.FunctionDeclarations
            .Should()
            .ContainSingle();

        FunctionDeclarationSyntax declaration = sourceFile.FunctionDeclarations[0];

        declaration.Name.Type
            .Should()
            .Be(SyntaxType.IdentifierToken);

        declaration.Body.Statements
            .Should()
            .ContainSingle();

        sourceFile.UnparsedContentSpan
            .Should()
            .BeNull();
    }

    [TestCase(TestName = "Parser Should Parse Consecutive Function Declarations")]
    public void ParserShould_6()
    {
        const string source = """
        package Example;
        namespace Example;

        public int32 first() {
            return 1;
        }

        public int32 second() {
            return 2;
        }
        """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseSourceFile(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        SourceFileSyntax sourceFile = GetSourceFile(result);

        sourceFile.FunctionDeclarations
            .Should()
            .HaveCount(2);

        sourceFile.FunctionDeclarations[0].Span.End
            .Should()
            .BeLessThan(sourceFile.FunctionDeclarations[1].Span.Start);

        sourceFile.UnparsedContentSpan
            .Should()
            .BeNull();
    }

    [TestCase(TestName = "Parser Should Preserve Unsupported Content After Parsed Function")]
    public void ParserShould_7()
    {
        const string source = """
        package Example;
        namespace Example;

        public int32 main() {
            return 42;
        }

        using Example.Other;
        """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseSourceFile(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        SourceFileSyntax sourceFile = GetSourceFile(result);

        sourceFile.FunctionDeclarations
            .Should()
            .ContainSingle();

        sourceFile.UnparsedContentSpan
            .Should()
            .NotBeNull();
        
        SourceSpan remainder = sourceFile.UnparsedContentSpan!.Value;

        remainder.Start
            .Should()
            .Be(source.IndexOf("using", StringComparison.Ordinal));

        remainder.End
            .Should()
            .Be(source.Length);

        remainder.Snapshot
            .Should()
            .BeSameAs(result.Tree.Snapshot);
    }

    [TestCase(TestName = "Parser Should Not Speculatively Parse Non Function Declaration")]
    public void ParserShould_8()
    {
        const string source = """
            package Example;
            namespace Example;
            public int32 value;
        """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseSourceFile(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        SourceFileSyntax sourceFile = GetSourceFile(result);

        sourceFile.FunctionDeclarations
            .Should()
            .BeEmpty();

        sourceFile.UnparsedContentSpan
            .Should()
            .NotBeNull();

        sourceFile.UnparsedContentSpan!.Value.Start
            .Should()
            .Be(source.IndexOf("public", StringComparison.Ordinal));
    }

    [TestCase("public", TestName = "Parser Should Recognize Public Function Access Modifier")]
    [TestCase("internal", TestName = "Parser Should Recognize Internal Function Access Modifier")]
    [TestCase("package", TestName = "Parser Should Recognize Package Function Access Modifier")]
    [TestCase("protected", TestName = "Parser Should Recognize Protected Function Access Modifier")]
    [TestCase("private", TestName = "Parser Should Recognize Private Function Access Modifier")]
    public void ParserShould_9(string accessModifier)
    {
        string source = $"{accessModifier} int32 test() {{ return 42; }}";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseFunctionDeclaration(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        FunctionDeclarationSyntax declaration = GetFunction(result);

        declaration.AccessModifier.Type
            .Should()
            .Be(SyntaxType.AccessModifierKeyword);

        declaration.AccessModifier.IsMissing
            .Should()
            .BeFalse();
    }

    private static FunctionDeclarationSyntax GetFunction(ParserResult result)
        => result.Tree.Root
            .Should()
            .BeOfType<FunctionDeclarationSyntax>()
            .Subject;

    private static SourceFileSyntax GetSourceFile(ParserResult result)
        => result.Tree.Root
            .Should()
            .BeOfType<SourceFileSyntax>()
            .Subject;
}