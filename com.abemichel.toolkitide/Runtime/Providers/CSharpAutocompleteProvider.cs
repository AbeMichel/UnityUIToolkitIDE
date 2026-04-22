using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AbesIde.Autocomplete;
using AbesIde.Document;
using AbesIde.Tokenizing;

namespace AbesIde.Providers
{
    public class CSharpAutocompleteProvider : IAutocompleteProvider
    {
        private readonly List<AutocompleteSuggestion> _suggestions;

        public CSharpAutocompleteProvider()
        {
            _suggestions = new List<AutocompleteSuggestion>(_initialSuggestions);
        }

        private static readonly List<AutocompleteSuggestion> _initialSuggestions = new()
        {
            // Keywords
            new AutocompleteSuggestion("abstract", "Abstract modifier", TokenType.Keyword),
            new AutocompleteSuggestion("as", "As operator", TokenType.Keyword),
            new AutocompleteSuggestion("base", "Base class reference", TokenType.Keyword),
            new AutocompleteSuggestion("bool", "Boolean type", TokenType.Keyword),
            new AutocompleteSuggestion("break", "Break loop", TokenType.Keyword),
            new AutocompleteSuggestion("byte", "Byte type", TokenType.Keyword),
            new AutocompleteSuggestion("case", "Switch case", TokenType.Keyword),
            new AutocompleteSuggestion("catch", "Exception catch", TokenType.Keyword),
            new AutocompleteSuggestion("char", "Character type", TokenType.Keyword),
            new AutocompleteSuggestion("checked", "Checked context", TokenType.Keyword),
            new AutocompleteSuggestion("class", "Class definition", TokenType.Keyword),
            new AutocompleteSuggestion("const", "Constant value", TokenType.Keyword),
            new AutocompleteSuggestion("continue", "Continue loop", TokenType.Keyword),
            new AutocompleteSuggestion("decimal", "Decimal type", TokenType.Keyword),
            new AutocompleteSuggestion("default", "Default value", TokenType.Keyword),
            new AutocompleteSuggestion("delegate", "Delegate definition", TokenType.Keyword),
            new AutocompleteSuggestion("do", "Do loop", TokenType.Keyword),
            new AutocompleteSuggestion("double", "Double type", TokenType.Keyword),
            new AutocompleteSuggestion("else", "Else block", TokenType.Keyword),
            new AutocompleteSuggestion("enum", "Enum definition", TokenType.Keyword),
            new AutocompleteSuggestion("event", "Event definition", TokenType.Keyword),
            new AutocompleteSuggestion("explicit", "Explicit conversion", TokenType.Keyword),
            new AutocompleteSuggestion("extern", "External method", TokenType.Keyword),
            new AutocompleteSuggestion("false", "Boolean false", TokenType.Keyword),
            new AutocompleteSuggestion("finally", "Finally block", TokenType.Keyword),
            new AutocompleteSuggestion("fixed", "Fixed pointer", TokenType.Keyword),
            new AutocompleteSuggestion("float", "Float type", TokenType.Keyword),
            new AutocompleteSuggestion("for", "For loop", TokenType.Keyword),
            new AutocompleteSuggestion("foreach", "Foreach loop", TokenType.Keyword),
            new AutocompleteSuggestion("goto", "Goto statement", TokenType.Keyword),
            new AutocompleteSuggestion("if", "If statement", TokenType.Keyword),
            new AutocompleteSuggestion("implicit", "Implicit conversion", TokenType.Keyword),
            new AutocompleteSuggestion("in", "In parameter", TokenType.Keyword),
            new AutocompleteSuggestion("int", "Integer type", TokenType.Keyword),
            new AutocompleteSuggestion("interface", "Interface definition", TokenType.Keyword),
            new AutocompleteSuggestion("internal", "Internal access", TokenType.Keyword),
            new AutocompleteSuggestion("is", "Is operator", TokenType.Keyword),
            new AutocompleteSuggestion("lock", "Lock statement", TokenType.Keyword),
            new AutocompleteSuggestion("long", "Long type", TokenType.Keyword),
            new AutocompleteSuggestion("namespace", "Namespace definition", TokenType.Keyword),
            new AutocompleteSuggestion("new", "New instance", TokenType.Keyword),
            new AutocompleteSuggestion("null", "Null value", TokenType.Keyword),
            new AutocompleteSuggestion("object", "Object type", TokenType.Keyword),
            new AutocompleteSuggestion("operator", "Operator definition", TokenType.Keyword),
            new AutocompleteSuggestion("out", "Out parameter", TokenType.Keyword),
            new AutocompleteSuggestion("override", "Override method", TokenType.Keyword),
            new AutocompleteSuggestion("params", "Params array", TokenType.Keyword),
            new AutocompleteSuggestion("private", "Private access", TokenType.Keyword),
            new AutocompleteSuggestion("protected", "Protected access", TokenType.Keyword),
            new AutocompleteSuggestion("public", "Public access", TokenType.Keyword),
            new AutocompleteSuggestion("readonly", "Readonly field", TokenType.Keyword),
            new AutocompleteSuggestion("record", "Record definition", TokenType.Keyword),
            new AutocompleteSuggestion("ref", "Ref parameter", TokenType.Keyword),
            new AutocompleteSuggestion("return", "Return value", TokenType.Keyword),
            new AutocompleteSuggestion("sbyte", "Signed byte type", TokenType.Keyword),
            new AutocompleteSuggestion("sealed", "Sealed class", TokenType.Keyword),
            new AutocompleteSuggestion("short", "Short type", TokenType.Keyword),
            new AutocompleteSuggestion("sizeof", "Sizeof operator", TokenType.Keyword),
            new AutocompleteSuggestion("stackalloc", "Stackalloc operator", TokenType.Keyword),
            new AutocompleteSuggestion("static", "Static modifier", TokenType.Keyword),
            new AutocompleteSuggestion("string", "String type", TokenType.Keyword),
            new AutocompleteSuggestion("struct", "Struct definition", TokenType.Keyword),
            new AutocompleteSuggestion("switch", "Switch statement", TokenType.Keyword),
            new AutocompleteSuggestion("this", "This reference", TokenType.Keyword),
            new AutocompleteSuggestion("throw", "Throw exception", TokenType.Keyword),
            new AutocompleteSuggestion("true", "Boolean true", TokenType.Keyword),
            new AutocompleteSuggestion("try", "Try block", TokenType.Keyword),
            new AutocompleteSuggestion("typeof", "Typeof operator", TokenType.Keyword),
            new AutocompleteSuggestion("uint", "Unsigned int type", TokenType.Keyword),
            new AutocompleteSuggestion("ulong", "Unsigned long type", TokenType.Keyword),
            new AutocompleteSuggestion("unchecked", "Unchecked context", TokenType.Keyword),
            new AutocompleteSuggestion("unsafe", "Unsafe context", TokenType.Keyword),
            new AutocompleteSuggestion("ushort", "Unsigned short type", TokenType.Keyword),
            new AutocompleteSuggestion("using", "Using statement", TokenType.Keyword),
            new AutocompleteSuggestion("var", "Var keyword", TokenType.Keyword),
            new AutocompleteSuggestion("virtual", "Virtual modifier", TokenType.Keyword),
            new AutocompleteSuggestion("void", "Void type", TokenType.Keyword),
            new AutocompleteSuggestion("volatile", "Volatile field", TokenType.Keyword),
            new AutocompleteSuggestion("while", "While loop", TokenType.Keyword),
            new AutocompleteSuggestion("yield", "Yield statement", TokenType.Keyword),
            new AutocompleteSuggestion("async", "Async modifier", TokenType.Keyword),
            new AutocompleteSuggestion("await", "Await operator", TokenType.Keyword),
        };

        public IEnumerable<AutocompleteSuggestion> GetSuggestions(TextDocument document, int line, int col, List<TextToken> lineTokens)
        {
            var lineText = document.GetLine(line);
            var prefix = GetPrefixAt(lineText, col);

            if (string.IsNullOrEmpty(prefix))
                return Enumerable.Empty<AutocompleteSuggestion>();

            // Simple C# Harvesting (minimal for now)
            var harvested = HarvestSymbols(document);

            var fullWord = GetFullWordAt(lineText, col);

            return _suggestions.Concat(harvested)
                .Where(s => s.Text.StartsWith(prefix) && s.Text != fullWord)
                .OrderBy(s => s.Text);
        }

        public void OnDocumentChanged(DocumentChangeArgs args, TextDocument document)
        {
            // TODO: Implement incremental harvesting for C#
        }

        private IEnumerable<AutocompleteSuggestion> HarvestSymbols(TextDocument document)
        {
            var symbols = new HashSet<string>();
            var suggestions = new List<AutocompleteSuggestion>();
            
            // Very basic regex for C# variables/methods
            var memberRegex = new Regex(@"\b(?:class|struct|interface|enum|void|int|string|bool|var)\s+([a-zA-Z_][a-zA-Z0-9_]*)\b");

            for (int i = 0; i < document.LineCount; i++)
            {
                var line = document.GetLine(i);
                var matches = memberRegex.Matches(line);
                foreach (Match match in matches)
                {
                    var name = match.Groups[1].Value;
                    if (symbols.Add(name))
                    {
                        suggestions.Add(new AutocompleteSuggestion(name, "C# Symbol", TokenType.Default));
                    }
                }
            }

            return suggestions;
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
