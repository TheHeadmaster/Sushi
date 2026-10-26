namespace Sushi.Semantics;

/// <summary>
/// Identifies a built-in unary integer operation after semantic resolution.
/// </summary>
public enum IntegerUnaryOperator
{
    /// <summary>
    /// Preserves the operand's integer value.
    /// </summary>
    Plus,

    /// <summary>
    /// Computes the checked mathematical negation of the operand.
    /// </summary>
    Negate,

    /// <summary>
    /// Complements the operand's fixed-width two's-complement representation.
    /// </summary>
    Complement
}