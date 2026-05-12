using Sushi.Parsing.Nodes.Expressions.Core;

namespace Sushi.Parsing.Nodes.Configuration;

public interface IModifierOrderThenConfiguration
{
    public IModifierOrderThenConfiguration ThenBy(Modifier modifier);
}
