using System;
using System.Collections.Generic;
using System.Text;
using Sushi.Diagnostics;

namespace Sushi.Verification;

public sealed class VerificationContext
{
    public List<CompilerMessage> Messages { get; } = [];
}
