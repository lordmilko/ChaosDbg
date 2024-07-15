using System;
using ChaosLib;

namespace ChaosDbg.DbgEng.Server
{
    /// <summary>
    /// Represents an argument that is specified when launching a debugger server that does not have an associaited value.
    /// e.g. "hidden" in "tcp:port=1234,hidden"
    /// </summary>
    public class DbgEngServerOption
    {
        public StringEnum<DbgEngServerOptionKind> Kind { get; }

        public DbgEngServerOption(string name)
        {
            Kind = GetKind(name);
        }

        public DbgEngServerOption(DbgEngServerOptionKind kind)
        {
            Kind = kind;
        }

        protected StringEnum<DbgEngServerOptionKind> GetKind(string str)
        {
            if (Enum.TryParse(str, true, out DbgEngServerOptionKind kind))
            {
                return kind;
            }

            return new StringEnum<DbgEngServerOptionKind>(DbgEngServerOptionKind.Unknown, str);
        }

        public override string ToString()
        {
            return Kind.StringValue.ToLower();
        }
    }
}
