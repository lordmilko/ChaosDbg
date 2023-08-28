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
    }
}
