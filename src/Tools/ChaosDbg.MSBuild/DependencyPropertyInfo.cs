using System;

namespace ChaosDbg.MSBuild
{
    class DependencyPropertyInfo
    {
        public string ClassName { get; }

        public string PropertyName { get; }

        public string PropertyType { get; }

        public bool IsReadOnly { get; }

        public bool IsAttached { get; }

        public string[] Flags { get; }

        public string DefaultValue { get; }

        public bool NeedMetadata { get; }

        public DependencyPropertyInfo(
            string className,
            string propertyName,
            string propertyType,
            bool isReadOnly,
            bool isAttached,
            string[] flags,
            string defaultValue)
        {
            ClassName = className;
            PropertyName = propertyName;
            PropertyType = propertyType;
            IsReadOnly = isReadOnly;
            IsAttached = isAttached;
            Flags = flags;
            DefaultValue = defaultValue;
            NeedMetadata = defaultValue != null || Flags.Length > 0;

            if (NeedMetadata && defaultValue == null)
                throw new InvalidOperationException("A default value must be specified when one or more metadata customization values have been set.");
        }
    }
}
