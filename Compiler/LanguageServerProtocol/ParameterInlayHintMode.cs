namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Controls which compiler-resolved parameter names are shown as editor annotations.
/// </summary>
internal enum ParameterInlayHintMode
{
    /// <summary>No parameter labels are displayed.</summary>
    Off,

    /// <summary>Redundant direct-identifier labels are suppressed.</summary>
    Selective,

    /// <summary>All eligible positional argument labels are displayed.</summary>
    All
}
