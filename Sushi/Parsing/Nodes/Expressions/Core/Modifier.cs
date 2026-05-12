using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi.Parsing.Nodes.Expressions.Core;

/// <summary>
/// Represents a modifier to another node that changes its behavior, such as its access restrictions or its instantiability.
/// </summary>
public enum Modifier
{
    /// <summary>
    /// This node is public, meaning it can be accessed anywhere.
    /// </summary>
    Public,

    /// <summary>
    /// This node is internal, meaning it can only be accessed within the same assembly.
    /// </summary>
    Internal,

    /// <summary>
    /// This node is private, meaning it can only be accessed by its parent node or its siblings.
    /// </summary>
    Private,

    /// <summary>
    /// This node is static, meaning it is instance agnostic and/or cannot be instantiated.
    /// </summary>
    Static
}
