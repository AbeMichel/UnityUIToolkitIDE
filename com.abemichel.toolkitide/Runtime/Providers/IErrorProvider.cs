using System.Collections.Generic;
using Document;

namespace Providers
{
    public enum ErrorSeverity
    {
        Error,
        Todo
    }

    public struct CodeError
    {
        public int Line;
        public int Column; // Start column
        public int Length; // Length of offending part
        public string Message;
        public ErrorSeverity Severity;
    }

    public interface IErrorProvider
    {
        List<CodeError> GetErrors(TextDocument document);
    }
}