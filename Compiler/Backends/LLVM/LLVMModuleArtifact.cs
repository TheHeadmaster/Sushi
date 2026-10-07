using LLVMSharp.Interop;

namespace Sushi.Backends.LLVM;

/// <summary>
/// Owns an LLVM module together with the context in which it was created.
/// </summary>
public sealed class LLVMModuleArtifact : IDisposable
{
    private bool disposed;

    /// <summary>
    /// Gets the generated LLVM module.
    /// </summary>
    /// <remarks>
    /// The module is owned by this artifact and shall not be disposed independently.
    /// </remarks>
    public LLVMModuleRef Module { get; }

    internal LLVMContextRef Context { get; }

    internal LLVMModuleArtifact(LLVMContextRef context, LLVMModuleRef module)
    {
        this.Context = context;
        this.Module = module;
    }

    /// <summary>
    /// Releases the native LLVM module and context.
    /// </summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.Module.Dispose();
        this.Context.Dispose();
        this.disposed = true;
    }
}