namespace Sushi.Parsing.Nodes.Configuration;

/// <summary>
/// Contains configuration methods for access modifiers.
/// </summary>
public interface IAccessModifierConfiguration
{
    /// <summary>
    /// Sets the node to require an access modifier.
    /// </summary>
    /// <param name="value">
    /// True if an access modifier is required. False otherwise. Default is true.
    /// </param>
    /// <returns>
    /// The <see cref="IAccessModifierConfiguration"/>.
    /// </returns>
    public IAccessModifierConfiguration IsRequired(bool value = true);

    /// <summary>
    /// Sets the node to require that only one access modifier be defined.
    /// </summary>
    /// <param name="value">
    /// True if the access modifier is single. False otherwise.
    /// </param>
    /// <returns>
    /// The <see cref="IAccessModifierConfiguration"/>.
    /// </returns>
    public IAccessModifierConfiguration IsSingle(bool value = true);
}
