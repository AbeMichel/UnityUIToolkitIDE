using Tokenizing;

namespace Autocomplete
{
    public struct AutocompleteSuggestion
    {
        public string Text;
        public string Description;
        public TokenType Type;

        public AutocompleteSuggestion(string text, string description, TokenType type)
        {
            Text = text;
            Description = description;
            Type = type;
        }
    }
}
