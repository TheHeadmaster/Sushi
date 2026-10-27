namespace Sushi.Semantics;

/// <summary>
/// Identifies a built-in mathematical integer comparison.
/// </summary>
public enum IntegerComparisonOperator
{
    /// <summary>
    /// Tests whether the operands have equal mathematical values.
    /// </summary>
    Equal,

    /// <summary>
    /// Tests whether the operands have different mathematical values.
    /// </summary>
    NotEqual,

    /// <summary>
    /// Tests whether the left operand is smaller than the right operand.
    /// </summary>
    LessThan,

    /// <summary>
    /// Tests whether the left operand is no greater than the right operand.
    /// </summary>
    LessThanOrEqual,

    /// <summary>
    /// Tests whether the left operand is greater than the right operand.
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Tests whether the left operand is no smaller than the right operand.
    /// </summary>
    GreaterThanOrEqual
}