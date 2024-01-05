namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Provides facilities for creating new <see cref="CordbEngine"/> instances.
    /// </summary>
    public class CordbEngineProvider : DebugEngineProvider<ICordbEngine>
    {
        private readonly CordbEngineServices services;

        public CordbEngineProvider(CordbEngineServices services)
        {
            this.services = services;
        }

        protected override ICordbEngine NewEngine() => new CordbEngine(services);
    }
}
