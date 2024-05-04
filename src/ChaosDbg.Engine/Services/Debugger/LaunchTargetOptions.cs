using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using ChaosDbg.Metadata;

namespace ChaosDbg
{
    public class LaunchTargetOptions
    {
        private Dictionary<string, object> dict = new();

        public bool IsAttach { get; }

        public LaunchTargetOptions(string commandLine)
        {
            CommandLine = commandLine;

            //Initialize the environment variables dictionary so that we can easily add them against the environment variables
            //property on the options object
            EnvironmentVariables = new StringDictionary();

            StartMinimized = false;
        }

        public LaunchTargetOptions(int processId)
        {
            ProcessId = processId;
            IsAttach = true;
        }

        #region Create

        [Create]
        public string CommandLine
        {
            get => GetProperty<string>();
            set => SetProperty(value);
        }

        [Create]
        public bool StartMinimized
        {
            get => GetProperty<bool>();
            set => SetProperty(value);
        }

        [Create]
        public FrameworkKind? FrameworkKind
        {
            get => GetProperty<FrameworkKind?>();
            set => SetProperty(value);
        }

        [Create]
        public StringDictionary EnvironmentVariables
        {
            get => GetProperty<StringDictionary>();
            set => SetProperty(value);
        }

        #endregion
        #region Attach

        [Attach]
        public int ProcessId
        {
            get => GetProperty<int>();
            set => SetProperty(value);
        }

        #endregion
        #region DbgEng

        [Attach(DbgEngineKind.DbgEng)]
        public bool NonInvasive
        {
            get => GetProperty<bool>();
            set => SetProperty(value);
        }

        [Attach(DbgEngineKind.DbgEng)]
        public bool NoSuspend
        {
            get => GetProperty<bool>();
            set => SetProperty(value);
        }

        [Common(DbgEngineKind.DbgEng)]
        public bool? UseDbgEngSymOpts
        {
            get => GetProperty<bool?>();
            set => SetProperty(value);
        }

        [Common(DbgEngineKind.DbgEng)]
        public int? DbgEngEngineId
        {
            get => GetProperty<int?>();
            set => SetProperty(value);
        }

        #endregion
        #region Cordb

        [Common(DbgEngineKind.Cordb)]
        public bool UseInterop
        {
            get => GetProperty<bool>();
            set => SetProperty(value);
        }

        #endregion

        public LaunchTargetOptions Clone()
        {
            LaunchTargetOptions clone;

            if (IsAttach)
                clone = new LaunchTargetOptions(ProcessId);
            else
                clone = new LaunchTargetOptions(CommandLine);

            clone.dict = dict.ToDictionary(kv => kv.Key, kv =>
            {
                var v = kv.Value;

                if (v == null)
                    return null;

                var t = v.GetType();

                if (Type.GetTypeCode(t) != TypeCode.Object)
                    return v;

                if (t.IsEnum)
                    return v;

                if (v is StringDictionary d)
                {
                    var newDict = new StringDictionary();

                    foreach (KeyValuePair<string, string> item in d)
                        newDict[item.Key] = item.Value;

                    return newDict;
                }

                throw new NotImplementedException($"Don't know how to clone value of type {v.GetType().Name}");
            });

            return clone;
        }

        private T GetProperty<T>([CallerMemberName] string name = null)
        {
            if (Nullable.GetUnderlyingType(typeof(T)) != null)
            {
                if (dict.TryGetValue(name, out var value))
                    return (T) value;

                return default;
            }

            return (T) dict[name];
        }

        private void SetProperty<T>(T value, [CallerMemberName] string name = null)
        {
            dict[name] = value;
        }

        class CreateAttribute : Attribute
        {
        }

        class AttachAttribute : Attribute
        {
            public DbgEngineKind? EngineKind { get; }

            public AttachAttribute()
            {
            }

            public AttachAttribute(DbgEngineKind engineKind)
            {
                EngineKind = engineKind;
            }
        }

        class CommonAttribute : Attribute
        {
            public DbgEngineKind? EngineKind { get; }

            public CommonAttribute()
            {
            }

            public CommonAttribute(DbgEngineKind engineKind)
            {
                EngineKind = engineKind;
            }
        }
    }
}
