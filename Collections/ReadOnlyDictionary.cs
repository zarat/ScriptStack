using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace ScriptStack.Collections
{
    [Serializable]
    public class ReadOnlyDictionary<TKey, TValue> 
        : IDictionary<TKey, TValue>
        , ICollection<KeyValuePair<TKey, TValue>>
        , IEnumerable<KeyValuePair<TKey, TValue>>
        , IDictionary
        , ICollection
        , IEnumerable
        , ISerializable
        , IDeserializationCallback
    {
        #region Private Members
        private IDictionary<TKey, TValue> m_dictionaryTyped;
        private IDictionary m_dictionary;

        #endregion

        #region Default Methods
        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_dictionary.GetEnumerator();
        }
        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return m_dictionary.GetEnumerator();
        }

        #endregion

        #region Default Properties
        ICollection IDictionary.Keys
        {
            get
            {
                return m_dictionary.Keys;
            }
        }
        ICollection IDictionary.Values
        {
            get
            {
                return m_dictionary.Values;
            }
        }

        #endregion

        #region Public Methods
        public ReadOnlyDictionary(IDictionary<TKey, TValue> dictionaryToWrap)
        {
            m_dictionaryTyped = dictionaryToWrap;
            m_dictionary = (IDictionary)m_dictionaryTyped;
        }
        public static ReadOnlyDictionary<TKey, TValue> AsReadOnly(IDictionary<TKey, TValue> dictionaryToWrap)
        {
            return new ReadOnlyDictionary<TKey, TValue>(dictionaryToWrap);
        }
        public void Add(TKey key, TValue value)
        {
        }
        public bool ContainsKey(TKey key)
        {
            return m_dictionaryTyped.ContainsKey(key);
        }
        public bool Remove(TKey key)
        {
            return false;
        }
        public bool TryGetValue(TKey key, out TValue value)
        {
            return m_dictionaryTyped.TryGetValue(key, out value);
        }
        public void Add(KeyValuePair<TKey, TValue> item)
        {
        }
        public void Clear()
        {
        }
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return m_dictionaryTyped.Contains(item);
        }
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            m_dictionaryTyped.CopyTo(array, arrayIndex);
        }
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return false;
        }
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return m_dictionaryTyped.GetEnumerator();
        }
        public void Add(object key, object value)
        {
        }
        public bool Contains(object key)
        {
            return m_dictionary.Contains(key);
        }
        public void Remove(object key)
        {
        }
        public object this[object key]
        {
            get
            {
                return m_dictionary[key];
            }
            set
            {
            }
        }
        public void CopyTo(Array array, int index)
        {
        }
        public void OnDeserialization(object sender)
        {
            IDeserializationCallback callback = m_dictionaryTyped as IDeserializationCallback;
            callback.OnDeserialization(sender);
        }
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ISerializable serializable = m_dictionaryTyped as ISerializable;
            serializable.GetObjectData(info, context);
        }

        #endregion

        #region Public Methods
        public ICollection<TKey> Keys
        {
            get
            {
                return ReadOnlyICollection<TKey>.AsReadOnly(m_dictionaryTyped.Keys);
            }
        }
        public ICollection<TValue> Values
        {
            get
            {
                return ReadOnlyICollection<TValue>.AsReadOnly(m_dictionaryTyped.Values);
            }
        }
        public int Count
        {
            get
            {
                return m_dictionaryTyped.Count;
            }
        }
        public bool IsReadOnly
        {
            get
            {
                return true;
            }
        }
        public TValue this[TKey key]
        {
            get
            {
                return m_dictionaryTyped[key];
            }
            set
            {
            }
        }
        public bool IsFixedSize
        {
            get
            {
                return m_dictionary.IsFixedSize;
            }
        }
        public bool IsSynchronized
        {
            get
            {
                return m_dictionary.IsSynchronized;
            }
        }
        public object SyncRoot
        {
            get
            {
                return m_dictionary.SyncRoot;
            }
        }

        #endregion
    }
}
