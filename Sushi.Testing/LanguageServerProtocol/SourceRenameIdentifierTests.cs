using FluentAssertions;
using NUnit.Framework;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies that rename spellings follow the language's actual lexical identifier rules.
/// </summary>
[TestFixture]
public sealed class SourceRenameIdentifierTests
{
    [TestCase("total", "total")]
    [TestCase("_total", "_total")]
    [TestCase("total2", "total2")]
    [TestCase("@return", "return")]
    [TestCase("@int32", "int32")]
    public void ValidIdentifierSpellingsShouldRetainTheirSemanticName(string spelling, string semanticName)
    {
        SourceRenameIdentifier.TryValidate(spelling, out string actual, CancellationToken.None)
            .Should()
            .BeTrue();
        actual.Should().Be(semanticName);
    }

    [TestCase("")]
    [TestCase("return")]
    [TestCase("_")]
    [TestCase("___")]
    [TestCase("123total")]
    [TestCase("total-value")]
    [TestCase("total value")]
    [TestCase("\uFEFFtotal")]
    [TestCase("@ordinaryName")]
    [TestCase("@@return")]
    public void InvalidOrIncompleteIdentifierSpellingsShouldBeRejected(string spelling)
    {
        SourceRenameIdentifier.TryValidate(spelling, out _, CancellationToken.None)
            .Should()
            .BeFalse();
    }

    [Test]
    public void IdentifierValidationShouldPropagateCancellation()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Action validate = () => SourceRenameIdentifier.TryValidate("total", out _, cancellation.Token);

        validate.Should().Throw<OperationCanceledException>();
    }
}
