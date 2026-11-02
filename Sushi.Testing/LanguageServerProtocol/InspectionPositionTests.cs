using FluentAssertions;
using NUnit.Framework;
using Sushi.Source;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies strict editor-coordinate conversion independently of disk and lossy decoded text.
/// </summary>
[TestFixture]
public class InspectionPositionTests
{
    [TestCase("", 0, 0, 0)]
    [TestCase("abc", 0, 0, 0)]
    [TestCase("abc", 0, 3, 3)]
    [TestCase("é界X", 0, 1, 2)]
    [TestCase("é界X", 0, 2, 5)]
    [TestCase("a😀b", 0, 1, 1)]
    [TestCase("a😀b", 0, 3, 5)]
    [TestCase("a😀b", 0, 4, 6)]
    [TestCase("a\r\n😀\r\n\r\n", 1, 2, 7)]
    [TestCase("a\r\n😀\r\n\r\n", 2, 0, 9)]
    [TestCase("a\r\n😀\r\n\r\n", 3, 0, 11)]
    [TestCase("é\n\n界", 1, 0, 3)]
    [TestCase("é\n\n界", 2, 1, 7)]
    [TestCase("a\rb", 1, 1, 3)]
    [TestCase("é\r\n", 0, 1, 2)]
    [TestCase("é\r\n", 1, 0, 4)]
    [TestCase("\r\n", 0, 0, 0)]
    [TestCase("\n", 1, 0, 1)]
    public void EditorPositionsShouldResolveToCanonicalByteBoundaries(string text, int line, int character, int expected)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri("file:///Position.sus"), 1, text);
        snapshot.GetByteOffset(line, character)
            .Should()
            .Be(expected);
        snapshot.GetUtf16Position(expected)
            .Should()
            .Be((line, character));
    }

    [TestCase(-1, 0)]
    [TestCase(0, -1)]
    [TestCase(4, 0)]
    [TestCase(0, 3)]
    [TestCase(1, 1)]
    [TestCase(1, 3)]
    [TestCase(2, 1)]
    public void InvalidPositionsShouldNotClampOrSplitScalars(int line, int character)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri("file:///Position.sus"), 1, "é\r\n😀\r\n");
        Action convert = () => snapshot.GetByteOffset(line, character);
        convert
            .Should()
            .Throw<ArgumentException>();
    }

    [TestCase(new byte[] { 0xF0, 0x9F })]
    [TestCase(new byte[] { 0xFF })]
    public void MalformedUtf8ShouldNotUseReplacementCharactersForSelection(byte[] bytes)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(new Uri("file:///Position.sus"), 1, bytes);
        Action convert = () => snapshot.GetByteOffset(0, 0);
        convert
            .Should()
            .Throw<InvalidOperationException>();
    }
}