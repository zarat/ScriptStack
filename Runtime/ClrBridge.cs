using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace ScriptStack.Runtime
{
    /// <summary>
    /// Centralized CLR interop bridge for ScriptStack.
    /// Handles reflection-based member access, method invocation, indexers, and value coercion.
    /// 
    /// All access is mediated by an <see cref="IClrPolicy"/>.
    /// </summary>
    public sealed class ClrBridge
    {

        private readonly IClrPolicy _policy;

        private static readonly BindingFlags ClrMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
        private static readonly BindingFlags ClrInstanceMemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
        private static readonly BindingFlags ClrStaticMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy;

        public ClrBridge(IClrPolicy policy)
        {
            _policy = policy ?? new DenyAllClrPolicy();
        }

        internal static object? ScriptNullToClr(object? v) => v is NullReference ? null : v;

        private static object ClrNullToScript(object? v) => v ?? NullReference.Instance;

        private void EnsureTypeAllowed(Type t)
        {
            if (!_policy.IsTypeAllowed(t))
                throw new ExecutionException($"CLR interop is disabled or access denied for type '{t.FullName}'.");
        }

        private void EnsureReturnAllowed(object? value, string context)
        {
            if (!_policy.IsReturnValueAllowed(value))
                throw new ExecutionException($"CLR interop blocked return value in {context} (type '{value?.GetType().FullName ?? "null"}').");
        }

        public object GetMember(object target, string memberName) => GetMemberValue(target, memberName);
        public void SetMember(object target, string memberName, object scriptValue) => SetMemberValue(target, memberName, scriptValue);
        public bool TryGetIndex(object target, object scriptKey, out object value) => TryGetIndexedValue(target, scriptKey, out value);
        public bool TrySetIndex(object target, object scriptKey, object scriptValue) => TrySetIndexedValue(target, scriptKey, scriptValue);
        public object Invoke(object target, string methodName, List<object> args) => InvokeMethod(target, methodName, args);

        // ------------------------
        // Coercion / Converters
        // ------------------------

        private static object CoerceTo(object value, Type targetType)
        {
            value = ScriptNullToClr(value);

            // Handle Nullable<T>
            var underlyingNullable = Nullable.GetUnderlyingType(targetType);
            if (underlyingNullable != null)
                targetType = underlyingNullable;

            if (value == null)
            {
                // null für ValueTypes nicht erlaubt -> Default
                return targetType.IsValueType ? Activator.CreateInstance(targetType)! : null!;
            }

            var srcType = value.GetType();
            if (targetType.IsAssignableFrom(srcType))
                return value;

            // --- ScriptStack ArrayList -> CLR types ---
            if (value is ArrayList scriptArr)
            {
                // CLR Array (e.g. int[])
                if (targetType.IsArray)
                {
                    var elemType = targetType.GetElementType()!;
                    return ConvertScriptArrayListToClrArray(scriptArr, elemType);
                }

                // Generic collections (List<T>, ICollection<T>, IEnumerable<T>, etc.)
                if (TryConvertScriptArrayListToGenericCollection(scriptArr, targetType, out var coll))
                    return coll;

                // Generic dictionaries (Dictionary<TKey,TValue>, IDictionary<TKey,TValue>)
                if (TryConvertScriptArrayListToGenericDictionary(scriptArr, targetType, out var dict))
                    return dict;
            }

            // Enum conversions
            if (targetType.IsEnum)
                return Enum.Parse(targetType, value.ToString()!, ignoreCase: true);

            // numeric / string conversions etc.
            if (value is IConvertible)
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);

            // last resort: keep as-is (Reflection may still accept via implicit operators)
            return value;
        }

        private static object ConvertScriptArrayListToClrArray(ArrayList scriptArr, Type elementType)
        {
            // We only support list-like arrays here: integer keys, 0..n-1 (no gaps).
            // Anything associative should be mapped to dictionaries instead.
            if (scriptArr.Count == 0)
                return Array.CreateInstance(elementType, 0);

            // ensure all keys are ints
            var keys = new List<int>(scriptArr.Count);
            foreach (var k in scriptArr.Keys)
            {
                if (k is int i)
                    keys.Add(i);
                else
                    throw new ExecutionException($"Associatives Array kann nicht zu '{elementType.Name}[]' konvertiert werden (Key-Typ: {k?.GetType().Name ?? "null"}).");
            }

            keys.Sort();
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] != i)
                    throw new ExecutionException($"Array kann nicht zu '{elementType.Name}[]' konvertiert werden: Keys müssen 0..n-1 ohne Lücken sein.");
            }

            var arr = Array.CreateInstance(elementType, keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                var v = scriptArr[i]; // uses our indexer: returns NullReference.Instance if missing
                var coerced = CoerceTo(v, elementType);
                arr.SetValue(coerced, i);
            }
            return arr;
        }

        private static Type? GetGenericInterface(Type type, Type genericDefinition)
        {
            if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition)
                return type;

            return type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericDefinition);
        }

        private static bool TryConvertScriptArrayListToGenericCollection(ArrayList scriptArr, Type targetType, out object? collection)
        {
            collection = null;

            var iface = GetGenericInterface(targetType, typeof(ICollection<>))
                     ?? GetGenericInterface(targetType, typeof(IList<>))
                     ?? GetGenericInterface(targetType, typeof(IEnumerable<>))
                     ?? GetGenericInterface(targetType, typeof(IReadOnlyCollection<>))
                     ?? GetGenericInterface(targetType, typeof(IReadOnlyList<>));

            if (iface == null)
                return false;

            var elemType = iface.GetGenericArguments()[0];

            // Choose concrete type
            Type concrete = targetType;
            if (targetType.IsInterface || targetType.IsAbstract)
                concrete = typeof(List<>).MakeGenericType(elemType);

            object instance;
            try
            {
                instance = Activator.CreateInstance(concrete)!;
            }
            catch
            {
                // If the target type is non-instantiable, fallback to List<T>
                instance = Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType))!;
                concrete = instance.GetType();
            }

            // Find Add(T)
            var add = concrete.GetMethod("Add", new[] { elemType })
                   ?? concrete.GetMethods().FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 1);

            if (add == null)
                return false;

            // Add items in key order (int keys). If there are non-int keys, refuse.
            var intKeys = new List<int>();
            foreach (var k in scriptArr.Keys)
            {
                if (k is int i) intKeys.Add(i);
                else return false;
            }
            intKeys.Sort();

            foreach (var k in intKeys)
            {
                var v = scriptArr[k];
                var coerced = CoerceTo(v, elemType);
                add.Invoke(instance, new[] { coerced });
            }

            collection = instance;
            return true;
        }

        private static bool TryConvertScriptArrayListToGenericDictionary(ArrayList scriptArr, Type targetType, out object? dict)
        {
            dict = null;

            var iface = GetGenericInterface(targetType, typeof(IDictionary<,>));
            if (iface == null)
                return false;

            var ga = iface.GetGenericArguments();
            var keyType = ga[0];
            var valType = ga[1];

            Type concrete = targetType;
            if (targetType.IsInterface || targetType.IsAbstract)
                concrete = typeof(Dictionary<,>).MakeGenericType(keyType, valType);

            object instance;
            try
            {
                instance = Activator.CreateInstance(concrete)!;
            }
            catch
            {
                instance = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valType))!;
                concrete = instance.GetType();
            }

            var add = concrete.GetMethod("Add", new[] { keyType, valType })
                   ?? concrete.GetMethods().FirstOrDefault(m => m.Name == "Add" && m.GetParameters().Length == 2);

            if (add == null)
                return false;

            foreach (var k in scriptArr.Keys)
            {
                var v = scriptArr[k];
                var ck = CoerceTo(k, keyType);
                var cv = CoerceTo(v, valType);
                add.Invoke(instance, new[] { ck, cv });
            }

            dict = instance;
            return true;
        }

        private static bool TryCoerceTo(object value, Type targetType, out object? coerced)
        {
            try
            {
                coerced = CoerceTo(value, targetType);
                return true;
            }
            catch
            {
                coerced = null;
                return false;
            }
        }

        // ------------------------
        // Member get/set
        // ------------------------

        private object GetMemberValue(object target, string memberName)
        {
            if (target == null || target is NullReference)
                return NullReference.Instance;

            bool isStatic = target is Type;
            var t = isStatic ? (Type)target : target.GetType();
            EnsureTypeAllowed(t);

            var flags = isStatic ? ClrStaticMemberFlags : ClrInstanceMemberFlags;

            var field = t.GetField(memberName, flags);
            if (field != null)
            {
                if (!_policy.IsMemberAllowed(field))
                    throw new ExecutionException($"CLR member access denied: '{t.FullName}.{field.Name}'.");

                var v = field.GetValue(isStatic ? null : target);
                EnsureReturnAllowed(v, $"field '{t.FullName}.{field.Name}'");
                return v ?? NullReference.Instance;
            }

            var prop = t.GetProperty(memberName, flags);
            if (prop != null && prop.CanRead)
            {
                if (!_policy.IsMemberAllowed(prop))
                    throw new ExecutionException($"CLR member access denied: '{t.FullName}.{prop.Name}'.");

                var v = prop.GetValue(isStatic ? null : target);
                EnsureReturnAllowed(v, $"property '{t.FullName}.{prop.Name}'");
                return v ?? NullReference.Instance;
            }

            return NullReference.Instance;
        }

        private void SetMemberValue(object target, string memberName, object scriptValue)
        {
            if (target == null || target is NullReference)
                throw new ExecutionException($"Null reference bei Zuweisung auf Member '{memberName}'.");

            var t = target.GetType();
            EnsureTypeAllowed(t);

            var field = t.GetField(memberName, ClrMemberFlags);
            if (field != null)
            {
                if (!_policy.IsMemberAllowed(field))
                    throw new ExecutionException($"CLR member write denied: '{t.FullName}.{field.Name}'.");

                var coerced = CoerceTo(scriptValue, field.FieldType);
                field.SetValue(target, coerced);
                return;
            }

            var prop = t.GetProperty(memberName, ClrMemberFlags);
            if (prop != null && prop.CanWrite)
            {
                if (!_policy.IsMemberAllowed(prop))
                    throw new ExecutionException($"CLR member write denied: '{t.FullName}.{prop.Name}'.");

                var coerced = CoerceTo(scriptValue, prop.PropertyType);
                prop.SetValue(target, coerced);
                return;
            }

            throw new ExecutionException($"Member '{memberName}' nicht gefunden oder nicht schreibbar auf Typ '{t.FullName}'.");
        }

        // ------------------------
        // Method invocation
        // ------------------------

        private object InvokeMethod(object target, string methodName, List<object> args)
        {
            if (target == null || target is NullReference)
                return NullReference.Instance;

            bool isStatic = target is Type;
            var t = isStatic ? (Type)target : target.GetType();
            EnsureTypeAllowed(t);

            var flags = isStatic ? ClrStaticMemberFlags : ClrInstanceMemberFlags;
            var methods = t.GetMethods(flags);

            MethodInfo? best = null;
            object[]? bestArgs = null;
            int bestScore = int.MaxValue;

            bool anyNameMatch = false;
            bool anyAllowedCandidate = false;

            foreach (var m in methods)
            {
                if (!string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase))
                    continue;

                anyNameMatch = true;

                if (!_policy.IsCallAllowed(m))
                    continue;

                anyAllowedCandidate = true;

                var ps = m.GetParameters();
                if (ps.Length != args.Count)
                    continue;

                int score = 0;
                var coercedArgs = new object[ps.Length];
                bool ok = true;

                for (int i = 0; i < ps.Length; i++)
                {
                    var pType = ps[i].ParameterType;
                    var a = ScriptNullToClr(args[i]);

                    if (a == null)
                    {
                        // null passt auf RefTypes/Nullable
                        if (pType.IsValueType && Nullable.GetUnderlyingType(pType) == null)
                        {
                            ok = false;
                            break;
                        }

                        coercedArgs[i] = null!;
                        score += 1;
                        continue;
                    }

                    var aType = a.GetType();

                    if (pType.IsAssignableFrom(aType))
                    {
                        coercedArgs[i] = a;
                        score += (pType == aType) ? 0 : 1;
                        continue;
                    }

                    if (TryCoerceTo(a, pType, out var c) && c != null)
                    {
                        coercedArgs[i] = c;
                        score += 2;
                        continue;
                    }

                    ok = false;
                    break;
                }

                if (!ok)
                    continue;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = m;
                    bestArgs = coercedArgs;
                }
            }

            if (best == null)
            {
                if (anyNameMatch && !anyAllowedCandidate)
                    throw new ExecutionException($"CLR method call denied: '{t.FullName}.{methodName}(...)'.");

                throw new ExecutionException($"Methode '{methodName}' mit {args.Count} Parametern nicht gefunden auf Typ '{t.FullName}'.");
            }

            try
            {
                
                object? result = best.Invoke(isStatic ? null : target, bestArgs);
                if (best.ReturnType == typeof(void))
                    return NullReference.Instance;

                EnsureReturnAllowed(result, $"method '{t.FullName}.{best.Name}'");
                return ClrNullToScript(result);
            }
            catch (TargetInvocationException tie)
            {
                // unwrap inner exception for better diagnostics
                throw new ExecutionException($"Fehler in CLR-Methode '{t.FullName}.{best.Name}': {tie.InnerException?.Message ?? tie.Message}");
            }
            catch (Exception ex)
            {
                throw new ExecutionException($"Fehler beim Aufruf von CLR-Methode '{t.FullName}.{best.Name}': {ex.Message}");
            }
        }

        // ------------------------
        // Index get/set
        // ------------------------

        private bool TryGetIndexedValue(object target, object scriptKey, out object value)
        {
            value = NullReference.Instance;

            if (target == null || target is NullReference)
                return true;

            var t = target.GetType();
            EnsureTypeAllowed(t);

            // IList / arrays
            if (target is IList list)
            {
                var idxObj = ScriptNullToClr(scriptKey);
                if (idxObj is int idx)
                {
                    var v = list[idx];
                    EnsureReturnAllowed(v, $"index get '{t.FullName}[{idx}]'");
                    value = ClrNullToScript(v);
                    return true;
                }

                return false;
            }

            // IDictionary
            if (target is IDictionary dict)
            {
                object? key = ScriptNullToClr(scriptKey);

                // Try to coerce key to generic TKey if possible (avoids InvalidCastException)
                var keyType = target.GetType().GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    ?.GetGenericArguments()[0];

                if (keyType != null && key != null && TryCoerceTo(key, keyType, out var ck) && ck != null)
                    key = ck;

                try
                {
                    var v = dict[key!];
                    EnsureReturnAllowed(v, $"dictionary get '{t.FullName}[key]'");
                    value = ClrNullToScript(v);
                    return true;
                }
                catch
                {
                    // fall through to indexer reflection
                }
            }

            // indexer property (Item[...])
            foreach (var p in t.GetProperties(ClrMemberFlags))
            {
                var ip = p.GetIndexParameters();
                if (ip.Length != 1 || !p.CanRead)
                    continue;

                if (!_policy.IsMemberAllowed(p))
                    continue;

                if (!TryCoerceTo(scriptKey, ip[0].ParameterType, out var ck))
                    continue;

                try
                {
                    var v = p.GetValue(target, new object[] { ck });
                    EnsureReturnAllowed(v, $"indexer get '{t.FullName}.{p.Name}[...]' ");
                    value = ClrNullToScript(v);
                    return true;
                }
                catch
                {
                    // try next
                }
            }

            return false;
        }

        private bool TrySetIndexedValue(object target, object scriptKey, object scriptValue)
        {
            if (target == null || target is NullReference)
                return false;

            var t = target.GetType();
            EnsureTypeAllowed(t);

            // IList / arrays
            if (target is IList list)
            {
                var idxObj = ScriptNullToClr(scriptKey);
                if (idxObj is not int idx)
                    return false;

                // try to coerce value to element type if we can infer it
                Type? elemType = null;
                var tt = target.GetType();
                if (tt.IsArray)
                    elemType = tt.GetElementType();
                else
                {
                    var gi = tt.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
                    if (gi != null) elemType = gi.GetGenericArguments()[0];
                }

                object v = scriptValue;
                if (elemType != null && TryCoerceTo(scriptValue, elemType, out var cv) && cv != null)
                    v = cv;

                list[idx] = ScriptNullToClr(v);
                return true;
            }

            // IDictionary
            if (target is IDictionary dict)
            {
                object? key = ScriptNullToClr(scriptKey);
                object? val = ScriptNullToClr(scriptValue);

                var iface = target.GetType().GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
                if (iface != null)
                {
                    var ga = iface.GetGenericArguments();
                    var keyType = ga[0];
                    var valType = ga[1];
                    if (key != null && TryCoerceTo(key, keyType, out var ck) && ck != null) key = ck;
                    if (val != null && TryCoerceTo(val, valType, out var cv) && cv != null) val = cv;
                }

                try
                {
                    dict[key!] = val;
                    return true;
                }
                catch
                {
                    // fall through to indexer reflection
                }
            }

            // indexer property (Item[...])
            foreach (var p in t.GetProperties(ClrMemberFlags))
            {
                var ip = p.GetIndexParameters();
                if (ip.Length != 1 || !p.CanWrite)
                    continue;

                if (!_policy.IsMemberAllowed(p))
                    continue;

                if (!TryCoerceTo(scriptKey, ip[0].ParameterType, out var ck))
                    continue;

                object v = scriptValue;
                if (TryCoerceTo(scriptValue, p.PropertyType, out var cv) && cv != null)
                    v = cv;

                try
                {
                    p.SetValue(target, ScriptNullToClr(v), new object[] { ck });
                    return true;
                }
                catch
                {
                    // try next
                }
            }

            return false;
        }
    
    }
}
