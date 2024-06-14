using System.Linq;
using ChaosDbg.TTD;
using ChaosLib;
using ClrDebug;
using ClrDebug.DbgEng;
using ClrDebug.TTD;
using static ClrDebug.TTD.TtdExtensions;

namespace ChaosDbg.DbgEng
{
    public class DbgEngTtdServices : TtdDebugServices
    {
        private DbgEngEngine engine;

        //Don't dispose this, as it's not ours to dispose
        private ReplayEngine replayEngine;

        /// <summary>
        /// Gets the internal <see cref="ReplayEngine"/> that is being used by the internal dbgeng!TtdTargetInfo
        /// </summary>
        public override ReplayEngine ReplayEngine
        {
            get
            {
                if (replayEngine == null)
                {
                    var apis = engine.ActiveClient.Control.GetWindbgExtensionApis();

                    //Note that we don't cache a cursor, as it's expected that the caller will create a new cursor for each
                    //usage they have.
                    replayEngine = new ReplayEngine(apis.Ioctl().QueryTargetInterface(IID_IReplayEngine));
                }

                return replayEngine;
            }
        }

        private Cursor unsafeEngineCursor;

        public Cursor UnsafeEngineCursor
        {
            get
            {
                if (unsafeEngineCursor == null)
                {
                    var apis = engine.ActiveClient.Control.GetWindbgExtensionApis();

                    unsafeEngineCursor = new Cursor(apis.Ioctl().QueryTargetInterface(IID_ICursor));
                }

                return unsafeEngineCursor;
            }
        }

        public TtdRawFunctionCall[] GetFunctionCalls(string[] name)
        {
            var symbols = engine.GetSymbols(name);

            //You could have multiple symbols at the same address, so do DistinctBy just in case
            var addressToNameMap = symbols.DistinctBy(s => s.Offset).ToDictionary(s => s.Offset, s => s.Buffer);

            var results = GetFunctionCalls(addressToNameMap.Keys.ToArray(), address =>
            {
                if (addressToNameMap.TryGetValue(address, out var name))
                    return name;

                if (engine.ActiveClient.Symbols.TryGetNameByOffset(address, out var result) == HRESULT.S_OK)
                {
                    name = result.Displacement == 0 ? result.NameBuffer : result.NameBuffer + "+0x" + result.Displacement.ToString("X");
                }
                else
                    name = address.ToString();

                addressToNameMap[address] = name;

                return name;
            });

            return results;
        }

        public DbgEngTtdServices(DbgEngEngine engine)
        {
            this.engine = engine;
        }
    }
}
