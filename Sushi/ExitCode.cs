using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi;

/// <summary>
/// Represents a specific reason why the program has exited.
/// </summary>
public enum ExitCode
{
    Success,
    UnhandledException,
    ProjectPathNotSpecified,
    ProjectPathNotFound,
    InvalidParameterSyntax,
    LexingSyntaxError,
    ParsingSyntaxError,
}
