using System;
using System.Runtime.CompilerServices;

namespace ScriptStack.Runtime
{
    public class ExecutionException : ScriptStackException
    {
        public ExecutionException()
            : base()
        {
        }

        public ExecutionException(
            string strMessage,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
            : base($"{strMessage} (Caller: {caller}, {System.IO.Path.GetFileName(file)}:{line})")
        {
        }

        public ExecutionException(
            string strMessage,
            Exception exceptionInner,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
            : base($"{strMessage} (Caller: {caller}, {System.IO.Path.GetFileName(file)}:{line})", exceptionInner)
        {
        }
    }
}
