using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using ChaosDbg.Metadata;

namespace ChaosDbg
{
    /// <summary>
    /// Represents a set of options that may be specified when launching a new debug target.<para/>
    /// Not all options may be compatible with all debugger engines and debug types.
    /// </summary>
    public abstract class LaunchTargetOptions
    {
        private Dictionary<string, object> dict = new();

        public LaunchTargetKind Kind { get; }

        public bool IsAttach => Kind == LaunchTargetKind.AttachProcess;

        protected LaunchTargetOptions(LaunchTargetKind kind)
        {
            Kind = kind;
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
        #region Dump

        [Dump]
        public string DumpFile
        {
            get => GetProperty<string>();
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

            switch (Kind)
            {
                case LaunchTargetKind.CreateProcess:
                    clone = new CreateProcessTargetOptions(CommandLine);
                    break;

                case LaunchTargetKind.AttachProcess:
                    clone = new AttachProcessTargetOptions(ProcessId);
                    break;

                case LaunchTargetKind.OpenDump:
                    clone = new OpenDumpTargetOptions(DumpFile);
                    break;

                default:
                    throw new UnknownEnumValueException(Kind);
            }

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

        class DumpAttribute : Attribute
        {
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
