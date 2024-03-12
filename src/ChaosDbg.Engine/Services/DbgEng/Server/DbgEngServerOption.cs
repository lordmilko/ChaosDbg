using System;
using ChaosLib;

namespace ChaosDbg.DbgEng.Server
{
    class DbgEngServerOption
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
