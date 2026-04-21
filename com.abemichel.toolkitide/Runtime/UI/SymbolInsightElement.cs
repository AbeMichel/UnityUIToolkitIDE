using Configuration;
using Providers;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class SymbolInsightElement : VisualElement
    {
        private readonly Label _signatureLabel;
        private readonly Label _parametersLabel;
        private readonly Label _returnValueLabel;
        private readonly Label _documentationLabel;
        private readonly EditorConfig _config;

        public SymbolInsightElement(EditorConfig config)
        {
            _config = config;
            style.position = Position.Absolute;
            style.backgroundColor = new StyleColor(config.Theme.GutterBackgroundColor);
            style.borderLeftColor = style.borderRightColor = style.borderTopColor = style.borderBottomColor = new StyleColor(Color.gray);
            style.borderLeftWidth = style.borderRightWidth = style.borderTopWidth = style.borderBottomWidth = 1;
            style.paddingLeft = style.paddingRight = style.paddingTop = style.paddingBottom = 8;
            style.display = DisplayStyle.None;
            style.maxWidth = 400;
            style.width = StyleKeyword.Auto;
            style.height = StyleKeyword.Auto;
            style.flexDirection = FlexDirection.Column;

            _signatureLabel = new Label();
            _signatureLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _signatureLabel.style.color = new StyleColor(config.Theme.KeywordColor);
            _signatureLabel.style.fontSize = config.FontSize;
            _signatureLabel.style.marginBottom = 4;
            _signatureLabel.style.whiteSpace = WhiteSpace.Normal;
            Add(_signatureLabel);

            _parametersLabel = new Label();
            _parametersLabel.style.color = new StyleColor(config.Theme.DefaultTextColor);
            _parametersLabel.style.fontSize = config.FontSize - 1;
            _parametersLabel.style.marginBottom = 2;
            _parametersLabel.style.whiteSpace = WhiteSpace.Normal;
            Add(_parametersLabel);

            _returnValueLabel = new Label();
            _returnValueLabel.style.color = new StyleColor(config.Theme.BuiltinColor);
            _returnValueLabel.style.fontSize = config.FontSize - 1;
            _returnValueLabel.style.marginBottom = 4;
            _returnValueLabel.style.whiteSpace = WhiteSpace.Normal;
            Add(_returnValueLabel);

            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new StyleColor(Color.gray);
            separator.style.marginTop = 4;
            separator.style.marginBottom = 4;
            Add(separator);

            _documentationLabel = new Label();
            _documentationLabel.style.color = new StyleColor(config.Theme.CommentColor);
            _documentationLabel.style.fontSize = config.FontSize - 1;
            _documentationLabel.style.whiteSpace = WhiteSpace.Normal;
            Add(_documentationLabel);
        }

        public void Show(SymbolInsight insight, Vector2 position)
        {
            _signatureLabel.text = insight.Signature;
        
            if (string.IsNullOrEmpty(insight.Parameters))
                _parametersLabel.style.display = DisplayStyle.None;
            else
            {
                _parametersLabel.text = "Parameters: " + insight.Parameters;
                _parametersLabel.style.display = DisplayStyle.Flex;
            }

            if (string.IsNullOrEmpty(insight.ReturnValue))
                _returnValueLabel.style.display = DisplayStyle.None;
            else
            {
                _returnValueLabel.text = "Returns: " + insight.ReturnValue;
                _returnValueLabel.style.display = DisplayStyle.Flex;
            }

            _documentationLabel.text = insight.Documentation;

            style.left = position.x;
            style.top = position.y;
            style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            style.display = DisplayStyle.None;
        }

        public bool IsVisible => style.display == DisplayStyle.Flex;
    }
}
