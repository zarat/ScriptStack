using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Globalization;

namespace ScriptStack.Runtime
{

    public class ArrayList : Dictionary<object, object>
    {

        #region Private Methods

        private void OutputValue(StringBuilder stringBuilder, object objectValue)
        {

            if (objectValue.GetType() != typeof(ArrayList))
                stringBuilder.Append("\"" + objectValue + "\"");

            else
                stringBuilder.Append(objectValue);

            return;

        }

        private bool EqualValues(object objectValue1, object objectValue2)
        {

            Type type1 = objectValue1.GetType();

            Type type2 = objectValue2.GetType();

            if (type1 == typeof(int) && type2 == typeof(int))
                return (int)objectValue1 == (int)objectValue2;

            else if (type1 == typeof(int) && type2 == typeof(float))
                return (int)objectValue1 == (float)objectValue2;

            else if (type1 == typeof(float) && type2 == typeof(int))
                return (float)objectValue1 == (int)objectValue2;

            else if (type1 == typeof(float) && type2 == typeof(float))
                return (float)objectValue1 == (float)objectValue2;

            else if (type1 == typeof(string) || type2 == typeof(string))
                return objectValue1.ToString() == objectValue2.ToString();

            else return objectValue1 == objectValue2;

        }

        private void AddValue(object objectValue)
        {
            int iIndex = 0;
            while (ContainsKey(iIndex)) ++iIndex;
            this[iIndex] = objectValue;
        }

        private void SubtractValue(object objectValue)
        {
            List<object> listValues = new List<object>();
            foreach (object objectOldValue in Values)
            {
                if (!EqualValues(objectOldValue, objectValue)) listValues.Add(objectOldValue);
            }
            Clear();
            int iIndex = 0;
            foreach (object objectOldValue in listValues)
            {
                this[iIndex++] = objectOldValue;
            }
        }

        private void AddArray(ArrayList assocativeArray)
        {
            int iIndex = 0;
            while (ContainsKey(iIndex)) ++iIndex;
            foreach (object objectValue in assocativeArray.Values)
            {
                this[iIndex++] = objectValue;
            }
        }

        private void SubtractArray(ArrayList associativeArray)
        {
            foreach (object objectValue in associativeArray.Values)
                SubtractValue(objectValue);
        }

        #endregion

        #region Public Methods

        public void Add(object objectValue)
        {

            if (objectValue.GetType() == typeof(ArrayList))
                AddArray((ArrayList)objectValue);

            else
                AddValue(objectValue);

        }

        public void Subtract(object objectValue)
        {

            if (objectValue.GetType() == typeof(ArrayList))
                SubtractArray((ArrayList)objectValue);

            else SubtractValue(objectValue);

        }

        public override string ToString()
        {
            // We intentionally output JSON here, because ScriptStack's "string(x)" uses Convert.ToString(x)
            // which calls ToString(). Arrays vs. objects are distinguished by their key-shape:
            // - purely non-negative int keys => JSON array (supports sparse arrays)
            // - otherwise => JSON object
            var sb = new StringBuilder();
            var stack = new HashSet<object>(ReferenceEqualityComparer.Instance);
            WriteJsonValue(sb, this, stack);
            return sb.ToString();
        }

        public object[] ToArray()
        {
            var list = new List<object>();
            foreach (var el in this.Values)
                list.Add(el);
        
            return list.ToArray();
        }

        private static void WriteJsonValue(StringBuilder sb, object value, HashSet<object> stack)
        {
            if (value == null || value is NullReference)
            {
                sb.Append("null");
                return;
            }

            if (value is ArrayList al)
            {
                WriteJsonArrayList(sb, al, stack);
                return;
            }

            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Boolean:
                    sb.Append(((bool)value) ? "true" : "false");
                    return;
                case TypeCode.String:
                    WriteJsonString(sb, (string)value);
                    return;
                case TypeCode.Char:
                    WriteJsonString(sb, value.ToString());
                    return;
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.SByte:
                case TypeCode.Byte:
                    // JSON numbers must be culture-invariant.
                    sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
                case TypeCode.DateTime:
                    // No dedicated JSON date type: emit as ISO-8601 string.
                    WriteJsonString(sb, ((DateTime)value).ToString("O", CultureInfo.InvariantCulture));
                    return;
                default:
                    // Fallback: emit as string.
                    WriteJsonString(sb, value.ToString());
                    return;
            }
        }

        private static void WriteJsonArrayList(StringBuilder sb, ArrayList al, HashSet<object> stack)
        {
            if (!stack.Add(al))
            {
                // Cyclic structure: represent as null to avoid infinite recursion.
                sb.Append("null");
                return;
            }

            // Detect "array" shape: all keys are non-negative int.
            bool allIntKeys = true;
            int maxIndex = -1;
            foreach (var k in al.Keys)
            {
                if (k is int i && i >= 0)
                {
                    if (i > maxIndex) maxIndex = i;
                    continue;
                }

                allIntKeys = false;
                break;
            }

            if (allIntKeys)
            {
                sb.Append('[');
                for (int i = 0; i <= maxIndex; i++)
                {
                    if (i > 0) sb.Append(',');
                    if (al.TryGetValue(i, out var v))
                        WriteJsonValue(sb, v, stack);
                    else
                        sb.Append("null");
                }
                sb.Append(']');
            }
            else
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in al)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteJsonString(sb, kv.Key?.ToString() ?? "null");
                    sb.Append(':');
                    WriteJsonValue(sb, kv.Value, stack);
                }
                sb.Append('}');
            }

            stack.Remove(al);
        }

        private static void WriteJsonString(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                foreach (char c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20)
                                sb.Append("\\u" + ((int)c).ToString("x4"));
                            else
                                sb.Append(c);
                            break;
                    }
                }
            }
            sb.Append('"');
        }

        /// <summary>
        /// Reference equality comparer for cycle detection.
        /// </summary>
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        #endregion

        #region Public Properties

        public new object this[object objectKey]
        {

            get
            {

                if (objectKey.GetType() == typeof(string) && ((string)objectKey) == "size")
                    return this.Count;

                if (!ContainsKey(objectKey))
                    return NullReference.Instance;

                return base[objectKey];

            }
            set
            {

                if (objectKey == null)
                    objectKey = NullReference.Instance;

                if (objectKey.GetType() == typeof(string) && ((string)objectKey) == "size")
                    throw new ExecutionException("Der Member 'size' eines Arrays ist eine 'read-only' Eigenschaft.");

                if (value == null)
                    base[objectKey] = NullReference.Instance;

                else
                    base[objectKey] = value;

            }

        }

        #endregion

    }

}

