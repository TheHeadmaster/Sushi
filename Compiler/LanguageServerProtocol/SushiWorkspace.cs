using System.Collections.Concurrent;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Represents an open workspace.
/// </summary>
public sealed class SushiWorkspace
{
    /// <summary>
    /// Contains source documents tied to their <see cref="Uri"/>.
    /// </summary>
    private readonly ConcurrentDictionary<Uri, SourceDocument> documents = [];

}