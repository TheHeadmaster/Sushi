namespace Sushi.Semantics;

/// <summary>
/// Identifies a built-in binary integer operation while preserving the
/// distinction between arithmetic, bitwise, and shift semantics.
/// </summary>
public enum IntegerBinaryOperator
{
    /// <summary>
    /// Performs checked integer addition.
    /// </summary>
    Add,

    /// <summary>
    /// Performs checked integer subtraction.
    /// </summary>
    Subtract,

    /// <summary>
    /// Performs checked integer multiplication.
    /// </summary>
    Multiply,

    /// <summary>
    /// Performs signed integer division with truncation toward zero.
    /// </summary>
    Divide,

    /// <summary>
    /// Computes signed integer remainder.
    /// </summary>
    Remainder,

    /// <summary>
    /// Performs fixed-width bitwise AND.
    /// </summary>
    BitwiseAnd,

    /// <summary>
    /// Performs fixed-width bitwise OR.
    /// </summary>
    BitwiseOr,

    /// <summary>
    /// Performs fixed-width bitwise XOR.
    /// </summary>
    BitwiseXor,

    /// <summary>
    /// Performs a checked left shift with a separately validated shift count.
    /// </summary>
    ShiftLeft,

    /// <summary>
    /// Performs an arithmetic right shift with a separately validated shift count.
    /// </summary>
    ShiftRight
}