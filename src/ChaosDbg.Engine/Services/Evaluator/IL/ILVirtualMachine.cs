using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using ChaosDbg.IL;
using SymHelp.Metadata;

namespace ChaosDbg.Evaluator.IL
{
    class ILVirtualMachine
    {
        /* Per ECMA 335 (https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf)
         * I.12.3.2.1 - The evaluation stack (PDF page 109) IL is interpreted via the use of an "evaluation stack",
         * which contains "slots" that can hold arbitrary values. Ostensibly then, a "slot" is a "container" that can then
         * hold a value. */

        private Stack<ILVirtualFrame> callStack = new Stack<ILVirtualFrame>();

        internal IILEvaluatorContext EvaluatorContext { get; }

        public ILVirtualMachine(IILEvaluatorContext evaluatorContext)
        {
            EvaluatorContext = evaluatorContext;
        }

        public object Execute(MethodInfo methodInfo, int numLocals, params object[] arguments)
        {
            arguments ??= Array.Empty<object>();

            ValidateArgs(methodInfo, arguments);

            var argSlots = GetArgs(arguments);

            var store = new ReflectionModuleMetadataStore();

            var metadataModule = store.GetOrAddModule(methodInfo.Module);

            var methodBody = methodInfo.GetMethodBody();

            var bytes = methodBody.GetILAsByteArray();

            var dis = ILDisassembler.Create(bytes, metadataModule);

            var instrs = dis.EnumerateInstructions().ToArray();

            var frame = new ILVirtualFrame(instrs, this, numLocals, argSlots);
            callStack.Push(frame);

            var result = frame.Execute();
            Debug.Assert(frame.StackEmpty);
            return result.Value;
        }

        public object Execute(ILInstruction[] instrs, int numLocals, Slot[] args)
        {
            var frame = new ILVirtualFrame(instrs, this, numLocals, args);
            callStack.Push(frame);

            var result = frame.Execute();
            Debug.Assert(frame.StackEmpty);
            return result.Value;
        }

        private Slot[] GetArgs(object[] arguments)
        {
            Slot[] argSlots = null;

            if (arguments.Length != 0)
            {
                argSlots = new Slot[arguments.Length];

                for (var i = 0; i < arguments.Length; i++)
                    argSlots[i] = Slot.New(arguments[i]);
            }

            return argSlots;
        }
    }
}
