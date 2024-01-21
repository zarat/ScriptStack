using System;
using System.Collections.Generic;
using System.Text;

using ScriptStack.Runtime;

namespace ScriptStack.Compiler
{

    public class LexerException : ScriptStackException
    {

        #region Public Methods

        public LexerException() : base()
        {
        }

        public LexerException(string message) : base("LexerException: " + message)
        {
        }

        public LexerException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public LexerException(string message, int line, int column, string text) : base(message + " Zeile " + (line + 1) + ", Zeichen " + System.Math.Abs(column - 1) + ": " + text)
        {
        }

        #endregion

    }

}
