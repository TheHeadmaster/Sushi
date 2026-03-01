# sushi.py
import re

from pygments.lexer import RegexLexer, bygroups, include, using, this, default, words
from pygments.token import *

class SushiLexer(RegexLexer):
    name = 'Sushi'
    aliases = ['sushi', 'sus']
    filenames = ['*.sus']

    flags = re.MULTILINE | re.DOTALL

    tokens = {
        'root': [
            (r'//.*?\n', Token.Comment.Single),
            (r'/[*].*?[*]/', Token.Comment.Multiline),
            (r'(int32|float32|bool|true|false)', Token.Keyword),
            (r'[a-zA-Z_]\w*', Token.Name),
        ]
    }