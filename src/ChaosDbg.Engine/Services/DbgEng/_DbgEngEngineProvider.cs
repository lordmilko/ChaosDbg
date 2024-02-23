﻿namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Provides facilities for creating new <see cref="DbgEngEngine"/> instances.
    /// </summary>
    public class DbgEngEngineProvider : DebugEngineProvider<DbgEngEngine>
    {
        private readonly DbgEngEngineServices services;

        public DbgEngEngineProvider(DbgEngEngineServices services)
        {
            this.services = services;
        }

        protected override DbgEngEngine NewEngine()
        {
            var engine = new DbgEngEngine(services);
            engine.EventHandlers.AddHandlers(events);
            return engine;
        }
    }
}
