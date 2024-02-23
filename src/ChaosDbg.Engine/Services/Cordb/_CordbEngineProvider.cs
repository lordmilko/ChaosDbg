namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Provides facilities for creating new <see cref="CordbEngine"/> instances.
    /// </summary>
    public class CordbEngineProvider : DebugEngineProvider<CordbEngine>
    {
        private readonly CordbEngineServices services;

        public CordbEngineProvider(CordbEngineServices services)
        {
            this.services = services;
        }

        protected override CordbEngine NewEngine()
        {
            var engine = new CordbEngine(services);
            engine.EventHandlers.AddHandlers(events);
            return engine;
        }
    }
}
