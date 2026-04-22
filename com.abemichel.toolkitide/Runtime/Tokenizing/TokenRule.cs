using System.Text.RegularExpressions;

namespace AbesIde.Tokenizing
{
    public struct TokenRule
    {
        public Regex Pattern;
        public string OriginalPattern;
        public TokenType Type;

        public TokenRule(string pattern, TokenType type)
        {
            // Anchored to current position via \G so we always
            // match at the scan head, never skip characters
            Pattern = new Regex(@"\G(?:" + pattern + ")",
                RegexOptions.Compiled | RegexOptions.Multiline);
            OriginalPattern = pattern;
            Type = type;
        }
    }
}