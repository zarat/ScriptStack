using System;
using System.Collections.Generic;
using System.Text;

using ScriptStack.Runtime;

namespace ScriptStack.Compiler
{

    public class ParserException : ScriptStackException
    {

        #region Private Variables

        private Token token;

        #endregion

        #region Public Methods

        public ParserException()
            : base()
        {
            token = null;
        }

        public ParserException(string strMessage)
            : base("ParserException: " + strMessage)
        {
            token = null;
        }

        public ParserException(string strMessage, Exception exceptionInner)
            : base(strMessage, exceptionInner)
        {
            token = null;
        }

        public ParserException(string message, Token token)
            : base(message + " Zeile " + (token.Line + 1)  + ", Zeichen "+ System.Math.Abs(token.Column - 1) + ": " + token.Text)
        {
            this.token = token;
        }

        #endregion

    }

}
