using System;

namespace Dataportal.Services
{
    public class NotebookApiResponseTooLargeException : Exception
    {
        public NotebookApiResponseTooLargeException(string message)
            : base(message)
        {
        }
    }
}
