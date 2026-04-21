using System.Collections.Generic;
using Document;
using Tokenizing;

namespace Autocomplete
{
    public interface IAutocompleteProvider
    {
        /// <summary>
        /// Gets a list of autocomplete suggestions based on the current document, line, and column.
        /// </summary>
        IEnumerable<AutocompleteSuggestion> GetSuggestions(TextDocument document, int line, int col, List<TextToken> lineTokens);

        void AddSuggestion(AutocompleteSuggestion suggestion);
        void RemoveSuggestion(string text);
    }
}
