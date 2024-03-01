namespace ChaosDbg.IL
{
    /// <summary>
    /// Represents a local or parameter variable that is referenced in an <see cref="ILInstruction"/>.
    /// </summary>
    public class ILVariable
    {
        /// <summary>
        /// Gets whether the variable is a local or a parameter.
        /// </summary>
        public ILVariableKind Kind { get; }

        /// <summary>
        /// Gets the 0-based index of the variable.
        /// </summary>
        public short Index { get; }

        internal ILVariable(ILVariableKind kind, short index)
        {
            Kind = kind;
            Index = index;
        }

        public override string ToString()
        {
            return $"{Kind} {Index}";
        }
    }
}
