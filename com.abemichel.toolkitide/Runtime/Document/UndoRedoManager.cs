using System.Collections.Generic;

namespace AbesIde.Document
{
    public class UndoRedoManager
    {
        private readonly LinkedList<UndoStep> _undoStack = new();
        private readonly Stack<UndoStep> _redoStack = new();
        private readonly int _maxSize = 200;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void Push(UndoStep step)
        {
            // Ignore steps with no actual changes
            step.Edits.RemoveAll(e => string.IsNullOrEmpty(e.AddedText) && string.IsNullOrEmpty(e.RemovedText));
            if (step.Edits.Count == 0) return;

            // Try to merge with the previous step if it's "typing"
            if (_undoStack.Count > 0)
            {
                var last = _undoStack.Last.Value;
                if (TryMerge(last, step))
                {
                    return;
                }
            }

            _undoStack.AddLast(step);
            if (_undoStack.Count > _maxSize)
            {
                _undoStack.RemoveFirst();
            }
            _redoStack.Clear();
        }

        private bool TryMerge(UndoStep last, UndoStep current)
        {
            // Only merge single character insertions (typing)
            if (last.Edits.Count != 1 || current.Edits.Count != 1) return false;
        
            var e1 = last.Edits[0];
            var e2 = current.Edits[0];

            // Ensure we have text to merge
            if (string.IsNullOrEmpty(e1.AddedText) || string.IsNullOrEmpty(e2.AddedText)) return false;

            // Only merge insertions, no deletions or newlines
            if (!string.IsNullOrEmpty(e1.RemovedText) || !string.IsNullOrEmpty(e2.RemovedText)) return false;
            if (e1.AddedText.Contains("\n") || e2.AddedText.Contains("\n")) return false;

            // Must be on the same line and contiguous
            if (e1.Line != e2.Line || e1.Col + e1.AddedText.Length != e2.Col) return false;

            // Break on whitespace (except at start of word)
            if (char.IsWhiteSpace(e2.AddedText[0]) && !char.IsWhiteSpace(e1.AddedText[^1])) return false;

            // Break on time (e.g. 2 seconds)
            if (current.Timestamp - last.Timestamp > 2.0) return false;

            // Merge e2 into e1
            e1.AddedText += e2.AddedText;
            last.After = current.After;
            last.Timestamp = current.Timestamp;
            return true;
        }

        public UndoStep PopUndo()
        {
            if (_undoStack.Count == 0) return null;
            var step = _undoStack.Last.Value;
            _undoStack.RemoveLast();
            _redoStack.Push(step);
            return step;
        }

        public UndoStep PopRedo()
        {
            if (_redoStack.Count == 0) return null;
            var step = _redoStack.Pop();
            _undoStack.AddLast(step);
            return step;
        }
    }
}
