namespace ChaosDbg.DbgEng.Server
{
    /// <summary>
    /// Represents an that is specified when launching a debugger server that has an associated value.
    /// e.g."port=1234" in "tcp:port=1234,hidden"
    /// </summary>
    class DbgEngServerValueOption : DbgEngServerOption
    {
        public string Value { get; }

        public DbgEngServerValueOption(string name, string value) : base(name)
        {
            Value = value;
        }

        public DbgEngServerValueOption(DbgEngServerOptionKind kind, string value) : base(kind)
        {
            Value = value;
        }

        public override string ToString()
        {
            return $"{base.ToString()}={Value}";
        }
    }
}
