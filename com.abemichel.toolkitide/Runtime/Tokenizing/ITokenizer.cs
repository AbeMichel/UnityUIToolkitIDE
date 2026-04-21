using System.Collections.Generic;
using Document;

namespace Tokenizing
{
    public enum LineState
    {
        Normal,
        InBlockComment,
        InMultilineStringDouble,
        InMultilineStringSingle,
        InTodoBlock
    }

    public interface ITokenizer
    {
        string CommentPrefix { get; }

        List<List<TextToken>> Tokenize(TextDocument document);
        
        List<TextToken> TokenizeLine(string line, LineState initialState, out LineState exitState);

        void AddRule(TokenRule rule);
        
        void AddRules(params TokenRule[] rules);
        
        void RemoveRule(string pattern);
        
        void RemoveRules(params TokenRule[] rules);
        void RemoveRules(params string[] patterns);
    }
}