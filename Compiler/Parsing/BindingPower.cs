namespace Sushi.Parsing;

/// <summary>
/// Defines the precedence thresholds used by the expression parser.
/// Larger values bind more tightly.
/// </summary>
internal enum BindingPower
{
    Lowest = 0,
    LogicalOr = 1,
    LogicalAnd = 2,
    Equality = 3,
    Relational = 4,
    BitwiseOr = 6,
    BitwiseXor = 7,
    BitwiseAnd = 8,
    Shift = 9,
    Additive = 10,
    Multiplicative = 20,
    UnaryPrefix = 30
}