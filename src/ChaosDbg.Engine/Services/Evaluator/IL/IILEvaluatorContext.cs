using ChaosLib.Metadata;

namespace ChaosDbg.Evaluator.IL
{
    /// <summary>
    /// Provides facilities for resolving metadata tokens to <see cref="Slot"/> instances inside an <see cref="ILVirtualMachine"/>.
    /// </summary>
    interface IILEvaluatorContext
    {
        /// <summary>
        /// Gets the value of a field on a specified object instance.
        /// </summary>
        /// <param name="instance">The slot containing the instance whose field should be accessed.</param>
        /// <param name="metadataFieldInfo">The field to access on the object instance.</param>
        /// <returns>The value of the specified field.</returns>
        Slot GetFieldValue(Slot instance, MetadataFieldInfo metadataFieldInfo);

        Slot CallMethod(Slot instance, MetadataMethodInfo metadataMethodInfo, Slot[] args);
    }
}
