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
    [TestCase(TestName = "Parser Should Emit SUSE0002 When Attempting To Invoke A Number Literal")]
    public async Task ParserShouldEmit_0()
    {
        this.SourceFile.Tokens.AddRange
        ([
            DummyToken(TokenType.NumberLiteral, "5"),
            DummyToken(TokenType.OpeningParenthesis),
            DummyToken(TokenType.ClosingParenthesis),
            DummyToken(TokenType.Terminator)
        ]);

        AbstractSyntaxTree ast = await Parser.ParseSource([this.SourceFile]);

        ast.Messages.Count.Should().Be(1);
        ast.Messages[0].Should().BeOfType<NonInvocableError>();
    }
}
