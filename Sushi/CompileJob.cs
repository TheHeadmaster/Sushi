using Sushi.Compilation;
using Sushi.Lexing;

namespace Sushi;

public sealed class CompileJob
{
    private List<SourceFile> sourceFiles = null!;

    /// <summary>
    /// Initializes the <see cref="CompileJob"/>.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task Initialize()
    {
        this.sourceFiles = [.. await SushiLexer.Initialize()];
        await ASMCompiler.Initialize();
    }

    /// <summary>
    /// Runs the <see cref="CompileJob"/>.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task Run()
    {
        await SushiLexer.LexFiles(this.sourceFiles);
        await ASMCompiler.Compile();
    }
}
