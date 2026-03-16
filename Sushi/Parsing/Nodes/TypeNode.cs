using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a type that is defined somewhere else in the code.
/// </summary>
/// <param name="token">
/// The <see cref="Token"/> used to mark the start of the node.
/// </param>
public class TypeNode([NotNull] Token token) : StatementNode
{
    /// <summary>
    /// The name of the type.
    /// </summary>
    public string Name { get; set; } = token.Type is TokenType.Identifier ? token.Value : Constants.TryGetPrimitiveType(token);

    /// <inheritdoc />
    public override Token GetStartToken() => token;

    /// <inheritdoc />
    public override Task Verify(VerificationContext context) => Task.CompletedTask;
}
