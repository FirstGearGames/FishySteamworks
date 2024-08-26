using System;
using System.Collections;
using System.Collections.Generic;

namespace FishySteamworks
{
    public class BidirectionalDictionary<T1, T2> : IEnumerable<KeyValuePair<T1, T2>>
    {
        private readonly Dictionary<T1, T2> t1ToT2Dict = new Dictionary<T1, T2>();
        private readonly Dictionary<T2, T1> t2ToT1Dict = new Dictionary<T2, T1>();
        private int lockCount = 0;
        private readonly List<PendingOperation> pendingOperations = new List<PendingOperation>();

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<KeyValuePair<T1, T2>> IEnumerable<KeyValuePair<T1, T2>>.GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        public int Count => t1ToT2Dict.Count;

        public void Add(T1 key, T2 value)
        {
            if (lockCount > 0)
            {
                pendingOperations.Add(new PendingOperation(isAdd: true, key, value));
                return;
            }

            Remove(key);

            t1ToT2Dict[key] = value;
            t2ToT1Dict[value] = key;
        }

        public void Add(T2 key, T1 value)
        {
            Add(value, key);
        }

        public void Clear()
        {
            t1ToT2Dict.Clear();
            t2ToT1Dict.Clear();
        }

        public bool TryGetValue(T1 key, out T2 value) => t1ToT2Dict.TryGetValue(key, out value);

        public bool TryGetValue(T2 key, out T1 value) => t2ToT1Dict.TryGetValue(key, out value);

        public bool Contains(T1 key) => t1ToT2Dict.ContainsKey(key);

        public bool Contains(T2 key) => t2ToT1Dict.ContainsKey(key);

        public void Remove(T1 key)
        {
            if (!t1ToT2Dict.TryGetValue(key, out T2 val))
            {
                return;
            }

            if (lockCount > 0)
            {
                pendingOperations.Add(new PendingOperation(isAdd: false, key, val));
                return;
            }

            t1ToT2Dict.Remove(key);
            t2ToT1Dict.Remove(val);
        }

        public void Remove(T2 key)
        {
            if (!t2ToT1Dict.TryGetValue(key, out T1 val))
            {
                return;
            }

            if (lockCount > 0)
            {
                pendingOperations.Add(new PendingOperation(isAdd: false, val, key));
                return;
            }

            t1ToT2Dict.Remove(val);
            t2ToT1Dict.Remove(key);
        }

        public T1 this[T2 key]
        {
            get => t2ToT1Dict[key];
            set
            {
                Add(key, value);
            }
        }

        public T2 this[T1 key]
        {
            get => t1ToT2Dict[key];
            set
            {
                Add(key, value);
            }
        }

        private void Lock()
        {
            ++lockCount;
        }

        private void Unlock()
        {
            if (lockCount == 0)
            {
                throw new InvalidOperationException("Unlock called without a corresponding lock");
            }
            if (--lockCount == 0)
            {
                foreach (var operation in pendingOperations)
                {
                    if (operation.AddOperation)
                    {
                        Add(operation.Key, operation.Value);
                    }
                    else
                    {
                        Remove(operation.Key);
                    }
                }
                pendingOperations.Clear();
            };
        }

        public struct Enumerator : IEnumerator<KeyValuePair<T1, T2>>, IDisposable
        {
            private readonly BidirectionalDictionary<T1, T2> _dictionary;
            private Dictionary<T1, T2>.Enumerator _enumerator;

            public Enumerator(BidirectionalDictionary<T1, T2> dictionary)
            {
                _dictionary = dictionary;
                _enumerator = dictionary.t1ToT2Dict.GetEnumerator();
                _dictionary.Lock();
            }

            public void Reset()
            {
                throw new InvalidOperationException("Enumerator reset during enumeration");
            }

            public KeyValuePair<T1, T2> Current => _enumerator.Current;

            object IEnumerator.Current => _enumerator.Current;

            public bool MoveNext()
            {
                return _enumerator.MoveNext();
            }

            public void Dispose()
            {
                _enumerator.Dispose();
                _dictionary.Unlock();
            }
        }

        private readonly struct PendingOperation
        {
            public readonly bool AddOperation;
            public readonly T1 Key;
            public readonly T2 Value;

            public PendingOperation(bool isAdd, T1 key, T2 value)
            {
                AddOperation = isAdd;
                Key = key;
                Value = value;
            }
        }
    }
}
