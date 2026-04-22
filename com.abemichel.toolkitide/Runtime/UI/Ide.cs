using System.Collections.Generic;
using AbesIde.Autocomplete;
using AbesIde.Configuration;
using AbesIde.Document;
using AbesIde.Providers;
using AbesIde.Tokenizing;
using UnityEngine;
using UnityEngine.UIElements;

namespace AbesIde.UI
{
    public class Ide : VisualElement
    {
        private readonly TextDocument _document;
        private readonly GutterElement _gutter;
        private readonly CodeElement _code;
        private readonly ScrollView _scrollView;
    
        private EditorConfig _config;
    
        private ITokenizer _tokenizer;
        private IAutocompleteProvider _autocompleteProvider;
        private IErrorProvider _errorProvider;
        private ISymbolInsightProvider _symbolInsightProvider;
    
        private readonly List<LineState> _lineEntryStates = new();

        private readonly AutocompleteElement _autocompleteMenu;
        private readonly SymbolInsightElement _symbolInsightMenu;
        private readonly ErrorTooltipElement _errorTooltip;
        private IVisualElementScheduledItem _errorDebounceTask;
        private List<CodeError> _currentErrors = new();
        private HashSet<int> _todoLines = new();

        private bool _forceShowAutocomplete;

        public Ide(EditorConfig config, ITokenizer tokenizer, IAutocompleteProvider autocompleteProvider, IErrorProvider errorProvider, ISymbolInsightProvider symbolInsightProvider)
        {
            style.flexDirection = FlexDirection.Row;
            this.style.flexGrow = 1;
        
            _config = config;
        
            _tokenizer = tokenizer;
            _autocompleteProvider = autocompleteProvider;
            _errorProvider = errorProvider;
            _symbolInsightProvider = symbolInsightProvider;
            _document = new TextDocument();
        
            _gutter = new GutterElement(config);
            Add(_gutter);
        
            _scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _scrollView.style.flexGrow = 1;
            _scrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            _scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            _scrollView.focusable = false;
            _scrollView.verticalScroller.focusable = false;
            _scrollView.horizontalScroller.focusable = false;
            // The Scroller's child Slider and its dragger are focusable by default.
            // Disabling them prevents keyboard focus from being stolen when a scrollbar
            // appears (Auto visibility mode).
            _scrollView.verticalScroller.Query<VisualElement>().ForEach(el => el.focusable = false);
            _scrollView.horizontalScroller.Query<VisualElement>().ForEach(el => el.focusable = false);
            Add(_scrollView);
        
            _code = new CodeElement(config, _document, OnLegacyDocumentChanged);
            _code.CommentPrefix = tokenizer.CommentPrefix;
            _scrollView.Add(_code);
        
            _autocompleteMenu = new AutocompleteElement(config);
            Add(_autocompleteMenu);
            _autocompleteMenu.OnSuggestionSelected += OnSuggestionSelected;

            _symbolInsightMenu = new SymbolInsightElement(config);
            Add(_symbolInsightMenu);

            _errorTooltip = new ErrorTooltipElement(config);
            Add(_errorTooltip);

            _code.OnCharacterTyped = () => _forceShowAutocomplete = true;

            _code.OnCursorMoved = () =>
            {
                _code.ScrollToCursor(_scrollView);
                UpdateAutocomplete(_forceShowAutocomplete);
                _forceShowAutocomplete = false;
                _symbolInsightMenu.Hide();
                _errorTooltip.Hide();
            };

            _code.OnHover = (line, col, pos) => UpdateSymbolInsight(line, col, pos);
            _code.OnHoverExit = () => _symbolInsightMenu.Hide();

            _gutter.OnErrorHover = (line, pos) => ShowGutterError(line, pos);
            _gutter.OnErrorHoverExit = () => _errorTooltip.Hide();

            // Ensure the editor is focused so the first keystroke works.
            // Defer by one frame so the panel has completed its first layout pass.
            _code.RegisterCallback<AttachToPanelEvent>(e => schedule.Execute(() => _code.Focus()).ExecuteLater(0));
            this.RegisterCallback<PointerDownEvent>(e => _code.Focus());
            _scrollView.RegisterCallback<PointerDownEvent>(e => _code.Focus());
            _scrollView.contentContainer.RegisterCallback<PointerDownEvent>(e => _code.Focus());
            // Scrollers have focusable=false but their internal handlers call StopPropagation,
            // so bubble-up focus restores above don't fire when clicking on a scrollbar.
            _scrollView.verticalScroller.RegisterCallback<PointerDownEvent>(e => _code.Focus());
            _scrollView.horizontalScroller.RegisterCallback<PointerDownEvent>(e => _code.Focus());
            // Safety net: if any element inside the IDE unexpectedly gains focus, redirect it.
            this.RegisterCallback<FocusInEvent>(e => 
            { 
                if (e.target != _code && e.target is VisualElement ve && this.Contains(ve)) 
                    _code.Focus(); 
            });

            // Unity fires NavigationMoveEvent alongside KeyDownEvent for arrow keys, and
            // the ScrollView's internal trickle-down handler processes it before CodeElement
            // sees it. Registering on `this` with TrickleDown runs before any ScrollView
            // handler, so the ScrollView never receives the event.
            this.RegisterCallback<NavigationMoveEvent>(e => e.StopPropagation(), TrickleDown.TrickleDown);
            this.RegisterCallback<NavigationSubmitEvent>(e => e.StopPropagation(), TrickleDown.TrickleDown);
            this.RegisterCallback<NavigationCancelEvent>(e => e.StopPropagation(), TrickleDown.TrickleDown);

            // Intercept keys for autocomplete
            this.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            _scrollView.verticalScroller.valueChanged += (v) =>
            {
                _gutter.ScrollOffset = v;
                _gutter.MarkDirtyRepaint();
            
                _code.ScrollOffset = v;
                _code.MarkDirtyRepaint();
                UpdateAutocompletePosition();
                _symbolInsightMenu.Hide();
                _errorTooltip.Hide();
            };

            _scrollView.contentViewport.RegisterCallback<GeometryChangedEvent>(e =>
            {
                _gutter.ViewportHeight = e.newRect.height;
                _gutter.MarkDirtyRepaint();
            
                _code.ViewportHeight = e.newRect.height;
                _code.MarkDirtyRepaint();
            });

            _document.OnChanged += OnDocumentChanged;
            _document.OnChanged += _ => { _symbolInsightMenu.Hide(); _errorTooltip.Hide(); };
        
            _errorDebounceTask = schedule.Execute(RunErrorCheck);
        
            config.OnConfigChanged += () => ApplyTheme(config);
            ApplyTheme(config);

            // Initial render
            FullRebuild();
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (!_autocompleteMenu.IsVisible) return;

            switch (e.keyCode)
            {
                case KeyCode.UpArrow:
                    _autocompleteMenu.MoveSelection(-1);
                    e.StopImmediatePropagation();
                    break;
                case KeyCode.DownArrow:
                    _autocompleteMenu.MoveSelection(1);
                    e.StopImmediatePropagation();
                    break;
                case KeyCode.Return:
                case KeyCode.Tab:
                    if (_autocompleteMenu.SelectCurrent())
                    {
                        e.StopImmediatePropagation();
                    }
                    break;
                case KeyCode.Escape:
                    _autocompleteMenu.Hide();
                    e.StopImmediatePropagation();
                    break;
            }
        }

        private void UpdateAutocomplete(bool forceShow = false)
        {
            if (_code.HasSelection)
            {
                _autocompleteMenu.Hide();
                return;
            }

            if (!forceShow && !_autocompleteMenu.IsVisible)
            {
                return;
            }

            var lineTokens = _code.GetLineTokens(_code.CursorLine);
            var suggestions = _autocompleteProvider.GetSuggestions(_document, _code.CursorLine, _code.CursorCol, lineTokens);
            _autocompleteMenu.SetSuggestions(suggestions);
            UpdateAutocompletePosition();
        }

        private void UpdateAutocompletePosition()
        {
            if (!_autocompleteMenu.IsVisible) return;

            var localPos = _code.GetCursorLocalPosition();
            // Adjust for scroll and gutter
            var x = localPos.x + _gutter.layout.width - _scrollView.scrollOffset.x;
            var y = localPos.y + _code.style.marginTop.value.value - _scrollView.scrollOffset.y + _code.layout.y + 20; // 20 for line height approx

            _autocompleteMenu.style.left = x;
            _autocompleteMenu.style.top = y;
        }

        private void UpdateSymbolInsight(int line, int col, Vector2 pos)
        {
            if (_symbolInsightProvider == null) return;
            var insight = _symbolInsightProvider.GetInsight(_document, line, col);
            if (insight.HasValue)
            {
                var displayPos = pos;
                displayPos.x += _gutter.layout.width - _scrollView.scrollOffset.x;
                displayPos.y += _code.style.marginTop.value.value - _scrollView.scrollOffset.y + _code.layout.y + 20;
                _symbolInsightMenu.Show(insight.Value, displayPos);
            }
            else
            {
                _symbolInsightMenu.Hide();
            }
        }

        private void ShowGutterError(int line, Vector2 pos)
        {
            var error = _currentErrors.Find(e => e.Line == line);
        
            if (error.Message != null)
            {
                // Position it next to the gutter
                var lineTop = _config.TopPadding + line * _config.LineHeight - _scrollView.scrollOffset.y;
            
                // If the line is at the very top of the view, align tooltip with the bottom of the line
                // to ensure it's fully visible and doesn't feel cramped against the top edge.
                var above = lineTop >= 20f;
                var yOffset = above ? 0f : _config.LineHeight;
            
                var displayPos = new Vector2(_gutter.layout.width + 5, lineTop + yOffset + _code.layout.y);
                _errorTooltip.Show(error.Message, displayPos, above);
            }
            else
            {
                _errorTooltip.Hide();
            }
        }

        private void UpdateTodoLines()
        {
            _todoLines.Clear();
            for (int i = 0; i < _code.GetLineCount(); i++)
            {
                var tokens = _code.GetLineTokens(i);
                if (tokens != null)
                {
                    foreach (var token in tokens)
                    {
                        if (token.Type == TokenType.Todo)
                        {
                            _todoLines.Add(i);
                            break;
                        }
                    }
                }
            }
            _gutter.SetTodoLines(_todoLines);
        }

        private void OnSuggestionSelected(AutocompleteSuggestion suggestion)
        {
            _code.InsertCompletion(suggestion);
        }

        private void ApplyTheme(EditorConfig config)
        {
            this.style.backgroundColor = new StyleColor(config.Theme.BackgroundColor);
        
            ApplyScrollerTheme(_scrollView.verticalScroller, config.Theme);
            ApplyScrollerTheme(_scrollView.horizontalScroller, config.Theme);
        }

        private void ApplyScrollerTheme(Scroller scroller, IdeTheme theme)
        {
            scroller.style.backgroundColor = new StyleColor(theme.ScrollbarBackgroundColor);
        
            var dragger = scroller.Q("unity-dragger");
            if (dragger != null)
            {
                dragger.style.backgroundColor = new StyleColor(theme.ScrollbarThumbColor);
            
                // Re-apply on hover since C# style overrides USS hover
                dragger.RegisterCallback<MouseEnterEvent>(e => 
                    dragger.style.backgroundColor = new StyleColor(theme.ScrollbarThumbHoverColor));
                dragger.RegisterCallback<MouseLeaveEvent>(e => 
                    dragger.style.backgroundColor = new StyleColor(theme.ScrollbarThumbColor));
            }
        }

        private void OnLegacyDocumentChanged(TextDocument doc)
        {
            // Handled by event now
        }

        private void RunErrorCheck()
        {
            if (_errorProvider == null) return;
            _currentErrors = _errorProvider.GetErrors(_document);
            _code.SetErrors(_currentErrors);
            _gutter.SetErrors(_currentErrors);
        }

        private void FullRebuild()
        {
            var tokenized = _tokenizer.Tokenize(_document);
            _lineEntryStates.Clear();
            var state = LineState.Normal;
            for (int i = 0; i < _document.LineCount; i++)
            {
                _lineEntryStates.Add(state);
                _tokenizer.TokenizeLine(_document.GetLine(i), state, out state);
            }
        
            _code.SetTokens(tokenized);
            _code.ScrollToCursor(_scrollView);
            _gutter.SetLineCount(_document.LineCount);
            UpdateTodoLines();
            _autocompleteProvider.OnDocumentChanged(new DocumentChangeArgs(0, 0, 0, _document.LineCount, string.Empty, string.Join("\n", _document.Lines)), _document);
            _errorDebounceTask.ExecuteLater(500);
        }

        private void OnDocumentChanged(DocumentChangeArgs args)
        {
            // 1. Sync the entry states list length
            if (args.LinesRemoved > args.LinesAdded)
            {
                _lineEntryStates.RemoveRange(args.StartLine + 1, args.LinesRemoved - args.LinesAdded);
            }
            else if (args.LinesAdded > args.LinesRemoved)
            {
                for (int i = 0; i < (args.LinesAdded - args.LinesRemoved); i++)
                    _lineEntryStates.Insert(args.StartLine + 1, LineState.Normal);
            }

            // 2. Incremental re-tokenize
            var newTokens = new List<List<TextToken>>();
            var state = _lineEntryStates[args.StartLine];
            var currentLine = args.StartLine;
        
            // We need to keep track of how many lines in the OLD _code._lines list 
            // we are replacing. Initially it's args.LinesRemoved.
            int totalLinesToReplace = args.LinesRemoved;
            int linesProcessed = 0;

            // Shift TODO line indices below the edit
            if (args.LinesAdded != args.LinesRemoved)
            {
                var newTodoLines = new HashSet<int>();
                int delta = args.LinesAdded - args.LinesRemoved;
                foreach (var line in _todoLines)
                {
                    if (line < args.StartLine) newTodoLines.Add(line);
                    else if (line >= args.StartLine + args.LinesRemoved) newTodoLines.Add(line + delta);
                }
                _todoLines = newTodoLines;
            }

            while (true)
            {
                var lineText = _document.GetLine(currentLine);
                _lineEntryStates[currentLine] = state;
                var tokens = _tokenizer.TokenizeLine(lineText, state, out var nextState);
                newTokens.Add(tokens);

                // Update TODO status for this line
                _todoLines.Remove(currentLine);
                foreach (var token in tokens)
                {
                    if (token.Type == TokenType.Todo)
                    {
                        _todoLines.Add(currentLine);
                        break;
                    }
                }

                state = nextState;
                currentLine++;
                linesProcessed++;

                // Stop if we've processed at least all "added" lines AND the state has stabilized
                if (linesProcessed >= args.LinesAdded)
                {
                    // If we're at the end of the document, we're done
                    if (currentLine >= _document.LineCount) break;

                    // If the next line's stored entry state matches our new exit state, the cascade stops
                    if (_lineEntryStates[currentLine] == state) break;
                
                    // Otherwise, keep cascading down. 
                    // We are now consuming additional existing lines.
                    totalLinesToReplace++;
                }
            }

            _code.UpdateTokens(args.StartLine, totalLinesToReplace, newTokens);
            _code.ScrollToCursor(_scrollView);
            _gutter.SetLineCount(_document.LineCount);
            _gutter.SetTodoLines(_todoLines);
            _autocompleteProvider.OnDocumentChanged(args, _document);
            _errorDebounceTask.ExecuteLater(500);
        }

        #region Public API

        public void SetLanguage(ITokenizer tokenizer, IAutocompleteProvider autocompleteProvider, IErrorProvider errorProvider, ISymbolInsightProvider symbolInsightProvider)
        {
            _tokenizer = tokenizer;
            _autocompleteProvider = autocompleteProvider;
            _errorProvider = errorProvider;
            _symbolInsightProvider = symbolInsightProvider;
            _code.CommentPrefix = tokenizer.CommentPrefix;
            FullRebuild();
        }

        public void SetAutocompleteProvider(IAutocompleteProvider provider)
        {
            _autocompleteProvider = provider;
        }

        public void SetErrorProvider(IErrorProvider provider)
        {
            _errorProvider = provider;
            FullRebuild();
        }

        public void SetSymbolInsightProvider(ISymbolInsightProvider provider)
        {
            _symbolInsightProvider = provider;
            FullRebuild();
        }

        #endregion
    }
}