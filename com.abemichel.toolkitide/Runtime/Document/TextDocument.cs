using System;
using System.Collections.Generic;

namespace Document
{
    public class TextDocument
    {
        #region Events

        public event Action<DocumentChangeArgs> OnChanged;

        #endregion

        #region Fields

        private readonly List<string> _lines = new() { string.Empty };

        #endregion

        #region Public API

        public int LineCount => _lines.Count;
    
        public string GetLine(int index) => _lines[index];
    
        public IReadOnlyList<string> Lines => _lines;

        #endregion
    
        #region Mutations

        public void InsertChar(int line, int col, char c)
        {
            string text = CleanText(c.ToString());
            _lines[line] = _lines[line].Insert(col, text);
            OnChanged?.Invoke(new DocumentChangeArgs(line, col, 1, 1, string.Empty, text));
        }

        public void InsertNewline(int line, int col)
        {
            var current = _lines[line];
            var before = current[..col];
            var after = current[col..];
        
            _lines[line] = before;
            _lines.Insert(line + 1, after);
            OnChanged?.Invoke(new DocumentChangeArgs(line, col, 1, 2, string.Empty, "\n"));
        }

        public void DeleteCharBefore(int line, int col)
        {
            if (col > 0)  // Delete within the line
            {
                string removed = _lines[line].Substring(col - 1, 1);
                _lines[line] = _lines[line].Remove(col - 1, 1);
                OnChanged?.Invoke(new DocumentChangeArgs(line, col - 1, 1, 1, removed, string.Empty));
            }
            else if (line > 0)  // At start of line, merge with above line
            {
                var merged = _lines[line - 1] + _lines[line];
                int startCol = _lines[line - 1].Length;
                _lines.RemoveAt(line);
                _lines[line - 1] = merged;
                OnChanged?.Invoke(new DocumentChangeArgs(line - 1, startCol, 2, 1, "\n", string.Empty));
            }
        }

        public void DeleteCharAfter(int line, int col)
        {
            if (col < _lines[line].Length)  // Delete within the line
            {
                string removed = _lines[line].Substring(col, 1);
                _lines[line] = _lines[line].Remove(col, 1);
                OnChanged?.Invoke(new DocumentChangeArgs(line, col, 1, 1, removed, string.Empty));
            }
            else if (line < _lines.Count - 1)  // At end of line, merge next line into this
            {
                _lines[line] += _lines[line + 1];
                _lines.RemoveAt(line + 1);
                OnChanged?.Invoke(new DocumentChangeArgs(line, col, 2, 1, "\n", string.Empty));
            }
        }

        public void DeleteSelection(int anchorLine, int anchorCol, int cursorLine, int cursorCol)
        {
            // Normalize so start is always before end
            NormalizeRange(
                anchorLine, anchorCol, cursorLine, cursorCol,
                out int startLine, out int startCol, out int endLine, out int endCol);

            string removedText = GetSelectedText(startLine, startCol, endLine, endCol);

            if (startLine == endLine)
            {
                // Selection within a single line
                _lines[startLine] = _lines[startLine].Remove(startCol, endCol - startCol);
                OnChanged?.Invoke(new DocumentChangeArgs(startLine, startCol, 1, 1, removedText, string.Empty));
            }
            else
            {
                // Keep the text before the selection on the first line
                // and the text after the selection on the last line, then merge
                var before = _lines[startLine][..startCol];
                var after = _lines[endLine][endCol..];
            
                int oldLineCount = endLine - startLine + 1;
                _lines[startLine] = before + after;
                _lines.RemoveRange(startLine + 1, endLine - startLine);
                OnChanged?.Invoke(new DocumentChangeArgs(startLine, startCol, oldLineCount, 1, removedText, string.Empty));
            }
        }

        public string GetSelectedText(int anchorLine, int anchorCol, int cursorLine, int cursorCol)
        {
            // Normalize so start is always before end
            NormalizeRange(
                anchorLine, anchorCol, cursorLine, cursorCol,
                out int startLine, out int startCol, out int endLine, out int endCol);

            if (startLine == endLine) return _lines[startLine][startCol..endCol];
        
            var sb = new System.Text.StringBuilder();
            sb.Append(_lines[startLine][startCol..]);
            for (var i = startLine + 1; i < endLine; i++)
            {
                sb.Append("\n");
                sb.Append(_lines[i]);
            }
            sb.Append("\n");
            sb.Append(_lines[endLine][..endCol]);
        
            return sb.ToString();
        }

        public void InsertText(int line, int col, string text, out int newLine, out int newCol)
        {
            string cleanedText = CleanText(text);
            // Used for paste so need to cleanly handle embedded newlines
            var incoming = cleanedText.Split('\n');
        
            var before = _lines[line][..col];
            var after = _lines[line][col..];
        
            _lines[line] = before + incoming[0];

            for (var i = 1; i < incoming.Length; i++)
            {
                _lines.Insert(line + i, incoming[i]);
            }

            newLine = line + incoming.Length - 1;
            newCol = incoming.Length == 1
                ? col + incoming[0].Length
                : incoming[^1].Length;
        
            // Append whatever was after the cursor on the last new line
            _lines[newLine] += after;
        
            OnChanged?.Invoke(new DocumentChangeArgs(line, col, 1, incoming.Length, string.Empty, cleanedText));
        }

        #endregion

        #region Helpers

        public static string CleanText(string text) =>
            text.Replace("\r\n", "\n")
                .Replace('\u201C', '"')
                .Replace('\u201D', '"');
    
        public static void NormalizeRange(
            int aLine, int aCol, int bLine, int bCol,
            out int startLine, out int startCol, out int endLine, out int endCol)
        {
            if (aLine < bLine || (aLine == bLine && aCol <= bCol))
            {
                startLine = aLine; 
                startCol = aCol; 
                endLine = bLine;  
                endCol = bCol;
            }
            else
            {
                startLine = bLine;
                startCol = bCol;
                endLine = aLine;
                endCol = aCol;
            }
        }

        #endregion
    }
}