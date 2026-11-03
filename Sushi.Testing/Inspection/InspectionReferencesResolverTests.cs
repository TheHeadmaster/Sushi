using FluentAssertions;
using NUnit.Framework;
using Sushi.Inspection;

namespace Sushi.Testing.Inspection;

/// <summary>
/// Verifies that generated-reference searches use complete formatter identities and stay within one document.
/// </summary>
[TestFixture]
public class InspectionReferencesResolverTests
{
    [TestCase(TestName = "References Match Complete Semantic Identities And Honor Declaration Inclusion")]
    public void ResolveShouldMatchExactIdentityAndDeclarationPreference()
    {
        InspectionDocument document = CreateDocument(out IReadOnlyDictionary<string, IReadOnlyList<int>> positions);
        string originalText = document.Text;
        IReadOnlyList<InspectionSymbolOccurrence> originalOccurrences = document.Occurrences;

        foreach (string token in new[] { "function", "parameter", "local", "temporary", "block" })
        {
            int definitionOffset = positions[$"{token}-definition"][0];
            IReadOnlyList<InspectionSymbolOccurrence> includingDefinition = InspectionReferencesResolver.Resolve(document,
                definitionOffset, includeDeclaration: true);
            IReadOnlyList<InspectionSymbolOccurrence> fromReference = InspectionReferencesResolver.Resolve(document,
                positions[$"{token}-reference"][0], includeDeclaration: true);

            fromReference
                .Should()
                .BeEquivalentTo(includingDefinition);
            includingDefinition
                .Should()
                .BeInAscendingOrder(occurrence => occurrence.Start);
            includingDefinition
                .Select(occurrence => (occurrence.Start, occurrence.End))
                .Should()
                .OnlyHaveUniqueItems();

            IReadOnlyList<InspectionSymbolOccurrence> referencesOnly = InspectionReferencesResolver.Resolve(document,
                definitionOffset, includeDeclaration: false);
            referencesOnly
                .Should()
                .OnlyContain(occurrence => !occurrence.IsDefinition)
                .And
                .HaveCount(1);
        }

        InspectionReferencesResolver.Resolve(document, positions["local-definition"][0], includeDeclaration: true)
            .Select(occurrence => occurrence.Identity)
            .Should()
            .OnlyContain(identity => identity == new InspectionSymbolIdentity(InspectionSymbolKind.Local, "caller", 0));
        InspectionReferencesResolver.Resolve(document, positions["local-one-definition"][0], includeDeclaration: true)
            .Should()
            .OnlyContain(occurrence => occurrence.Identity == new InspectionSymbolIdentity(InspectionSymbolKind.Local, "caller", 1));
        InspectionReferencesResolver.Resolve(document, positions["foreign-parameter-definition"][0], includeDeclaration: true)
            .Should()
            .OnlyContain(occurrence => occurrence.Identity == new InspectionSymbolIdentity(InspectionSymbolKind.Parameter, "callee", 0));
        InspectionReferencesResolver.Resolve(document, positions["foreign-function-reference"][0], includeDeclaration: true)
            .Should()
            .ContainSingle()
            .Which
            .Identity
            .Should()
            .Be(new InspectionSymbolIdentity(InspectionSymbolKind.Function, "another-function"));
        InspectionReferencesResolver.Resolve(document, positions["argument-reference"][0], includeDeclaration: true)
            .Should()
            .HaveCount(2)
            .And
            .OnlyContain(occurrence => occurrence.Identity == new InspectionSymbolIdentity(InspectionSymbolKind.Parameter, "callee", 0));

        InspectionReferencesResolver.Resolve(document, document.Text.IndexOf(' ', StringComparison.Ordinal), includeDeclaration: true)
            .Should()
            .BeEmpty();
        InspectionReferencesResolver.Resolve(document, -1, includeDeclaration: true)
            .Should()
            .BeEmpty();
        InspectionReferencesResolver.Resolve(document, document.Text.Length, includeDeclaration: true)
            .Should()
            .BeEmpty();
        InspectionReferencesResolver.Resolve(document, positions["local-definition"][0], includeDeclaration: false)
            .Should()
            .NotContain(occurrence => occurrence.IsDefinition);
        InspectionReferencesResolver.Resolve(document, positions["block-one-definition"][0], includeDeclaration: false)
            .Should()
            .BeEmpty();
        document.Text
            .Should()
            .Be(originalText);
        document.Occurrences
            .Should()
            .BeSameAs(originalOccurrences);
    }

    private static InspectionDocument CreateDocument(out IReadOnlyDictionary<string, IReadOnlyList<int>> positions)
    {
        (string Text, InspectionSymbolIdentity Identity, bool IsDefinition, string Key)[] items =
        [
            ("function", new InspectionSymbolIdentity(InspectionSymbolKind.Function, "caller"), true, "function-definition"),
            ("function", new InspectionSymbolIdentity(InspectionSymbolKind.Function, "caller"), false, "function-reference"),
            ("function", new InspectionSymbolIdentity(InspectionSymbolKind.Function, "another-function"), false, "foreign-function-reference"),
            ("parameter", new InspectionSymbolIdentity(InspectionSymbolKind.Parameter, "caller", 0), true, "parameter-definition"),
            ("parameter", new InspectionSymbolIdentity(InspectionSymbolKind.Parameter, "caller", 0), false, "parameter-reference"),
            ("parameter", new InspectionSymbolIdentity(InspectionSymbolKind.Parameter, "callee", 0), true, "foreign-parameter-definition"),
            ("local", new InspectionSymbolIdentity(InspectionSymbolKind.Local, "caller", 0), true, "local-definition"),
            ("local", new InspectionSymbolIdentity(InspectionSymbolKind.Local, "caller", 0), false, "local-reference"),
            ("local", new InspectionSymbolIdentity(InspectionSymbolKind.Local, "caller", 1), true, "local-one-definition"),
            ("temporary", new InspectionSymbolIdentity(InspectionSymbolKind.Temporary, "caller", 0), true, "temporary-definition"),
            ("temporary", new InspectionSymbolIdentity(InspectionSymbolKind.Temporary, "caller", 0), false, "temporary-reference"),
            ("block", new InspectionSymbolIdentity(InspectionSymbolKind.BasicBlock, "caller", 0), true, "block-definition"),
            ("block", new InspectionSymbolIdentity(InspectionSymbolKind.BasicBlock, "caller", 0), false, "block-reference"),
            ("block", new InspectionSymbolIdentity(InspectionSymbolKind.BasicBlock, "caller", 1), true, "block-one-definition"),
            ("argument", new InspectionSymbolIdentity(InspectionSymbolKind.Parameter, "callee", 0), false, "argument-reference"),
            ("argument", new InspectionSymbolIdentity(InspectionSymbolKind.Parameter, "other-callee", 0), false, "other-argument-reference")
        ];
        string text = string.Join(' ', items.Select(item => item.Text));
        List<InspectionSymbolOccurrence> occurrences = new(items.Length);
        Dictionary<string, IReadOnlyList<int>> itemPositions = new(StringComparer.Ordinal);
        int searchStart = 0;
        foreach ((string token, InspectionSymbolIdentity identity, bool isDefinition, string key) in items)
        {
            int start = text.IndexOf(token, searchStart, StringComparison.Ordinal);
            occurrences.Add(new InspectionSymbolOccurrence(identity, start, token.Length, isDefinition));
            itemPositions[key] = [start];
            searchStart = start + token.Length + 1;
        }

        positions = itemPositions;
        return new InspectionDocument(InspectionStage.StructuredIR, "caller", text, occurrences,
            new Dictionary<InspectionSymbolIdentity, InspectionSymbolMetadata>());
    }
}
