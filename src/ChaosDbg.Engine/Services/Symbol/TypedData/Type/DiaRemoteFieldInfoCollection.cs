using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChaosLib.TypedData;
using ClrDebug.DIA;

namespace ChaosDbg.TypedData
{
    class DiaRemoteFieldInfoCollection : IDbgRemoteFieldInfoCollection
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DiaRemoteType type;

        public DiaRemoteFieldInfoCollection(DiaRemoteType type)
        {
            this.type = type;
        }

        public IDbgRemoteFieldInfo this[string name]
        {
            get
            {
                var child = type.DiaSymbol.FindChildren(SymTagEnum.Null, name, NameSearchOptions.nsNone).FirstOrDefault();

                if (child == null)
                {
                    //Maybe it's a field in a base class

                    var baseClassStack = new Stack<DiaSymbol>();

                    var baseClasses = type.DiaSymbol.FindChildren(SymTagEnum.BaseClass, null, NameSearchOptions.nsNone);

                    foreach (var item in baseClasses)
                        baseClassStack.Push(item);

                    while (baseClassStack.Count > 0)
                    {
                        var current = baseClassStack.Pop();

                        var match = current.FindChildren(SymTagEnum.Null, name, NameSearchOptions.nsNone).FirstOrDefault();

                        if (match != null)
                        {
                            child = match;
                            break;
                        }

                        baseClasses = current.FindChildren(SymTagEnum.BaseClass, null, NameSearchOptions.nsNone);

                        foreach (var item in baseClasses)
                            baseClassStack.Push(item);
                    }
                }

                if (child == null)
                {
                    var availableChildren = type.DiaSymbol.FindChildren(SymTagEnum.Null, null, NameSearchOptions.nsNone).ToArray();

                    throw new InvalidOperationException($"Could not find field '{name}' under type '{type.DiaSymbol.Name}' or any base classes. Available fields: {string.Join(", ", (IEnumerable<DiaSymbol>) availableChildren)}");
                }

                return new DiaRemoteFieldInfo(child);
            }
        }

        public IEnumerator<IDbgRemoteFieldInfo> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
