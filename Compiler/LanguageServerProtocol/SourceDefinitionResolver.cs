using Sushi.Analysis;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Resolves ordinary source definitions exclusively from compiler-bound semantic occurrences.
/// </summary>
internal static class SourceDefinitionResolver
{
    /// <summary>
    /// Finds and validates the authoritative declaration token for a source position.
    /// </summary>
    /// <param name="analysis">The project-aware analysis containing the selected occurrence.</param>
    /// <param name="offset">The cursor offset in canonical UTF-8 bytes.</param>
    /// <param name="cancellationToken">Cancels occurrence and provenance validation.</param>
    /// <returns>The defining name-token span, or null when the occurrence has no source definition.</returns>
    internal static SourceSpan? Resolve(SourceAnalysisResult analysis, int offset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        cancellationToken.ThrowIfCancellationRequested();
        SourceSemanticOccurrence? occurrence = SourceSemanticOccurrenceLookup.FindOccurrence(
            analysis.SemanticOccurrences, analysis.Snapshot, offset);
        if (occurrence?.Symbol.DefinitionSpan is not SourceSpan definition)
        {
            return null;
        }

        SourceSnapshot snapshot = definition.Snapshot;
        if (definition.Start < 0 || definition.Start >= definition.End || definition.End > snapshot.SourceLength
            || !snapshot.IsValidUtf8)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            (int startLine, int startCharacter) = snapshot.GetUtf16Position(definition.Start);
            (int endLine, int endCharacter) = snapshot.GetUtf16Position(definition.End);
            if (snapshot.GetByteOffset(startLine, startCharacter) != definition.Start
                || snapshot.GetByteOffset(endLine, endCharacter) != definition.End)
            {
                return null;
            }
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return definition;
    }
}
