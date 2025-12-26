using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace ScriptStack.Runtime
{
    /// <summary>
    /// Central binding layer between ScriptStack runtime values and CLR objects.
    ///
    /// The interpreter should not contain dozens of type checks for member access,
    /// indexing and foreach iteration. Instead, it delegates to this binder.
    ///
    /// Supported (out of the box):
    /// - ScriptArray / ScriptObject / string
    /// - CLR arrays + List&lt;T&gt; + any IList
    /// - CLR dictionaries + any IDictionary
    /// - CLR objects (public instance fields + properties)
    /// - CLR indexers (Item[key]) via pointer access
    ///
    /// Notes:
    /// - Missing keys/members generally yield NullReference.Instance (script-friendly).
    /// - Writes to non-existing CLR members throw an ExecutionException.
    /// </summary>
    public sealed class RuntimeBinder
    {
        public bool CaseInsensitiveMembers { get; set; } = true;

        /// <summary>
        /// Compatibility mode: if no field/property exists, allow invoking a public
        /// parameterless method with the same name when reading a member.
        ///
        /// Example (legacy): person.GetName  -> calls person.GetName().
        /// </summary>
        public bool InvokeZeroArgMethodsOnMissingMember { get; set; } = false;

        private readonly Dictionary<Type, TypeCache> _typeCache = new();

        public object GetMember(object? target, object? member)
        {
            if (target == null || target == NullReference.Instance)
                throw new ExecutionException("Member-Zugriff auf null ist nicht erlaubt.");

            // Arrays: numeric indices + read-only member 'size'
            if (target is ScriptArray sa)
                return GetMember_ScriptArray(sa, member);

            // Objects: key-value members
            if (target is ScriptObject so)
                return so.Get(member);

            // Strings: numeric indices + length
            if (target is string s)
                return GetMember_String(s, member);

            // IDictionary: allow dot-member as key lookup (dict.foo)
            if (target is IDictionary dict)
                return Get_Dictionary(dict, member);

            // IList: numeric indices + 'size'
            if (target is IList list)
                return GetMember_IList(list, member);

            // CLR object: public instance property/field
            if (member is string name)
                return GetMember_Clr(target, name);

            return NullReference.Instance;
        }

        public void SetMember(object? target, object? member, object? value)
        {
            if (target == null || target == NullReference.Instance)
                throw new ExecutionException("Member-Zuweisung auf null ist nicht erlaubt.");

            if (target is ScriptArray sa)
            {
                SetMember_ScriptArray(sa, member, value);
                return;
            }

            if (target is ScriptObject so)
            {
                so.Set(member, value);
                return;
            }

            if (target is string)
                throw new ExecutionException("Strings sind read-only und können nicht verändert werden.");

            if (target is IDictionary dict)
            {
                Set_Dictionary(dict, member, value);
                return;
            }

            if (target is IList list)
            {
                SetMember_IList(list, member, value);
                return;
            }

            if (member is string name)
            {
                SetMember_Clr(target, name, value);
                return;
            }

            throw new ExecutionException("Ungültiger Member-Schlüssel.");
        }

        public object GetIndex(object? target, object? index)
        {
            if (target == null || target == NullReference.Instance)
                throw new ExecutionException("Index-Zugriff auf null ist nicht erlaubt.");

            if (target is ScriptArray sa)
                return GetIndex_ScriptArray(sa, index);

            if (target is ScriptObject so)
                return so.Get(index);

            if (target is string s)
                return GetIndex_String(s, index);

            if (target is IDictionary dict)
                return Get_Dictionary(dict, index);

            if (target is IList list)
                return GetIndex_IList(list, index);

            // CLR object indexer: obj[key]
            return GetIndex_ClrIndexer(target, index);
        }

        public void SetIndex(object? target, object? index, object? value)
        {
            if (target == null || target == NullReference.Instance)
                throw new ExecutionException("Index-Zuweisung auf null ist nicht erlaubt.");

            if (target is ScriptArray sa)
            {
                SetIndex_ScriptArray(sa, index, value);
                return;
            }

            if (target is ScriptObject so)
            {
                so.Set(index, value);
                return;
            }

            if (target is string)
                throw new ExecutionException("Strings sind read-only und können nicht verändert werden.");

            if (target is IDictionary dict)
            {
                Set_Dictionary(dict, index, value);
                return;
            }

            if (target is IList list)
            {
                SetIndex_IList(list, index, value);
                return;
            }

            // CLR object indexer: obj[key] = value
            SetIndex_ClrIndexer(target, index, value);
        }

        /// <summary>
        /// Foreach helper for ScriptStack's current foreach implementation.
        /// Returns the next iterator key, or NullReference.Instance if the iteration ends.
        /// </summary>
        public object NextIteratorKey(object? enumerable, object? currentKey)
        {
            if (enumerable == null || enumerable == NullReference.Instance)
                return NullReference.Instance;

            if (enumerable is ScriptArray sa)
                return NextKey_Array(sa, currentKey);

            if (enumerable is ScriptObject so)
                return NextKey_Object(so, currentKey);

            if (enumerable is string s)
                return NextKey_String(s, currentKey);

            if (enumerable is IDictionary dict)
                return NextKey_Dictionary(dict, currentKey);

            if (enumerable is IList list)
                return NextKey_IList(list, currentKey);

            // IEnumerable fallback: materialize once into a ScriptArray
            if (enumerable is IEnumerable en)
            {
                var tmp = new ScriptArray();
                foreach (var it in en)
                    tmp.Add(ScriptValue.NormalizeValue(it));
                return NextKey_Array(tmp, currentKey);
            }

            return NullReference.Instance;
        }

        #region Script native

        private static object GetMember_ScriptArray(ScriptArray arr, object? member)
        {
            if (member is string ms)
            {
                if (ms == "size")
                    return arr.Count;
                if (ms == "toString")
                    return arr.ToString();
                throw new ExecutionException("Ein Array ist nur numerisch indexierbar (oder der Member 'size').");
            }

            if (member is int idx)
                return arr.GetAt(idx);

            throw new ExecutionException("Ein Array ist nur numerisch indexierbar.");
        }

        private static void SetMember_ScriptArray(ScriptArray arr, object? member, object? value)
        {
            if (member is string ms && ms == "size")
                throw new ExecutionException("Der Member 'size' eines Arrays ist read-only.");

            if (member is not int idx)
                throw new ExecutionException("Ein Array ist nur numerisch indexierbar.");

            arr.SetAt(idx, value);
        }

        private static object GetMember_IList(IList list, object? member)
        {
            if (member is string ms)
            {
                if (ms == "size")
                    return list.Count;
                throw new ExecutionException("Eine Liste ist nur numerisch indexierbar (oder der Member 'size').");
            }

            if (member is int idx)
                return GetIndex_IList(list, idx);

            throw new ExecutionException("Eine Liste ist nur numerisch indexierbar.");
        }

        private static void SetMember_IList(IList list, object? member, object? value)
        {
            if (member is string ms && ms == "size")
                throw new ExecutionException("Der Member 'size' einer Liste ist read-only.");

            if (member is not int idx)
                throw new ExecutionException("Eine Liste ist nur numerisch indexierbar.");

            SetIndex_IList(list, idx, value);
        }

        private static object GetMember_String(string str, object? member)
        {
            if (member is string ms && ms == "length")
                return str.Length;
            if (member is int idx)
                return GetIndex_String(str, idx);
            throw new ExecutionException("Ein String ist nur numerisch indexierbar (oder der Member 'length').");
        }

        private static object GetIndex_ScriptArray(ScriptArray arr, object? index)
        {
            if (index is not int idx)
                throw new ExecutionException("Ein Array ist nur numerisch indexierbar.");
            return arr.GetAt(idx);
        }

        private static void SetIndex_ScriptArray(ScriptArray arr, object? index, object? value)
        {
            if (index is not int idx)
                throw new ExecutionException("Ein Array ist nur numerisch indexierbar.");
            arr.SetAt(idx, value);
        }

        private static object GetIndex_IList(IList list, object? index)
        {
            if (index is not int idx)
                throw new ExecutionException("Eine Liste ist nur numerisch indexierbar.");
            if (idx < 0)
                throw new ExecutionException("Array index must be >= 0.");
            if (idx >= list.Count)
                return NullReference.Instance;
            return ScriptValue.NormalizeValue(list[idx]);
        }

        private static void SetIndex_IList(IList list, object? index, object? value)
        {
            if (index is not int idx)
                throw new ExecutionException("Eine Liste ist nur numerisch indexierbar.");
            if (idx < 0)
                throw new ExecutionException("Array index must be >= 0.");

            object? v = (value == NullReference.Instance) ? null : value;

            if (idx < list.Count)
            {
                try
                {
                    list[idx] = v;
                }
                catch (Exception ex)
                {
                    throw new ExecutionException("Fehler beim Setzen des Listenelements: " + ex.Message);
                }
                return;
            }

            // allow append at Count for growable lists
            if (!list.IsFixedSize && idx == list.Count)
            {
                try
                {
                    list.Add(v);
                }
                catch (Exception ex)
                {
                    throw new ExecutionException("Fehler beim Hinzufügen zum Array/List: " + ex.Message);
                }
                return;
            }

            throw new ExecutionException("Index außerhalb des gültigen Bereichs.");
        }

        private static object GetIndex_String(string str, object? index)
        {
            if (index is not int idx)
                throw new ExecutionException("Ein String ist nur numerisch indexierbar.");
            if (idx < 0)
                throw new ExecutionException("String index must be >= 0.");
            if (idx >= str.Length)
                return NullReference.Instance;
            return str[idx].ToString();
        }

        private static object Get_Dictionary(IDictionary dict, object? key)
        {
            object k = ScriptValue.NormalizeKey(key);
            return dict.Contains(k) ? ScriptValue.NormalizeValue(dict[k]) : NullReference.Instance;
        }

        private static void Set_Dictionary(IDictionary dict, object? key, object? value)
        {
            object k = ScriptValue.NormalizeKey(key);
            object v = ScriptValue.NormalizeValue(value);
            dict[k] = v;
        }

        #endregion

        #region Iteration

        private static object NextKey_Array(IList list, object? currentKey)
        {
            if (list.Count == 0)
                return NullReference.Instance;

            int nextIndex;
            if (currentKey is int i)
                nextIndex = i + 1;
            else
                nextIndex = 0;

            return nextIndex < list.Count ? nextIndex : NullReference.Instance;
        }

        private static object NextKey_IList(IList list, object? currentKey)
            => NextKey_Array(list, currentKey);

        private static object NextKey_String(string str, object? currentKey)
        {
            if (str.Length == 0)
                return NullReference.Instance;

            int nextIndex;
            if (currentKey is int i)
                nextIndex = i + 1;
            else
                nextIndex = 0;

            return nextIndex < str.Length ? nextIndex : NullReference.Instance;
        }

        private static object NextKey_Object(ScriptObject obj, object? currentKey)
        {
            if (obj.Count == 0)
                return NullReference.Instance;

            bool found = false;
            object? next = null;

            foreach (var k in obj.Keys)
            {
                if (found)
                {
                    next = k;
                    break;
                }

                if (ScriptValue.EqualValues(k, currentKey ?? NullReference.Instance))
                    found = true;
            }

            if (!found)
            {
                var keys = obj.Keys.GetEnumerator();
                keys.MoveNext();
                next = keys.Current;
            }

            return ScriptValue.NormalizeKey(next);
        }

        private static object NextKey_Dictionary(IDictionary dict, object? currentKey)
        {
            if (dict.Count == 0)
                return NullReference.Instance;

            bool found = false;
            object? next = null;

            foreach (var k in dict.Keys)
            {
                if (found)
                {
                    next = k;
                    break;
                }

                if (ScriptValue.EqualValues(ScriptValue.NormalizeKey(k), ScriptValue.NormalizeKey(currentKey)))
                    found = true;
            }

            if (!found)
            {
                var keys = dict.Keys.GetEnumerator();
                keys.MoveNext();
                next = keys.Current;
            }

            return ScriptValue.NormalizeKey(next);
        }

        #endregion

        #region CLR object access (properties/fields/indexers)

        private object GetMember_Clr(object target, string name)
        {
            var cache = GetCache(target.GetType());

            if (cache.Properties.TryGetValue(name, out var p))
                return ScriptValue.NormalizeValue(p.GetValue(target));

            if (cache.Fields.TryGetValue(name, out var f))
                return ScriptValue.NormalizeValue(f.GetValue(target));

            if (InvokeZeroArgMethodsOnMissingMember && cache.ZeroArgMethods.TryGetValue(name, out var m))
                return ScriptValue.NormalizeValue(m.Invoke(target, Array.Empty<object>()));

            return NullReference.Instance;
        }

        private void SetMember_Clr(object target, string name, object? value)
        {
            var cache = GetCache(target.GetType());
            object? v = (value == NullReference.Instance) ? null : value;

            if (cache.Properties.TryGetValue(name, out var p))
            {
                if (!p.CanWrite)
                    throw new ExecutionException($"Property '{name}' ist read-only.");
                var cv = CoerceTo(v, p.PropertyType);
                p.SetValue(target, cv);
                return;
            }

            if (cache.Fields.TryGetValue(name, out var f))
            {
                if (f.IsInitOnly)
                    throw new ExecutionException($"Field '{name}' ist read-only.");
                var cv = CoerceTo(v, f.FieldType);
                f.SetValue(target, cv);
                return;
            }

            throw new ExecutionException($"Member '{name}' existiert nicht auf Typ '{target.GetType().Name}'.");
        }

        private object GetIndex_ClrIndexer(object target, object? key)
        {
            var cache = GetCache(target.GetType());

            foreach (var ip in cache.Indexers)
            {
                var idxParams = ip.GetIndexParameters();
                if (idxParams.Length != 1)
                    continue;

                try
                {
                    var ck = CoerceTo(key == NullReference.Instance ? null : key, idxParams[0].ParameterType);
                    return ScriptValue.NormalizeValue(ip.GetValue(target, new[] { ck }));
                }
                catch
                {
                    // try next indexer
                }
            }

            throw new ExecutionException($"Typ '{target.GetType().Name}' ist nicht indexierbar.");
        }

        private void SetIndex_ClrIndexer(object target, object? key, object? value)
        {
            var cache = GetCache(target.GetType());
            object? v = (value == NullReference.Instance) ? null : value;

            foreach (var ip in cache.Indexers)
            {
                if (!ip.CanWrite)
                    continue;

                var idxParams = ip.GetIndexParameters();
                if (idxParams.Length != 1)
                    continue;

                try
                {
                    var ck = CoerceTo(key == NullReference.Instance ? null : key, idxParams[0].ParameterType);
                    var cv = CoerceTo(v, ip.PropertyType);
                    ip.SetValue(target, cv, new[] { ck });
                    return;
                }
                catch
                {
                    // try next indexer
                }
            }

            throw new ExecutionException($"Typ '{target.GetType().Name}' ist nicht indexierbar.");
        }

        private TypeCache GetCache(Type t)
        {
            if (_typeCache.TryGetValue(t, out var cache))
                return cache;

            var comparer = CaseInsensitiveMembers ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            cache = new TypeCache(comparer);

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            foreach (var p in t.GetProperties(flags))
            {
                if (p.GetIndexParameters().Length == 0)
                    cache.Properties[p.Name] = p;
                else
                    cache.Indexers.Add(p);
            }

            foreach (var f in t.GetFields(flags))
                cache.Fields[f.Name] = f;

            foreach (var m in t.GetMethods(flags))
            {
                if (m.GetParameters().Length == 0)
                    cache.ZeroArgMethods[m.Name] = m;
            }

            _typeCache[t] = cache;
            return cache;
        }

        private static object? CoerceTo(object? value, Type targetType)
        {
            // null handling
            if (value == null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                    return null;
                return Activator.CreateInstance(targetType);
            }

            var underlying = Nullable.GetUnderlyingType(targetType);
            if (underlying != null)
                targetType = underlying;

            if (targetType.IsInstanceOfType(value))
                return value;

            // ScriptArray -> CLR array/list
            if (value is ScriptArray sa)
            {
                if (targetType.IsArray)
                {
                    var et = targetType.GetElementType()!;
                    var arr = Array.CreateInstance(et, sa.Count);
                    for (int i = 0; i < sa.Count; i++)
                        arr.SetValue(CoerceTo(sa[i] == NullReference.Instance ? null : sa[i], et), i);
                    return arr;
                }

                // List<T>
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var et = targetType.GetGenericArguments()[0];
                    var list = (IList)Activator.CreateInstance(targetType)!;
                    foreach (var it in sa)
                        list.Add(CoerceTo(it == NullReference.Instance ? null : it, et));
                    return list;
                }
            }

            // ScriptObject -> Dictionary<K,V>
            if (value is ScriptObject so)
            {
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var args = targetType.GetGenericArguments();
                    var kt = args[0];
                    var vt = args[1];
                    var dict = (IDictionary)Activator.CreateInstance(targetType)!;
                    foreach (var kv in so)
                    {
                        var ck = CoerceTo(kv.Key == NullReference.Instance ? null : kv.Key, kt);
                        var cv = CoerceTo(kv.Value == NullReference.Instance ? null : kv.Value, vt);
                        dict[ck!] = cv;
                    }
                    return dict;
                }
            }

            // Enum support
            if (targetType.IsEnum)
            {
                if (value is string es)
                    return Enum.Parse(targetType, es, ignoreCase: true);

                var num = Convert.ChangeType(value, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture);
                return Enum.ToObject(targetType, num!);
            }

            // Basic numeric/string conversions
            if (value is IConvertible)
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);

            return value;
        }

        private sealed class TypeCache
        {
            public readonly Dictionary<string, PropertyInfo> Properties;
            public readonly Dictionary<string, FieldInfo> Fields;
            public readonly Dictionary<string, MethodInfo> ZeroArgMethods;
            public readonly List<PropertyInfo> Indexers;

            public TypeCache(IEqualityComparer<string> comparer)
            {
                Properties = new Dictionary<string, PropertyInfo>(comparer);
                Fields = new Dictionary<string, FieldInfo>(comparer);
                ZeroArgMethods = new Dictionary<string, MethodInfo>(comparer);
                Indexers = new List<PropertyInfo>();
            }
        }

        #endregion
    }
}
