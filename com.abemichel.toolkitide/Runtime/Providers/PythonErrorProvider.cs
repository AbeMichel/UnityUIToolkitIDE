using System.Collections.Generic;
using Document;

namespace Providers
{
    public class PythonErrorProvider : IErrorProvider
    {
        public List<CodeError> GetErrors(TextDocument document)
        {
            var errors = new List<CodeError>();
            for (int i = 0; i < document.LineCount; i++)
            {
                var line = document.GetLine(i);
                var trimmed = line.Trim();

                // Dummy error: Missing colon after def/class/if/else/elif/for/while
                if ((trimmed.StartsWith("def ") || trimmed.StartsWith("class ") || 
                     trimmed.StartsWith("if ") || trimmed.StartsWith("else") || 
                     trimmed.StartsWith("elif ") || trimmed.StartsWith("for ") || 
                     trimmed.StartsWith("while ")) && !trimmed.EndsWith(":"))
                {
                    errors.Add(new CodeError
                    {
                        Line = i,
                        Column = line.Length - trimmed.Length + trimmed.Length - 1, // At end of trimmed text
                        Length = 1,
                        Message = "Expected ':' at end of line",
                        Severity = ErrorSeverity.Error
                    });
                }
            }
            return errors;
        }
    }
}
