using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

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
            string culture = Thread.CurrentThread.CurrentCulture.NumberFormat.PercentDecimalSeparator;
            string decimalSeparator = ",";
            //if (culture.ToString().Trim() == ",") decimalSeparator = ";";
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("{ ");        
            bool bFirst = true;
            foreach (object objectKey in Keys)
            {
                if (!bFirst) stringBuilder.Append(decimalSeparator + " ");
                bFirst = false;
                OutputValue(stringBuilder, objectKey);
                stringBuilder.Append(": ");
                OutputValue(stringBuilder, this[objectKey]);
            }
            stringBuilder.Append(" }");
            return stringBuilder.ToString();
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
