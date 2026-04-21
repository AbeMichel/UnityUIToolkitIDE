using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Autocomplete;
using Configuration;
using Document;
using Providers;
using Tokenizing;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UIElements;

namespace UI
{
    public class CodeElement : VisualElement
    {
        #region Fields

        private readonly EditorConfig _config;
        private readonly TextDocument _document;
        private readonly Action<TextDocument> _onDocumentChange;
    
        // Tokenized lines - outer list is lines, inner is tokens on that line
        private List<List<TextToken>> _lines = new();
    
        // Cursor state
        private int _cursorLine = 0;
        private int _cursorCol = 0;
        private bool _cursorVisible = true;
        private double _lastCursorActivityTime = 0.0;
    
        // Selection state (anchor is where selection started)
        private int _selectionAnchorLine = -1;
        private int _selectionAnchorChar = -1;

        private List<CodeError> _errors = new();
        private readonly UndoRedoManager _undoRedo = new();
        private bool _isApplyingHistory = false;
    
        private bool _isDragging = false;
    
        public float ScrollOffset { get; set; }
        public float ViewportHeight { get; set; }

        public int CursorLine => _cursorLine;
        public int CursorCol => _cursorCol;

        public Action OnCursorMoved { get; set; }
        public Action<int, int, Vector2> OnHover { get; set; }
        public Action OnHoverExit { get; set; }

        public string CommentPrefix { get; set; } = "#";

        private IVisualElementScheduledItem _hoverTask;
        private Vector2 _lastMousePos;

        public Vector2 GetCursorLocalPosition()
        {
            return new Vector2(GetCursorX(), _config.TopPadding + _cursorLine * _config.LineHeight);
        }

        private CursorState GetCursorState() => new(_cursorLine, _cursorCol, _selectionAnchorLine, _selectionAnchorChar);

        private void ApplyCursorState(CursorState state)
        {
            _cursorLine = state.CursorLine;
            _cursorCol = state.CursorCol;
            _selectionAnchorLine = state.SelectionAnchorLine;
            _selectionAnchorChar = state.SelectionAnchorChar;
            OnCursorMoved?.Invoke();
            MarkDirtyRepaint();
        }
    
        private bool HasSelection =>
            _selectionAnchorLine >= 0 &&
            (_selectionAnchorLine != _cursorLine || _selectionAnchorChar != _cursorCol);

        #endregion
    
        #region Construction

        public CodeElement(EditorConfig config, TextDocument textDocument, Action<TextDocument> onDocumentChange)
        {
            _config = config;
            _document = textDocument;
            _onDocumentChange = onDocumentChange;
        
            focusable = true;
            // style.color = new StyleColor(_config.Theme.DefaultTextColor);
            style.color = new StyleColor(Color.white);
            style.backgroundColor = new StyleColor(Color.clear);
            style.unityBackgroundImageTintColor = new StyleColor(Color.white);
        
            generateVisualContent += OnGenerateVisualContent;

            RegisterMouseCallbacks();
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<MouseLeaveEvent>(e => OnHoverExit?.Invoke());

            _hoverTask = schedule.Execute(TriggerHover);
            _hoverTask.Pause();

            _config.OnConfigChanged += () =>
            {
                UpdateLayout();
                MarkDirtyRepaint();
            };
        
            UpdateLayout();
        }
    
        #endregion

        #region Public API

        public void SetTokens(List<List<TextToken>> tokenizedLines)
        {
            _lines = tokenizedLines;
            UpdateLayout();
            MarkDirtyRepaint();
        }

        public void SetErrors(List<CodeError> errors)
        {
            _errors = errors;
            MarkDirtyRepaint();
        }

        public void UpdateTokens(int startLine, int linesRemoved, List<List<TextToken>> newTokens)
        {
            _lines.RemoveRange(startLine, linesRemoved);
            _lines.InsertRange(startLine, newTokens);
            UpdateLayout();
            MarkDirtyRepaint();
        }
    
        public int GetLineCount() => _lines.Count;

        public List<TextToken> GetLineTokens(int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= _lines.Count) return null;
            return _lines[lineIndex];
        }

        public void InsertCompletion(AutocompleteSuggestion suggestion)
        {
            var text = suggestion.Text;
            var isFunction = suggestion.Type == TokenType.Builtin;
        
            var lineText = _document.GetLine(_cursorLine);
        
            // Find start of word
            int start = _cursorCol - 1;
            while (start >= 0 && (char.IsLetterOrDigit(lineText[start]) || lineText[start] == '_'))
                start--;
            start++;

            // Find end of word
            int end = _cursorCol;
            while (end < lineText.Length && (char.IsLetterOrDigit(lineText[end]) || lineText[end] == '_'))
                end++;

            CommitEdit(() =>
            {
                _document.DeleteSelection(_cursorLine, start, _cursorLine, end);
            
                string completionText = isFunction ? text + "()" : text;
                _document.InsertText(_cursorLine, start, completionText, out _, out int newCol);
            
                if (isFunction)
                {
                    // Put cursor inside parens
                    _cursorCol = newCol - 1;
                }
                else
                {
                    _cursorCol = newCol;
                }
            });
        }

        public void ScrollToCursor(ScrollView scrollView)
        {
            var cursorTop    = _config.TopPadding + _cursorLine * _config.LineHeight;
            var cursorBottom = cursorTop + _config.LineHeight;
            var cursorX      = GetCursorX();
            var cursorRight  = cursorX + 2f;

            var viewTop    = scrollView.scrollOffset.y;
            var viewBottom = viewTop + scrollView.contentViewport.layout.height;
            var viewLeft   = scrollView.scrollOffset.x;
            var viewRight  = viewLeft + scrollView.contentViewport.layout.width;

            var newScrollY = scrollView.scrollOffset.y;
            var newScrollX = scrollView.scrollOffset.x;

            if (cursorTop < viewTop)              newScrollY = cursorTop;
            else if (cursorBottom > viewBottom)   newScrollY = cursorBottom - scrollView.contentViewport.layout.height;

            if (cursorX < viewLeft)              newScrollX = cursorX;
            else if (cursorRight > viewRight)    newScrollX = cursorRight - scrollView.contentViewport.layout.width;

            var newOffset = new Vector2(newScrollX, newScrollY);
            if (!Mathf.Approximately(newOffset.x, scrollView.scrollOffset.x) ||
                !Mathf.Approximately(newOffset.y, scrollView.scrollOffset.y))
            {
                scrollView.scrollOffset = newOffset;
            }
        }
    
        #endregion

        #region Layout

        private void UpdateLayout()
        {
            var totalHeight = _config.TopPadding + _lines.Count * _config.LineHeight;
            style.height = totalHeight;
            style.minWidth = new StyleLength(new Length(100, LengthUnit.Percent));

            var maxWidth = 0f;
            for (int i = 0; i < _lines.Count; i++)
                maxWidth = Mathf.Max(maxWidth, GetLineWidth(i));
            style.width = maxWidth + _config.FontSize * 2f;
        }

        #endregion

        #region Mesh Generation

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (rect.width <= 0 || ViewportHeight <= 0) return;

            DrawCursorAndSelection(mgc, rect);
            DrawErrors(mgc);

            var scale = ((float)_config.FontSize / _config.Font.faceInfo.pointSize) * _config.Font.faceInfo.scale;

            for (var i = 0; i < _lines.Count; i++)
            {
                var y = _config.TopPadding + i * _config.LineHeight;
                if (!IsLineVisible(i)) continue;

                var cursorX = 0f;

                foreach (var token in _lines[i])
                {
                    var color = _config.Theme.GetColor(token.Type);
                    var font = token.Type == TokenType.Todo ? _config.FontBold : _config.Font;
                
                    foreach (var c in token.Text)
                    {
                        mgc.DrawText(c.ToString(), new Vector2(cursorX, y), _config.FontSize, color, font);

                        if (TryGetGlyph(c, out var glyph))
                        {
                            cursorX += glyph.metrics.horizontalAdvance * scale;
                        }
                    }
                }
            }
        }
    
        private bool IsLineVisible(int lineIndex)
        {
            var y = _config.TopPadding + lineIndex * _config.LineHeight;
            // Pad by 1 line above and below to avoid clipping during smooth scroll
            return y + _config.LineHeight >= ScrollOffset - _config.LineHeight && 
                   y <= ScrollOffset + ViewportHeight + _config.LineHeight;
        }

        private void DrawRect(MeshGenerationContext mgc, float x, float y, float width, float height)
        {
            mgc.painter2D.MoveTo(new Vector2(x, y));
            mgc.painter2D.LineTo(new Vector2(x + width, y));
            mgc.painter2D.LineTo(new Vector2(x + width, y + height));
            mgc.painter2D.LineTo(new Vector2(x, y + height));
        }

        private void DrawCursorAndSelection(MeshGenerationContext mgc, Rect rect)
        {
            if (HasSelection)
            {
                TextDocument.NormalizeRange(
                    _selectionAnchorLine, _selectionAnchorChar,
                    _cursorLine, _cursorCol,
                    out int startLine, out int startCol, out int endLine, out int endCol);

                var selColor = _config.Theme.SelectionColor;
                mgc.painter2D.fillColor = selColor;

                for (var i = startLine; i <= endLine; i++)
                {
                    if (!IsLineVisible(i)) continue;

                    var y = _config.TopPadding + i * _config.LineHeight;

                    var startX = (i == startLine) ? GetCharX(i, startCol) : 0f;
                    var endX = (i == endLine) ? GetCharX(i, endCol) : GetLineWidth(i);
                
                    if (endX <= startX) continue;

                    mgc.painter2D.BeginPath();
                    DrawRect(mgc, startX, y, endX - startX, _config.LineHeight);
                    mgc.painter2D.ClosePath();
                    mgc.painter2D.Fill();
                }
            }

            if (_cursorVisible && IsLineVisible(_cursorLine))
            {
                var x = GetCursorX();
                var y = _config.TopPadding + _cursorLine * _config.LineHeight;
                var cursorWidth = 2f;
            
                mgc.painter2D.fillColor = _config.Theme.CursorColor;
                mgc.painter2D.BeginPath();
                DrawRect(mgc, x, y, cursorWidth, _config.LineHeight);
                mgc.painter2D.ClosePath();
                mgc.painter2D.Fill();
            }
        }

        private void DrawErrors(MeshGenerationContext mgc)
        {
            foreach (var error in _errors)
            {
                if (!IsLineVisible(error.Line)) continue;

                var y = _config.TopPadding + error.Line * _config.LineHeight;
                var startX = GetCharX(error.Line, error.Column);
                var endX = GetCharX(error.Line, error.Column + error.Length);

                if (endX <= startX) endX = startX + 10f; // Minimum width if it's at end of line

                mgc.painter2D.strokeColor = _config.Theme.ErrorSquiggleColor;
                mgc.painter2D.lineWidth = 1f;
            
                var squiggleY = y + _config.LineHeight - 2f;
                mgc.painter2D.BeginPath();
                mgc.painter2D.MoveTo(new Vector2(startX, squiggleY));
            
                // Draw a simple zigzag for squiggle
                float step = 2f;
                bool up = true;
                for (float x = startX + step; x <= endX; x += step)
                {
                    mgc.painter2D.LineTo(new Vector2(x, up ? squiggleY - 1f : squiggleY + 1f));
                    up = !up;
                }
                mgc.painter2D.Stroke();
            }
        }

        #endregion

        #region Glyph Helpers

        private float GetFontScale()
        {
            return ((float)_config.FontSize / _config.Font.faceInfo.pointSize) * _config.Font.faceInfo.scale;
        }

        private bool TryGetGlyph(char c, out Glyph glyph)
        {
            if (_config.Font.HasCharacter(c))
            {
                if (_config.Font.characterLookupTable.TryGetValue(c, out var character))
                {
                    return _config.Font.glyphLookupTable.TryGetValue(character.glyphIndex, out glyph);
                }
            }
            glyph = null;
            return false;
        }

        #endregion

        #region Cursor & Selection Helpers

        private float GetCursorX() => GetCharX(_cursorLine, _cursorCol);
    
        private float GetCharX(int line, int charIndex)
        {
            if (line >= _lines.Count) return 0f;
            var x = 0f;
            var charsSeen = 0;
            var scale = GetFontScale();

            foreach (var token in _lines[line])
            {
                foreach (var c in token.Text)
                {
                    if (charsSeen >= charIndex) return x;
                    if (TryGetGlyph(c, out var glyph))
                    {
                        x += glyph.metrics.horizontalAdvance * scale;
                    }
                    charsSeen++;
                }
            }
            return x;
        }

        private float GetLineWidth(int line)
        {
            if (line >= _lines.Count) return 0f;
            var x = 0f;
            var scale = GetFontScale();
            foreach (var token in _lines[line])
            foreach (var c in token.Text)
                if (TryGetGlyph(c, out var glyph))
                    x += glyph.metrics.horizontalAdvance * scale;
        
            return x;
        }

        private int GetLineCharCount(int line)
        {
            if (line >= _lines.Count) return 0;
            var count = 0;
            foreach (var token in _lines[line])
            {
                count += token.Text.Length;
            }
            return count;
        }

        #endregion


        #region Mouse Input

        private void RegisterMouseCallbacks()
        {
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }
    
        private void OnMouseDown(MouseDownEvent e)
        {
            MarkActivity();
            this.Focus();
        
            var (line, col) = HitTester.GetDocumentPosition(
                e.localMousePosition, _document, _config
            );

            if (e.clickCount == 2)
            {
                SelectWordAt(line, col);
                e.StopPropagation();
                return;
            }
            else if (e.clickCount == 3)
            {
                SelectLineAt(line);
            }

            // Shift click extends the existing selection
            if (e.shiftKey) BeginSelectionIfNeeded(true);
            else ClearSelection();

            _cursorLine = line;
            _cursorCol = col;
        
            _isDragging = true;
            this.CaptureMouse();
        
            MarkDirtyRepaint();
            e.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent e)
        {
            _lastMousePos = e.localMousePosition;
            _hoverTask.Resume();
            _hoverTask.ExecuteLater(500);

            if (!_isDragging) return;
            MarkActivity();
            // Start a selection from where the drag began if we haven't yet
            BeginSelectionIfNeeded(true);

            var (line, col) = HitTester.GetDocumentPosition(
                e.localMousePosition, _document, _config
            );
        
            _cursorLine = line;
            _cursorCol = col;
        
            MarkDirtyRepaint();
            e.StopPropagation();
        }

        private void TriggerHover()
        {
            var (line, col) = HitTester.GetDocumentPosition(_lastMousePos, _document, _config);
            OnHover?.Invoke(line, col, _lastMousePos);
            _hoverTask.Pause();
        }

        private void OnMouseUp(MouseUpEvent e)
        {
            if (!_isDragging) return;
        
            _isDragging = false;
            this.ReleaseMouse();
            e.StopPropagation();
        }

        private void SelectLineAt(int line)
        {
            var lineText = _document.GetLine(line);
            _selectionAnchorLine = line;
            _selectionAnchorChar = 0;
            _cursorLine = line;
            _cursorCol = lineText.Length;
            MarkDirtyRepaint();
        }

        private void SelectWordAt(int line, int col)
        {
            var lineText = _document.GetLine(line);
            if (string.IsNullOrEmpty(lineText))
            {
                _cursorLine = line;
                _cursorCol = 0;
                ClearSelection();
                MarkDirtyRepaint();
                return;
            }

            // Clamp col in case of double click past end of line
            col = Mathf.Clamp(col, 0, lineText.Length - 1);

            var start = col;
            var end = col;

            // Expand left
            while (start > 0 && IsWordChar(lineText[start - 1])) start--;
            // Expand right
            while (end < lineText.Length && IsWordChar(lineText[end])) end++;

            _selectionAnchorLine = line;
            _selectionAnchorChar = start;
            _cursorLine = line;
            _cursorCol = end;

            MarkDirtyRepaint();
        }

        private static bool IsWordChar(char c) 
            => char.IsLetterOrDigit(c) || c == '_';

        #endregion
    
        #region Keyboard Input
    
        private void OnKeyDown(KeyDownEvent e)
        {
            MarkActivity();
            var ctrl = e.ctrlKey || e.commandKey;
            var alt = e.altKey;

            if (alt && !ctrl)
            {
                OnHover?.Invoke(_cursorLine, _cursorCol, GetCursorLocalPosition() + new Vector2(0, _config.LineHeight));
            }
        
            #region Zoom

            if (ctrl)
            {
                if (e.keyCode == KeyCode.Equals || e.keyCode == KeyCode.KeypadPlus)
                {
                    _config.FontSize += 2;
                    e.StopPropagation();
                    return;
                }
                if (e.keyCode == KeyCode.Minus || e.keyCode == KeyCode.KeypadMinus)
                {
                    _config.FontSize -= 2;
                    e.StopPropagation();
                    return;
                }
            }

            #endregion

            #region Clipboard

            if (ctrl)
            {
                switch (e.keyCode)
                {
                    case KeyCode.C:
                        HandleCopy();
                        e.StopPropagation();
                        return;
                    case KeyCode.X:
                        HandleCut();
                        e.StopPropagation();
                        return;
                    case KeyCode.V:
                        HandlePaste();
                        e.StopPropagation();
                        return;
                    case KeyCode.A:
                        SelectAll();
                        e.StopPropagation();
                        return;
                    case KeyCode.Z:
                        if (e.shiftKey) Redo();
                        else Undo();
                        e.StopPropagation();
                        return;
                    case KeyCode.Y:
                        Redo();
                        e.StopPropagation();
                        return;
                }
            }

            if (ctrl && e.keyCode == KeyCode.Slash)
            {
                ToggleComment();
                e.StopPropagation();
                return;
            }

            #endregion

            #region Navigation

            var moveByWord = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? e.altKey : ctrl;
            var moveToLineEdge = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && e.commandKey;
            var moveToDocEdge = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? e.commandKey : ctrl;

            switch (e.keyCode)
            {
                case KeyCode.LeftArrow:
                    if (moveToLineEdge)
                        MoveHome(e.shiftKey);
                    else
                        MoveLeft(e.shiftKey, moveByWord);
                    e.StopPropagation();
                    MarkDirtyRepaint();
                    OnCursorMoved?.Invoke();
                    return;
                case KeyCode.RightArrow:
                    if (moveToLineEdge)
                        MoveEnd(e.shiftKey);
                    else
                        MoveRight(e.shiftKey, moveByWord);
                    e.StopPropagation();
                    MarkDirtyRepaint();
                    OnCursorMoved?.Invoke();
                    return;
                case KeyCode.UpArrow:
                    if (moveToDocEdge)
                        MoveToDocumentStart(e.shiftKey);
                    else
                        MoveCursorVertical(-1, e.shiftKey);
                    e.StopPropagation();
                    MarkDirtyRepaint();
                    OnCursorMoved?.Invoke();
                    return;
                case KeyCode.DownArrow:
                    if (moveToDocEdge)
                        MoveToDocumentEnd(e.shiftKey);
                    else
                        MoveCursorVertical(1, e.shiftKey);
                    e.StopPropagation();
                    MarkDirtyRepaint();
                    OnCursorMoved?.Invoke();
                    return;
                case KeyCode.Home:
                    MoveHome(e.shiftKey);
                    e.StopPropagation();
                    MarkDirtyRepaint();
                    OnCursorMoved?.Invoke();
                    return;
                case KeyCode.End:
                    MoveEnd(e.shiftKey);
                    e.StopPropagation();
                    MarkDirtyRepaint();
                    OnCursorMoved?.Invoke();
                    return;
                case KeyCode.PageUp:
                    MoveCursorVertical(-10, e.shiftKey);
                    e.StopPropagation();
                    MarkDirtyRepaint();
                    OnCursorMoved?.Invoke();
                    return;
                case KeyCode.PageDown:
                    MoveCursorVertical(10, e.shiftKey);
                    e.StopPropagation();
                    MarkDirtyRepaint();
                    OnCursorMoved?.Invoke();
                    return;
            }

            #endregion
        
            #region Editing

            switch (e.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    CommitEdit(() =>
                    {
                        if (HasSelection) DeleteSelectedText();
                    
                        var lineText = _document.GetLine(_cursorLine);
                        var partBeforeCursor = lineText.Substring(0, _cursorCol);
                    
                        // Get leading whitespace
                        int whitespaceCount = 0;
                        while (whitespaceCount < partBeforeCursor.Length && char.IsWhiteSpace(partBeforeCursor[whitespaceCount]))
                            whitespaceCount++;
                        var indentation = partBeforeCursor.Substring(0, whitespaceCount);
                    
                        // Add extra indent if line ends with colon
                        if (partBeforeCursor.TrimEnd().EndsWith(":"))
                        {
                            indentation += "    ";
                        }

                        _document.InsertNewline(_cursorLine, _cursorCol);
                        _cursorLine++;
                        _cursorCol = 0;
                    
                        if (indentation.Length > 0)
                        {
                            _document.InsertText(_cursorLine, 0, indentation, out _, out int newCol);
                            _cursorCol = newCol;
                        }
                    });
                    e.StopPropagation();
                    return;

                case KeyCode.Backspace:
                    CommitEdit(() =>
                    {
                        if (HasSelection)
                        {
                            DeleteSelectedText();
                        }
                        else if (moveToLineEdge)
                        {
                            DeleteToLineStart();
                        }
                        else if (moveByWord)
                        {
                            DeleteWordBefore();
                        }
                        else if (_cursorCol > 0)
                        {
                            var lineText = _document.GetLine(_cursorLine);
                            // Smart delete 4 spaces if aligned to 4
                            if (_cursorCol % 4 == 0 && _cursorCol >= 4 && lineText.Substring(_cursorCol - 4, 4) == "    ")
                            {
                                _document.DeleteSelection(_cursorLine, _cursorCol - 4, _cursorLine, _cursorCol);
                                _cursorCol -= 4;
                            }
                            else
                            {
                                _document.DeleteCharBefore(_cursorLine, _cursorCol);
                                _cursorCol--;
                            }
                        }
                        else if (_cursorLine > 0)
                        {
                            // At start of line, merge with previous
                            var prevLine = _cursorLine - 1;
                            var targetCol = _document.GetLine(prevLine).Length;
                            _document.DeleteCharBefore(_cursorLine, _cursorCol);
                            _cursorLine = prevLine;
                            _cursorCol = targetCol;
                        }
                    });
                    e.StopPropagation();
                    return;

                case KeyCode.Delete:
                    CommitEdit(() =>
                    {
                        if (HasSelection)
                        {
                            DeleteSelectedText();
                        }
                        else if (moveToLineEdge)
                        {
                            DeleteToLineEnd();
                        }
                        else if (moveByWord)
                        {
                            DeleteWordAfter();
                        }
                        else
                        {
                            _document.DeleteCharAfter(_cursorLine, _cursorCol);
                        }
                    });
                    e.StopPropagation();
                    return;

                case KeyCode.Tab:
                    CommitEdit(() =>
                    {
                        if (e.shiftKey)
                        {
                            if (HasSelection)
                            {
                                TextDocument.NormalizeRange(_selectionAnchorLine, _selectionAnchorChar, _cursorLine, _cursorCol, out int startLine, out _, out int endLine, out _);
                                if (startLine != endLine) UnindentSelection(startLine, endLine);
                                else UnindentLine(_cursorLine);
                            }
                            else
                            {
                                UnindentLine(_cursorLine);
                            }
                        }
                        else
                        {
                            if (HasSelection)
                            {
                                TextDocument.NormalizeRange(_selectionAnchorLine, _selectionAnchorChar, _cursorLine, _cursorCol, out int startLine, out _, out int endLine, out _);
                                if (startLine != endLine)
                                {
                                    IndentSelection(startLine, endLine);
                                }
                                else
                                {
                                    DeleteSelectedText();
                                    InsertSmartTab();
                                }
                            }
                            else
                            {
                                InsertSmartTab();
                            }
                        }
                    }, keepSelection: HasSelection && (e.shiftKey || (_selectionAnchorLine != _cursorLine)));
                    e.StopPropagation();
                    return;
            }

            #endregion

            #region Printable Characters

            // Only handle characters that are not control characters and not part of a shortcut
            if (!char.IsControl(e.character) && !ctrl)
            {
                CommitEdit(() =>
                {
                    if (HasSelection) DeleteSelectedText();
                    _document.InsertChar(_cursorLine, _cursorCol, e.character);
                    _cursorCol++;
                });
                e.StopPropagation();
            }

            #endregion
        }
    
        #region Edit Helpers

        private void CommitEdit(Action mutation, bool keepSelection = false)
        {
            if (_isApplyingHistory)
            {
                mutation();
                if (!keepSelection) ClearSelection();
                return;
            }

            var stateBefore = GetCursorState();
            var step = new UndoStep(stateBefore, default);

            Action<DocumentChangeArgs> recordEdit = (args) =>
            {
                step.Edits.Add(new TextEdit(args.StartLine, args.StartCol, args.RemovedText, args.AddedText));
            };

            _document.OnChanged += recordEdit;
            mutation();
            _document.OnChanged -= recordEdit;

            if (!keepSelection) ClearSelection();

            var stateAfter = GetCursorState();
            step.After = stateAfter;

            if (step.Edits.Count > 0)
            {
                _undoRedo.Push(step);
            }

            OnDocumentChanged();
            // Cursor position is now final (mutation lambda has run); scroll to it.
            // The earlier ScrollToCursor triggered by _document.OnChanged inside the
            // mutation used the pre-update cursor position and needs to be corrected.
            OnCursorMoved?.Invoke();
        }

        private void DeleteSelectedText()
        {
            _document.DeleteSelection(_selectionAnchorLine, _selectionAnchorChar,
                _cursorLine, _cursorCol);
        
            // Move cursor to start of the deleted range
            TextDocument.NormalizeRange(
                _selectionAnchorLine, _selectionAnchorChar,
                _cursorLine, _cursorCol,
                out int startLine, out int startCol, out _, out _);
        
            _cursorLine = startLine;
            _cursorCol = startCol;
            ClearSelection();
        }

        private void DeleteWordBefore()
        {
            if (_cursorCol == 0)
            {
                if (_cursorLine > 0)
                {
                    var prevLine = _cursorLine - 1;
                    var targetCol = _document.GetLine(prevLine).Length;
                    _document.DeleteCharBefore(_cursorLine, _cursorCol);
                    _cursorLine = prevLine;
                    _cursorCol = targetCol;
                }
                return;
            }

            var to = PreviousWordBoundary(_document.GetLine(_cursorLine), _cursorCol);
            _document.DeleteSelection(_cursorLine, _cursorCol, _cursorLine, to);

            _cursorCol = to;
        }

        private void DeleteWordAfter()
        {
            var lineText = _document.GetLine(_cursorLine);
            if (_cursorCol == lineText.Length)
            {
                if (_cursorLine < _document.LineCount - 1)
                {
                    _document.DeleteCharAfter(_cursorLine, _cursorCol);
                }
                return;
            }

            var to = NextWordBoundary(lineText, _cursorCol);
            _document.DeleteSelection(_cursorLine, _cursorCol, _cursorLine, to);
        }

        private void DeleteToLineStart()
        {
            if (_cursorCol == 0 && _cursorLine > 0)
            {
                var prevLine = _cursorLine - 1;
                var targetCol = _document.GetLine(prevLine).Length;
                _document.DeleteCharBefore(_cursorLine, _cursorCol);
                _cursorLine = prevLine;
                _cursorCol = targetCol;
            }
            else
            {
                _document.DeleteSelection(_cursorLine, _cursorCol, _cursorLine, 0);
                _cursorCol = 0;
            }
        }

        private void DeleteToLineEnd()
        {
            var lineText = _document.GetLine(_cursorLine);
            if (_cursorCol == lineText.Length && _cursorLine < _document.LineCount - 1)
            {
                _document.DeleteCharAfter(_cursorLine, _cursorCol);
            }
            else
            {
                _document.DeleteSelection(_cursorLine, _cursorCol, _cursorLine, lineText.Length);
            }
        }

        private void ToggleComment()
        {
            int startLine, endLine;
            if (HasSelection)
            {
                TextDocument.NormalizeRange(_selectionAnchorLine, _selectionAnchorChar, _cursorLine, _cursorCol,
                    out startLine, out _, out endLine, out _);
            
                // If selection ends at col 0 of a line, don't include that last line
                TextDocument.NormalizeRange(_selectionAnchorLine, _selectionAnchorChar, _cursorLine, _cursorCol,
                    out _, out _, out int rawEndLine, out int rawEndCol);
                if (rawEndCol == 0 && rawEndLine > startLine)
                    endLine = rawEndLine - 1;
            }
            else
            {
                startLine = _cursorLine;
                endLine = _cursorLine;
            }

            // 1. Check if all lines (that have content) are commented
            bool allCommented = true;
            bool foundContent = false;
            for (int i = startLine; i <= endLine; i++)
            {
                var line = _document.GetLine(i);
                var trimmed = line.TrimStart();
                if (string.IsNullOrEmpty(trimmed)) continue;
            
                foundContent = true;
                if (!trimmed.StartsWith(CommentPrefix))
                {
                    allCommented = false;
                    break;
                }
            }

            if (!foundContent) allCommented = false;

            CommitEdit(() =>
            {
                for (int i = startLine; i <= endLine; i++)
                {
                    var line = _document.GetLine(i);
                    if (allCommented)
                    {
                        // Uncomment: remove prefix and optional space
                        int prefixIdx = line.IndexOf(CommentPrefix);
                        if (prefixIdx >= 0)
                        {
                            int removeLen = CommentPrefix.Length;
                            if (line.Length > prefixIdx + removeLen && line[prefixIdx + removeLen] == ' ')
                                removeLen++;
                        
                            _document.DeleteSelection(i, prefixIdx, i, prefixIdx + removeLen);
                        
                            if (i == _cursorLine) _cursorCol = Mathf.Max(prefixIdx, _cursorCol - removeLen);
                            if (i == _selectionAnchorLine) _selectionAnchorChar = Mathf.Max(prefixIdx, _selectionAnchorChar - removeLen);
                        }
                    }
                    else
                    {
                        // Comment: add prefix and space at start of line
                        _document.InsertText(i, 0, CommentPrefix + " ", out _, out _);
                    
                        if (i == _cursorLine) _cursorCol += CommentPrefix.Length + 1;
                        if (i == _selectionAnchorLine) _selectionAnchorChar += CommentPrefix.Length + 1;
                    }
                }
            }, keepSelection: HasSelection);
        }

        private void OnDocumentChanged()
        {
            _onDocumentChange?.Invoke(_document);
            MarkDirtyRepaint();
        }

        private void InsertSmartTab()
        {
            int spacesToInsert = 4 - (_cursorCol % 4);
            string spaces = new string(' ', spacesToInsert);
            _document.InsertText(_cursorLine, _cursorCol, spaces, out _, out int newCol);
            _cursorCol = newCol;
        }

        private void IndentSelection(int startLine, int endLine)
        {
            for (int i = startLine; i <= endLine; i++)
            {
                _document.InsertText(i, 0, "    ", out _, out _);
            }

            if (_cursorLine >= startLine && _cursorLine <= endLine)
                _cursorCol += 4;
            if (_selectionAnchorLine >= startLine && _selectionAnchorLine <= endLine)
                _selectionAnchorChar += 4;
        }

        private void UnindentSelection(int startLine, int endLine)
        {
            for (int i = startLine; i <= endLine; i++)
            {
                UnindentLine(i);
            }
        }

        private void UnindentLine(int line)
        {
            var text = _document.GetLine(line);
            int spacesToRemove = 0;
            for (int i = 0; i < 4 && i < text.Length; i++)
            {
                if (text[i] == ' ') spacesToRemove++;
                else break;
            }

            if (spacesToRemove > 0)
            {
                _document.DeleteSelection(line, 0, line, spacesToRemove);

                if (line == _cursorLine)
                    _cursorCol = Mathf.Max(0, _cursorCol - spacesToRemove);
                if (line == _selectionAnchorLine)
                    _selectionAnchorChar = Mathf.Max(0, _selectionAnchorChar - spacesToRemove);
            }
        }

        #endregion

        #region Clipboard

        private void HandleCopy()
        {
            if (!HasSelection) return;
            GUIUtility.systemCopyBuffer = _document.GetSelectedText(
                _selectionAnchorLine, _selectionAnchorChar,
                _cursorLine, _cursorCol);
        }

        private void HandleCut()
        {
            if (!HasSelection) return;
            HandleCopy();
            CommitEdit(DeleteSelectedText);
        }

        private void HandlePaste()
        {
            var text = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(text)) return;
        
            CommitEdit(() =>
            {
                if (HasSelection) DeleteSelectedText();
                _document.InsertText(_cursorLine, _cursorCol, text,
                    out int newLine, out int newCol);
                _cursorLine = newLine;
                _cursorCol = newCol;
            });
        }

        public void SelectAll()
        {
            _selectionAnchorLine = 0;
            _selectionAnchorChar = 0;
            _cursorLine = _document.LineCount - 1;
            _cursorCol = _document.GetLine(_cursorLine).Length;
            MarkDirtyRepaint();
        }

        #endregion

        #region History

        public void Undo()
        {
            var step = _undoRedo.PopUndo();
            if (step == null) return;

            _isApplyingHistory = true;
            // Apply edits in reverse order and inverted
            for (int i = step.Edits.Count - 1; i >= 0; i--)
            {
                var edit = step.Edits[i];
                // To undo: delete AddedText and insert RemovedText
                if (!string.IsNullOrEmpty(edit.AddedText))
                {
                    // Find end range of added text
                    var lines = edit.AddedText.Split('\n');
                    int linesAdded = lines.Length;
                    int endLine = edit.Line + linesAdded - 1;
                    int endCol = (linesAdded == 1) 
                        ? edit.Col + lines[0].Length 
                        : lines[^1].Length;

                    _document.DeleteSelection(edit.Line, edit.Col, endLine, endCol);
                }
                if (!string.IsNullOrEmpty(edit.RemovedText))
                {
                    _document.InsertText(edit.Line, edit.Col, edit.RemovedText, out _, out _);
                }
            }
            _isApplyingHistory = false;

            ApplyCursorState(step.Before);
            OnDocumentChanged();
        }

        public void Redo()
        {
            var step = _undoRedo.PopRedo();
            if (step == null) return;

            _isApplyingHistory = true;
            // Apply edits in normal order as they happened
            foreach (var edit in step.Edits)
            {
                // To redo: delete RemovedText and insert AddedText
                if (!string.IsNullOrEmpty(edit.RemovedText))
                {
                    var lines = edit.RemovedText.Split('\n');
                    int linesRemoved = lines.Length;
                    int endLine = edit.Line + linesRemoved - 1;
                    int endCol = (linesRemoved == 1)
                        ? edit.Col + lines[0].Length
                        : lines[^1].Length;

                    _document.DeleteSelection(edit.Line, edit.Col, endLine, endCol);
                }
                if (!string.IsNullOrEmpty(edit.AddedText))
                {
                    _document.InsertText(edit.Line, edit.Col, edit.AddedText, out _, out _);
                }
            }
            _isApplyingHistory = false;

            ApplyCursorState(step.After);
            OnDocumentChanged();
        }

        #endregion

        #region Navigation Helpers

        private void MoveLeft(bool extendSelection, bool byWord)
        {
            BeginSelectionIfNeeded(extendSelection);

            if (HasSelection && !extendSelection)
            {
                TextDocument.NormalizeRange(_selectionAnchorLine, _selectionAnchorChar, _cursorLine, _cursorCol, out int sLine, out int sCol, out _, out _);
                _cursorLine = sLine;
                _cursorCol = sCol;
                ClearSelection();
                return;
            }

            if (_cursorCol > 0)
            {
                if (byWord) MoveByWord(-1);
                else _cursorCol--;
            }
            else if (_cursorLine > 0)
            {
                _cursorLine--;
                _cursorCol = _document.GetLine(_cursorLine).Length;
            }

            if (!extendSelection) ClearSelection();
        }

        private void MoveRight(bool extendSelection, bool byWord)
        {
            BeginSelectionIfNeeded(extendSelection);

            if (HasSelection && !extendSelection)
            {
                TextDocument.NormalizeRange(_selectionAnchorLine, _selectionAnchorChar, _cursorLine, _cursorCol, out _, out _, out int eLine, out int eCol);
                _cursorLine = eLine;
                _cursorCol = eCol;
                ClearSelection();
                return;
            }

            var lineLength = _document.GetLine(_cursorLine).Length;
            if (_cursorCol < lineLength)
            {
                if (byWord) MoveByWord(1);
                else _cursorCol++;
            }
            else if (_cursorLine < _document.LineCount - 1)
            {
                _cursorLine++;
                _cursorCol = 0;
            }

            if (!extendSelection) ClearSelection();
        }

        private void MoveCursorVertical(int dy, bool extendSelection)
        {
            BeginSelectionIfNeeded(extendSelection);
            _cursorLine = Mathf.Clamp(_cursorLine + dy, 0, _document.LineCount - 1);
            _cursorCol = Mathf.Min(_cursorCol, _document.GetLine(_cursorLine).Length);
            if (!extendSelection) ClearSelection();
        }

        private void MoveHome(bool extendSelection)
        {
            BeginSelectionIfNeeded(extendSelection);

            var lineText = _document.GetLine(_cursorLine);
            int firstNonSpace = 0;
            while (firstNonSpace < lineText.Length && char.IsWhiteSpace(lineText[firstNonSpace]))
                firstNonSpace++;

            if (_cursorCol == firstNonSpace)
            {
                _cursorCol = 0;
            }
            else
            {
                _cursorCol = firstNonSpace;
            }

            if (!extendSelection) ClearSelection();
        }

        private void MoveEnd(bool extendSelection)
        {
            BeginSelectionIfNeeded(extendSelection);

            var lineText = _document.GetLine(_cursorLine);
            int lastNonSpace = lineText.Length;
            while (lastNonSpace > 0 && char.IsWhiteSpace(lineText[lastNonSpace - 1]))
                lastNonSpace--;

            if (_cursorCol == lastNonSpace)
            {
                _cursorCol = lineText.Length;
            }
            else
            {
                _cursorCol = lastNonSpace;
            }

            if (!extendSelection) ClearSelection();
        }

        private void MoveByWord(int direction)
        {
            var lineText = _document.GetLine(_cursorLine);
            if (direction < 0) _cursorCol = PreviousWordBoundary(lineText, _cursorCol);
            else _cursorCol = NextWordBoundary(lineText, _cursorCol);
        }

        private void MoveToDocumentStart(bool extendSelection)
        {
            BeginSelectionIfNeeded(extendSelection);
            _cursorLine = 0;
            _cursorCol = 0;
        
            if (!extendSelection) ClearSelection();
        }

        private void MoveToDocumentEnd(bool extendSelection)
        {
            BeginSelectionIfNeeded(extendSelection);
            _cursorLine = _document.LineCount - 1;
            _cursorCol = _document.GetLine(_cursorLine).Length;
        
            if (!extendSelection) ClearSelection();
        }

        private static int NextWordBoundary(string line, int col)
        {
            // Skip whitespace, then skip the word
            while (col < line.Length && char.IsWhiteSpace(line[col])) col++;
            while (col < line.Length && !char.IsWhiteSpace(line[col])) col++;
            return col;
        }

        private static int PreviousWordBoundary(string line, int col)
        {
            if (col == 0) return 0;
            col--;
            while (col > 0 && char.IsWhiteSpace(line[col])) col--;
            while (col > 0 && !char.IsWhiteSpace(line[col])) col--;
            return col > 0 ? Mathf.Clamp(col + 1, 0, line.Length - 1) : 0;
        }

        private void BeginSelectionIfNeeded(bool extendSelection)
        {
            if (extendSelection && !HasSelection)
            {
                _selectionAnchorLine = _cursorLine;
                _selectionAnchorChar = _cursorCol;
            }
        }

        private void ClearSelection()
        {
            _selectionAnchorLine = -1;
            _selectionAnchorChar = -1;
        }

        #endregion
    
        #endregion

        #region Cursor Blink

        private void MarkActivity()
        {
            _lastCursorActivityTime = Time.realtimeSinceStartupAsDouble;
            if (_cursorVisible) return;
            _cursorVisible = true;
            MarkDirtyRepaint();
        }
    
        private void BlinkCursor(TimerState _)
        {
            double now = Time.realtimeSinceStartupAsDouble;

            // If user is active, keep cursor visible and do not blink
            if (now - _lastCursorActivityTime < 1.0)
            {
                if (_cursorVisible) return;
                _cursorVisible = true;
                MarkDirtyRepaint();
                return;
            }

            // Idle state: allow blinking
            _cursorVisible = !_cursorVisible;
            MarkDirtyRepaint();
        }

        #endregion
    }
}