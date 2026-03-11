using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing;
using Sushi.Tokenization;

namespace Sushi.Testing.Parsing;

public sealed class ParsingErrorTests : ParsingTest
{
    private static Parser parser;

    private TokenFile sourceFile;

    [OneTimeSetUp]
    public void OneTimeSetUp() => parser = new Parser();

    [SetUp]
    public void SetUp() => this.sourceFile = new TokenFile()
    {
        FileName = string.Empty,
        FilePath = string.Empty,
        RawSourceCode = string.Empty
    };

    [TestCase(TestName = "Parser Should Emit SUSE0002 When Attempting To Invoke A Number Literal")]
    public async Task ParserShouldEmit_0()
    {
        this.sourceFile.Tokens.AddRange
        ([
            this.DummyToken(TokenType.NumberLiteral, "5"),
            this.DummyToken(TokenType.OpeningParenthesis),
            this.DummyToken(TokenType.ClosingParenthesis),
            this.DummyToken(TokenType.Terminator)
        ]);

        AbstractSyntaxTree ast = await parser.ParseSource([this.sourceFile]);

        ast.Messages.Count.Should().Be(1);
        ast.Messages[0].Should().BeOfType<NonInvocableError>();
    }
}
