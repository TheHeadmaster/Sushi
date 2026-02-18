using Sushi.Compilation;

namespace Sushi;

public sealed class CompileJob
{
    /// <summary>
    /// Initializes the <see cref="CompileJob"/>.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task Initialize() => await ASMCompiler.Initialize();

    /// <summary>
    /// Runs the <see cref="CompileJob"/>.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task Run() => await ASMCompiler.Compile();
}
