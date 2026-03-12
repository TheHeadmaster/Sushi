using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public abstract class SyntaxNode : ICompilerNode
{
    public virtual Task Compile([NotNull] Compiler compiler) => Task.CompletedTask;

    public virtual Task CompileHeader([NotNull] Compiler compiler) => Task.CompletedTask;

    public abstract Token GetStartToken();

    public abstract Task Verify(VerificationContext context);
}
