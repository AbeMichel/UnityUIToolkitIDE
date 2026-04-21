using Configuration;
using Providers;
using Tokenizing.Tokenizers;
using UI;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

public class IdeInitializer : MonoBehaviour
{
    [SerializeField] private UIDocument _document;
    [SerializeField] private FontAsset _font;
    [SerializeField] private FontAsset _fontBold;
    [SerializeField] private IdeTheme _theme;
    [SerializeField] private StyleSheet _styleSheet;
    
    private void Start()
    {
        var config = new EditorConfig
        {
            Font = _font,
            FontBold = _fontBold,
            FontSize = 14,
            LineHeightMultiplier = 1.4f,
            GutterWidthMultiplier = 3.5f,
            TopPadding = 10f,   
            Theme = _theme
        };
        
        var tokenizer = new PythonTokenizer();
        var autocomplete = new PythonAutocompleteProvider();
        var errorProvider = new PythonErrorProvider();
        var symbolInsight = new PythonSymbolInsightProvider();
        
        var ide = new Ide(config, tokenizer, autocomplete, errorProvider, symbolInsight);
        if (_styleSheet != null) ide.styleSheets.Add(_styleSheet);
        _document.rootVisualElement.Add(ide);
    }
}