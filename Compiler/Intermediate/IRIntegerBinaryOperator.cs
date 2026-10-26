namespace Sushi.Intermediate;

/// <summary>
/// Identifies a concrete binary integer operation in structured Sushi IR.
/// </summary>
public enum IRIntegerBinaryOperator
{
    /// <summary>
    /// Performs checked addition.
    /// </summary>
    Add,

    /// <summary>
    /// Performs checked subtraction.
    /// </summary>
    Subtract,

    /// <summary>
    /// Performs checked multiplication.
    /// </summary>
    Multiply,

    /// <summary>
    /// Performs signed division.
    /// </summary>
    Divide,

    /// <summary>
    /// Performs signed remainder.
    /// </summary>
    Remainder,

    /// <summary>
    /// Performs bitwise AND.
    /// </summary>
    BitwiseAnd,

    /// <summary>
    /// Performs bitwise OR.
    /// </summary>
    BitwiseOr,

    /// <summary>
    /// Performs bitwise XOR.
    /// </summary>
    BitwiseXor,

    /// <summary>
    /// Performs checked left shifting.
    /// </summary>
    ShiftLeft,

    /// <summary>
    /// Performs arithmetic right shifting.
    /// </summary>
    ShiftRight
}