using System;
using System.Collections.Generic;
using System.Text;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public interface IParser
{
    public BindingPower Power();

    /*
    public List<TokenType> AllowedStartTokens { get; }

    public TokenType? TerminatingToken { get; }
    */
}
