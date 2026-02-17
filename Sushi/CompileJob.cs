using System;
using System.Collections.Generic;
using System.Text;
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
    public async Task Initialize()
    {

    }

    /// <summary>
    /// Runs the <see cref="CompileJob"/>.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task Run() => await ASMCompiler.Compile();
}
