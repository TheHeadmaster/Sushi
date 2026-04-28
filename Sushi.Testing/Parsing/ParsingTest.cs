using Sushi.Parsing.Core;
using Sushi.Tokenization;

namespace Sushi.Testing.Parsing;

public abstract class ParsingTest
{
    protected static Parser Parser { get; private set; }

    protected TokenFile SourceFile { get; private set; }

    [OneTimeSetUp]
    public void OneTimeSetUp() => Parser = new Parser();

    [SetUp]
    public void SetUp() => this.SourceFile = new TokenFile()
    {
        FileName = string.Empty,
        FilePath = string.Empty,
        RawSourceCode = string.Empty
    };

    protected static Token DummyToken(TokenType type, string value = "") => new()
    {
        LineNumber = 0,
        LinePosition = 0,
        Type = type,
        Value = value
    };
}
