using System.Diagnostics.CodeAnalysis;

namespace Sushi.Compilation;

public interface ICompilerNode
{
    public Task Compile([NotNull] Compiler compiler);
}
