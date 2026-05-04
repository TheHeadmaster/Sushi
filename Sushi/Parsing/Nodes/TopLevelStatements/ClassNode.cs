using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes.TopLevelStatements;

/// <summary>
/// Represents a class declaration.
/// </summary>
/// <param name="classToken">
/// The <see cref="Token"/> that marks the start of the entire class declaration.
/// </param>
/// <param name="typeName">
/// The name as a <see cref="TypeNode"/>.
/// </param>
/// <param name="members">
/// The class members.
/// </param>
/// <param name="terminatorToken">
/// The terminator token that is expected to be at the end of the node.
/// Not all statements require a terminator, such as sub-statements.
/// </param>
/// <param name="openingSquiggly">
/// The opening squiggly token.
/// </param>
/// <param name="closingSquiggly">
/// The closing squiggly token.
/// </param>
public sealed class ClassNode(
    [NotNull] Token classToken,
    TypeNode? typeName,
    [NotNull] List<StatementNode> members,
    Token? terminatorToken,
    Token? openingSquiggly,
    Token? closingSquiggly)
    : StatementNode(terminatorToken), ICanBeStatic, IAccessModifiable
{
    /// <inheritdoc />
    public bool IsStatic { get; set; }

    /// <summary>
    /// The name as a <see cref="TypeNode"/>.
    /// </summary>
    public TypeNode? TypeName { get; set; } = typeName;

    /// <summary>
    /// The class members.
    /// </summary>
    public List<StatementNode> Members { get; set; } = members;

    /// <inheritdoc />
    public AccessModifier AccessModifier { get; set; }

    /// <inheritdoc />
    public Token? AccessModifierToken { get; set; }

    /// <inheritdoc />
    public Token? StaticToken { get; set; }

    /// <inheritdoc />
    public Token? OpeningSquiggly { get; set; } = openingSquiggly;

    /// <inheritdoc />
    public Token? ClosingSquiggly { get; set; } = closingSquiggly;

    /// <inheritdoc />
    public bool AllowsModifier(AccessModifier modifier) => modifier switch
    {
        AccessModifier.Public or AccessModifier.Internal => true,
        _ => false
    };

    /// <inheritdoc />
    public override async IAsyncEnumerable<CompilerMessage> GetMessages()
    {
        if (this.TypeName is not null)
        {
            await foreach (CompilerMessage message in this.TypeName.GetMessages())
            {
                yield return message;
            }
        }

        foreach (StatementNode member in this.Members)
        {
            await foreach (CompilerMessage message in member.GetMessages())
            {
                yield return message;
            }
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<Token> GetTokens()
    {
        if (this.AccessModifierToken is not null)
        {
            yield return this.AccessModifierToken;
        }

        if (this.StaticToken is not null)
        {
            yield return this.StaticToken;
        }

        yield return classToken;

        if (this.TypeName is not null)
        {
            await foreach (Token token in this.TypeName.GetTokens())
            {
                yield return token;
            }
        }

        if (this.OpeningSquiggly is not null)
        {
            yield return this.OpeningSquiggly;
        }

        foreach (StatementNode statement in this.Members)
        {
            await foreach (Token token in statement.GetTokens())
            {
                yield return token;
            }
        }

        if (this.ClosingSquiggly is not null)
        {
            yield return this.ClosingSquiggly;
        }
    }
}

