using System;
using System.Collections.Generic;

namespace AbesIde.Tokenizing.Tokenizers
{
    public class PythonTokenizer : RegexTokenizerBase
    {
        public override string CommentPrefix => "#";

        public PythonTokenizer()
        {
            if (_rules == null) return; // Should not happen with field initializer in base
            foreach (var rule in _initialRules)
            {
                _rules.Add(rule);
            }
        }

        #region Rules

        private static readonly IReadOnlyList<TokenRule> _initialRules = new[]
        {
            // Whitespace
            new TokenRule(@"\s+", TokenType.Default),

            // Comments
            new TokenRule(@"#.*?\bTODO\b.*", TokenType.Todo),
            new TokenRule(@"#[^\n]*", TokenType.Comment),

            // Triple-quoted strings (open-close handled in multiline state)
            new TokenRule(@"(""""""|\'\'\').*?(?:\1|$)", TokenType.StringLiteral),

            // Single-line strings
            new TokenRule(@"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'", TokenType.StringLiteral),

            // Keywords
            new TokenRule(@"\b(?:False|None|True|and|as|assert|async|await|" +
                          @"break|class|continue|def|del|elif|else|except|" +
                          @"finally|for|from|global|if|import|in|is|lambda|" +
                          @"nonlocal|not|or|pass|raise|return|try|while|with|yield)\b",
                TokenType.Keyword),

            // Builtins
            new TokenRule(@"\b(?:print|len|range|type|int|str|float|list|dict|" +
                          @"set|tuple|bool|input|open|enumerate|zip|map|filter)\b",
                TokenType.Builtin),

            // Decorators
            new TokenRule(@"@\w+", TokenType.Decorator),

            // Numbers
            new TokenRule(@"\b0[xX][0-9a-fA-F]+\b|\b\d+\.?\d*\b", TokenType.Number),

            // Operators
            new TokenRule(@"[+\-*/%&|^~<>=!]+|[\[\]{}().,:;]", TokenType.Operator)
        };
        
        #endregion

        #region Multiline Tokens

        protected override (int consumed, LineState exitState) ContinueMultilineToken(
            string line, int pos, LineState currentState, List<TextToken> tokens)
        {
            if (currentState == LineState.InTodoBlock)
            {
                var remaining = line[pos..];
                var trimmed = remaining.TrimStart();
                if (trimmed.StartsWith("#"))
                {
                    tokens.Add(new TextToken(remaining, TokenType.Todo));
                    return (remaining.Length, LineState.InTodoBlock);
                }
                
                // Any non-comment line breaks the TODO block
                return (0, LineState.Normal);
            }

            if (currentState != LineState.InMultilineStringDouble && currentState != LineState.InMultilineStringSingle)
                return (0, currentState);

            var quote = (currentState == LineState.InMultilineStringDouble) ? "\"\"\"" : "'''";
            
            // Scan forward looking for the closing triple quote
            int closeIndex = line.IndexOf(quote, pos, StringComparison.Ordinal);
            if (closeIndex == -1)
            {
                // Entire remaining line is still inside the string
                string span = line[pos..];
                if (span.Length > 0)
                    tokens.Add(new TextToken(span, TokenType.StringLiteral));
                return (line.Length - pos, currentState);
            }

            // Found the closing triple quote — consume up to and including it
            string closing = line[pos..(closeIndex + 3)];
            tokens.Add(new TextToken(closing, TokenType.StringLiteral));
            return (closing.Length, LineState.Normal);
        }

        protected override LineState GetStateTransition(
            TokenType tokenType, string value, LineState currentState)
        {
            if (currentState == LineState.Normal)
            {
                if (tokenType == TokenType.Todo)
                    return LineState.InTodoBlock;

                if (tokenType == TokenType.StringLiteral)
                {
                    // Transition if it starts with triple quotes but doesn't end with them
                    if (value.StartsWith("\"\"\""))
                    {
                        if (value.Length < 6 || !value.EndsWith("\"\"\""))
                            return LineState.InMultilineStringDouble;
                    }
                    else if (value.StartsWith("'''"))
                    {
                        if (value.Length < 6 || !value.EndsWith("'''"))
                            return LineState.InMultilineStringSingle;
                    }
                }
            }

            return currentState;
        }

        public override List<TextToken> TokenizeLine(string line, LineState initialState, out LineState exitState)
        {
            if (initialState == LineState.InTodoBlock && string.IsNullOrWhiteSpace(line))
            {
                exitState = LineState.Normal;
                return base.TokenizeLine(line, LineState.Normal, out _);
            }
            return base.TokenizeLine(line, initialState, out exitState);
        }

        #endregion
    }
}