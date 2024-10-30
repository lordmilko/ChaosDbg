using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using ChaosDbg.IL;
using ClrDebug;
using SymHelp.Metadata;

namespace ChaosDbg.Evaluator.IL
{
    /// <summary>
    /// Represents a virtual frame for emulating <see cref="ILInstruction"/> instructions within the context of a <see cref="ILVirtualMachine"/>.
    /// </summary>
    class ILVirtualFrame
    {
        private Stack<Slot> evaluationStack = new Stack<Slot>();

        public bool StackEmpty => evaluationStack.Count == 0;

        /// <summary>
        /// Gets the <see cref="ILVirtualMachine"/> that this frame is associated with.
        /// </summary>
        public ILVirtualMachine VirtualMachine { get; }

        /// <summary>
        /// Gets the current instruction that is being evaluated on this frame.
        /// </summary>
        public ILInstruction CurrentInstruction => instructions[currentInstructionIndex];

        /// <summary>
        /// Gets the arguments that were passed into this virtual frame.
        /// </summary>
        public Slot[] Arguments { get; }

        /// <summary>
        /// Gets the locals that are defined in this virtual frame.
        /// </summary>
        public Slot[] Locals { get; }

        private ILInstruction[] instructions;
        private int currentInstructionIndex;

        public ILVirtualFrame(ILInstruction[] instructions, ILVirtualMachine virtualMachine, int numLocals, Slot[] arguments)
        {
            this.instructions = instructions;
            VirtualMachine = virtualMachine;
            Arguments = arguments ?? Array.Empty<Slot>();

            Locals = numLocals == 0 ? Array.Empty<Slot>() : new Slot[numLocals];
        }

        internal Slot Execute()
        {
            /* ECMA 335 describes the "stack transition" of each operand (pdf page 327)
             * e.g. if the transition is
             *     ..., value1, value2 -> ..., result
             *
             * this means the stack had some stuff on it + value1 and value2 at the end. value1 and value2
             * were removed, leaving whatever stuff existed before them + the new result value */

            //We can't just for-loop over the instructions, because branching instructions will cause us to skip ahead/go back
            while (true)
            {
                var instr = CurrentInstruction;

                switch (instr.Kind)
                {
                    #region III.3: Base Instructions

                    //III.3.1: add numeric values
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Add:                    //Add two values, returning a new value
                        MathOp(instr.Kind, MathFlags.Signed);
                        break;

                    //III.3.2: add integer values with overflow check
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Add_Ovf:                //Add signed integer values with overflow check
                        MathOp(instr.Kind, MathFlags.Signed | MathFlags.Checked);
                        break;

                    case OpCodeKind.Add_Ovf_Un:             //Add unsigned integer values with overflow check
                        MathOp(instr.Kind, MathFlags.Unsigned | MathFlags.Checked);
                        break;

                    //III.3.3: bitwise AND
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.And:                    //Bitwise AND of two integral values, returns an integral value
                        BoolOp(instr.Kind);
                        break;

                    //III.3.4: get argument list
                    //... -> ..., argListHandle
                    case OpCodeKind.Arglist:                //Return argument list handle for the current method
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.5: branch on equal
                    //..., value1, value2 -> ...
                    case OpCodeKind.Beq:                    //Branch to "target" (int32) if equal
                    case OpCodeKind.Beq_S:                  //Branch to "target" (int8) if equal, short form
                    {
                        IntegerSlot value2;
                        IntegerSlot value1;

                        Pop(out value2, out value1);

                        if ((bool) value1.Equals(value2).Value)
                            MoveTo((ILInstruction) instr.Operand);
                        else
                            MoveNext();
                        break;
                    }

                    //III.3.6: branch on greater than or equal to
                    //..., value1, value2 -> ...
                    case OpCodeKind.Bge:                    //Branch to "target" (int32) if greater than or equal to
                    case OpCodeKind.Bge_S:                  //Branch to "target" (int8) if greater than or equal to, short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.7: branch on greater than or equal to, unsigned or ordered
                    //..., value1, value2 -> ...
                    case OpCodeKind.Bge_Un:                 //Branch to "target" (int32) if greater than or equal to (unsigned or unordered)
                    case OpCodeKind.Bge_Un_S:               //Branch to "target" (int8) if greater than or equal to (unsigned or unordered), short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.8: branch on greater than
                    //..., value1, value2 -> ...
                    case OpCodeKind.Bgt:                    //Branch to "target" (int32) if greater than
                    case OpCodeKind.Bgt_S:                  //Branch to "target" (int8) if greater than, short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.9: branch on greater than, unsigned or unordered
                    //..., value1, value2 -> ...
                    case OpCodeKind.Bgt_Un:                 //Branch to "target" (int32) if greater than (unsigned or unordered)
                    case OpCodeKind.Bgt_Un_S:               //Branch to "target" (int8) if greater than (unsigned or unordered), short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.10: branch on less than or equal to
                    //..., value1, value2 -> ...
                    case OpCodeKind.Ble:                    //Branch to "target" (int32) if less than or equal to
                    case OpCodeKind.Ble_S:                  //Branch to "target" (int8) if less than or equal to, short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.11: branch on less than or equal to, unsigned or unordered
                    //..., value1, value2 -> ...
                    case OpCodeKind.Ble_Un:                 //Branch to "target" (int32) if less than or equal to (unsigned or unordered)
                    case OpCodeKind.Ble_Un_S:               //Branch to "target" (int8) if less than or equal to (unsigned or unordered), short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.12: branch on less than
                    //..., value1, value2 -> ...
                    case OpCodeKind.Blt:                    //Branch to "target" (int32) if less than
                    case OpCodeKind.Blt_S:                  //Branch to "target" (int8) if less than, short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.13: branch on less than, unsigned or unordered
                    //..., value1, value2 -> ...
                    case OpCodeKind.Blt_Un:                 //Branch to "target" (int32) if less than (unsigned or unordered)
                    case OpCodeKind.Blt_Un_S:               //Branch to "target" (int8) if less than (unsigned or unordered), short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.14: branch on not equal or unordered
                    //..., value1, value2 -> ...
                    case OpCodeKind.Bne_Un:                 //Branch to "target" (int32) if unequal or unordered
                    case OpCodeKind.Bne_Un_S:               //Branch to "target" (int8) if unequal or unordered, short form
                    {
                        IntegerSlot value2;
                        IntegerSlot value1;

                        Pop(out value2, out value1);

                        if (!(bool) value1.Equals(value2).Value)
                            MoveTo((ILInstruction) instr.Operand);
                        else
                            MoveNext();
                        break;
                    }

                    //III.3.15: unconditional branch
                    //... -> ...
                    case OpCodeKind.Br:                     //Branch to "target" (int32)
                    case OpCodeKind.Br_S:                   //Branch to "target" (int8), short form
                        MoveTo((ILInstruction) instr.Operand);
                        break;

                    //III.3.16: breakpoint instruction
                    //... -> ...
                    case OpCodeKind.Break:                  //Inform a debugger that a breakpoint has been reached
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.17: branch on false, null, or zero
                    //..., value -> ...
                    case OpCodeKind.Brfalse:                //Branch to "target" (int32) if "value" is zero (false/null)
                    case OpCodeKind.Brfalse_S:              //Branch to "target" (int8) if "value" is zero (false/null), short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.18: branch on non-false or non-null
                    //..., value -> ...
                    case OpCodeKind.Brtrue:                 //Branch to "target" (int32) if "value" is non-zero (true/non-null)
                    case OpCodeKind.Brtrue_S:               //Branch to "target" (int8) if "value" is non-zero (true/non-null), short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.19: call a method
                    //arg0, arg1 ... argN -> ..., retVal (not always returned)
                    case OpCodeKind.Call:                   //Call method described by "method"
                    {
                        var metadataMethod = (MetadataMethodInfo) instr.Operand;

                        var args = new List<Slot>();

                        for (var i = metadataMethod.GetParameters().Length - 1; i >= 0; i--)
                            args.Insert(0, Pop());

                        Slot instance = null;

                        if (!metadataMethod.Attributes.HasFlag(MethodAttributes.Static))
                            instance = Pop();

                        var result = VirtualMachine.EvaluatorContext.CallMethod(instance, metadataMethod, args.ToArray());
                        Push(result);
                        MoveNext();
                    //III.3.20: indirect method call
                    //..., arg0, arg1, argN, ftn -> ..., retVal (not always returned)
                    case OpCodeKind.Calli:                  //Call method indicated on the stack with arguments described by "callsitedescr"
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.21: compare equal
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Ceq:                    //Push 1 (of type int32) if "value1" equals "value2", else push 0
                        BoolOp(instr.Kind);
                        break;

                    //III.3.22: compare greater than
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Cgt:                    //Push 1 (of type int32) if "value1" > "value2", else push 0
                        BoolOp(instr.Kind);
                        break;

                    //III.3.23: compare greater than, unsigned or unordered
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Cgt_Un:                 //Push 1 (of type int32) if "value1" > "value2", unsigned or unordered, else push 0
                        BoolOp(instr.Kind);
                        break;

                    //III.3.24: check for a finite real number
                    //..., value1 -> ..., value
                    case OpCodeKind.Ckfinite:               //Throw ArithmeticException if "value" is not a finite number
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.25: compare less than
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Clt:                    //Push 1 (of type int32) if "value1" < "value2", else push 0
                        BoolOp(instr.Kind);
                        break;

                    //III.3.26: compare less than, unsigned or unordered
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Clt_Un:                 //Push 1 (of type int32) if "value1" < "value2", unsigned or unordered, else push 0
                        BoolOp(instr.Kind);
                        break;

                    #region III.3.27: Conv

                    //III.3.27: data conversion
                    //..., value -> ..., result
                    case OpCodeKind.Conv_I1:                //Convert to int8, pushing int32 on stack
                        ConvOp(CorElementType.I1, MathFlags.Signed);
                        break;

                    case OpCodeKind.Conv_I2:                //Convert to int16, pushing int32 on stack
                        ConvOp(CorElementType.I2, MathFlags.Signed);
                        break;

                    case OpCodeKind.Conv_I4:                //Convert to int32, pushing int32 on stack
                        ConvOp(CorElementType.I4, MathFlags.Signed);
                        break;

                    case OpCodeKind.Conv_I8:                //Convert to int64, pushing int64 on stack
                        ConvOp(CorElementType.I8, MathFlags.Signed);
                        break;

                    case OpCodeKind.Conv_R4:                //Convert to float32, pushing F on stack
                        ConvOp(CorElementType.R4, MathFlags.Signed);
                        break;

                    case OpCodeKind.Conv_R8:                //Convert to float64, pushing F on stack
                        ConvOp(CorElementType.R8, MathFlags.Signed);
                        break;

                    case OpCodeKind.Conv_U1:                //Convert to unsigned int8, pushing int32 on stack
                        ConvOp(CorElementType.U1, MathFlags.Signed);
                        break;

                    case OpCodeKind.Conv_U2:                //Convert to unsigned int16, pushing int32 on stack
                        ConvOp(CorElementType.U2, MathFlags.Signed);
                        break;

                    case OpCodeKind.Conv_U4:                //Convert to unsigned int32, pushing int32 on stack
                        ConvOp(CorElementType.U4, MathFlags.Signed);
                        break;

                    case OpCodeKind.Conv_U8:                //Convert to unsigned int64, pushing int64 on stack
                        ConvOp(CorElementType.U8, MathFlags.Signed);
                        break;

                    case OpCodeKind.Conv_I:                 //Convert to native int, pushing native int on stack
                        ConvOp(CorElementType.I, MathFlags.Signed);
                        break;

                    case OpCodeKind.Conv_U:                 //Convert to native unsigned int, pushing native int on stack
                        ConvOp(CorElementType.U, MathFlags.Signed);
                        break;

                    case OpCodeKind.Conv_R_Un:              //Convert unsigned integer to floating-point, pushing F on stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    #endregion
                    #region III.3.28: Conv_Ovf

                    //III.3.28: data conversion with overflow detection
                    //..., value -> ..., result
                    case OpCodeKind.Conv_Ovf_I1:            //Convert to an int8 (on the stack as int32) and throw an exception on overflow
                        ConvOp(CorElementType.I1, MathFlags.Signed | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_I2:            //Convert to an int16 (on the stack as int32) and throw an exception on overflow
                        ConvOp(CorElementType.I2, MathFlags.Signed | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_I4:            //Convert to an int32 (on the stack as int32) and throw an exception on overflow
                        ConvOp(CorElementType.I4, MathFlags.Signed | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_I8:            //Convert to an int64 (on the stack as int64) and throw an exception on overflow
                        ConvOp(CorElementType.I8, MathFlags.Signed | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_U1:            //Convert to an unsigned int8 (on the stack as int32) and throw an exception on overflow
                        ConvOp(CorElementType.U1, MathFlags.Signed | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_U2:            //Convert to an unsigned int16 (on the stack as int32) and throw an exception on overflow
                        ConvOp(CorElementType.U2, MathFlags.Signed | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_U4:            //Convert to an unsigned int32 (on the stack as int32) and throw an exception on overflow
                        ConvOp(CorElementType.U4, MathFlags.Signed | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_U8:            //Convert to an unsigned int64 (on the stack as int64) and throw an exception on overflow
                        ConvOp(CorElementType.U8, MathFlags.Signed | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_I:             //Convert to a native int (on the stack as native int) and throw an exception on overflow
                        ConvOp(CorElementType.I, MathFlags.Signed | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_U:             //Convert to a native unsigned int (on the stack as native int) and throw an exception on overflow
                        ConvOp(CorElementType.I, MathFlags.Signed | MathFlags.Checked);
                        break;

                    #endregion
                    #region III.3.29: Conv_Ovf_Un

                    //III.3.29: unsigned data conversion with overflow detection
                    //..., value -> ..., result
                    case OpCodeKind.Conv_Ovf_I1_Un:         //Convert unsigned to an int8 (on the stack as int32) and throw an exception on overflow. 
                        ConvOp(CorElementType.I1, MathFlags.Unsigned | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_I2_Un:         //Convert unsigned to an int16 (on the stack as int32) and throw an exception on overflow
                        ConvOp(CorElementType.I2, MathFlags.Unsigned | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_I4_Un:         //Convert unsigned to an int32 (on the stack as int32) and throw an exception on overflow
                        ConvOp(CorElementType.I4, MathFlags.Unsigned | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_I8_Un:         //Convert unsigned to an int64 (on the stack as int64) and throw an exception on overflow
                        ConvOp(CorElementType.I8, MathFlags.Unsigned | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_U1_Un:         //Convert unsigned to an unsigned int8 (on the stack as int32) and throw an exception on overflow
                        ConvOp(CorElementType.U1, MathFlags.Unsigned | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_U2_Un:         //Convert unsigned to an unsigned int16 (on the stack as int32) and throw an exception on overflow
                        ConvOp(CorElementType.U2, MathFlags.Unsigned | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_U4_Un:         //Convert unsigned to an unsigned int32 (on the stack as int32) and throw an exception on overflow
                        ConvOp(CorElementType.U4, MathFlags.Unsigned | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_U8_Un:         //Convert unsigned to an unsigned int64 (on the stack as int64) and throw an exception on overflow
                        ConvOp(CorElementType.U8, MathFlags.Unsigned | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_I_Un:          //Convert unsigned to a native int (on the stack as native int) and throw an exception on overflow
                        ConvOp(CorElementType.I, MathFlags.Unsigned | MathFlags.Checked);
                        break;

                    case OpCodeKind.Conv_Ovf_U_Un:          //Convert unsigned to a native unsigned int (on the stack as native int) and throw an exception on overflow
                        ConvOp(CorElementType.U, MathFlags.Unsigned | MathFlags.Checked);
                        break;

                    #endregion

                    //III.3.30: copy data from memory to memory
                    //..., destaddr, srcaddr, size -> ...
                    case OpCodeKind.Cpblk:                  //Copy data from memory to memory
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.31: divide values
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Div:                    //Divide two values to return a quotient or floating-point result
                        MathOp(instr.Kind, MathFlags.Signed);
                        break;

                    //III.3.32: divide integer values, unsigned
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Div_Un:                 //Divide two values, unsigned, returning a quotient
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.33: duplicate the top value of the stack
                    //..., value -> ..., value, value
                    case OpCodeKind.Dup:                    //Duplicate the value on the top of the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.34: end exception handling filter clause
                    //..., value -> ...
                    case OpCodeKind.Endfilter:              //End an exception handling filter clause
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.35: end the finally or fault clause of an exception block
                    //... -> ...
                    case OpCodeKind.Endfinally:             //End finally clause of an exception block
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.36: initialize a block of memory to a value
                    //..., addr, value, size -> ...
                    case OpCodeKind.Initblk:                //Set all bytes in a block of memory to a given byte value
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.37: jump to method
                    //... -> ...
                    case OpCodeKind.Jmp:                    //Exit current method and jump to the specified method
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    #region III.3.38: Ldarg

                    //III.3.38: load argument onto the stack
                    //... -> ..., value
                    case OpCodeKind.Ldarg:                  //Load argument numbered "num" (int16) onto the stack
                    case OpCodeKind.Ldarg_S:                //Load argument numbered "num" (int8) onto the stack, short form
                        Ldarg(((ILVariable) instr.Operand).Index);
                        break;

                    case OpCodeKind.Ldarg_0:                //Load argument 0 onto the stack
                        Ldarg(0);
                        break;

                    case OpCodeKind.Ldarg_1:                //Load argument 1 onto the stack
                        Ldarg(1);
                        break;

                    case OpCodeKind.Ldarg_2:                //Load argument 2 onto the stack
                        Ldarg(2);
                        break;

                    case OpCodeKind.Ldarg_3:                //Load argument 3 onto the stack
                        Ldarg(3);
                        break;

                    #endregion

                    //III.3.39: load an argument address
                    //... -> ..., address of argument number argNum
                    case OpCodeKind.Ldarga:                 //Fetch the address of argument "argNum" (int16)
                    case OpCodeKind.Ldarga_S:               //Fetch the address of argument "argNum" (int8), short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    #region III.3.40: Ldc

                    //III.3.40: load numeric constant
                    //... -> ..., num
                    case OpCodeKind.Ldc_I4:                 //Push "num" (int32) of type int32 onto the stack as int32
                        Ldc((int) instr.Operand);
                        break;

                    case OpCodeKind.Ldc_I8:                 //Push "num" (int64) of type int64 onto the stack as int64
                        Ldc((long) instr.Operand);
                        break;

                    case OpCodeKind.Ldc_R4:                 //Push "num" (float32) of type float32 onto the stack as F
                        Ldc((float) instr.Operand);
                        break;

                    case OpCodeKind.Ldc_R8:                 //Push "num" (float64) of type float64 onto the stack as F
                        Ldc((double) instr.Operand);
                        break;

                    case OpCodeKind.Ldc_I4_0:               //Push 0 onto the stack as int32
                        Ldc(0);
                        break;

                    case OpCodeKind.Ldc_I4_1:               //Push 1 onto the stack as int32
                        Ldc(1);
                        break;

                    case OpCodeKind.Ldc_I4_2:               //Push 2 onto the stack as int32
                        Ldc(2);
                        break;

                    case OpCodeKind.Ldc_I4_3:               //Push 3 onto the stack as int32
                        Ldc(3);
                        break;

                    case OpCodeKind.Ldc_I4_4:               //Push 4 onto the stack as int32
                        Ldc(4);
                        break;

                    case OpCodeKind.Ldc_I4_5:               //Push 5 onto the stack as int32
                        Ldc(5);
                        break;

                    case OpCodeKind.Ldc_I4_6:               //Push 6 onto the stack as int32
                        Ldc(6);
                        break;

                    case OpCodeKind.Ldc_I4_7:               //Push 7 onto the stack as int32
                        Ldc(7);
                        break;

                    case OpCodeKind.Ldc_I4_8:               //Push 8 onto the stack as int32
                        Ldc(8);
                        break;

                    case OpCodeKind.Ldc_I4_M1:              //Push -1 onto the stack as int32
                        Ldc(-1);
                        break;

                    case OpCodeKind.Ldc_I4_S:               //Push "num" (int8) onto the stack as int32, short form
                        Ldc((sbyte) instr.Operand);
                        break;

                    #endregion

                    //III.3.41: load method pointer
                    //... -> ..., ftn
                    case OpCodeKind.Ldftn: //Push a pointer to a method referenced by "method", on the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.42: load value indirect onto the stack
                    //..., addr -> ..., value
                    case OpCodeKind.Ldind_I1:               //Indirect load value of type int8 as int32 on the stack
                    case OpCodeKind.Ldind_I2:               //Indirect load value of type int16 as int32 on the stack
                    case OpCodeKind.Ldind_I4:               //Indirect load value of type int32 as int32 on the stack
                    case OpCodeKind.Ldind_I8:               //Indirect load value of type int64 as int64 on the stack
                    case OpCodeKind.Ldind_U1:               //Indirect load value of type unsigned int8 as int32 on the stack
                    case OpCodeKind.Ldind_U2:               //Indirect load value of type unsigned int16 as int32 on the stack
                    case OpCodeKind.Ldind_U4:               //Indirect load value of type unsigned int32 as int32 on the stack
                    case OpCodeKind.Ldind_R4:               //Indirect load value of type float32 as F on the stack
                    case OpCodeKind.Ldind_R8:               //Indirect load value of type float64 as F on the stack
                    case OpCodeKind.Ldind_I:                //Indirect load value of type native int as native int on the stack
                    case OpCodeKind.Ldind_Ref:              //Indirect load value of type object ref as O on the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    #region III.3.43: Ldloc

                    //III.3.43: load local variable onto the stack
                    //... -> ..., value
                    case OpCodeKind.Ldloc:                  //Load local variable of index "indx" (unsigned int16) onto stack
                        Ldloc((short) instr.Operand);
                        break;

                    case OpCodeKind.Ldloc_S:                //Load local variable of index "indx" (unsigned int8) onto stack, short form
                        Ldloc((sbyte) instr.Operand);
                        break;

                    case OpCodeKind.Ldloc_0:                //Load local variable 0 onto stack
                        Ldloc(0);
                        break;

                    case OpCodeKind.Ldloc_1:                //Load local variable 1 onto stack
                        Ldloc(1);
                        break;

                    case OpCodeKind.Ldloc_2:                //Load local variable 2 onto stack
                        Ldloc(2);
                        break;

                    case OpCodeKind.Ldloc_3:                //Load local variable 3 onto stack
                        Ldloc(3);
                        break;

                    #endregion

                    //III.3.44: load local variable address
                    //... -> ..., address
                    case OpCodeKind.Ldloca:                 //Load address of local variable with index "indx" (unsigned int16)
                    case OpCodeKind.Ldloca_S:               //Load address of local variable with index "indx" (unsigned int8), short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.45: load a null pointer
                    //... -> ..., null value
                    case OpCodeKind.Ldnull:                 //Push a null reference on the stack
                        Push(new NullSlot());
                        MoveNext();
                        break;

                    //III.3.46:  exit a protected region of code
                    //... ->
                    case OpCodeKind.Leave:                  //Exit a protected region of code ("target": int32)
                    case OpCodeKind.Leave_S:                //Exit a protected region of code, short form ("target": int8)
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.47: allocate space in the local dynamic memory pool
                    //size -> address
                    case OpCodeKind.Localloc:               //Allocate space from the local memory pool
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.48: multiply values
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Mul:                    //Multiply values
                        MathOp(instr.Kind, MathFlags.Signed);
                        break;

                    //III.3.49: multiply integer values with overflow check
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Mul_Ovf:                //Multiply signed integer values. Signed result shall fit in same size
                    case OpCodeKind.Mul_Ovf_Un:             //Multiply unsigned integer values. Unsigned result shall fit in same size
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.50: negate
                    //..., value -> ..., result
                    case OpCodeKind.Neg:                    //Negate value
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.51: no operation
                    //... -> ...
                    case OpCodeKind.Nop:                    //Do nothing
                        MoveNext();
                        break;

                    //III.3.52: bitwise complement
                    //..., value -> ..., result
                    case OpCodeKind.Not:                    //Bitwise complement
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.53: bitwise OR
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Or:                     //Bitwise OR of two integer values, returns an integer
                        BoolOp(instr.Kind);
                        break;

                    //III.3.54: remove the top element of the stack
                    //..., value -> ...
                    case OpCodeKind.Pop:                    //Pop "value" from the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.55: compute remainder
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Rem:                    //Remainder when dividing one value by another
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.56: compute integer remainder, unsigned
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Rem_Un:                 //Remainder when dividing one unsigned value by another
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.57: return from method
                    //retVal on callee evaluation stack (not always present) -> ..., retVal on caller evaluation stack (not always present)
                    case OpCodeKind.Ret:                    //Return from method, possibly with a value
                    {
                        var result = Pop();
                        MoveNext();
                        return result;
                    }

                    //III.3.58: shift integer left
                    //..., value, shiftAmount -> ..., result
                    case OpCodeKind.Shl:                    //Shift an integer left (shifting in zeros), return an integer
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.59: shift integer right
                    //..., value, shiftAmount -> ..., result
                    case OpCodeKind.Shr:                    //Shift an integer right (shift in sign), return an integer
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.60: shift integer right, unsigned
                    //..., value, shiftAmount -> ..., result
                    case OpCodeKind.Shr_Un:                 //Shift an integer right (shift in zero), return an integer
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.61: store a value in an argument slot
                    //..., value -> ...
                    case OpCodeKind.Starg:                  //Store "value" (unsigned int16) to the argument numbered num
                    case OpCodeKind.Starg_S:                //Store "value" (unsigned int8) to the argument numbered num, short form
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.62: store value indirect from stack
                    //..., addr, val -> ...
                    case OpCodeKind.Stind_I1:               //Store value of type int8 into memory at address
                    case OpCodeKind.Stind_I2:               //Store value of type int16 into memory at address
                    case OpCodeKind.Stind_I4:               //Store value of type int32 into memory at address
                    case OpCodeKind.Stind_I8:               //Store value of type int64 into memory at address
                    case OpCodeKind.Stind_R4:               //Store value of type float32 into memory at address
                    case OpCodeKind.Stind_R8:               //Store value of type float64 into memory at address
                    case OpCodeKind.Stind_I:                //Store value of type native int into memory at address
                    case OpCodeKind.Stind_Ref:              //Store value of type object ref (type O) into memory at address
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    #region III.3.63: Stloc

                    //III.3.63: pop value from stack to local variable
                    //..., value -> ...
                    case OpCodeKind.Stloc:                  //Pop a value from stack into local variable "indx" (unsigned int16)
                        Stloc((short) instr.Operand);
                        break;

                    case OpCodeKind.Stloc_S:                //Pop a value from stack into local variable "indx" (unsigned int8), short form
                        Stloc((sbyte) instr.Operand);
                        break;

                    case OpCodeKind.Stloc_0:                //Pop a value from stack into local variable 0
                        Stloc(0);
                        break;

                    case OpCodeKind.Stloc_1:                //Pop a value from stack into local variable 1
                        Stloc(1);
                        break;

                    case OpCodeKind.Stloc_2:                //Pop a value from stack into local variable 2
                        Stloc(2);
                        break;

                    case OpCodeKind.Stloc_3:                //Pop a value from stack into local variable 3
                        Stloc(3);
                        break;

                    #endregion

                    //III.3.64: subtract numeric values
                    //..., value1, value2 -> ...
                    case OpCodeKind.Sub:                    //Subtract value2 from value1, returning a new value
                        MathOp(instr.Kind, MathFlags.Signed);
                        break;

                    //III.3.65: subtract integer values, checking for overflow
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Sub_Ovf:                //Subtract native int from a native int. Signed result shall fit in same size
                    case OpCodeKind.Sub_Ovf_Un:             //Subtract native unsigned int from a native unsigned int. Unsigned result shall fit in same size
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.66: table switch based on value
                    //..., value -> ...
                    case OpCodeKind.Switch:                 //Jump to one of n values (numTargets: unsigned int32, targets: target1 (int32) ... targetN (int32))
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.3.67: bitwise XOR
                    //..., value1, value2 -> ..., result
                    case OpCodeKind.Xor:                    //Bitwise XOR of integer values, returns an integer
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    #endregion
                    #region III.4: Object Model Instructions

                    //III.4.1: convert a boxable value to its boxed form
                    //..., val -> ..., obj
                    case OpCodeKind.Box:                    //Convert a boxable value to its boxed form
                    {
                        var value = Pop();
                        var box = new BoxSlot(value);

                        Push(box);
                        MoveNext();
                        break;
                    }

                    //III.4.2: call a method associated, at runtime, with an object
                    //..., obj, arg1, ..., argN -> ..., returnVal (not always returned)
                    case OpCodeKind.Callvirt:               //Call a method associated with an object
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.3: cast an object to a class
                    //..., obj -> ..., obj2
                    case OpCodeKind.Castclass:              //Cast "obj" to "typeTok"
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.4: copy a value from one address to anot her 
                    //..., dest, src -> ...
                    case OpCodeKind.Cpobj:                  //Copy a value type from "src" to "dest"
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.5: initialize the value at an address
                    //..., dest -> ...
                    case OpCodeKind.Initobj: //Initialize the value at address "dest"
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.6: test if an object is an instance of a class or interface
                    //..., obj -> ..., result
                    case OpCodeKind.Isinst:                 //Test if "obj" is an instance of "typeTok", returning null or an instance of that class or interface
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.7: load element from array
                    //..., array, index -> ..., value
                    case OpCodeKind.Ldelem: //Load the element at "index" onto the top of the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.8: load an element of an array
                    //..., array, index -> ..., value
                    case OpCodeKind.Ldelem_I1:              //Load the element with type int8 at "index" onto the top of the stack as an int32
                    case OpCodeKind.Ldelem_I2:              //Load the element with type int16 at "index" onto the top of the stack as an int32
                    case OpCodeKind.Ldelem_I4:              //Load the element with type int32 at "index" onto the top of the stack as an int32
                    case OpCodeKind.Ldelem_I8:              //Load the element with type int64 at "index" onto the top of the stack as an int64
                    case OpCodeKind.Ldelem_U1:              //Load the element with type unsigned int8 at "index" onto the top of the stack as an int32
                    case OpCodeKind.Ldelem_U2:              //Load the element with type unsigned int16 at "index" onto the top of the stack as an int32
                    case OpCodeKind.Ldelem_U4:              //Load the element with type unsigned int32 at "index" onto the top of the stack as an int32
                    case OpCodeKind.Ldelem_R4:              //Load the element with type float32 at "index" onto the top of the stack as an F
                    case OpCodeKind.Ldelem_R8:              //Load the element with type float64 at "index" onto the top of the stack as an F
                    case OpCodeKind.Ldelem_I:               //Load the element with type native int at "index" onto the top of the stack as a native int
                    case OpCodeKind.Ldelem_Ref:             //Load the element at "index" onto the top of the stack as an O. The type of the O is the same as the element type of the array pushed on the CIL stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.9: load address of an element of an array
                    //..., array, index -> ..., address
                    case OpCodeKind.Ldelema:                //Load the address of element at "index" onto the top of the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.10: load field of an object
                    //..., obj -> ..., value
                    case OpCodeKind.Ldfld:                  //Push the value of "field" of object (or value type) "obj", onto the stack
                    {
                        //todo: it could maybe be a memberref?
                        var metadataFieldInfo = (MetadataFieldInfo) instr.Operand;

                        Slot instance = null;

                        if (!metadataFieldInfo.Attributes.HasFlag(FieldAttributes.Static))
                            instance = Pop();

                        var value = VirtualMachine.EvaluatorContext.GetFieldValue(instance, metadataFieldInfo);
                        Push(value);
                        MoveNext();
                        break;
                    }

                    //III.4.11: load field address 
                    //..., obj -> ..., address
                    case OpCodeKind.Ldflda:                 //Push the address of "field" of object "obj" on the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.12: load the length of an array
                    //..., array -> ..., length
                    case OpCodeKind.Ldlen:                  //Push the "length" (of type native unsigned int) of array on the stack
                    {
                        var array = (ArraySlot) Pop();
                        var length = Slot.New(array.Length);

                        Push(length);
                        MoveNext();
                        break;
                    }

                    //III.4.13: copy a value from an address to the stack
                    //..., src -> ..., val
                    case OpCodeKind.Ldobj:                  //Copy the value stored at address "src" to the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.14: load static field of a class
                    //... -> ..., value
                    case OpCodeKind.Ldsfld: //Push the value of "field" on the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.15: load static field address
                    //... -> ..., address
                    case OpCodeKind.Ldsflda:                //Push the address of the static field, "field", on the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.16: load a literal string
                    //... -> ..., string
                    case OpCodeKind.Ldstr:                  //Push a string object for the literal "string"
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.17: load the runtime representation of a metadata token
                    //... -> ..., RuntimeHandle
                    case OpCodeKind.Ldtoken:                //Convert metadata "token" to its runtime representation
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.18: load a virtual method pointer
                    //..., object -> ..., ftn
                    case OpCodeKind.Ldvirtftn:              //Push address of virtual method "method" on the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.19: push a typed reference on the stack
                    //..., ptr -> ..., typedRef
                    case OpCodeKind.Mkrefany:               //Push a typed reference to "ptr" of type "class" onto the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.20: create a zero-based, one-dimensional array
                    //..., numElems -> ..., array
                    case OpCodeKind.Newarr:                 //Create a new array with elements of type "etype"
                    //III.4.21: create a new object
                    //..., arg1, ... argN -> ..., obj
                    case OpCodeKind.Newobj:                 //Allocate an uninitialized object or value type and call "ctor"
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.22: load the type out of a typed reference
                    //..., TypedRef -> ..., type
                    case OpCodeKind.Refanytype:             //Push the type token stored in a typed reference
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.23: load the address out of a typed reference 
                    //..., TypedRef -> ..., address
                    case OpCodeKind.Refanyval:              //Push the address stored in a typed reference
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.24: rethrow the current exception
                    //... -> ...
                    case OpCodeKind.Rethrow:                //Rethrow the current exception
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.25: load the size, in bytes,of a type
                    //... -> ..., size (4 bytes, unsigned)
                    case OpCodeKind.Sizeof:                 //Push the size, in bytes, of a type as an unsigned int32
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.26: store element to array
                    //..., array, index, value -> ...
                    case OpCodeKind.Stelem:                 //Replace array element at "index" with the "value" on the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.27: store an element of an array
                    //..., array, index, value -> ...,
                    case OpCodeKind.Stelem_I1:              //Replace "array" element at "index" with the int8 value on the stack
                    case OpCodeKind.Stelem_I2:              //Replace "array" element at "index" with the int16 value on the stack
                    case OpCodeKind.Stelem_I4:              //Replace "array" element at "index" with the int32 value on the stack
                    case OpCodeKind.Stelem_I8:              //Replace "array" element at "index" with the int64 value on the stack
                    case OpCodeKind.Stelem_R4:              //Replace "array" element at "index" with the float32 value on the stack
                    case OpCodeKind.Stelem_R8:              //Replace "array" element at "index" with the float64 value on the stack
                    case OpCodeKind.Stelem_I:               //Replace "array" element at "index" with the native int value on the stack
                    case OpCodeKind.Stelem_Ref:             //Replace "array" element at "index" with the ref value on the stack
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.28: store into a field of an object
                    //..., obj, value -> ..., 
                    case OpCodeKind.Stfld:                  //Replace the "value" of "field" of the object "obj" with "value"
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.29: store a value at an address
                    //..., dest, src -> ...
                    case OpCodeKind.Stobj:                  //Store a value of type "typeTok" at an address
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.30: store a static field of a class 
                    //..., val -> ...
                    case OpCodeKind.Stsfld:                 //Replace the value of "field" with "val".
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.31: throw an exception
                    //..., object -> ...
                    case OpCodeKind.Throw:                  //Throw an exception
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.32: convert boxed value type to its raw form
                    //..., obj -> ..., valueTypePtr
                    case OpCodeKind.Unbox:                  //Extract a value-type from "obj", its boxed representation
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");

                    //III.4.33: convert boxed type to value
                    //..., obj -> ..., value or obj
                    case OpCodeKind.Unbox_Any:              //Extract a value-type from obj, its boxed representation
                        throw new NotImplementedException($"Handling {instr.Kind} is not implemented");
                    #endregion
        private void Ldarg(int index)
        {
            //Create a container for storing the value in argument "index" and push the container onto the evaluation stack
            var argSlot = Arguments[index];
            Push(argSlot);
            MoveNext();
        }

        private void Ldc(object value)
        {
            var slot = Slot.New(value);
            Push(slot);
            MoveNext();
        }

        private void Stloc(int index)
        {
            var slot = Pop();

            Locals[index] = slot;
            MoveNext();
        }

        private void Ldloc(int index)
        {
            var slot = Locals[index];

            //I don't know what would happen if we wanted to have a "ref" variable. Could you "ref" to the local without needing
            //to store it?
            Debug.Assert(slot != null);
            Push(slot);
            MoveNext();
        }

            switch (kind)
            {
                case OpCodeKind.Add:
                case OpCodeKind.Add_Ovf:
                case OpCodeKind.Add_Ovf_Un:
                    result = value1.Add(value2, flags);
                    break;

                case OpCodeKind.Sub:
                case OpCodeKind.Sub_Ovf:
                case OpCodeKind.Sub_Ovf_Un:
                    result = value1.Subtract(value2, flags);
                    break;

                case OpCodeKind.Mul:
                case OpCodeKind.Mul_Ovf:
                case OpCodeKind.Mul_Ovf_Un:
                    result = value1.Multiply(value2, flags);
                    break;
        private void BoolOp(OpCodeKind kind)
        {
            //There's no such thing as bool. Boolean values compile to either Ldc_I4_0 or Ldc_I4_1, which we
            //wrap as IntegerSlot

            NumericSlot value2;
            NumericSlot value1;
            Pop(out value2, out value1);

            var result = kind switch
            {
                OpCodeKind.And => value1.And(value2),
                OpCodeKind.Or => value1.Or(value2),
                OpCodeKind.Ceq => value1.Equals(value2),
                OpCodeKind.Clt => value1.LessThan(value2),
                OpCodeKind.Cgt => value1.GreaterThan(value2),
                _ => throw new UnknownEnumValueException(kind)
            };

            Push(result);
            MoveNext();
        }
        private void Pop<T2, T1>(out T2 value2, out T1 value1)
            where T1 : Slot
            where T2: Slot
        {
            value2 = (T2) Pop();
            value1 = (T1) Pop();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int MoveNext() => currentInstructionIndex++;

        private void MoveTo(ILInstruction instr)
        {
            var targetIndex = Array.IndexOf(instructions, instr);
            Debug.Assert(targetIndex != -1);
            currentInstructionIndex = targetIndex;
        }
    }
}
