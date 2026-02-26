# sushi.py
from pygments.lexer import RegexLexer
from pygments.token import Token

class SushiLexer(RegexLexer):
    name = 'Sushi'
    aliases = ['sushi']
    tokens = {
        'root': [
            (r'MyKeyword', Token.Sushi.Keyword),
            (r'[a-zA-Z_]\w*', Token.Name),
            (r'\s', Token.Text),
            (r'.', Token.Operator),
        ]
    }