using System;
using System.Runtime.InteropServices;
using ChaosLib;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace ChaosDbg.Hook
{
    public delegate IntPtr LoadLibraryExWDelegate(
        [In, MarshalAs(UnmanagedType.LPWStr)] string lpLibFileName, [In] IntPtr hFile, [In] LoadLibraryFlags dwFlags);

    class LoadLibraryExWConditionalHook : ConditionalHook<LoadLibraryExWDelegate>
    {
        private IntPtr pLibraryName;

        public LoadLibraryExWConditionalHook(string libraryName, IntPtr pOriginal, LoadLibraryExWDelegate hookDelegate) : base(pOriginal, hookDelegate)
        {
            try
            {
                pLibraryName = Marshal.StringToHGlobalUni(libraryName);
                var trampolineSize = GuessTrampolineSize(unicode: true);

                pTrampoline = AllocateTrampoline(trampolineSize);

                Assembler c;

                if (IntPtr.Size == 4)
                    c = WriteTrampoline32(unicode: true);
                else
                    c = WriteTrampoline64(unicode: true);

                CreateTrampoline(c, trampolineSize);

                Debug.WriteLine("created trampoline " + pTrampoline.ToString("X"));
            }
            catch
            {
                if (pLibraryName != IntPtr.Zero)
                    Marshal.FreeHGlobal(pLibraryName);

                if (pTrampoline != IntPtr.Zero)
                    Kernel32.VirtualFreeEx(Kernel32.GetCurrentProcess(), pTrampoline);

                throw;
            }
        }

        private Assembler WriteTrampoline32(bool unicode)
        {
            var c = new Assembler(32);

            var loopStart = c.CreateLabel("start");
            var fail = c.CreateLabel("fail");
            var success = c.CreateLabel("success");
            var end = c.CreateLabel("end");

            var callCustom = c.CreateLabel("callCustom");

            const int lpLibFileName = 8;
            const int hFile = 0xC;
            const int dwFlags = 0x10;

            //Prolog
            c.push(ebp);
            c.mov(ebp, esp);

            c.mov(eax, __[ebp + lpLibFileName]);
            c.mov(ecx, (int) pLibraryName);
            c.nop(__dword_ptr[eax + eax + 0]);

            //When we're unicode, each char is 2 bytes wide and we use the whole dx register.
            //When we're not, each char is 1 byte wide and we use the 8-bit dl register

            void strcmp()
            {
                //Main loop
                //strcmp compares two bytes per loop iteration,
                //checking whether we've hit 0 in Str1 (lpProcName) after each byte compared

                c.Label(ref loopStart);

                //Compare byte 1
                c.mov(dl, __[eax]); //Load a character from lpProcName into dl
                c.cmp(dl, __[ecx]); //Compare with a character in "First"
                c.jnz(fail);        //If they're not equal, jump to fail

                //Is byte 1 \0?
                c.test(dl, dl);     //They were equal...but is dl \0? if so, that means they were both \0 and it's a success!
                c.jz(success);

                //Compare byte 2
                c.mov(dl, __[eax + 1]); //Load the next character from lpProcName into dl
                c.cmp(dl, __[ecx + 1]); //Compare with the next character in "First"
                c.jnz(fail);

                //Prepare for next loop and check is byte 2 \0?
                c.add(eax, 2); //Increment lpProcName by the two characters we compared
                c.add(ecx, 2); //Increment "First" by the two characters we compared
                c.test(dl, dl); //Regarding the comparison we just performed, they were equal, but were they \0? if so we're done and it's a success!
                c.jnz(loopStart);

                //Success!
                c.Label(ref success);
                c.xor(eax, eax); //Set eax to 0 (return value indicating equal)
                c.jmp(end);

                //Fail. Based on the result of the last comparison, (carry flag set by last cmp)
                //this sets eax to either 1 or -1
                c.Label(ref fail);
                c.sbb(eax, eax);
                c.or(eax, 1);
            }

            void wcscmp()
            {
                //Main loop
                //strcmp compares two bytes per loop iteration,
                //checking whether we've hit 0 in Str1 (lpProcName) after each byte compared

                c.Label(ref loopStart);

                //Compare byte 1
                c.mov(dx, __[eax]); //Load a character from lpProcName into dl
                c.cmp(dx, __[ecx]); //Compare with a character in "First"
                c.jnz(fail);        //If they're not equal, jump to fail

                //Is byte 1 \0?
                c.test(dx, dx);     //They were equal...but is dl \0? if so, that means they were both \0 and it's a success!
                c.jz(success);

                //Compare byte 2
                c.mov(dx, __[eax + 2]); //Load the next character from lpProcName into dl
                c.cmp(dx, __[ecx + 2]); //Compare with the next character in "First"
                c.jnz(fail);

                //Prepare for next loop and check is byte 2 \0?
                c.add(eax, 4); //Increment lpProcName by the two characters we compared
                c.add(ecx, 4); //Increment "First" by the two characters we compared
                c.test(dx, dx); //Regarding the comparison we just performed, they were equal, but were they \0? if so we're done and it's a success!
                c.jnz(loopStart);

                //Success!
                c.Label(ref success);
                c.xor(eax, eax); //Set eax to 0 (return value indicating equal)
                c.jmp(end);

                //Fail. Based on the result of the last comparison, (carry flag set by last cmp)
                //this sets eax to either 1 or -1
                c.Label(ref fail);
                c.sbb(eax, eax);
                c.or(eax, 1);
            }

            if (unicode)
                wcscmp();
            else
                strcmp();

            //If the strings were not equal (i.e. eax is not 0), call the original LoadLibrary.
            //Else, call our custom one
            c.Label(ref end);
            c.test(eax, eax);
            c.jz(callCustom); //If eax is 0 (success) call our custom handler

            //Call the default handler
            c.push(__dword_ptr[ebp + dwFlags]);
            c.push(__dword_ptr[ebp + hFile]);
            c.push(__dword_ptr[ebp + lpLibFileName]); //This is an important gotcha: if you just do __[ebp + lpLibFileName], Assembler.push(AssemblyMemoryOperand) will get upset because the Size is None. It needs to be set to Dword
            c.call((uint) pOriginal);

            //Cleanup for return
            c.pop(ebp);
            c.ret(12);

            c.Label(ref callCustom);
            c.push(__dword_ptr[ebp + dwFlags]);
            c.push(__dword_ptr[ebp + hFile]);
            c.push(__dword_ptr[ebp + lpLibFileName]);
            c.call((uint) pHook);

            //Cleanup for return
            c.pop(ebp);
            c.ret(12);

            return c;
        }

        private unsafe Assembler WriteTrampoline64(bool unicode)
        {
            var c = new Assembler(64);

            var end = c.CreateLabel("end");
            var callCustom = c.CreateLabel("callCustom");

            //Backup the old parameters. When you aren't using many registers, you can backup your parameters
            //to any volatile registers. However, this function uses all volatile registers.
            //r12-r15 are nonvolatile registers, so we can't use them. Thus, we must
            //instead backup these values to the stack
            c.push(rcx); //lpLibFileName
            c.push(rdx); //hFile
            c.push(r8); //dwFlags

            //Load the name we're comparing against
            c.mov(rdx, (ulong) (void*) pLibraryName);

            void wcscmp()
            {
                var loopStart = c.CreateLabel("loopStart");

                c.movzx(eax, __word_ptr[rdx]); //Move the first character from "First" into eax
                c.movzx(r8d, __word_ptr[rcx]); //Move the first character from lpLibFileName into r8d
                c.sub(r8d, eax); //Subtract these two characters
                c.jnz(end); //If the characters aren't the same, it's already over

                c.sub(rcx, rdx);

                c.Label(ref loopStart);
                c.test(ax, ax); //Have we hit a \0?
                c.jz(end); //If so, we're at the end

                c.add(rdx, 2); //Move to the next character in "First"
                c.movzx(eax, __word_ptr[rdx]); //Load the next character from "First: into eax
                c.movzx(r8d, __word_ptr[rcx + rdx]); //Load the next character from lpLibFileName into r8d
                c.sub(r8d, eax); //Are they the same?
                c.jz(loopStart); //If so, do another iteration of the loop

                c.Label(ref end);
                c.mov(eax, r8d); //Move the last comparison result into eax
                c.shr(r8d, 0x1F);
                c.neg(eax);
                c.shr(eax, 0x1F);
                c.sub(eax, r8d);
            }

            void strcmp()
            {
                //This does some crazy shit based on whether or not the data is aligned. Apparently, if we are aligned,
                //it will attempt to compare 8 bytes at a time

                var alignedLoopInit = c.CreateLabel("alignedLoopInit");
                var alignedLoopStart = c.CreateLabel("alignedLoopStart");
                var unalignedLoopStart = c.CreateLabel("unalignedLoopStart");
                var success = c.CreateLabel("success");
                var fail = c.CreateLabel("fail");

                c.sub(rdx, rcx);
                c.test(cl, 7); //I think this checks if the bottom bytes are 0 to check for alignment?
                c.je(alignedLoopInit);

                c.Label(ref unalignedLoopStart);
                c.movzx(eax, __byte_ptr[rcx]);
                c.cmp(al, __byte_ptr[rdx + rcx]);
                c.jne(fail);

                c.inc(rcx);
                c.test(al, al);
                c.je(success);

                c.test(cl, 7);
                c.jne(unalignedLoopStart);

                c.Label(ref alignedLoopInit);
                c.mov(r11, 0x8080808080808080);
                c.mov(r10, 0xFEFEFEFEFEFEFEFF);

                c.Label(ref alignedLoopStart);
                c.lea(eax, __[edx + ecx]);
                c.and(eax, 0xFFF);
                c.cmp(eax, 0xFF8);
                c.ja(unalignedLoopStart);

                c.mov(rax, __qword_ptr[rcx]);
                c.cmp(rax, __qword_ptr[rdx + rcx]);
                c.jne(unalignedLoopStart);

                c.lea(r9, __[rax + r10]);
                c.not(rax);
                c.and(rcx, 8);
                c.and(rax, r9);
                c.test(r11, rax);
                c.je(alignedLoopStart);

                c.Label(ref success);
                c.xor(eax, eax);
                c.jmp(end);

                c.Label(ref fail);
                c.sbb(rax, rax);
                c.or(rax, 1);
                c.Label(ref end);
            }

            if (unicode)
                wcscmp();
            else
                strcmp();

            c.test(eax, eax); //Did the function return 0?
            c.jz(callCustom);
            c.pop(r8); //Restore dwFlags
            c.pop(rdx); //Restore hFile
            c.pop(rcx); //Restore lpLibFileName
            c.jmp((ulong) (void*) pOriginal);

            c.Label(ref callCustom);
            c.pop(r8); //Restore dwFlags
            c.pop(rdx); //Restore hFile
            c.pop(rcx); //Restore lpLibFileName
            c.jmp((ulong) (void*) pHook);

            return c;
        public override void Dispose()
        {
            if (pLibraryName != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pLibraryName);
                pLibraryName = IntPtr.Zero;
            }

            base.Dispose();
        }
    }
}
