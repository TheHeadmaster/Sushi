namespace Sushi.Intermediate;

/// <summary>
/// Identifies a concrete unary integer operation in structured Sushi IR.
/// </summary>
public enum IRIntegerUnaryOperator
{
    /// <summary>
    /// Preserves the integer operand.
    /// </summary>
    Plus,

    /// <summary>
    /// Computes checked signed negation.
    /// </summary>
    Negate,

    /// <summary>
    /// Complements the fixed-width integer representation.
    /// </summary>
    Complement
}