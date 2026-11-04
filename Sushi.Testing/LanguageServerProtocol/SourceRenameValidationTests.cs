using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Sushi.LanguageServerProtocol;
using Sushi.Source;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies byte-span rebasing for prospective source edits.
/// </summary>
[TestFixture]
public sealed class SourceRenameValidationTests
{
    [Test]
    public void TryMapSpanShouldApplyEditsBeforeTheSpanToBothBoundaries()
    {
        SourceSnapshot snapshot = CreateSnapshot("x target");
        SourceSpan span = new(snapshot, 2, 8);
        SourceRenameTextEdit[] edits = [new(new SourceSpan(snapshot, 0, 1), "long")];

        SourceRenameValidation.TryMapSpan(span, edits, out int start, out int end).Should().BeTrue();

        (start, end).Should().Be((5, 11));
    }

    [Test]
    public void TryMapSpanShouldIgnoreEditsAfterTheSpan()
    {
        SourceSnapshot snapshot = CreateSnapshot("target x");
        SourceSpan span = new(snapshot, 0, 6);
        SourceRenameTextEdit[] edits = [new(new SourceSpan(snapshot, 7, 8), "long")];

        SourceRenameValidation.TryMapSpan(span, edits, out int start, out int end).Should().BeTrue();

        (start, end).Should().Be((0, 6));
    }

    [Test]
    public void TryMapSpanShouldMapAnExactReplacementToTheNewUtf8Length()
    {
        SourceSnapshot snapshot = CreateSnapshot("name");
        SourceSpan span = new(snapshot, 0, 4);
        SourceRenameTextEdit[] edits = [new(span, "😀é")];

        SourceRenameValidation.TryMapSpan(span, edits, out int start, out int end).Should().BeTrue();

        (start, end).Should().Be((0, Encoding.UTF8.GetByteCount("😀é")));
    }

    [Test]
    public void TryMapSpanShouldKeepAnEnclosingStartAndAdjustItsEndForContainedEdits()
    {
        SourceSnapshot snapshot = CreateSnapshot("abcdefghij");
        SourceSpan span = new(snapshot, 1, 9);
        SourceRenameTextEdit[] edits =
        [
            new(new SourceSpan(snapshot, 2, 3), "long"),
            new(new SourceSpan(snapshot, 6, 8), "x")
        ];

        SourceRenameValidation.TryMapSpan(span, edits, out int start, out int end).Should().BeTrue();

        (start, end).Should().Be((1, 11));
    }

    [Test]
    public void TryMapSpanShouldRejectPartialOverlaps()
    {
        SourceSnapshot snapshot = CreateSnapshot("abcdefgh");
        SourceSpan span = new(snapshot, 2, 6);
        SourceRenameTextEdit[] edits = [new(new SourceSpan(snapshot, 1, 3), "x")];

        SourceRenameValidation.TryMapSpan(span, edits, out _, out _).Should().BeFalse();
    }

    [Test]
    public void TryMapSpanShouldUseUtf8ByteDeltasForUnicodeReplacements()
    {
        SourceSnapshot snapshot = CreateSnapshot("éabcZ");
        SourceSpan span = new(snapshot, 0, 5);
        SourceRenameTextEdit[] edits = [new(new SourceSpan(snapshot, 0, 2), "😀")];

        SourceRenameValidation.TryMapSpan(span, edits, out int start, out int end).Should().BeTrue();

        (start, end).Should().Be((0, 7));
    }

    private static SourceSnapshot CreateSnapshot(string text)
        => SourceSnapshot.FromText(new Uri("file:///RenameMapping.sus"), 1, text);
}
