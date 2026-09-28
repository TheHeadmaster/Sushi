using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Source;

namespace Sushi.Testing.Source;

[TestFixture]
public class SourceSnapshotTests
{
    private static readonly Uri testUri = new("file:///TestProject/Test.sus");

    [TestCase(TestName = "FromUtf8 Should Remove Leading UTF-8 BOM")]
    public void FromUtf8Should_0()
    {
        byte[] source =
        [
            0xEF,
            0xBB,
            0xBF,
            (byte)'a',
            (byte)'b',
            (byte)'c'
        ];

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(testUri, version: null, source);

        snapshot.Bytes
            .ToArray()
            .Should()
            .Equal("abc"u8.ToArray());

        snapshot.Text
            .Should()
            .Be("abc");

        snapshot.SourceLength
            .Should()
            .Be(3);

        snapshot.EncodingIssues
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "FromUtf8 Should Preserve Source Without BOM")]
    public void FromUtf8Should_1()
    {
        byte[] source = Encoding.UTF8.GetBytes("abc");

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(testUri, version: null, source);

        snapshot.Bytes
            .ToArray()
            .Should()
            .Equal(source);

        snapshot.Text
            .Should()
            .Be("abc");

        snapshot.SourceLength
            .Should()
            .Be(source.Length);
    }

    [TestCase(TestName = "FromUtf8 Should Preserve Valid Multibyte UTF-8")]
    public void FromUtf8Should_2()
    {
        const string text = "寿司";

        byte[] source = Encoding.UTF8.GetBytes(text);

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(testUri, version: null, source);

        snapshot.Bytes
            .ToArray()
            .Should()
            .Equal(source);

        snapshot.Text
            .Should()
            .Be(text);

        snapshot.SourceLength
            .Should()
            .Be(source.Length);

        snapshot.IsValidUtf8
            .Should()
            .BeTrue();

        snapshot.EncodingIssues
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "FromUtf8 Should Preserve Malformed UTF-8 Bytes")]
    public void FromUtf8Should_3()
    {
        byte[] source =
        [
            (byte)'a',
            0xFF,
            (byte)'b'
        ];

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(testUri, version: null, source);

        snapshot.Bytes
            .ToArray()
            .Should()
            .Equal(source);

        snapshot.SourceLength
            .Should()
            .Be(source.Length);

        snapshot.IsValidUtf8
            .Should()
            .BeFalse();

        snapshot.EncodingIssues
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .Be(new SourceEncodingIssue(1, 1));
    }

    [TestCase(TestName = "FromUtf8 Should Preserve Truncated UTF-8 Sequence As One Encoding Issue")]
    public void FromUtf8Should_4()
    {
        byte[] source =
        [
            (byte)'a',

            // Beginning of the three-byte sequence for U+20AC EURO SIGN,
            // with the final continuation byte missing.
            0xE2,
            0x82
        ];

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(testUri, version: null, source);

        snapshot.Bytes
            .ToArray()
            .Should()
            .Equal(source);

        snapshot.IsValidUtf8
            .Should()
            .BeFalse();

        snapshot.EncodingIssues
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .Be(new SourceEncodingIssue(1, 2));
    }

    [TestCase(TestName = "FromUtf8 Should Preserve Physical Line Terminators")]
    public void FromUtf8Should_5()
    {
        const string text = "a\r\nb\rc\nd";

        byte[] source = Encoding.UTF8.GetBytes(text);

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(testUri, version: null, source);

        snapshot.Bytes
            .ToArray()
            .Should()
            .Equal(source);

        snapshot.Text
            .Should()
            .Be(text);

        snapshot.LineStarts
            .Should()
            .Equal(0, 3, 5, 7);
    }

    [TestCase(TestName = "FromText Should Remove Leading BOM Character")]
    public void FromTextShould_0()
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, "\uFEFFabc");

        snapshot.Text
            .Should()
            .Be("abc");

        snapshot.Bytes
            .ToArray()
            .Should()
            .Equal(
                Encoding.UTF8.GetBytes("abc"));

        snapshot.SourceLength
            .Should()
            .Be(3);
    }

    [TestCase(TestName = "FromUtf8 And FromText Should Produce Equivalent Canonical Source")]
    public void FromUtf8AndFromTextShould_0()
    {
        byte[] source =
        [
            0xEF,
            0xBB,
            0xBF,
            (byte)'a',
            (byte)'b',
            (byte)'c'
        ];

        SourceSnapshot byteSnapshot = SourceSnapshot.FromUtf8(testUri, version: null, source);

        SourceSnapshot textSnapshot = SourceSnapshot.FromText(testUri, version: null, "\uFEFFabc");

        byteSnapshot.Bytes
            .ToArray()
            .Should()
            .Equal(textSnapshot.Bytes.ToArray());

        byteSnapshot.Text
            .Should()
            .Be(textSnapshot.Text);

        byteSnapshot.SourceLength
            .Should()
            .Be(textSnapshot.SourceLength);

        byteSnapshot.EncodingIssues
            .Should()
            .Equal(textSnapshot.EncodingIssues);
    }

    [TestCase(TestName = "GetUtf16Position Should Handle Malformed UTF-8")]
    public void GetUtf16PositionShould_0()
    {
        byte[] source =
        [
            (byte)'a',
            0xFF,
            (byte)'b'
        ];

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(testUri, version: null, source);

        snapshot.GetUtf16Position(0)
            .Should()
            .Be((0, 0));

        snapshot.GetUtf16Position(1)
            .Should()
            .Be((0, 1));

        snapshot.GetUtf16Position(2)
            .Should()
            .Be((0, 2));

        snapshot.GetUtf16Position(3)
            .Should()
            .Be((0, 3));
    }

    [TestCase(TestName = "FromUtf8 Should Copy Input Bytes")]
    public void FromUtf8Should_6()
    {
        byte[] source = Encoding.UTF8.GetBytes("abc");

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(testUri, version: null, source);

        source[0] = (byte)'z';

        snapshot.Bytes
            .ToArray()
            .Should()
            .Equal("abc"u8.ToArray());

        snapshot.Text
            .Should()
            .Be("abc");
    }
}