using System;
using System.Reflection;
using SymHelp.Metadata;

namespace ChaosDbg.Evaluator.IL
{
    /// <summary>
    /// Represents an <see cref="IILEvaluatorContext"/> capable of parsing <see cref="Slot"/> instances that contain
    /// reflected values from within the current process.
    /// </summary>
    class ReflectionILEvaluatorContext : IILEvaluatorContext
    {
        public Slot GetFieldValue(Slot instance, MetadataFieldInfo metadataFieldInfo)
        {
            var runtimeFieldInfo = metadataFieldInfo.GetUnderlyingField<FieldInfo>();

            object runtimeInstance = null;

            if (instance != null)
                runtimeInstance = (instance as ObjectSlot ?? new ObjectSlot(instance.Value, instance.Value.GetType())).Value;

            var runtimeResult = runtimeFieldInfo.GetValue(runtimeInstance);

            var result = Slot.New(runtimeResult);

            return result;
        }

        public Slot CallMethod(Slot instance, MetadataMethodInfo metadataMethodInfo, Slot[] args)
        {
            var runtimeMethodInfo = metadataMethodInfo.GetUnderlyingMethod<MethodInfo>();

            object runtimeInstance = null;

            if (instance != null)
                runtimeInstance = (instance as ObjectSlot ?? new ObjectSlot(instance.Value, instance.Value.GetType())).Value;

            object[] runtimeArgs;

            if (args.Length == 0)
                runtimeArgs = Array.Empty<object>();
            else
            {
                runtimeArgs = new object[args.Length];

                for (var i = 0; i < args.Length; i++)
                    runtimeArgs[i] = args[i].Value;
            }

            var runtimeResult = runtimeMethodInfo.Invoke(runtimeInstance, runtimeArgs);

            var result = Slot.New(runtimeResult);

            return result;
        }
    }
}
