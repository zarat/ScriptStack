using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptStack.Runtime
{
    /// <summary>
    /// Helper utilities for ScriptStack runtime values.
    /// 
    /// Arrays and Objects are separate runtime types, but we still need shared
    /// behavior (e.g. equality used by array subtraction, pretty printing).
    /// </summary>
    internal static class ScriptValue
    {
        public static object NormalizeValue(object? value)
            => value ?? NullReference.Instance;

        public static object NormalizeKey(object? key)
            => key ?? NullReference.Instance;

        public static bool EqualValues(object a, object b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a == NullReference.Instance || b == NullReference.Instance)
                return a == b;

            var ta = a.GetType();
            var tb = b.GetType();

            // int/float comparisons (legacy behavior)
            if (ta == typeof(int) && tb == typeof(int))
                return (int)a == (int)b;

            if (ta == typeof(int) && tb == typeof(float))
                return (int)a == (float)b;

            if (ta == typeof(float) && tb == typeof(int))
                return (float)a == (int)b;

            if (ta == typeof(float) && tb == typeof(float))
                return (float)a == (float)b;

            // string comparisons if any side is a string
            if (ta == typeof(string) || tb == typeof(string))
                return a.ToString() == b.ToString();

            return a.Equals(b);
        }

        public static string ToScriptString(object? value)
        {
            if (value == null || value == NullReference.Instance)
                return "null";

            if (value is ScriptArray arr)
                return arr.ToString();

            if (value is ScriptObject obj)
                return obj.ToString();

            if (value is string s)
                return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

            if (value is char c)
                return "\"" + c.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

            return value.ToString() ?? "null";
        }

        public static void AppendScriptString(StringBuilder sb, object? value)
        {
            sb.Append(ToScriptString(value));
        }
    }
}
