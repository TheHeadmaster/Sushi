from pygments.style import Style
from pygments.token import Token, Comment, Keyword, Name, String, \
     Error, Generic, Number, Operator

class SushiLexerStyle(Style):

    styles = {
        Token: '',
        Comment: '',
        Keyword: '#aa0000',
        Name: '#ffffb6',
        Name.Class: '',
        Name.Function: '',
        Operator: '#aa00aa',
        String: ''
    }