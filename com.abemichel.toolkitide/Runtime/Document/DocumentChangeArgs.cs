namespace AbesIde.Document
{
    public struct DocumentChangeArgs
    {
        public int StartLine;
        public int StartCol;
        public int LinesRemoved;
        public int LinesAdded;
        public string RemovedText;
        public string AddedText;

        public DocumentChangeArgs(int startLine, int startCol, int linesRemoved, int linesAdded, string removedText, string addedText)
        {
            StartLine = startLine;
            StartCol = startCol;
            LinesRemoved = linesRemoved;
            LinesAdded = linesAdded;
            RemovedText = removedText;
            AddedText = addedText;
        }
    }
}
