using System;
using System.Collections.Generic;
using System.Linq;

namespace ChaosDbg.DbgEng.Server
{
    class DbgEngServerOptionsModel
    {
        private Dictionary<DbgEngServerOptionKind, DbgEngServerOption> dict = new Dictionary<DbgEngServerOptionKind, DbgEngServerOption>();

        public DbgEngServerProtocol ServerProtocol { get; }

        public DbgEngServerOptionsModel(DbgEngServerProtocol protocol)
        {
            ServerProtocol = protocol;
        }

        #region RequiredValue

        protected T GetRequiredValue<T>(DbgEngServerOptionKind kind)
        {
            if (!dict.TryGetValue(kind, out var option))
                throw new InvalidOperationException($"Required value {kind} could not be found");

            return ConvertValue<T>(((DbgEngServerValueOption) option).Value);
        }

        protected void SetRequiredValue<T>(DbgEngServerOptionKind kind, T value)
        {
            if (value == null)
                throw new InvalidOperationException($"Value {kind} is required and cannot be removed");
            else
                dict[kind] = new DbgEngServerValueOption(kind, value.ToString());
        }

        #endregion
        #region OptionalValue

        protected T GetOptionalValue<T>(DbgEngServerOptionKind kind)
        {
            if (!dict.TryGetValue(kind, out var option))
                return (T) (object) null;

            return ConvertValue<T>(((DbgEngServerValueOption) option).Value);
        }

        protected void SetOptionalValue<T>(DbgEngServerOptionKind kind, T value)
        {
            if (value == null)
                dict.Remove(kind);
            else
                dict[kind] = new DbgEngServerValueOption(kind, value.ToString());
        }

        #endregion
        #region OptionalOption

        protected bool GetOptionalOption(DbgEngServerOptionKind kind) => dict.ContainsKey(kind);

        protected void SetOptionalOption(DbgEngServerOptionKind kind, bool value)
        {
            if (value)
            {
                if (!dict.ContainsKey(kind))
                    dict[kind] = new DbgEngServerOption(kind);
            }
            else
            {
                dict.Remove(kind);
            }
        }

        #endregion

        public string CreateConnectionString(DbgEngServerKind kind)
        {
            var info = new DbgEngServerConnectionInfo(kind, ServerProtocol, dict.Values.ToArray());

            return info.ServerConnectionString;
        }

        private T ConvertValue<T>(string value)
        {
            var t = typeof(T);

            object result;

            Type underlying;

            if (t == typeof(string))
                result = value;
            else if (t == typeof(int))
                result = int.Parse(value);
            else if ((underlying = Nullable.GetUnderlyingType(t)) != null)
            {
                if (underlying.IsEnum)
                    result = Enum.Parse(underlying, value);
                else
                    throw new NotImplementedException($"Don't know how to handle having a nullable value of type {t}");
            }
            else
                throw new NotImplementedException($"Don't know how to handle a value of type {t}");

            return (T) result;
        }

        public override string ToString()
        {
            return CreateConnectionString(DbgEngServerKind.Debugger);
        }
    }
}
