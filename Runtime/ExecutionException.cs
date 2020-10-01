using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptStack.Runtime
{

    public class ExecutionException
        : ScriptStackException
    {
        #region Public Methods

        public ExecutionException()
            : base()
        {
        }

        public ExecutionException(String strMessage)
            : base(strMessage)
        {
        }

        public ExecutionException(String strMessage, Exception exceptionInner)
            : base(strMessage, exceptionInner)
        {
        }

        #endregion
    }
}
