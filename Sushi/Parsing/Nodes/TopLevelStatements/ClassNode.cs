using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Sushi.Diagnostics;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Nodes.Configuration;
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
/// <param name="openingSquiggly">
/// The opening squiggly token.
/// </param>
/// <param name="closingSquiggly">
/// The closing squiggly token.
/// </param>
/// <param name="filePath">
/// The file path that the node came from.
/// </param>
public sealed class ClassNode(
    [NotNull] Token classToken,
    TypeNode? typeName,
    [NotNull] List<StatementNode> members,
    Token? openingSquiggly,
    Token? closingSquiggly,
    [NotNull] string filePath)
    : StatementNode(null), IConfigurableNode<ClassNode>
{

    /// <summary>
    /// The name as a <see cref="TypeNode"/>.
    /// </summary>
    public TypeNode? TypeName { get; set; } = typeName;

    /// <summary>
    /// The class members.
    /// </summary>
    public List<StatementNode> Members { get; set; } = members;

    /// <inheritdoc />
    public Token? OpeningSquiggly { get; set; } = openingSquiggly;

    /// <inheritdoc />
    public Token? ClosingSquiggly { get; set; } = closingSquiggly;

    /// <inheritdoc />
    public List<AccessModifier> Modifiers { get; set; } = [];

    /// <inheritdoc />
    public List<Token> ModifierTokens { get; set; } = [];

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

        Token? accessModifierDefined = null;
        bool staticDefined = false;

        foreach (AccessModifier modifier in this.Modifiers)
        {
            string modifierName = Constants.TryGetModifierKeyword(modifier);
            Token existing = this.ModifierTokens.First(x => x.Value == modifierName);

            if (modifier is AccessModifier. accessModifierDefined is not null)
            {
                yield return new IllegalModifierError(existing, $"Class already has access modifier \"{accessModifierDefined.Value}\", but \"{existing.Value}\" was also declared", filePath);
            }
            else
            {
                accessModifierDefined = existing;
            }

            if (!this.AllowsModifier(modifier))
            {
                yield return new IllegalModifierError(this.ModifierTokens.First(x => x.Value == modifierName), $"Classes cannot be {modifierName}", filePath);
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
        foreach (Token modifierToken in this.ModifierTokens)
        {
            yield return modifierToken;
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

    /// <inheritdoc />
    public Task Configure([NotNull] INodeConfigurationContext<ClassNode> context)
    {
        context
            .HasAccess(AccessModifier.Public, AccessModifier.Internal)
            .IsRequired()
            .IsSingle();

        context.HasStatic();

        context.HasModifierOrder()
            .ByAccessModifier()
            .ThenBy(Modifier.Static);

        context.HasChild(x => x.TypeName)
            .IsRequired();

        context.HasChildren(x => x.Members);

        context.HasToken(x => x.OpeningSquiggly)
            .IsRequired();

        context.HasToken(x => x.ClosingSquiggly)
            .IsRequired();

        return Task.CompletedTask;
    }

    public Task Parse([NotNull] IParserConfigurationContext<ClassNode> context)
    {
        context
            .Modifiers()
            .Keyword(TokenType.Class)
            .TypeName()
            .Enumerate(TokenType.OpeningSquiggly, TokenType.ClosingSquiggly, x =>
            {
                x.Statement(Member)
            });

    }

}

