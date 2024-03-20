using System;
using System.Reflection;

namespace ChaosDbg
{
    public static class ReflectionExtensions
    {
        private static BindingFlags internalFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        public static MethodInfo GetInternalMethodInfo(this Type type, string name)
        {
            var methodInfo = type.GetMethod(name, internalFlags);

            if (methodInfo == null)
                throw new MissingMemberException(type.Name, name);

            return methodInfo;
        }

        public static PropertyInfo GetInternalPropertyInfo(this Type type, string name)
        {
            var propertyInfo = type.GetProperty(name, internalFlags);

            if (propertyInfo == null)
                throw new MissingMemberException(type.Name, name);

            return propertyInfo;
        }

        public static FieldInfo GetInternalFieldInfo(this Type type, string name)
        {
            var fieldInfo = type.GetField(name, internalFlags);

            if (fieldInfo == null)
                throw new MissingMemberException(type.Name, name);

            return fieldInfo;
        }

        public static EventInfo GetEventInfo(this Type type, string name)
        {
            var eventInfo = type.GetEvent(name);

            if (eventInfo == null)
                throw new MissingMemberException(type.Name, name);

            return eventInfo;
        }

        public static void SetPropertyValue(object instance, string propertyName, object value)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));

            var type = instance.GetType();

            var propertyInfo = type.GetProperty(propertyName);

            if (propertyInfo == null)
                throw new MissingMemberException(type.Name, propertyName);

            var setter = propertyInfo.GetSetMethod();

            if (setter != null)
            {
                setter.Invoke(instance, new[] { value });
                return;
            }

            //There's no setter, so we need to find the backing field
            var field = type.GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
                throw new InvalidOperationException($"Could not find a backing field for property {type.Name}.{propertyName}");

            field.SetValue(instance, value);
        }
    }
}
