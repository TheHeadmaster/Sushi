from pygments.style import Style
from pygments.token import *



class SushiLexerStyle(Style):

    styles = {
        Token: '',
        Comment: '',
        Keyword: '#aa0000',
        Keyword.Constant: '#aa0000',
        Keyword.Declaration: '#aa0000',
        Keyword.Namespace: '#aa0000',
        Keyword.Pseudo: '#aa0000',
        Keyword.Reserved: '#aa0000',
        Keyword.Type : '#aa0000',
        Name: '#aa00aa',
        Name.Class: '',
        Name.Function: '',
        Operator: '#ffffb6',
        String: ''
    }