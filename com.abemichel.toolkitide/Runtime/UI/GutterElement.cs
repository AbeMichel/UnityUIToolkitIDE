using System.Collections.Generic;
using AbesIde.Configuration;
using AbesIde.Providers;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UIElements;

namespace AbesIde.UI
{
    public class GutterElement : VisualElement
    {
        #region Fields

        private readonly EditorConfig _config;
        private int _lineCount = 1;
        private List<CodeError> _errors = new();
        private Dictionary<int, ErrorSeverity> _lineSeverities = new();
        private HashSet<int> _errorLines = new();
        private HashSet<int> _todoLines = new();
        public float ScrollOffset { get; set; }
        public float ViewportHeight { get; set; }

        public System.Action<int, Vector2> OnErrorHover { get; set; }
        public System.Action OnErrorHoverExit { get; set; }

        #endregion

        #region Construction

        public GutterElement(EditorConfig config)
        {
            _config = config;
            style.width = config.GutterWidth;
            style.backgroundColor = new StyleColor(_config.Theme.GutterBackgroundColor);
            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseLeaveEvent>(e => OnErrorHoverExit?.Invoke());
            RegisterCallback<MouseDownEvent>(OnMouseDown);

            _config.OnConfigChanged += () =>
            {
                style.width = _config.GutterWidth;
                MarkDirtyRepaint();
            };
        }

        #endregion

        #region Public API

        public void SetLineCount(int count)
        {
            _lineCount = Mathf.Max(1, count);
            MarkDirtyRepaint();
        }

        public void SetErrors(List<CodeError> errors)
        {
            _errors = errors;
            _errorLines.Clear();
            _lineSeverities.Clear();
            foreach (var err in errors)
            {
                _errorLines.Add(err.Line);
                _lineSeverities[err.Line] = err.Severity;
            }
            MarkDirtyRepaint();
        }

        public void SetTodoLines(IEnumerable<int> todoLines)
        {
            _todoLines.Clear();
            foreach (var line in todoLines)
            {
                _todoLines.Add(line);
            }
            MarkDirtyRepaint();
        }

        private void OnMouseMove(MouseMoveEvent e)
        {
            int line = GetLineAtY(e.localMousePosition.y);
            if (_errorLines.Contains(line))
            {
                OnErrorHover?.Invoke(line, e.localMousePosition);
            }
            else
            {
                OnErrorHoverExit?.Invoke();
            }
        }

        private void OnMouseDown(MouseDownEvent e)
        {
            int line = GetLineAtY(e.localMousePosition.y);
            if (_errorLines.Contains(line))
            {
                OnErrorHover?.Invoke(line, e.localMousePosition);
            }
        }

        private int GetLineAtY(float y)
        {
            var line = Mathf.FloorToInt((y + ScrollOffset - _config.TopPadding) / _config.LineHeight);
            return Mathf.Clamp(line, 0, _lineCount - 1);
        }

        #endregion

        #region MeshGeneration

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (rect.width <= 0 || ViewportHeight <= 0) return;

            var labels = BuildLineLabels();
            var scale = GetFontScale();

            for (var i = 0; i < labels.Length; i++)
            {
                var y = _config.TopPadding + i * _config.LineHeight;
            
                // Viewport culling
                if (y + _config.LineHeight < ScrollOffset - _config.LineHeight) continue;
                if (y > ScrollOffset + ViewportHeight + _config.LineHeight) break;

                if (_errorLines.Contains(i))
                {
                    mgc.painter2D.fillColor = _config.Theme.ErrorGutterHighlightColor;
                    mgc.painter2D.BeginPath();
                    DrawRect(mgc, 0, y - ScrollOffset, rect.width, _config.LineHeight);
                    mgc.painter2D.ClosePath();
                    mgc.painter2D.Fill();
                }
                else if (_todoLines.Contains(i))
                {
                    mgc.painter2D.fillColor = _config.Theme.TodoGutterHighlightColor;
                    mgc.painter2D.BeginPath();
                    DrawRect(mgc, 0, y - ScrollOffset, rect.width, _config.LineHeight);
                    mgc.painter2D.ClosePath();
                    mgc.painter2D.Fill();
                }

                // Pre-warm dynamic font atlas
                _config.Font.TryAddCharacters(labels[i]);

                // Calculate width for right alignment
                float labelWidth = 0;
                foreach (var c in labels[i])
                {
                    if (TryGetGlyph(c, out var glyph))
                    {
                        labelWidth += glyph.metrics.horizontalAdvance * scale;
                    }
                }

                // Draw line number - manually right-aligned
                var drawX = rect.width - labelWidth - 8f;
                mgc.DrawText(labels[i], new Vector2(drawX, y - ScrollOffset), _config.FontSize, _config.Theme.GutterTextColor, _config.Font);
            }
        }

        private string[] BuildLineLabels()
        {
            var labels = new string[_lineCount];
            for (var i = 0; i < _lineCount; i++)
            {
                labels[i] = (i + 1).ToString();
            }
            return labels;
        }

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

        private void DrawRect(MeshGenerationContext mgc, float x, float y, float width, float height)
        {
            mgc.painter2D.MoveTo(new Vector2(x, y));
            mgc.painter2D.LineTo(new Vector2(x + width, y));
            mgc.painter2D.LineTo(new Vector2(x + width, y + height));
            mgc.painter2D.LineTo(new Vector2(x, y + height));
        }

        #endregion
    }
}