namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents either a native or managed parameter or local defined within a function.
    /// </summary>
    public abstract class CordbVariable
    {
        public abstract string Name { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}
