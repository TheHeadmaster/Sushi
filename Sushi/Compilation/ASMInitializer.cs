using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Sushi.Compilation;

public sealed class ASMInitializer
{
    public string Name { get; set; }

    public string Type { get; set; }

    public string? Value { get; set; }

    private static readonly Dictionary<string, string> types = new()
    {
        { "Int32", "dd" },
        { "Float32", "dd" },
        { "Boolean", "db" }
    };

    public void Print([NotNull] StringBuilder sb)
    {
        sb.Append($"    {this.Name} {types[this.Type]}");
        if (!string.IsNullOrWhiteSpace(this.Value))
        {
            sb.Append($" {this.Value}");
        }

        sb.Append(Environment.NewLine);
    }
}
