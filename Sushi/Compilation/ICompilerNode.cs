using System.Diagnostics.CodeAnalysis;

namespace Sushi.Compilation;

public interface ICompilerNode
{
    public Task Compile([NotNull] CompilerVisitor compiler);

    public Task CompileHeader([NotNull] CompilerVisitor compiler);
}
