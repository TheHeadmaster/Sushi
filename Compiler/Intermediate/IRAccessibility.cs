namespace Sushi.Intermediate;

/// <summary>
/// Identifies the source-level accessibility retained by an IR function before backend linkage decisions are made.
/// </summary>
public enum IRAccessibility
{
    Public,
    Internal,
    Package,
    Protected,
    Private
}