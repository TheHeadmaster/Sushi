namespace Sushi.Parsing.Core;

/// <summary>
/// Represents how tightly an expression binds, which determines things like operator precedence.
/// </summary>
public enum BindingPower
{
    /// <summary>
    /// The lowest binding power.
    /// </summary>
    Primary = 0,

    /// <summary>
    /// Assignment, sush as equals.
    /// </summary>
    Assignment = 1,

    /// <summary>
    /// Conditionals, such as && and ||.
    /// </summary>
    Conditional = 2,

    /// <summary>
    /// For things like + or -.
    /// </summary>
    SumDifference = 3,

    /// <summary>
    /// For things like / or *.
    /// </summary>
    ProductQuotient = 4,

    /// <summary>
    /// For exponentials like ^.
    /// </summary>
    Exponent = 5,

    /// <summary>
    /// For prefix operators like the negative sign and prefix increment/decrement.
    /// </summary>
    Prefix = 6,

    /// <summary>
    /// For postfix operators like postfix increment/decrement.
    /// </summary>
    Postfix = 7,

    /// <summary>
    /// The navigation operator, i.e. the . token.
    /// </summary>
    Navigation = 8,

    /// <summary>
    /// For method calls which are technically operators.
    /// </summary>
    Call = 9,

    /// <summary>
    /// For the create keyword.
    /// </summary>
    Create = 10
}
