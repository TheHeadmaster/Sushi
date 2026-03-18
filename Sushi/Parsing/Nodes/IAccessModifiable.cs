using Sushi.Parsing.Core;

namespace Sushi.Parsing.Nodes;

public interface IAccessModifiable
{
    public AccessModifier AccessModifier { get; set; }
}
