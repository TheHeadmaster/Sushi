namespace Sushi.Diagnostics;

/// <summary>
/// Represents a specific type of error, such as an invalid identifier or unresolved type.
/// </summary>
public class CompilerError
{
    /// <summary>
    /// Creates a new instance of <see cref="CompilerError"/>.
    /// </summary>
    protected CompilerError(int id, ErrorSeverity severity)
    {
        this.ID = severity is ErrorSeverity.Error ? $"SUSE{id:####}" : $"SUSWARN{id:####}";
        this.Severity = severity;
    }

    /// <summary>
    /// The ID of the error.
    /// </summary>
    public string ID { get; }

    /// <summary>
    /// The description of the error.
    /// </summary>
    public virtual required string Description { get; init; }

    /// <summary>
    /// The position information of the error.
    /// </summary>
    public required ErrorPosition Position { get; set; }

    /// <summary>
    /// The severity of the error.
    /// </summary>
    public ErrorSeverity Severity { get; }

    /// <summary>
    /// SUSE0001: The left-hand side of an assignment must be a variable.
    /// </summary>
    public static CompilerError SUSE0001(ErrorPosition position) => new(1, ErrorSeverity.Error)
    {
        Position = position,
        Description = "The left-hand side of an assignment must be a variable"
    };
}
