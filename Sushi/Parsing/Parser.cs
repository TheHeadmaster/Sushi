using Sushi.Tokenization;

namespace Sushi.Parsing;

/// <summary>
/// Handles parsing a <see cref="List{T}"/> of <see cref="TokenFile"/> objects.
/// </summary>
public sealed class Parser
{
    /// <summary>
    /// Parses the source code from a <see cref="List{T}"/> of <see cref="TokenFile"/> objects into an <see cref="AbstractSyntaxTree"/>.
    /// </summary>
    /// <param name="tokenFiles">
    /// The source files to parse.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns an <see cref="AbstractSyntaxTree"/>.
    /// </returns>
    public async Task<AbstractSyntaxTree> ParseSource(List<TokenFile> tokenFiles)
    {
        AbstractSyntaxTree tree = new();

        return tree;
    }
}
