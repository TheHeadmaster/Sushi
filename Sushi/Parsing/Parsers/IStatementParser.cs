using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public interface IStatementParser
{
    public Task<StatementNode> Parse([NotNull] Parser parser, [NotNull] Token token);
}
