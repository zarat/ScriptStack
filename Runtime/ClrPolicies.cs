using System;
using System.Linq;
using System.Reflection;

namespace ScriptStack.Runtime
{
    /// <summary>
    /// Denies all CLR interop. Default Policy!
    /// </summary>
    public sealed class DenyAllClrPolicy : IClrPolicy
    {
        public bool IsTypeAllowed(Type t) => false;
        public bool IsMemberAllowed(MemberInfo m) => false;
        public bool IsCallAllowed(MethodInfo m) => false;
        public bool IsReturnValueAllowed(object? value) => false;
    }

    /// <summary>
    /// Allows all CLR interop. Use only for trusted scripts.
    /// </summary>
    public sealed class AllowAllClrPolicy : IClrPolicy
    {
        public bool IsTypeAllowed(Type t) => true;
        public bool IsMemberAllowed(MemberInfo m) => true;
        public bool IsCallAllowed(MethodInfo m) => true;
        public bool IsReturnValueAllowed(object? value) => true;
    }

    /// <summary>
    /// A conservative "safe" policy: allows access only to a small set of generally harmless
    /// BCL types and members, blocks reflection/IO/process/threading by default.
    /// 
    /// This is intentionally restrictive. Extend/replace it for your use-case.
    /// </summary>
    public sealed class SafeClrPolicy : IClrPolicy
    {
        private static bool IsSafePrimitiveLike(Type t)
        {
            if (t.IsEnum) return true;
            if (t.IsPrimitive) return true;
            if (t == typeof(string)) return true;
            if (t == typeof(decimal)) return true;
            if (t == typeof(DateTime)) return true;
            if (t == typeof(TimeSpan)) return true;
            if (t == typeof(Guid)) return true;
            return false;
        }

        private static bool IsBlockedNamespace(string? ns)
        {
            if (string.IsNullOrWhiteSpace(ns)) return false;

            // Hard blocks: high-risk namespaces.
            return ns.StartsWith("System.IO", StringComparison.Ordinal)
                || ns.StartsWith("System.Reflection", StringComparison.Ordinal)
                || ns.StartsWith("System.Diagnostics", StringComparison.Ordinal)
                || ns.StartsWith("System.Runtime", StringComparison.Ordinal)
                || ns.StartsWith("System.Threading", StringComparison.Ordinal)
                || ns.StartsWith("System.Net", StringComparison.Ordinal)
                || ns.StartsWith("Microsoft.Win32", StringComparison.Ordinal);
        }

        public bool IsTypeAllowed(Type t)
        {
            if (t == null) return false;

            if (IsSafePrimitiveLike(t)) return true;

            if (t.IsArray)
                return IsTypeAllowed(t.GetElementType()!);

            if (IsBlockedNamespace(t.Namespace))
                return false;

            // Allow simple collection containers of safe types
            if (t.IsGenericType)
            {
                var def = t.GetGenericTypeDefinition();
                if (def == typeof(Nullable<>))
                    return IsTypeAllowed(Nullable.GetUnderlyingType(t)!);

                if (def == typeof(System.Collections.Generic.List<>)
                    || def == typeof(System.Collections.Generic.IList<>)
                    || def == typeof(System.Collections.Generic.ICollection<>)
                    || def == typeof(System.Collections.Generic.IEnumerable<>))
                {
                    return IsTypeAllowed(t.GetGenericArguments()[0]);
                }

                if (def == typeof(System.Collections.Generic.Dictionary<,>)
                    || def == typeof(System.Collections.Generic.IDictionary<,>))
                {
                    var ga = t.GetGenericArguments();
                    return IsTypeAllowed(ga[0]) && IsTypeAllowed(ga[1]);
                }
            }

            // Allow non-generic IList/IDictionary, but only as containers for safe values.
            if (typeof(System.Collections.IList).IsAssignableFrom(t)) return true;
            if (typeof(System.Collections.IDictionary).IsAssignableFrom(t)) return true;

            // Everything else: deny.
            return false;
        }

        public bool IsMemberAllowed(MemberInfo m)
        {
            if (m == null) return false;

            // Block obvious footguns
            if (m.Name.Equals("GetType", StringComparison.OrdinalIgnoreCase)) return false;

            var declaring = m.DeclaringType;
            if (declaring == null || !IsTypeAllowed(declaring)) return false;

            if (m is PropertyInfo p)
            {
                // Only allow properties that are safe to read/write
                if (!IsTypeAllowed(p.PropertyType)) return false;

                // Indexer properties are handled by index access; still ensure safe parameter types
                foreach (var ip in p.GetIndexParameters())
                {
                    if (!IsTypeAllowed(ip.ParameterType)) return false;
                }
                return true;
            }

            if (m is FieldInfo f)
            {
                // fields: allow only safe types
                return IsTypeAllowed(f.FieldType);
            }

            // other member kinds are not allowed here
            return false;
        }

        public bool IsCallAllowed(MethodInfo m)
        {
            if (m == null) return false;

            var declaring = m.DeclaringType;
            if (declaring == null || !IsTypeAllowed(declaring)) return false;

            // Block methods that open the door to reflection/unsafe access
            if (m.Name.Equals("GetType", StringComparison.OrdinalIgnoreCase)) return false;
            if (m.Name.Equals("GetHashCode", StringComparison.OrdinalIgnoreCase)) return true;
            if (m.Name.Equals("ToString", StringComparison.OrdinalIgnoreCase)) return true;

            // Only allow calls with safe parameter + return types
            if (m.ReturnType != typeof(void) && !IsTypeAllowed(m.ReturnType)) return false;

            foreach (var p in m.GetParameters())
            {
                if (!IsTypeAllowed(p.ParameterType)) return false;
            }

            // A small allowlist for common harmless string helpers
            if (declaring == typeof(string))
            {
                string[] allowed =
                {
                    "Contains", "StartsWith", "EndsWith", "Substring", "IndexOf", "Replace",
                    "ToUpper", "ToLower", "Trim", "TrimStart", "TrimEnd", "Split"
                };
                return allowed.Contains(m.Name, StringComparer.OrdinalIgnoreCase);
            }

            // Collections: allow basic operations only (Count is a property, indexers handled separately)
            if (declaring.IsGenericType && declaring.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                string[] allowed = { "Add", "Remove", "RemoveAt", "Clear", "Contains" };
                return allowed.Contains(m.Name, StringComparer.OrdinalIgnoreCase);
            }

            // Otherwise: deny.
            return false;
        }

        public bool IsReturnValueAllowed(object? value)
        {
            if (value == null) return true;
            if (value is NullReference) return true;

            return IsTypeAllowed(value.GetType());
        }
    
    }
}
