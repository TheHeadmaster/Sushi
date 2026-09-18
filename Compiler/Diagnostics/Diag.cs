using System.Diagnostics;
using Serilog;

namespace Sushi.Diagnostics;

/// <summary>
/// Contains methods for performing diagnostics.
/// </summary>
public static class Diag
{
    /// <summary>
    /// Monitors the function and logs the time it took to complete.
    /// </summary>
    /// <param name="label">
    /// The label of the task. This will be reported in the logs.
    /// </param>
    /// <param name="monitorAction">
    /// The monitor action.
    /// </param>
    public static async Task Monitor(string label, Func<Task> monitorAction)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(monitorAction);

        Stopwatch stopwatch = Stopwatch.StartNew();

        Log.Information("Starting {Label}...", label);

        await monitorAction();

        stopwatch.Stop();

        Log.Information("{Label} completed in {Time}.", label, stopwatch.Elapsed.AsFormattedString());
    }

    /// <summary>
    /// Monitors the function and logs the time it took to complete, then returns the value.
    /// </summary>
    /// <typeparam name="TResult">
    /// The type of the returned value.
    /// </typeparam>
    /// <param name="label">
    /// The label of the task. This will be reported in the logs.
    /// </param>
    /// <param name="monitorAction">
    /// The monitor action.
    /// </param>
    /// <returns>
    /// The result.
    /// </returns>
    public static async Task<TResult> Monitor<TResult>(string label, Func<Task<TResult>> monitorAction)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(monitorAction);

        Stopwatch stopwatch = Stopwatch.StartNew();

        Log.Information("Starting {Label}...", label);

        TResult result = await monitorAction();

        stopwatch.Stop();

        Log.Information("{Label} completed in {Time}.", label, stopwatch.Elapsed.AsFormattedString());

        return result;
    }
}