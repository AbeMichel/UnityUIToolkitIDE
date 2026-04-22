using AbesIde.Configuration;
using AbesIde.Document;
using UnityEngine;

namespace AbesIde.UI
{
        public static class HitTester
        {
                #region Public API

                public static (int line, int col) GetDocumentPosition(Vector2 localMousePosition, TextDocument document,
                        EditorConfig config)
                {
                        var line = GetLineIndex(localMousePosition.y, document.LineCount, config);
                        var col = GetColIndex(localMousePosition.x, document.GetLine(line), config);
                        return (line, col);
                }

                #endregion

                #region Line Detection

                private static int GetLineIndex(float y, int lineCount, EditorConfig config)
                {
                        // Offset by top padding then divide by line height
                        var line = Mathf.FloorToInt((y - config.TopPadding) / config.LineHeight);
                        return Mathf.Clamp(line, 0, lineCount - 1);
                }

                #endregion

                #region Column Detection

                private static int GetColIndex(float x, string line, EditorConfig config)
                {
                        if (string.IsNullOrEmpty(line)) return 0;

                        var cursorX = 0f;
                        var scale = ((float)config.FontSize / config.Font.faceInfo.pointSize) * config.Font.faceInfo.scale;

                        for (var i = 0; i < line.Length; i++)
                        {
                                if (!config.Font.characterLookupTable.TryGetValue(line[i], out var character)) continue;
                                if (!config.Font.glyphLookupTable.TryGetValue(character.glyphIndex, out var glyph)) continue;

                                var advance = glyph.metrics.horizontalAdvance * scale;
                                var charMidpoint = cursorX + advance * 0.5f;
                        
                                // If the click is left of this character's midpoint,
                                // the cursor should go left of it
                                if (x < charMidpoint) return i;
                                cursorX += advance;
                        }
                
                        // Click was past the last character
                        return line.Length;
                }

                #endregion
        }
}