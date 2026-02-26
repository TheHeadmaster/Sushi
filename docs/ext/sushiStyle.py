from pygments.style import Style
from pygments.token import *

Sushi.Keyword

class SushiLexerStyle(Style):

    styles = {
        Token: '',
        Comment: '',
        Sushi.Keyword: '#aa0000',
        Name: '#aa00aa',
        Name.Class: '',
        Name.Function: '',
        Operator: '#ffffb6',
        String: ''
    }