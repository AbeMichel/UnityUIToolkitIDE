using System.Collections.Generic;
using AbesIde.Document;

namespace AbesIde.Tokenizing
{
    public abstract class RegexTokenizerBase : ITokenizer
    {
        #region Rule Definition
        
        public abstract string CommentPrefix { get; }

        protected readonly List<TokenRule> _rules = new();

        #endregion

        #region ITokenizer

        public List<List<TextToken>> Tokenize(TextDocument document)
        {
            var result = new List<List<TextToken>>(document.LineCount);
            var state = LineState.Normal;

            for (var i = 0; i < document.LineCount; i++)
            {
                result.Add(TokenizeLine(document.GetLine(i), state, out state));
            }
            
            return result;
        }

        public virtual List<TextToken> TokenizeLine(string line, LineState initialState, out LineState exitState)
        {
            var tokens = new List<TextToken>();
            var state = initialState;
            var pos = 0;

            while (pos < line.Length)
            {
                // Let the subclass handle carry-over states
                // (e.g. we're mid block-comment from a previous line)
                if (state != LineState.Normal)
                {
                    var (consumed, exitedState) = ContinueMultilineToken(line, pos, state, tokens);

                    if (consumed > 0)
                    {
                        pos += consumed;
                        state = exitedState;
                        continue;
                    }
                }

                var matched = false;
                foreach (var rule in _rules)
                {
                    var match = rule.Pattern.Match(line, pos);
                    if (!match.Success) continue;
                    
                    // Check if this token opens a multiline span
                    var newState = GetStateTransition(rule.Type, match.Value, state);
                    
                    tokens.Add(new TextToken(match.Value, rule.Type));
                    pos += match.Length;
                    state = newState;
                    matched = true;
                    break;
                }

                if (!matched)
                {
                    // No rule matched so emit a single default character
                    // so we always make forward progress
                    tokens.Add(new TextToken(line[pos].ToString(), TokenType.Default));
                    pos++;
                }
            }
            
            exitState = state;
            return tokens;
        }

        public void AddRule(TokenRule rule)
        {
            _rules.Add(rule);
        }

        public void AddRules(params TokenRule[] rules) { foreach (var rule in rules) AddRule(rule); }

        public void RemoveRule(string pattern)
        {
            _rules.RemoveAll(r => r.OriginalPattern == pattern);
        }
        
        public void RemoveRules(params TokenRule[] rules) { foreach (var rule in rules) RemoveRule(rule.OriginalPattern); }
        public void RemoveRules(params string[] patterns) { foreach (var pattern in patterns) RemoveRule(pattern); }

        #endregion

        #region Vitual Hooks for Subclasses
        
        /// <summary>
        /// Called when we are inside a multiline token at the start of a line.
        /// Returns how many characters were consumed and the resulting state.
        /// </summary>
        protected virtual (int consumed, LineState exitState) ContinueMultilineToken(string line, int pos,
            LineState currentState, List<TextToken> tokens) => (0, currentState);

        
        /// <summary>
        /// Called after a token is matched to determine if it transitions
        /// into a multiline state (e.g. opening a block comment).
        /// </summary>
        protected virtual LineState GetStateTransition(TokenType tokenType, string value, LineState currentState) =>
            LineState.Normal;

        #endregion

        #region Helpers

        protected static bool TokenListEquals(List<TextToken> a, List<TextToken> b)
        {
            if (a == null || b == null) return a == b;
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
            {
                if (a[i].Type != b[i].Type || a[i].Text != b[i].Text) return false;
            }
            return true;
        }

        #endregion
    }
}