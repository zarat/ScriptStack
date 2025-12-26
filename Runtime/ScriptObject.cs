using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptStack.Runtime
{
    /// <summary>
    /// ScriptStack object value.
    /// 
    /// Backed by a Dictionary&lt;object, object&gt; (as requested).
    /// Missing keys return NullReference.Instance.
    /// </summary>
    public class ScriptObject : Dictionary<object, object>
    {
        public ScriptObject() : base() { }

        public ScriptObject(IDictionary<object, object> other) : base()
        {
            foreach (var kv in other)
                this[ScriptValue.NormalizeKey(kv.Key)] = ScriptValue.NormalizeValue(kv.Value);
        }

        public object Get(object? key)
        {
            var k = ScriptValue.NormalizeKey(key);
            return TryGetValue(k, out var v) ? v : NullReference.Instance;
        }

        public void Set(object? key, object? value)
        {
            var k = ScriptValue.NormalizeKey(key);
            this[k] = ScriptValue.NormalizeValue(value);
        }

        public void MergeFrom(ScriptObject other)
        {
            foreach (var kv in other)
                this[ScriptValue.NormalizeKey(kv.Key)] = ScriptValue.NormalizeValue(kv.Value);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("{");

            bool first = true;
            foreach (var kv in this)
            {
                if (!first) sb.Append(", ");
                first = false;

                // Keys: quote strings, otherwise print as-is.
                if (kv.Key is string)
                    sb.Append(ScriptValue.ToScriptString(kv.Key));
                else
                    sb.Append(kv.Key?.ToString() ?? "null");

                sb.Append(": ");
                ScriptValue.AppendScriptString(sb, kv.Value);
            }

            sb.Append("}");
            return sb.ToString();
        }
    }
}
