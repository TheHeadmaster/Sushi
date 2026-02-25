# sushi.py
from pygments.lexer import RegexLexer
from pygments import token

class SushiLexer(RegexLexer):
    name = 'Sushi'
    aliases = ['sushi']
    tokens = {
        'root': [
            (r'MyKeyword', token.Keyword),
            (r'[a-zA-Z_]\w*', token.Name),
            (r'\s', token.Text),
            (r'.', token.Operator),
        ]
    }