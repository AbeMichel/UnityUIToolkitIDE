using System;
using System.Collections.Generic;

namespace Tokenizing.Tokenizers
{
    public class CSharpTokenizer : RegexTokenizerBase
    {
        public override string CommentPrefix => "//";

        public CSharpTokenizer()
        {
            if (_rules == null) return;
            foreach (var rule in _initialRules)
            {
                _rules.Add(rule);
            }
        }

        #region Rules

        private static readonly IReadOnlyList<TokenRule> _initialRules = new[]
        {
            // Whitespace
            new TokenRule(@"\s+",                                       TokenType.Default),

            // Single-line comment
            new TokenRule(@"//[^\n]*",                                  TokenType.Comment),

            // Block comment open (close handled in multiline state)
            new TokenRule(@"/\*",                                       TokenType.Comment),

            // Verbatim strings
            new TokenRule(@"@""(?:[^""]|"""")*""",                     TokenType.StringLiteral),

            // Regular strings and chars
            new TokenRule(@"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'",   TokenType.StringLiteral),

            // Preprocessor directives
            new TokenRule(@"#(?:if|else|elif|endif|define|undef|" +
                          @"warning|error|line|region|endregion|pragma)\b", TokenType.Preprocessor),

            // Keywords
            new TokenRule(
                @"\b(?:abstract|as|base|bool|break|byte|case|catch|" +
                @"char|checked|class|const|continue|decimal|default|" +
                @"delegate|do|double|else|enum|event|explicit|extern|" +
                @"false|finally|fixed|float|for|foreach|goto|if|" +
                @"implicit|in|int|interface|internal|is|lock|long|" +
                @"namespace|new|null|object|operator|out|override|" +
                @"params|private|protected|public|readonly|record|ref|" +
                @"return|sbyte|sealed|short|sizeof|stackalloc|static|" +
                @"string|struct|switch|this|throw|true|try|typeof|" +
                @"uint|ulong|unchecked|unsafe|ushort|using|var|" +
                @"virtual|void|volatile|while|yield|async|await)\b",
                TokenType.Keyword),

            // Numbers (hex, float suffixes, integer suffixes)
            new TokenRule(
                @"\b0[xX][0-9a-fA-F]+[uUlL]*\b|\b\d+\.?\d*(?:[eE][+-]?\d+)?[fFdDmMuUlL]*\b",
                TokenType.Number),

            // Operators and punctuation
            new TokenRule(@"[+\-*/%&|^~<>=!?:]+|[\[\]{}().,:;]",      TokenType.Operator),
        };

        #endregion

        #region Block Comments

        protected override (int consumed, LineState exitState) ContinueMultilineToken(
            string line, int pos, LineState currentState, List<TextToken> tokens)
        {
            if (currentState != LineState.InBlockComment)
                return (0, currentState);

            int closeIndex = line.IndexOf("*/", pos, StringComparison.Ordinal);
            if (closeIndex == -1)
            {
                string span = line[pos..];
                if (span.Length > 0)
                    tokens.Add(new TextToken(span, TokenType.Comment));
                return (line.Length - pos, LineState.InBlockComment);
            }

            string closing = line[pos..(closeIndex + 2)];
            tokens.Add(new TextToken(closing, TokenType.Comment));
            return (closing.Length, LineState.Normal);
        }

        protected override LineState GetStateTransition(
            TokenType tokenType, string value, LineState currentState)
        {
            if (tokenType == TokenType.Comment && value == "/*" && currentState == LineState.Normal)
                return LineState.InBlockComment;

            return LineState.Normal;
        }

        #endregion
    }
}