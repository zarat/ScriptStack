using System;
using System.Reflection;

namespace ScriptStack.Runtime
{
    /// <summary>
    /// Policy hook for CLR interop. Implementations decide what is accessible from scripts.
    /// </summary>
    public interface IClrPolicy
    {
        bool IsTypeAllowed(Type t);
        bool IsMemberAllowed(MemberInfo m);
        bool IsCallAllowed(MethodInfo m);
        bool IsReturnValueAllowed(object? value);
    }
}
