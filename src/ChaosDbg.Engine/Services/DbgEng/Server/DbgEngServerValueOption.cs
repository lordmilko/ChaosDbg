namespace ChaosDbg.DbgEng.Server
{
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
