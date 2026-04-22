using UnityEngine;

namespace AbesIde.Tokenizing
{
    public enum TokenType
    {
        Default,
        Keyword,
        StringLiteral,
        Comment,
        Number,
        Operator,
        Error,
        Builtin,
        Decorator,
        Preprocessor,
        Todo
    }

    public struct TextToken
    {
        public string Text;
        public TokenType Type;

        public TextToken(string text, TokenType type)
        {
            Text = text;
            Type = type;
        }
    }
}