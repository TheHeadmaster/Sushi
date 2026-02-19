using FluentAssertions;
using Sushi.Lexing.Tokenization;

namespace Sushi.Testing;

public sealed class TokenFileTests
{
    public const string testStringA = "lorem ipsum dolor sit amet";

    public const string testStringB = "lorem\r\n ipsum dolor sit amet\r\n";

    [SetUp]
    public void Setup()
    {
    }

    [TestCase(testStringA, 0, 0, TestName = "GetLinePosition Should Return 0 When Position Is At Start")]
    [TestCase(testStringA, 26, 26, TestName = "GetLinePosition Should Return Proper Position When Position Is Last Index")]
    [TestCase(testStringA, 27, -1, TestName = "GetLinePosition Should Return -1 When Position Is Out Of Bounds")]
    [TestCase(testStringB, 0, 0, TestName = "GetLinePosition Should Return Proper Position When Position Is In First Line")]
    [TestCase(testStringB, 5, 5, TestName = "GetLinePosition Should Return Proper Position When Position Is In Arbitrary Line A")]
    [TestCase(testStringB, 6, 6, TestName = "GetLinePosition Should Return Proper Position When Position Is In Arbitrary Line B")]
    [TestCase(testStringB, 7, 0, TestName = "GetLinePosition Should Return Proper Position When Position Is In Arbitrary Line C")]
    [TestCase(testStringB, 8, 1, TestName = "GetLinePosition Should Return Proper Position When Position Is In Arbitrary Line D")]
    [TestCase(testStringB, 28, 21, TestName = "GetLinePosition Should Return Proper Position When Position Is In Arbitrary Line E")]
    [TestCase(testStringB, 29, 22, TestName = "GetLinePosition Should Return Proper Position When Position Is In Last Line A")]
    [TestCase(testStringB, 30, 0, TestName = "GetLinePosition Should Return Proper Position When Position Is In Last Line B")]
    [TestCase(testStringB, 31, -1, TestName = "GetLinePosition Should Return -1 When Position Is Out Of Bounds With Lines")]
    public void GetLinePositionShould_0(string rawSourceCode, int currentPosition, int expectedValue)
    {
        TokenFile file = new()
        {
            FileName = "",
            FilePath = "",
            RawSourceCode = rawSourceCode,
            CurrentPosition = currentPosition,
        };

        file.GetLinePosition().Should().Be(expectedValue);
    }

    [TestCase(testStringA, 0, 1, TestName = "GetLineNumber Should Return Proper Line When Position Is 0")]
    [TestCase(testStringA, 26, 1, TestName = "GetLineNumber Should Return Last Line When Position Is Last Index")]
    [TestCase(testStringA, 27, -1, TestName = "GetLineNumber Should Return -1 When Position Is Out Of Bounds")]
    [TestCase(testStringB, 0, 1, TestName = "GetLineNumber Should Return Proper Line When Position Is In First Line")]
    [TestCase(testStringB, 5, 1, TestName = "GetLineNumber Should Return Proper Line When Position Is In Arbitrary Line A")]
    [TestCase(testStringB, 6, 1, TestName = "GetLineNumber Should Return Proper Line When Position Is In Arbitrary Line B")]
    [TestCase(testStringB, 7, 2, TestName = "GetLineNumber Should Return Proper Line When Position Is In Arbitrary Line C")]
    [TestCase(testStringB, 8, 2, TestName = "GetLineNumber Should Return Proper Line When Position Is In Arbitrary Line D")]
    [TestCase(testStringB, 28, 2, TestName = "GetLineNumber Should Return Proper Line When Position Is In Arbitrary Line E")]
    [TestCase(testStringB, 29, 2, TestName = "GetLineNumber Should Return Proper Line When Position Is In Last Line A")]
    [TestCase(testStringB, 30, 3, TestName = "GetLineNumber Should Return Proper Line When Position Is In Last Line B")]
    [TestCase(testStringB, 31, -1, TestName = "GetLineNumber Should Return -1 When Position Is Out Of Bounds With Lines")]
    public void GetLineNumberShould_0(string rawSourceCode, int currentPosition, int expectedValue)
    {
        TokenFile file = new()
        {
            FileName = "",
            FilePath = "",
            RawSourceCode = rawSourceCode,
            CurrentPosition = currentPosition,
        };

        file.GetLineNumber().Should().Be(expectedValue);
    }

    [TestCase(testStringA, 0, testStringA, TestName = "GetCurrentLine Should Return Proper Line When Position Is 0")]
    [TestCase(testStringA, 26, testStringA, TestName = "GetCurrentLine Should Return Last Line When Position Is Last Index")]
    [TestCase(testStringA, 27, null, TestName = "GetCurrentLine Should Return \"\" When Position Is Out Of Bounds")]
    [TestCase(testStringB, 0, "lorem", TestName = "GetCurrentLine Should Return Proper Line When Position Is In First Line")]
    [TestCase(testStringB, 5, "lorem", TestName = "GetCurrentLine Should Return Proper Line When Position Is In Arbitrary Line A")]
    [TestCase(testStringB, 6, "lorem", TestName = "GetCurrentLine Should Return Proper Line When Position Is In Arbitrary Line B")]
    [TestCase(testStringB, 7, " ipsum dolor sit amet", TestName = "GetCurrentLine Should Return Proper Line When Position Is In Arbitrary Line C")]
    [TestCase(testStringB, 8, " ipsum dolor sit amet", TestName = "GetCurrentLine Should Return Proper Line When Position Is In Arbitrary Line D")]
    [TestCase(testStringB, 28, " ipsum dolor sit amet", TestName = "GetCurrentLine Should Return Proper Line When Position Is In Arbitrary Line E")]
    [TestCase(testStringB, 29, " ipsum dolor sit amet", TestName = "GetCurrentLine Should Return Proper Line When Position Is In Last Line A")]
    [TestCase(testStringB, 30, "", TestName = "GetCurrentLine Should Return Proper Line When Position Is In Last Line B")]
    [TestCase(testStringB, 31, null, TestName = "GetCurrentLine Should Return \"\" When Position Is Out Of Bounds With Lines")]
    public void GetCurrentLineShould_0(string rawSourceCode, int currentPosition, string? expectedValue)
    {
        TokenFile file = new()
        {
            FileName = "",
            FilePath = "",
            RawSourceCode = rawSourceCode,
            CurrentPosition = currentPosition,
        };

        file.GetCurrentLine().Should().Be(expectedValue);
    }
}
