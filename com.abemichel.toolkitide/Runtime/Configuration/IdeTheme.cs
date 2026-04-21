using Tokenizing;
using UnityEngine;

namespace Configuration
{
    [CreateAssetMenu(fileName = "IdeTheme", menuName = "IDE/Theme")]
    public class IdeTheme : ScriptableObject
    {
        [Header("General")]
        public Color BackgroundColor = new Color(0.12f, 0.12f, 0.12f);
        public Color SelectionColor = new Color(0.27f, 0.45f, 0.70f, 0.5f);
        public Color CursorColor = Color.white;

        [Header("Gutter")]
        public Color GutterBackgroundColor = new Color(0.15f, 0.15f, 0.15f);
        public Color GutterTextColor = new Color(0.6f, 0.6f, 0.6f);
        public Color ErrorGutterHighlightColor = new Color(0.95f, 0.30f, 0.30f, 0.2f);

        [Header("Scrollbars")]
        public Color ScrollbarBackgroundColor = new Color(0.12f, 0.12f, 0.12f, 0f);
        public Color ScrollbarThumbColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        public Color ScrollbarThumbHoverColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);

        [Header("Tokens")]
        public Color DefaultTextColor = Color.white;
        public Color KeywordColor = new Color(0.56f, 0.74f, 0.99f);
        public Color StringLiteralColor = new Color(0.80f, 0.52f, 0.40f);
        public Color CommentColor = new Color(0.47f, 0.53f, 0.47f);
        public Color NumberColor = new Color(0.71f, 0.86f, 0.65f);
        public Color OperatorColor = new Color(0.85f, 0.85f, 0.85f);
        public Color ErrorColor = new Color(0.95f, 0.30f, 0.30f);
        public Color BuiltinColor = new Color(0.74f, 0.56f, 0.99f);
        public Color DecoratorColor = new Color(0.99f, 0.74f, 0.56f);
        public Color PreprocessorColor = new Color(0.6f, 0.6f, 0.6f);
        public Color ErrorSquiggleColor = new Color(0.95f, 0.30f, 0.30f);
        public Color TodoColor = new Color(1f, 0.8f, 0f);
        public Color TodoGutterHighlightColor = new Color(1f, 0.8f, 0f, 0.2f);

        public Color GetColor(TokenType type) => type switch {
            TokenType.Keyword       => KeywordColor,
            TokenType.StringLiteral => StringLiteralColor,
            TokenType.Comment       => CommentColor,
            TokenType.Number        => NumberColor,
            TokenType.Operator      => OperatorColor,
            TokenType.Error         => ErrorColor,
            TokenType.Builtin       => BuiltinColor,
            TokenType.Decorator     => DecoratorColor,
            TokenType.Preprocessor  => PreprocessorColor,
            TokenType.Todo          => TodoColor,
            _ => DefaultTextColor,
        };
    }
}
