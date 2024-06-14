using System;
using System.Threading;
using ChaosLib;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Provides facilities for creating new <see cref="DbgEngEngine"/> instances.
    /// </summary>
    public class DbgEngEngineProvider : DebugEngineProvider<DbgEngEngine>
    {
        //DbgEng is not thread safe. Attempting to create multiple machines concurrently will overwrite g_Machine which can cause other DbgEng instances
        //to crash when the g_Machine gets nulled out

        private static object instanceLock = new object();

        private readonly DbgEngEngineServices services;

        public DbgEngEngineProvider(DbgEngEngineServices services)
        {
            this.services = services;
        }

        public DbgEngEngine OpenDump(OpenDumpTargetOptions options, CancellationToken cancellationToken = default) =>
            OpenDumpInternal(options, cancellationToken);

        protected override DbgEngEngine CreateEngine()
        {
            if (Monitor.TryEnter(instanceLock))
            {
                Log.Debug<DbgEngEngineProvider>("Acquired DbgEng instance lock");

                return base.CreateEngine();
            }

            Log.Debug<DbgEngEngineProvider>("Waiting for DbgEng instance lock");

            Monitor.Enter(instanceLock);
            {
                Log.Debug<DbgEngEngineProvider>("Acquired DbgEng instance lock after waiting");

                return base.CreateEngine();
            }
        }

        protected override DbgEngEngine NewEngine()
        {
            var engine = new DbgEngEngine(services, this);
            engine.EventHandlers.AddHandlers(events);
            return engine;
        }

        public override void Remove(DbgEngEngine engine)
        {
            base.Remove(engine);

            Log.Debug<DbgEngEngineProvider>("Releasing DbgEng instance lock");

            Monitor.Exit(instanceLock);
        }

        internal void WithDbgEng(Action<DbgEngEngineServices> action)
        {
            lock (instanceLock)
                action(services);
        }
    }
}
