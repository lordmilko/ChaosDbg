using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ClrDebug;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a managed array from the address space of a debug target.
    /// </summary>
    class CordbArrayValue : CordbValue, IEnumerable<CordbValue>
    {
        public int Count => CorDebugValue.Count;

        /// <summary>
        /// Gets the <see cref="ClrDebug.CorDebugArrayValue"/> that underpins this object.
        /// </summary>
        public new CorDebugArrayValue CorDebugValue => (CorDebugArrayValue) base.CorDebugValue;

        internal CordbArrayValue(CorDebugArrayValue corDebugValue, CordbThread thread, CordbValue? parent, MemberInfo? sourceMember) : base(corDebugValue, thread, parent, sourceMember)
        {
        }

        public CordbValue this[int index]
        {
            get
            {
                ThrowIfStale();

                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                var rawValue = CorDebugValue.GetElementAtPosition(index);

                var value = New(rawValue, Thread, this);

                return value;
            }
        }

        public override bool IsEquivalentTo(object other)
        {
            if (other is not Array a)
                return false;

            var count = Count;

            if (a.Length != Count)
                return false;

            for (var i = 0; i < count; i++)
            {
                var ours = this[i];
                var theirs = a.GetValue(i);

                if (!ours.IsEquivalentTo(theirs))
                    return false;
            }

            return true;
        }

        public IEnumerator<CordbValue> GetEnumerator()
        {
            ThrowIfStale();
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append("[");

            var count = CorDebugValue.Count;

            if (count <= 10)
            {
                var index = 0;

                foreach (var item in this)
                {
                    builder.Append(item);

                    if (index < count - 1)
                        builder.Append(", ");

                    index++;
                }
            }
            builder.Append("]");
            return builder.ToString();
        }

        struct Enumerator : IEnumerator<CordbValue>
        {
            public CordbValue Current { get; private set; }

            private CordbArrayValue array;
            private int count;
            private int index;

            public Enumerator(CordbArrayValue array)
            {
                this.array = array;
                this.count = array.Count;
                this.index = 0;
                Current = null;
            }

            public bool MoveNext()
            {
                if (index < count)
                {
                    Current = New(array.CorDebugValue.GetElementAtPosition(index), array.Thread, array);
                    index++;
                    return true;
                }

                return false;
            }

            object IEnumerator.Current => Current;

            public void Reset()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
