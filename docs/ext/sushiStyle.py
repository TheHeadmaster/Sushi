from pygments.style import Style
from pygments.token import Token, Comment, Keyword, Name, String, \
     Error, Generic, Number, Operator

class SushiLexerStyle(Style):

    styles = {
        Token: '',
        Comment: '',
        Keyword: '#aa0000',
        Name: '#aa00aa',
        Name.Class: '',
        Name.Function: '',
        Operator: '#ffffb6',
        String: ''
    }