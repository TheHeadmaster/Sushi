
namespace Sushi.Semantics;

/// <summary>
/// Identifies the lifetime policy governing an activity after its entry method completes.
/// </summary>
public enum ActivityLifetime
{
    /// <summary>
    /// Entry completion initiates the activity's normal stopping lifecycle.
    /// </summary>
    Task,

    /// <summary>
    /// Entry completion does not initiate stopping; the activity remains alive until closure is requested.
    /// </summary>
    Persistent
}
