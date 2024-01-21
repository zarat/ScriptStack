using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptStack.Runtime
{

    public class ScriptStackException : Exception
    {

        #region Private Variables

        private string message;
        private Exception innerException;

        #endregion

        #region Public Methods

        public ScriptStackException()
        {

            message = "Keine weiteren Details.";

            innerException = null;

        }

        public ScriptStackException(string message)
        {

            this.message = message;

            innerException = null;

        }

        public ScriptStackException(string message, Exception innerException)
        {
            this.message = message;
            this.innerException = innerException;
        }

        public override string ToString()
        {
            return MessageTrace;
        }

        #endregion

        #region Public Properties

        public new string Message
        {
            get { return message; }
        }

        public string MessageTrace
        {

            get
            {

                if (innerException != null)
                {

                    string messageTrace = message + "\nFehlerquelle: ";

                    if (typeof(ScriptStackException).IsAssignableFrom(innerException.GetType()))
                        messageTrace += ((ScriptStackException) innerException).MessageTrace;

                    else
                        messageTrace += innerException.Message;

                    return 
                        messageTrace;

                }

                else
                    return message;

            }

        }

        #endregion

    }

}
