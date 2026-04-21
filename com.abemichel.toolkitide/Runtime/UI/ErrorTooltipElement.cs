using Configuration;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class ErrorTooltipElement : VisualElement
    {
        private readonly Label _messageLabel;

        public ErrorTooltipElement(EditorConfig config)
        {
            style.position = Position.Absolute;
            style.backgroundColor = new StyleColor(config.Theme.GutterBackgroundColor);
            style.borderLeftColor = style.borderRightColor = style.borderTopColor = style.borderBottomColor = new StyleColor(config.Theme.ErrorSquiggleColor);
            style.borderLeftWidth = style.borderRightWidth = style.borderTopWidth = style.borderBottomWidth = new StyleFloat(1);
            style.borderTopLeftRadius = style.borderTopRightRadius =
                style.borderBottomLeftRadius = style.borderBottomRightRadius = new StyleLength(8);
            style.paddingLeft = style.paddingRight = style.paddingTop = style.paddingBottom = new StyleLength(6);
            style.display = DisplayStyle.None;
            style.maxWidth = new StyleLength(300);

            _messageLabel = new Label
            {
                style =
                {
                    color = new StyleColor(config.Theme.ErrorColor),
                    fontSize = config.FontSize - 1,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            Add(_messageLabel);
        }

        public void Show(string message, Vector2 position, bool above)
        {
            _messageLabel.text = message;
        
            style.left = new StyleLength(position.x);
            style.top = new StyleLength(
                above 
                    ? position.y - resolvedStyle.height 
                    : position.y
            );
        
            style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            style.display = DisplayStyle.None;
        }

        public bool IsVisible => style.display == DisplayStyle.Flex;
    }
}
