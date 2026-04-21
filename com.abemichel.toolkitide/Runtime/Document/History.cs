using System.Collections.Generic;

namespace Document
{
    public struct CursorState
    {
        public int CursorLine;
        public int CursorCol;
        public int SelectionAnchorLine;
        public int SelectionAnchorChar;

        public CursorState(int cursorLine, int cursorCol, int selectionAnchorLine, int selectionAnchorChar)
        {
            CursorLine = cursorLine;
            CursorCol = cursorCol;
            SelectionAnchorLine = selectionAnchorLine;
            SelectionAnchorChar = selectionAnchorChar;
        }

        public bool HasSelection => 
            SelectionAnchorLine >= 0 && 
            (SelectionAnchorLine != CursorLine || SelectionAnchorChar != CursorCol);
    }

    public class TextEdit
    {
        public int Line;
        public int Col;
        public string RemovedText;
        public string AddedText;

        public TextEdit(int line, int col, string removedText, string addedText)
        {
            Line = line;
            Col = col;
            RemovedText = removedText;
            AddedText = addedText;
        }
    }

    public class UndoStep
    {
        public List<TextEdit> Edits = new();
        public CursorState Before;
        public CursorState After;
        public double Timestamp;

        public UndoStep(CursorState before, CursorState after)
        {
            Before = before;
            After = after;
            Timestamp = UnityEngine.Time.realtimeSinceStartupAsDouble;
        }
    }
}