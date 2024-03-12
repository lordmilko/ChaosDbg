# x64 Instruction Format

An x64 instruction can consist of up to 15 bytes. Not all bytes need to be used. Instructions can contain the following components

Prefixes | OpCode | ModR/M | SIB | Displacement | Immediate

Suppose we have mov r10,rcx
Hex:    4C       8B       D1
Binary: 01001100 10001011 11010001

This instruction consists of a Prefix, OpCode and ModR/M byte

4C is a "REX" (Register Extension) prefix. In x64, all of the "new stuff" that was introduced
has this prefix, which provides alternate interpretations of the other bytes that will follow in
the rest of the instruction (e.g. new operands like r10 would require some REX bits in order to be
referred to). All REX instructions start with 4 (0100) followed by the bits for W R X and B
In 4C, W and R are set
- W means the operand size is 64 bit
- R changes the meaning of the Reg field in ModR/M below to have an alternate meaning
- X changes the meaning of the Index field in the SIB byte
- B if set, either changes the meaning of the Base field in the SIB byte, or changes the meaning of the R/M field in ModR/M below to have an alternate meaning

8B identifies the opcode, which is "mov r64, r/m64". Operand 0 is a 64-bit register, and operand 1 is
a 64-bit register or memory location

D1 is the ModR/M section (named after the 3 sections it contains). ModR/M is used to describe how the operands
to the opcode "work", or in simple scenarios can provide all the information about the operands to the opcode
entirely. ModR/M consists of three sections. In the case of our value D1 (11010001) these bits are split up as follows

Mod | Reg | R/M
 11   010   001

Mod specifies the Mode, Reg specifies a Register, and R/M specifies a Register or Memory location
Mod=11 means that both operands are registers

Normally, a Reg value of 010 indicates the register will be rdx. REX.R=1 however causes it to be interpreted
as r10 instead. (see the right-most grey columns of the 32/64-bit ModR/M Byte table http://ref.x86asm.net/coder64.html)

In terms of a mov instruction, Reg is the "destination" while "R/M" is the "source"

001 in the R/M field identifies rcx. If REX.B=1, R/M would be a different register

Thus, from ModR/M is interpreted as follows

Mode                 | Register | Register/Memory
Register-to-Register   r10        rcx

Hence, mov r10, rcx

----------------------------------------------------------------

Suppose we wanted to encode this instruction backwards: mov rcx, r10

R10, the second operand, will be in the R/M field. Since r10 is a new x64 specific register,
we set REX.B=1. Thus REX will be

01001001 = 49

We can continue to use the same opcode 8B for mov

Mod/RM will basically be the same, except Reg and R/M are swapped:

Mod | Reg | R/M
 11   001   010

= CA

Thus, mov rcx, r10 encodes to 49 8B CA

----------------------------------------------------------------

It's also possible to use opcode 89 instead. 89 takes a register or memory location (source) as its FIRST
operand, and a register (destination) as its second. If you were to encode the instruction using the 89 opcode,
you would have the bytes

And so, the following instructions are both the same
1.  4C 8B D1 mov r10,rcx
2.  49 89 CA mov r10,rcx

as are these
3.  49 8B CA mov rcx,r10
4.  4C 89 D1 mov rcx,r10

observe that there is symmetry between 1+4 and 2+3. 4C 8B D1 is mov r10,rcx, but 89 simply reverses the meaning
of the operands, giving mov rcx,r10 instead

See also: https://staffwww.fullcoll.edu/aclifton/cs241/lecture-instruction-format.html
Online decoder: https://defuse.ca/online-x86-assembler.htm
Opcode reference: http://ref.x86asm.net/coder64.html
