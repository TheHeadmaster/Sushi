using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when a destroyer doesn't call a destroyer for one of the members of the owning class.
/// </summary>
/// <param name="startToken">
/// The token where the error starts.
/// </param>
public sealed class UndestroyedMemberError([NotNull] Token startToken, List<MemberDeclarationNode> members) : CompilerMessage(startToken.CurrentLine, startToken.LineNumber, startToken.LinePosition)
{
    /// <inheritdoc />
    public override int MessageNumber => 10;

    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"Undestroyed members \"{string.Join(',', members.Select(x => x.Identifier?.Name ?? string.Empty))}\" in destroyer \"{startToken.Value}\" ");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(startToken.Value.Length);
}