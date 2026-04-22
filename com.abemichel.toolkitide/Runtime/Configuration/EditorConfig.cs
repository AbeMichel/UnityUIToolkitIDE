using System;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace AbesIde.Configuration
{
    [System.Serializable]
    public class EditorConfig
    {
        public FontAsset Font;
        public FontAsset FontBold;
        public IdeTheme Theme;
        public float TopPadding;

        private int _fontSize = 14;
        public int FontSize
        {
            get => _fontSize;
            set
            {
                var clamped = Mathf.Clamp(value, 8, 72);
                if (_fontSize != clamped)
                {
                    _fontSize = clamped;
                    OnConfigChanged?.Invoke();
                }
            }
        }

        public float LineHeightMultiplier = 1.4f;
        public float LineHeight => FontSize * LineHeightMultiplier;

        public float GutterWidthMultiplier = 3.5f;
        public float GutterWidth => FontSize * GutterWidthMultiplier;

        public event Action OnConfigChanged;
    }
}