namespace Sushi;

/// <summary>
/// Determines by which method the Language Server Protocol is transported between server and client.
/// </summary>
public enum LSPTransportMethod
{
    None,
    Stdio,
    TCP
}