namespace Sushi.Parsing;

/// <summary>
/// Defines the precedence thresholds used by the expression parser.
/// </summary>
internal enum BindingPower
{
    Lowest = 0,
    Additive = 10,
    Multiplicative = 20
}