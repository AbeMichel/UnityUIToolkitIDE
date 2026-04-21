using System.Collections.Generic;
using Document;

namespace Providers
{
    public class PythonSymbolInsightProvider : ISymbolInsightProvider
    {
        private static readonly Dictionary<string, SymbolInsight> _insights = new()
        {
            { "print", new SymbolInsight {
                Signature = "print(*objects, sep=' ', end='\\n', file=sys.stdout, flush=False)",
                Parameters = "objects: object(s) to print, sep: string inserted between values, end: string appended after last value",
                ReturnValue = "None",
                Documentation = "Prints the values to a stream, or to sys.stdout by default."
            }},
            { "len", new SymbolInsight {
                Signature = "len(obj)",
                Parameters = "obj: object (string, list, etc.)",
                ReturnValue = "int",
                Documentation = "Return the number of items in a container."
            }},
            { "range", new SymbolInsight {
                Signature = "range(start, stop[, step])",
                Parameters = "start: start integer, stop: stop integer, step: increment integer",
                ReturnValue = "iterable",
                Documentation = "Returns an object that produces a sequence of integers from start (inclusive) to stop (exclusive) by step."
            }},
            { "input", new SymbolInsight {
                Signature = "input(prompt=None)",
                Parameters = "prompt: optional prompt string",
                ReturnValue = "str",
                Documentation = "Read a string from standard input. The trailing newline is stripped."
            }}
        };

        public SymbolInsight? GetInsight(TextDocument document, int line, int col)
        {
            var lineText = document.GetLine(line);
            if (string.IsNullOrEmpty(lineText)) return null;

            // Simple word detection around (line, col)
            int start = col;
            while (start > 0 && IsWordChar(lineText[start - 1])) start--;
            int end = col;
            while (end < lineText.Length && IsWordChar(lineText[end])) end++;

            if (start == end) return null;

            var word = lineText.Substring(start, end - start);
            if (_insights.TryGetValue(word, out var insight))
            {
                return insight;
            }

            return null;
        }

        private bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
