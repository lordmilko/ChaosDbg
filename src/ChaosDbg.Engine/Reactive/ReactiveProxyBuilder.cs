using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ChaosLib;
using ChaosLib.Dynamic;
using ChaosLib.Dynamic.Emit;

namespace ChaosDbg.Reactive
{
    /// <summary>
    /// Provides facilities for dynamically generating reactive proxies - types deriving from some <see cref="ReactiveObject"/> derived type
    /// that require property overrides that automatically dispatch to OnPropertyChanged() upon modification.
    /// </summary>
    public class ReactiveProxyBuilder
    {
        public static ReactiveProxyInfo Build(Type type, params object[] args)
        {
            var baseCtor = FindBestConstructor(type, args.Select(a => a.GetType()).ToArray());

            var assembly = DynamicAssembly.Instance;

            var builder = assembly.DefineType($"{type.Name}Proxy", type);

            var reactiveProperties = GetReactiveProperties(type);

            var setPropertyDef = type.GetMethodInfo("SetProperty");

            foreach (var property in reactiveProperties)
            {
                var setProperty = setPropertyDef.MakeGenericMethod(property.PropertyType);

                AddReactiveProperty(builder, property, setProperty);
            }

            AddCtor(builder, args, baseCtor);

            var result = builder.CreateType();

            return new ReactiveProxyInfo(result);
        }

        private static ConstructorInfo FindBestConstructor(Type type, Type[] parameterTypes)
        {
            var candidates = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (candidates.Length == 1)
                return candidates[0];

            var ctorsWithParameters = candidates.Select(c => new
            {
                Ctor = c,
                Parameters = c.GetParameters()
            }).ToArray();

            var lengthMatches = ctorsWithParameters.Where(v => v.Parameters.Length == parameterTypes.Length).ToArray();

            if (lengthMatches.Length == 0)
                throw new InvalidOperationException($"Could not find a constructor on type '{type.Name}' that takes {parameterTypes.Length} arguments");

            if (lengthMatches.Length == 1)
                return lengthMatches[0].Ctor;

            var parameterTypeMatches = new List<ConstructorInfo>();

            foreach (var candidate in lengthMatches)
            {
                var bad = false;

                for (var i = 0; i < candidate.Parameters.Length; i++)
                {
                    var candidateParameter = candidate.Parameters[i].ParameterType;
                    var valueType = parameterTypes[i];

                    if (!IsAssignableTo(candidateParameter, valueType))
                    {
                        bad = true;
                        break;
                    }
                }

                if (!bad)
                    parameterTypeMatches.Add(candidate.Ctor);
            }

            if (parameterTypeMatches.Count == 0)
                throw new InvalidOperationException($"Could not find a constructor on type '{type.Name}' that can accept values of type {string.Join(", ", parameterTypes.Select(t => t.Name))}.");

            if (parameterTypeMatches.Count == 1)
                return parameterTypeMatches[0];

            throw new InvalidOperationException($"Could not find an appropriate constructor to use on type '{type.Name}'.");
        }

        private static bool IsAssignableTo(Type parameterType, Type valueType)
        {
            if (parameterType == valueType)
                return true;

            if (parameterType.IsAssignableFrom(valueType))
                return true;

            if (parameterType.IsEnum && valueType == typeof(int))
                return true;

            if (!parameterType.IsInterface)
                return false;

            var ifaces = valueType.GetInterfaces();

            foreach (var iface in ifaces)
            {
                if (iface == parameterType)
                    return true;
            }

            return false;
        }

        public static PropertyInfo[] GetReactiveProperties(Type type)
        {
            var properties = type.GetProperties().Where(p => p.GetCustomAttribute<ReactiveAttribute>() != null).ToArray();

            foreach (var property in properties)
                VerifyReactiveProperty(property);

            return properties.ToArray();
        }

        private static void AddCtor(TypeBuilder typeBuilder, object[] args, ConstructorInfo baseCtor)
        {
            var parameterTypes = baseCtor.GetParameters().Select(p => p.ParameterType).ToArray();

            var newCtor = typeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, CallingConventions.Standard, parameterTypes);

            var il = newCtor.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); //this

            for (var i = 1; i <= args.Length; i++)
                il.Ldarg(i);

            il.Emit(OpCodes.Call, baseCtor);
            il.Emit(OpCodes.Ret);
        }

        private static void AddReactiveProperty(TypeBuilder builder, PropertyInfo property, MethodInfo setProperty)
        {
            var field = GetReactiveField(builder, property);

            PropertyBuilder propertyBuilder = builder.DefineProperty(property.Name, PropertyAttributes.None, property.PropertyType, null);

            var getter = GetReactivePropertyGetter(builder, property, field);
            var setter = GetReactivePropertySetter(builder, property, field, setProperty);

            propertyBuilder.SetGetMethod(getter);
            propertyBuilder.SetSetMethod(setter);
        }

        private static FieldBuilder GetReactiveField(TypeBuilder builder, PropertyInfo property)
        {
            var fieldName = property.Name.Length > 1
                ? char.ToLower(property.Name[0]) + property.Name.Substring(1)
                : property.Name.ToLower();

            var fieldBuilder = builder.DefineField(fieldName, property.PropertyType, FieldAttributes.Private);

            return fieldBuilder;
        }

        private static MethodBuilder GetReactivePropertyGetter(TypeBuilder builder, PropertyInfo property, FieldInfo field)
        {
            var methodBuilder = builder.DefineMethod(
                property.GetGetMethod().Name,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.SpecialName,
                property.PropertyType,
                null
            );

            var il = methodBuilder.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); //this
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);

            return methodBuilder;
        }

        private static MethodBuilder GetReactivePropertySetter(TypeBuilder builder, PropertyInfo property, FieldInfo field, MethodInfo setProperty)
        {
            var methodBuilder = builder.DefineMethod(
                property.GetSetMethod().Name,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.SpecialName,
                null,
                new[] { property.PropertyType }
            );

            var il = methodBuilder.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); //this
            il.Emit(OpCodes.Ldarg_0); //this
            il.Emit(OpCodes.Ldflda, field);
            il.Emit(OpCodes.Ldarga_S, (byte) 1); //'value'

            il.Emit(OpCodes.Ldstr, property.Name);
            il.Emit(OpCodes.Call, setProperty);

            il.Emit(OpCodes.Ret);

            return methodBuilder;
        }

        public static void VerifyReactiveProperty(PropertyInfo property)
        {
            var manualReactiveAttrib = property.GetCustomAttribute<ManualReactiveAttribute>();

            if (manualReactiveAttrib != null)
                return;

            var reactiveAttrib = property.GetCustomAttribute<ReactiveAttribute>();

            var propertyName = $"{property.DeclaringType.Name}.{property.Name}";

            if (reactiveAttrib == null)
                throw new InvalidOperationException($"Attempted to use property '{propertyName}' in a reactive context however it is missing a '{nameof(ReactiveAttribute)}'.");

            if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string))
                throw new NotImplementedException($"Cannot bind property '{propertyName}'. Binding to collections has not yet been tested");

            var getter = property.GetGetMethod();

            if ((getter.Attributes & MethodAttributes.Virtual) == 0)
                throw new InvalidOperationException($"Property '{propertyName}' was marked with a {nameof(ReactiveAttribute)} however it is not marked as virtual.");

            var setter = property.GetSetMethod();

            if (setter == null)
                throw new InvalidOperationException($"Property '{propertyName}' was marked with a {nameof(ReactiveAttribute)} however does not have a setter.");

            //Doesn't seem like theres a way to check if the base type was assigned a default value, which is illegal because that value will be ignored

            if (char.IsLower(property.Name[0]))
                throw new InvalidOperationException($"Property '{propertyName}' was marked with a {nameof(ReactiveAttribute)} however does not begin with an uppercase character.");
        }
    }
}
