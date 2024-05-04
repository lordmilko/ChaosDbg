using System;
using System.Linq;
using ChaosLib;
using ChaosLib.TypedData;
using ClrDebug.DIA;

namespace ChaosDbg.TypedData
{
    class DiaTypedDataModelProvider : ITypedDataModelProvider
    {
        private Lazy<IDbgHelp> dbgHelp;

        public DiaTypedDataModelProvider(Func<IDbgHelp> getDbgHelp)
        {
            this.dbgHelp = new Lazy<IDbgHelp>(getDbgHelp);
        }

        public IDbgRemoteType CreateRemoteType(string expr, ITypedDataProvider provider, long baseOfDll = 0)
        {
            if (baseOfDll == 0)
            {
                var symbol = dbgHelp.Value.SymGetTypeFromName(baseOfDll, expr);

                baseOfDll = symbol.ModuleBase;
            }

            var diaSession = dbgHelp.Value.SymGetDiaSession(baseOfDll);

            var diaSymbol = diaSession.GlobalScope.FindChildren(SymTagEnum.Null, expr, NameSearchOptions.nsNone).First();

            return new DiaRemoteType(diaSymbol);
        }

        public IDbgRemoteObject CreateRemoteObject(long address, IDbgRemoteType type, ITypedDataProvider provider)
        {
            return new DiaRemoteObject((DiaRemoteType) type, address, provider);
        }
    }
}
