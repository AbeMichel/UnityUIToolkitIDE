using AbesIde.Document;

namespace AbesIde.Providers
{
    public struct SymbolInsight
    {
        public string Signature;
        public string Parameters;
        public string ReturnValue;
        public string Documentation;
    }

    public interface ISymbolInsightProvider
    {
        SymbolInsight? GetInsight(TextDocument document, int line, int col);
    }
}