using System;

namespace ChaosDbg.DbgEng.Model
{
    /// <summary>
    /// Represents a TTD DbgEng Data Model function call as retrieved from @$cursession.TTD.Calls()
    /// </summary>
    public class TtdModelFunctionCall
    {
        /// <summary>
        /// Memory access type
        /// </summary>
        public object AccessType => modelObject.AccessType;

        /// <summary>
        /// Address of accessed memory
        /// </summary>
        public object Address => modelObject.Address;

        /// <summary>
        /// Type of control flow
        /// </summary>
        public uint EventType => modelObject.EventType;

        /// <summary>
        /// Name of function
        /// </summary>
        public string Function => modelObject.Function;

        /// <summary>
        /// Address of function
        /// </summary>
        public ulong FunctionAddress => modelObject.FunctionAddress;

        /// <summary>
        /// Instruction Pointer
        /// </summary>
        public object IP => modelObject.IP;

        /// <summary>
        /// Value of memory read / written
        /// </summary>
        public object OverwrittenValue => modelObject.OverwrittenValue;

        /// <summary>
        /// Parameters to function pinned in time at the call entry with known type information applied
        /// </summary>
        public object Parameters => modelObject.Parameters;

        /// <summary>
        /// Parameters to function (untyped and not pinned in time)
        /// </summary>
        public object RawParameters => modelObject.RawParameters;

        /// <summary>
        /// Return value of the function (untyped and not pinned in time)
        /// </summary>
        public ulong RawReturnValue => modelObject.RawReturnValue;

        /// <summary>
        /// Return Address of function
        /// </summary>
        public ulong ReturnAddress => modelObject.ReturnAddress;

        /// <summary>
        /// Return value of the function pinned in time at the call return with known type information applied
        /// </summary>
        public ulong ReturnValue => modelObject.ReturnValue;

        /// <summary>
        /// Size of accessed memory
        /// </summary>
        public ulong? Size => modelObject.Size;

        /// <summary>
        /// Approximate System time of the first instruction executed in this function
        /// </summary>
        public DateTime SystemTimeStart => TtdModelDateTime.Parse(modelObject.SystemTimeStart);

        /// <summary>
        /// Approximate System time of the last instruction executed in this function
        /// </summary>
        public DateTime SystemTimeEnd => TtdModelDateTime.Parse(modelObject.SystemTimeEnd);

        /// <summary>
        /// Thread ID
        /// </summary>
        public uint ThreadId => modelObject.ThreadId;

        /// <summary>
        /// Time position of the first instruction executed in this function
        /// </summary>
        public TtdModelPosition TimeStart => new TtdModelPosition(modelObject.TimeStart);

        /// <summary>
        /// Time position of the last instruction executed in this function
        /// </summary>
        public TtdModelPosition TimeEnd => new TtdModelPosition(modelObject.TimeEnd);

        /// <summary>
        /// Unique Thread ID
        /// </summary>
        public uint UniqueThreadId => modelObject.UniqueThreadId;

        /// <summary>
        /// Value of memory read / written
        /// </summary>
        public object Value => modelObject.Value;

        private dynamic modelObject;

        public TtdModelFunctionCall(dynamic modelObject)
        {
            //It's very slow eagerly evaluating all of these properties (especially the DateTime ones), so we do it lazily as needed instead

            this.modelObject = modelObject;
        }
    }
}
