using LLVMSharp.Interop;

namespace Sushi.Backends.LLVM;

/// <summary>
/// Owns an LLVM module together with the context in which it was created.
/// </summary>
public sealed class LLVMModuleArtifact : IModuleArtifact
{
    private LLVMContextRef context;
    private LLVMModuleRef module;
    private bool disposed;

    /// <summary>
    /// Gets the generated LLVM module.
    /// </summary>
    /// <remarks>
    /// The module is owned by this artifact and shall not be disposed independently.
    /// </remarks>
    public LLVMModuleRef Module
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);

            return this.module;
        }
    }

    internal LLVMContextRef Context
    {
        get
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);

            return this.context;
        }
    }

    internal LLVMModuleArtifact(LLVMContextRef context, LLVMModuleRef module)
    {
        this.context = context;
        this.module = module;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.module.Dispose();
        this.module = default;

        this.context.Dispose();
        this.context = default;

        this.disposed = true;
    }
}