using System;
using System.Runtime.CompilerServices;
using ScriptStack.Runtime;

namespace ScriptStack.Compiler
{
    public class ParserException : ScriptStackException
    {
        private Token token;

        public ParserException() : base() => token = null;

        public ParserException(string strMessage)
            : base("ParserException: " + strMessage) => token = null;

        public ParserException(string strMessage, Exception exceptionInner)
            : base(strMessage, exceptionInner) => token = null;

        public ParserException(
            string message,
            Token token,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int callLine = 0)
            : base($"{message} (Caller: {caller}, {System.IO.Path.GetFileName(file)}:{callLine}) " +
                   $"Zeile {token.Line + 1}, Zeichen {Math.Abs(token.Column - 1)}: {token.Text}")
        {
            this.token = token;
        }
    }
}
