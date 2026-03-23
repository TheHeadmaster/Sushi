using System.Diagnostics.CodeAnalysis;
using Serilog;
using Sushi.Tokenization;

namespace Sushi.Diagnostics;

/// <summary>
/// Contains methods for performing diagnostics.
/// </summary>
public static class Diag
{
    /// <summary>
    /// Monitors the task function and logs the time it took to complete.
    /// </summary>
    /// <param name="label">
    /// The label of the task. This will be reported in the logs.
    /// </param>
    /// <param name="monitorTask">
    /// The monitor task.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task" />.
    /// </returns>
    public static async Task MonitorAsync([NotNull] string label, [NotNull] Func<Task> monitorTask)
    {
        DateTime startTime = DateTime.Now;
        
        Log.Information("Starting {Label}...", label);
        
        await monitorTask.Invoke();

        Log.Information("{Label} completed in {Time}.", label, startTime.TimeSinceAsString());
    }

    /// <summary>
    /// Monitors the task function and logs the time it took to complete, then returns the value.
    /// </summary>
    /// <param name="label">
    /// The label of the task. This will be reported in the logs.
    /// </param>
    /// <param name="monitorTask">
    /// The monitor task.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task" /> that returns the result.
    /// </returns>
    public static async Task<TResult> MonitorAsync<TResult>([NotNull] string label, [NotNull] Func<Task<TResult>> monitorTask)
    {
        DateTime startTime = DateTime.Now;
        
        Log.Information("Starting {Label}...", label);
        
        TResult result = await monitorTask.Invoke();
        
        Log.Information("{Label} completed in {Time}.", label, startTime.TimeSinceAsString());

        return result;
    }
}
