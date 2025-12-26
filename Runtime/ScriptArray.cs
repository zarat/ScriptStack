using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptStack.Runtime
{
    /// <summary>
    /// ScriptStack array value.
    /// 
    /// Backed by a List&lt;object&gt; (as requested). We also keep a few legacy
    /// behaviors (NullReference normalization and a read-only 'size' member).
    /// </summary>
    public class ScriptArray : List<object>
    {
        public ScriptArray() : base() { }

        public ScriptArray(IEnumerable<object> items) : base()
        {
            foreach (var it in items)
                Add(ScriptValue.NormalizeValue(it));
        }

        public object GetAt(int index)
        {
            if (index < 0)
                throw new ExecutionException("Array index must be >= 0.");

            if (index >= Count)
                return NullReference.Instance;

            return this[index];
        }

        public void SetAt(int index, object? value)
        {
            if (index < 0)
                throw new ExecutionException("Array index must be >= 0.");

            var v = ScriptValue.NormalizeValue(value);

            // Grow with NULL slots (JS-like sparse behavior, but backed by List)
            if (index >= Count)
            {
                while (Count < index)
                    base.Add(NullReference.Instance);

                base.Add(v);
                return;
            }

            this[index] = v;
        }

        public void AddScript(object? value)
        {
            if (value is ScriptArray arr)
            {
                foreach (var it in arr)
                    base.Add(ScriptValue.NormalizeValue(it));
                return;
            }

            if (value is ScriptObject obj)
            {
                foreach (var it in obj.Values)
                    base.Add(ScriptValue.NormalizeValue(it));
                return;
            }

            base.Add(ScriptValue.NormalizeValue(value));
        }

        public void SubtractScript(object? value)
        {
            if (value is ScriptArray arr)
            {
                foreach (var it in arr)
                    SubtractScript(it);
                return;
            }

            if (value is ScriptObject obj)
            {
                foreach (var it in obj.Values)
                    SubtractScript(it);
                return;
            }

            var v = ScriptValue.NormalizeValue(value);

            // Remove all matching values (legacy behavior)
            for (int i = Count - 1; i >= 0; i--)
            {
                if (ScriptValue.EqualValues(this[i], v))
                    RemoveAt(i);
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("[");

            for (int i = 0; i < Count; i++)
            {
                if (i > 0) sb.Append(", ");
                ScriptValue.AppendScriptString(sb, this[i]);
            }

            sb.Append("]");
            return sb.ToString();
        }
    }
}
