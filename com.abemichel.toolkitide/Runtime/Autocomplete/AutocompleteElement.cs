using System;
using System.Collections.Generic;
using System.Linq;
using Configuration;
using UnityEngine;
using UnityEngine.UIElements;

namespace Autocomplete
{
    public class AutocompleteElement : VisualElement
    {
        private readonly List<AutocompleteSuggestion> _suggestions = new();
        private int _selectedIndex = 0;
        private readonly int _menuWidth = 200;
        private readonly VisualElement _tooltipContainer;
        private readonly Label _descriptionLabel;

        private readonly EditorConfig _config;
        private readonly ScrollView _scrollView;
        
        public event Action<AutocompleteSuggestion> OnSuggestionSelected;
        
        public AutocompleteElement(EditorConfig config)
        {
            _config = config;
            style.position = Position.Absolute;
            style.backgroundColor = new StyleColor(config.Theme.GutterBackgroundColor);
            style.borderLeftColor = style.borderRightColor = style.borderTopColor = style.borderBottomColor = new StyleColor(Color.gray);
            style.borderLeftWidth = style.borderRightWidth = style.borderTopWidth = style.borderBottomWidth = 1;
            style.width = _menuWidth;
            style.maxHeight = 200;
            style.display = DisplayStyle.None;

            _scrollView = new ScrollView();
            Add(_scrollView);

            _tooltipContainer = new VisualElement();
            _tooltipContainer.style.position = Position.Absolute;
            _tooltipContainer.style.left = _menuWidth + 5;
            _tooltipContainer.style.top = 0;
            _tooltipContainer.style.width = 200;
            _tooltipContainer.style.backgroundColor = new StyleColor(config.Theme.GutterBackgroundColor);
            _tooltipContainer.style.borderLeftColor = _tooltipContainer.style.borderRightColor = _tooltipContainer.style.borderTopColor = _tooltipContainer.style.borderBottomColor = new StyleColor(Color.gray);
            _tooltipContainer.style.borderLeftWidth = _tooltipContainer.style.borderRightWidth = _tooltipContainer.style.borderTopWidth = _tooltipContainer.style.borderBottomWidth = 1;
            _tooltipContainer.style.paddingLeft = _tooltipContainer.style.paddingRight = _tooltipContainer.style.paddingTop = _tooltipContainer.style.paddingBottom = 5;
            _tooltipContainer.style.display = DisplayStyle.None;
            
            _descriptionLabel = new Label();
            _descriptionLabel.style.color = new StyleColor(config.Theme.DefaultTextColor);
            _descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            _tooltipContainer.Add(_descriptionLabel);
            Add(_tooltipContainer);
        }

        public void SetSuggestions(IEnumerable<AutocompleteSuggestion> suggestions)
        {
            _suggestions.Clear();
            _suggestions.AddRange(suggestions);
            _selectedIndex = 0;

            _scrollView.Clear();
            foreach (var suggestion in _suggestions)
            {
                var label = new Label(suggestion.Text);
                label.style.paddingLeft = 5;
                label.style.paddingRight = 5;
                label.style.color = new StyleColor(_config.Theme.GetColor(suggestion.Type));
                _scrollView.Add(label);
            }

            if (_suggestions.Count > 0)
            {
                style.display = DisplayStyle.Flex;
                UpdateSelection();
            }
            else
            {
                style.display = DisplayStyle.None;
            }
        }

        public void MoveSelection(int delta)
        {
            if (_suggestions.Count == 0) return;
            _selectedIndex = Mathf.Clamp(_selectedIndex + delta, 0, _suggestions.Count - 1);
            UpdateSelection();
        }

        public bool SelectCurrent()
        {
            if (_suggestions.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _suggestions.Count)
                return false;

            OnSuggestionSelected?.Invoke(_suggestions[_selectedIndex]);
            Hide();
            return true;
        }

        public void Hide()
        {
            style.display = DisplayStyle.None;
            _suggestions.Clear();
        }

        public bool IsVisible => style.display == DisplayStyle.Flex;

        private void UpdateSelection()
        {
            for (int i = 0; i < _scrollView.contentContainer.childCount; i++)
            {
                var child = _scrollView.contentContainer[i];
                if (i == _selectedIndex)
                {
                    child.style.backgroundColor = new StyleColor(_config.Theme.SelectionColor);
                }
                else
                {
                    child.style.backgroundColor = new StyleColor(Color.clear);
                }
            }
            
            // Update tooltip
            if (_selectedIndex >= 0 && _selectedIndex < _suggestions.Count)
            {
                var suggestion = _suggestions[_selectedIndex];
                if (!string.IsNullOrEmpty(suggestion.Description))
                {
                    _descriptionLabel.text = suggestion.Description;
                    _tooltipContainer.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _tooltipContainer.style.display = DisplayStyle.None;
                }
            }
            else
            {
                _tooltipContainer.style.display = DisplayStyle.None;
            }

            // Ensure selected item is visible
            if (_selectedIndex >= 0 && _selectedIndex < _scrollView.contentContainer.childCount)
            {
                var selectedLabel = _scrollView.contentContainer[_selectedIndex];
                // ScrollView handles this usually, but we might need manual logic if it doesn't
            }
        }
    }
}
