using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autocomplete;
using Document;
using Tokenizing;

namespace Providers
{
    public class PythonAutocompleteProvider : IAutocompleteProvider
    {
        private readonly List<AutocompleteSuggestion> _suggestions;
        private static readonly Regex _varRegex = new(@"^\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*=", RegexOptions.Compiled);
        private static readonly Regex _defRegex = new(@"^\s*def\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*\(", RegexOptions.Compiled);
        private static readonly Regex _classRegex = new(@"^\s*class\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*[:\(]", RegexOptions.Compiled);

        public PythonAutocompleteProvider()
        {
            _suggestions = new List<AutocompleteSuggestion>(_initialSuggestions);
        }

        private static readonly List<AutocompleteSuggestion> _initialSuggestions = new()
        {
            // Keywords
            new AutocompleteSuggestion("False", "Boolean False", TokenType.Keyword),
            new AutocompleteSuggestion("None", "None value", TokenType.Keyword),
            new AutocompleteSuggestion("True", "Boolean True", TokenType.Keyword),
            new AutocompleteSuggestion("and", "Logical AND", TokenType.Keyword),
            new AutocompleteSuggestion("as", "Alias", TokenType.Keyword),
            new AutocompleteSuggestion("assert", "Assertion", TokenType.Keyword),
            new AutocompleteSuggestion("async", "Async function", TokenType.Keyword),
            new AutocompleteSuggestion("await", "Await async function", TokenType.Keyword),
            new AutocompleteSuggestion("break", "Break loop", TokenType.Keyword),
            new AutocompleteSuggestion("class", "Class definition", TokenType.Keyword),
            new AutocompleteSuggestion("continue", "Continue loop", TokenType.Keyword),
            new AutocompleteSuggestion("def", "Function definition", TokenType.Keyword),
            new AutocompleteSuggestion("del", "Delete", TokenType.Keyword),
            new AutocompleteSuggestion("elif", "Else if", TokenType.Keyword),
            new AutocompleteSuggestion("else", "Else", TokenType.Keyword),
            new AutocompleteSuggestion("except", "Exception handler", TokenType.Keyword),
            new AutocompleteSuggestion("finally", "Finally block", TokenType.Keyword),
            new AutocompleteSuggestion("for", "For loop", TokenType.Keyword),
            new AutocompleteSuggestion("from", "Import from", TokenType.Keyword),
            new AutocompleteSuggestion("global", "Global variable", TokenType.Keyword),
            new AutocompleteSuggestion("if", "If statement", TokenType.Keyword),
            new AutocompleteSuggestion("import", "Import module", TokenType.Keyword),
            new AutocompleteSuggestion("in", "Membership operator", TokenType.Keyword),
            new AutocompleteSuggestion("is", "Identity operator", TokenType.Keyword),
            new AutocompleteSuggestion("lambda", "Anonymous function", TokenType.Keyword),
            new AutocompleteSuggestion("nonlocal", "Nonlocal variable", TokenType.Keyword),
            new AutocompleteSuggestion("not", "Logical NOT", TokenType.Keyword),
            new AutocompleteSuggestion("or", "Logical OR", TokenType.Keyword),
            new AutocompleteSuggestion("pass", "Empty statement", TokenType.Keyword),
            new AutocompleteSuggestion("raise", "Raise exception", TokenType.Keyword),
            new AutocompleteSuggestion("return", "Return from function", TokenType.Keyword),
            new AutocompleteSuggestion("try", "Try block", TokenType.Keyword),
            new AutocompleteSuggestion("while", "While loop", TokenType.Keyword),
            new AutocompleteSuggestion("with", "Context manager", TokenType.Keyword),
            new AutocompleteSuggestion("yield", "Generator yield", TokenType.Keyword),

            // Builtins
            new AutocompleteSuggestion("print", "Print to console", TokenType.Builtin),
            new AutocompleteSuggestion("len", "Length of object", TokenType.Builtin),
            new AutocompleteSuggestion("range", "Range generator", TokenType.Builtin),
            new AutocompleteSuggestion("type", "Type of object", TokenType.Builtin),
            new AutocompleteSuggestion("int", "Integer type", TokenType.Builtin),
            new AutocompleteSuggestion("str", "String type", TokenType.Builtin),
            new AutocompleteSuggestion("float", "Float type", TokenType.Builtin),
            new AutocompleteSuggestion("list", "List type", TokenType.Builtin),
            new AutocompleteSuggestion("dict", "Dictionary type", TokenType.Builtin),
            new AutocompleteSuggestion("set", "Set type", TokenType.Builtin),
            new AutocompleteSuggestion("tuple", "Tuple type", TokenType.Builtin),
            new AutocompleteSuggestion("bool", "Boolean type", TokenType.Builtin),
            new AutocompleteSuggestion("input", "Read input", TokenType.Builtin),
            new AutocompleteSuggestion("open", "Open file", TokenType.Builtin),
            new AutocompleteSuggestion("enumerate", "Enumerate iterable", TokenType.Builtin),
            new AutocompleteSuggestion("zip", "Zip iterables", TokenType.Builtin),
            new AutocompleteSuggestion("map", "Map function", TokenType.Builtin),
            new AutocompleteSuggestion("filter", "Filter function", TokenType.Builtin),
        };

        public IEnumerable<AutocompleteSuggestion> GetSuggestions(TextDocument document, int line, int col, List<TextToken> lineTokens)
        {
            var lineText = document.GetLine(line);
            var prefix = GetPrefixAt(lineText, col);

            if (string.IsNullOrEmpty(prefix))
                return Enumerable.Empty<AutocompleteSuggestion>();

            // 1. Syntactic Context Filtering
            if (lineTokens != null)
            {
                var curTokenIndex = GetTokenIndexAt(lineTokens, col);
                var prevTokenIndex = curTokenIndex - 1;
                
                // Skip whitespace tokens to find the semantic previous token
                while (prevTokenIndex >= 0 && string.IsNullOrWhiteSpace(lineTokens[prevTokenIndex].Text))
                {
                    prevTokenIndex--;
                }

                if (prevTokenIndex >= 0)
                {
                    var prevToken = lineTokens[prevTokenIndex];
                    
                    // If we just typed 'def' or 'class', don't suggest keywords/builtins
                    if (prevToken.Text == "def" || prevToken.Text == "class")
                    {
                        return Enumerable.Empty<AutocompleteSuggestion>();
                    }

                    // If we just typed a dot, we might want to suggest members (not implemented yet)
                    if (prevToken.Text == ".")
                    {
                         return Enumerable.Empty<AutocompleteSuggestion>();
                    }
                }
            }

            // 2. Harvesting Dynamic Symbols
            var harvested = HarvestSymbols(document);

            var fullWord = GetFullWordAt(lineText, col);

            return _suggestions.Concat(harvested)
                .Where(s => s.Text.StartsWith(prefix) && s.Text != fullWord)
                .OrderBy(s => s.Text);
        }

        private IEnumerable<AutocompleteSuggestion> HarvestSymbols(TextDocument document)
        {
            var symbols = new HashSet<string>();
            var suggestions = new List<AutocompleteSuggestion>();

            for (int i = 0; i < document.LineCount; i++)
            {
                var line = document.GetLine(i);
                
                // Functions
                var match = _defRegex.Match(line);
                if (match.Success)
                {
                    var name = match.Groups[1].Value;
                    if (symbols.Add(name))
                        suggestions.Add(new AutocompleteSuggestion(name, "User function", TokenType.Builtin));
                    continue;
                }

                // Classes
                match = _classRegex.Match(line);
                if (match.Success)
                {
                    var name = match.Groups[1].Value;
                    if (symbols.Add(name))
                        suggestions.Add(new AutocompleteSuggestion(name, "User class", TokenType.Builtin));
                    continue;
                }

                // Variables
                match = _varRegex.Match(line);
                if (match.Success)
                {
                    var name = match.Groups[1].Value;
                    if (symbols.Add(name))
                        suggestions.Add(new AutocompleteSuggestion(name, "Variable", TokenType.Default));
                }
            }

            return suggestions;
        }

        private int GetTokenIndexAt(List<TextToken> tokens, int col)
        {
            int currentPos = 0;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (currentPos >= col) return i;
                currentPos += tokens[i].Text.Length;
            }
            return tokens.Count;
        }

        public void AddSuggestion(AutocompleteSuggestion suggestion)
        {
            _suggestions.Add(suggestion);
        }

        public void RemoveSuggestion(string text)
        {
            _suggestions.RemoveAll(s => s.Text == text);
        }

        private string GetPrefixAt(string line, int col)
        {
            if (col < 0 || col > line.Length) return string.Empty;

            int start = col - 1;
            while (start >= 0 && (char.IsLetterOrDigit(line[start]) || line[start] == '_'))
            {
                start--;
            }

            return line.Substring(start + 1, col - (start + 1));
        }

        private string GetFullWordAt(string line, int col)
        {
            if (line.Length == 0) return string.Empty;
            
            int start = col - 1;
            while (start >= 0 && (char.IsLetterOrDigit(line[start]) || line[start] == '_'))
                start--;
            
            int end = col;
            while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_'))
                end++;
            
            start++;
            if (start >= end) return string.Empty;
            
            return line.Substring(start, end - start);
        }
    }
}
