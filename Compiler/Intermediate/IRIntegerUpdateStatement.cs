namespace Sushi.Intermediate;

/// <summary>
/// Executes an integer update while discarding its produced value.
/// The mutation remains observable.
/// </summary>
/// <param name="Expression">
/// The storage update to execute.
/// </param>
public sealed record IRIntegerUpdateStatement(IRIntegerUpdateExpression Expression) : IRStatement;