using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ChaosLib;
using ChaosLib.Memory;

namespace ChaosDbg.Terminal
{
    /// <summary>
    /// Represents a real terminal that forwards to the system console.
    /// </summary>
    public class ConsoleTerminal : ITerminal, IDisposable
    {
        private SafeFileHandle hConsoleOut;
        private SafeFileHandle hConsoleIn;

        private List<ConsoleCtrlHandlerRoutine> handlers = new List<ConsoleCtrlHandlerRoutine>();

        private bool isWriteProtected;
        private int protectedCursorTop;

        private object objLock = new object();

        #region Initialization

        public ConsoleTerminal()
        {
            hConsoleOut = Kernel32.CreateFileW(
                "CONOUT$",
                GENERIC.GENERIC_READ | GENERIC.GENERIC_WRITE,
                FILE_SHARE.WRITE,
                FileMode.Open
            );

            hConsoleIn = Kernel32.CreateFileW(
                "CONIN$",
                GENERIC.GENERIC_READ | GENERIC.GENERIC_WRITE,
                FILE_SHARE.READ,
                FileMode.Open
            );

            /* By default, F11 will cause the console to go full screen. Disabling ENABLE_PROCESSED_INPUT prevents this from occurring,
             * however also has the side effect of disabling automatic handling of Ctrl+C. PSReadLine handles Ctrl+C itself, and so disables
             * ENABLE_PROCESSED_INPUT...however, in certain circumstances (e.g. after ReadLine() exits) it may restore the original input mode.
             * This is a big problem, because if we hold down F11 we'll sometimes hit our custom F11 PSReadLine handler, and other times toggle
             * full screen. Thus, we pre-emptively disable ENABLE_PROCESSED_INPUT here so that PSReadLine never re-enables it.
             *
             * If PSReadLine is not available, we will need to use ENABLE_PROCESSED_INPUT so that we can manually detect Ctrl+C, which we
             * will do when we call ChaosHostUserInterface!ReadLineFromConsole()
             *
             * Note that this trick does not work in Windows Terminal. It has a special modern handler for F11 (defaults.json + AppActionHandlers.cpp!TerminalPage::_HandleToggleFullscreen */
            var inputMode = GetInputConsoleMode();
            inputMode &= ~ConsoleMode.ENABLE_PROCESSED_INPUT;
            SetInputConsoleMode(inputMode);

            using var holder = new DisposeHolder(hConsoleOut);

            ImpersonatePowerShell();

            holder.SuppressDispose();
        }

        /// <summary>
        /// Change the console colour and font to match that of PowerShell.
        /// </summary>
        private unsafe void ImpersonatePowerShell()
        {
            /* You can't change the "fill" colour of a console once it's already started.
             *
             * The subsystem of a process is specified in its PE headers. When a PE is a console application,
             * during process startup KERNELBASE!ConsoleCreateConnectionObject tries to connect to \\Device\\ConDrv\\Connect
             * which causes conhost to receive a CONSOLE_IO_CONNECT message. There are two background colours in a console application:
             * the "fill" attribute (which can be specified in your STARTUPINFO) and the highlight background colour. The highlight background
             * colour is the only thing you can control via an API. If you want to control the fill colour, you either need to have a console
             * profile under HKCU\Console prior to the application starting up (since conhost will be launched by kernel32 prior to your application
             * gaining control of the situation) or need to use a helper process to launch your process with the right startup args.
             *
             * Therefore, we will go with plan B: rather than modify the uncommonly used colours DarkYellow and DarkMagenta to be PowerShell's Alt White
             * and Alt Blue, and set those as our fill colours, we'll instead modify Gray and Black to be Alt White and Alt Blue instead. Black is used
             * when you highlight text with your mouse; I don't think gray is ever used. Surprisingly however, even though we overwrite black,
             * we still get black when we select with the mouse!
             *
             * We don't do the popup colours. These would normally appear if you did F7 in a cmd after typing a command, but that doesn't seem to work in
             * normal PowerShell, and modifying the popup colours doesn't seem to work for me anyway.
             *
             * You can explore how conhost works by debugging the Host.EXE project (which builds OpenConsole.exe) from https://github.com/microsoft/terminal
             * The fill attribute gets set in settings.cpp!Settings::SetFillAttribute(). In conhostv1, you can ostensibly send a CM_PROPERTIES_UPDATE
             * message to conhost (WM_USER+201, also called WM_SETCONSOLEINFO) which may or may not actually set the fill colour unlike SetConsoleScreenBufferInfoEx
             * (not sure). I haven't explored whether this actually works or not, but it doesn't matter because Windows 11 uses console.dll (conhost v2) which doesn't
             * support this window message.
             *
             * If this didn't work, it might have been because conhostv1SetScreenColors decided to skip setting the text/background colors.
             * Plan B in this scenario was going to be to either
             * - get at the NtCurrentTeb()->CsrClientThread->Process via DereferenceIoHandle to get the PSCREEN_INFORMATION.Console
             * - get the PCONSOLE_INFORMATION out of the GWLP_USERDATA
             *
             * and then set the colour of the CONSOLE_INFORMATION's HDC directly. It seems to me like all of this would be within conhost, and not csrss.
             *
             * The following tables show how the default console colours of cmd compare to PowerShell
             *
             * Fill
             * ----
             *
             * |     Property      |       CMD        |         PowerShell          |
             * |-------------------|------------------|-----------------------------|
             * | Screen Text       | 7  (Gray)        | 6  (DarkYellow [Alt White]) |
             * | Screen Background | 0  (Black)       | 5  (DarkMagenta [Alt Blue]) |
             * | Popup Text        | 5  (DarkMagenta) | 3  (DarkCyan)               |
             * | Popup Background  | 15 (White)       | 15 (White)                  |
             *
             * Colors
             * ------
             * The "names" assigned to each COLORREF correspond with the default colour that is rendered for that index. In reality, there are no such
             * thing as "colours"; there are merely 16 indices (0-15). The FOREGROUND_* enum members simply serve to provide 1, 2, 4 or 8 bits for constructing
             * such an index.
             *
             * | Index | ConsoleColor |                                  Attribute                                  |     CMD     | PowerShell  |
             * |-------|--------------|-----------------------------------------------------------------------------|-------------|-------------|
             * |   0   | Black        | 0                                                                           | 0   0   0   |             |
             * |   1   | DarkBlue     | FOREGROUND_BLUE                                                             | 0   0   128 |             |
             * |   2   | DarkGreen    | FOREGROUND_GREEN                                                            | 0   128 0   |             |
             * |   3   | DarkCyan     | FOREGROUND_BLUE      | FOREGROUND_GREEN                                     | 0   128 128 |             |
             * |   4   | DarkRed      | FOREGROUND_RED                                                              | 128 0   0   |             |
             * |   5   | DarkMagenta  | FOREGROUND_BLUE      | FOREGROUND_RED                                       | 128 0   128 | 1   36  86  | Alt Blue
             * |   6   | DarkYellow   | FOREGROUND_GREEN     | FOREGROUND_RED                                       | 128 128 0   | 238 237 240 | Alt White
             * |   7   | Gray         | FOREGROUND_BLUE      | FOREGROUND_GREEN | FOREGROUND_RED                    | 192 192 192 |             |
             * |   8   | DarkGray     | FOREGROUND_INTENSITY                                                        | 128 128 128 |             |
             * |   9   | Blue         | FOREGROUND_INTENSITY | FOREGROUND_BLUE                                      | 0   0   255 |             |
             * |   10  | Green        | FOREGROUND_INTENSITY | FOREGROUND_GREEN                                     | 0   255 0   |             |
             * |   11  | Cyan         | FOREGROUND_INTENSITY | FOREGROUND_BLUE  | FOREGROUND_GREEN                  | 0   255 255 |             |
             * |   12  | Red          | FOREGROUND_INTENSITY | FOREGROUND_RED                                       | 255 0   0   |             |
             * |   13  | Magenta      | FOREGROUND_INTENSITY | FOREGROUND_BLUE  | FOREGROUND_RED                    | 255 0   255 |             |
             * |   14  | Yellow       | FOREGROUND_INTENSITY | FOREGROUND_GREEN | FOREGROUND_RED                    | 255 255 0   |             |
             * |   15  | White        | FOREGROUND_INTENSITY | FOREGROUND_BLUE  | FOREGROUND_GREEN | FOREGROUND_RED | 255 255 255 |             |
             */

            var colors = new[]
            {
                //Since we can't tell conhost to use DarkMagenta (Alt Blue) and DarkYellow (Alt White) as our fill colours, modify the default fill colours
                //at indices 0 and 7 (Black and Gray) instead

                /* 0  */ WellKnownColor.AltBlue, //Black
                /* 1  */ WellKnownColor.DarkBlue,
                /* 2  */ WellKnownColor.DarkGreen,
                /* 3  */ WellKnownColor.DarkCyan,
                /* 4  */ WellKnownColor.DarkRed,
                /* 5  */ WellKnownColor.DarkMagenta,
                /* 6  */ WellKnownColor.DarkYellow,
                /* 7  */ WellKnownColor.AltWhite, //Gray
                /* 8  */ WellKnownColor.DarkGray,
                /* 9  */ WellKnownColor.Blue,
                /* 10 */ WellKnownColor.Green,
                /* 11 */ WellKnownColor.Cyan,
                /* 12 */ WellKnownColor.Red,
                /* 13 */ WellKnownColor.Magenta,
                /* 14 */ WellKnownColor.Yellow,
                /* 15 */ WellKnownColor.White
            };

            var screenBufferInfoEx = Kernel32.GetConsoleScreenBufferInfoEx(hConsoleOut);

            for (var i = 0; i < 16; i++)
                screenBufferInfoEx.ColorTable[i] = colors[i];

            Kernel32.SetConsoleScreenBufferInfoEx(hConsoleOut, screenBufferInfoEx);

            //todo: this fails when im connecting via anydesk - windowheight doesnt like the max size of the window or something
            Console.WindowWidth = 120;
            Console.WindowHeight = 50;
            Console.BufferWidth = 120;
            Console.BufferHeight = 3000;

            //Change the font from Consolas to Lucida Console. This is also what is responsible for
            //making the window a lot wider
            var font = Kernel32.GetCurrentConsoleFontEx(hConsoleOut, false);
            font.FaceName = "Lucida Console";
            Kernel32.SetCurrentConsoleFontEx(hConsoleOut, false, ref font);
        }

        #endregion

        public unsafe string ReadConsole(int charsToRead, System.Span<char> buffer, int charactersToRead, bool endOnTab, out int keyState)
        {
            var control = new CONSOLE_READCONSOLE_CONTROL
            {
                nLength = Marshal.SizeOf<CONSOLE_READCONSOLE_CONTROL>(),
                nInitialChars = charsToRead,
                dwControlKeyState = 0
            };

            if (endOnTab)
                control.dwCtrlWakeupMask = (1 << 0x9); //0x9: Tab

            fixed (char* p = &MemoryMarshal.GetReference(buffer))
            {
                var read = Kernel32.ReadConsoleW(
                    hConsoleIn,
                    (IntPtr) p,
                    charactersToRead,
                    ref control
                );

                keyState = control.dwControlKeyState;

                if (read > charactersToRead)
                    read = charactersToRead;

                return buffer.Slice(0, read).ToString();
            }
        }

        public unsafe void WriteConsole(string output, bool newLine)
        {
            //Console.Write(output);
            //return;

            if (output.Length == 0)
            {
                if (newLine)
                {
                    WriteConsole(Environment.NewLine);
                }

                return;
            }

            // Native WriteConsole doesn't support output buffer longer than 64K, so we need to chop the output string if it is too long.
            // This records the chopping position in output string.
            int cursor = 0;
            // This is 64K/4 - 1 to account for possible width of each character.
            const int MaxBufferSize = 16383;
            const int MaxStackAllocSize = 512;
            string outBuffer;

            // In case that a new line is required, we try to write out the last chunk and the new-line string together,
            // to avoid one extra call to 'WriteConsole' just for a new line string.
            while (cursor + MaxBufferSize < output.Length)
            {
                outBuffer = output.Substring(cursor, MaxBufferSize);
                cursor += MaxBufferSize;
                WriteConsole(outBuffer);
            }

            outBuffer = output.Substring(cursor);
            if (!newLine)
            {
                WriteConsole(outBuffer);
                return;
            }

            char[] rentedArray = null;
            string lineEnding = Environment.NewLine;
            int size = outBuffer.Length + lineEnding.Length;

            // We expect the 'size' will often be small, and thus optimize that case with 'stackalloc'.
            //System.Span<char> buffer = size <= MaxStackAllocSize ? stackalloc char[size] : default;

            try
            {
                /*if (buffer.IsEmpty)
                {
                    rentedArray = ArrayPool<char>.Shared.Rent(size);
                    buffer = rentedArray.AsSpan().Slice(0, size);
                }*/

                WriteConsole(outBuffer + lineEnding);

                //outBuffer.CopyTo(buffer);
                //lineEnding.CopyTo(buffer.Slice(outBuffer.Length));
                //throw new NotImplementedException();
                //WriteConsole(buffer);
            }
            finally
            {
                //if (rentedArray is not null)
                //{
                //    ArrayPool<char>.Shared.Return(rentedArray);
                //}
            }
        }

        private unsafe void WriteConsole(string buffer)
        {
            WriteProtect(buffer, s =>
            {
                fixed (char* p = s)
                {
                    //todo: theres two issues: when we write a value that wraps, that messes up us when we adjust the new cursor position
                    //and also when we hit the end of the screen, the colour we write is infecting the prompt text

                    Kernel32.WriteConsoleW(
                        hConsoleOut,
                        (IntPtr) p,
                        buffer.Length,
                        IntPtr.Zero
                    );
                }
            });
        }

        public void LockProtection(Action action)
        {
            lock (objLock)
                action();
        }

        public void EnterWriteProtection()
        {
            isWriteProtected = true;
            protectedCursorTop = Console.CursorTop;

            //Normally, you would enter write protection while still on the current line.
            //If you've gone to a new line, newly written lines won't be written above
            //the empty line properly, so move to above the previous line with content
            if (Console.CursorLeft == 0)
                protectedCursorTop--;
        }

        public void ExitWriteProtection()
        {
            isWriteProtected = false;
            protectedCursorTop = default;
        }

        public Action<int> OnProtectedWrite { get; set; }

        private void WriteProtect(string buffer, Action<string> action)
        {
            if (!isWriteProtected)
            {
                action(buffer);
                return;
            }

            lock (objLock)
            {
                //Write the line above the current line we're showing a prompt + requesting for input

                var inc = (buffer.Length / Console.WindowWidth) + 1;

                var newProtectedCursorTop = protectedCursorTop + inc;

                int cursorLeft = Console.CursorLeft;
                Console.MoveBufferArea(0, protectedCursorTop, Console.WindowWidth, 1, 0, newProtectedCursorTop);
                Console.SetCursorPosition(0, protectedCursorTop);

                OnProtectedWrite?.Invoke(inc);

                //All writes that occur during console protection are implicitly WriteLine's. Additionally, if we're colouring this text, the \n
                //will cause the colour to infect the next line below (which might be our prompt)
                action(buffer.TrimEnd('\n'));

                Console.SetCursorPosition(cursorLeft, newProtectedCursorTop); //todo: after opening a ttd dump and doing heaps of commands, we hit f10 and newprotectedcursortop was 3000 and this crashed
                protectedCursorTop = newProtectedCursorTop;
            }
        }

        public void FillConsoleOutputCharacter(char c, int length, COORD coord) =>
            Kernel32.FillConsoleOutputCharacterW(hConsoleOut, c, length, coord);

        public ConsoleMode GetOutputConsoleMode() => Kernel32.GetConsoleMode(hConsoleOut);
        public void SetOutputConsoleMode(ConsoleMode mode) => Kernel32.SetConsoleMode(hConsoleOut, mode);

        public ConsoleMode GetInputConsoleMode() => Kernel32.GetConsoleMode(hConsoleIn);
        public void SetInputConsoleMode(ConsoleMode mode) => Kernel32.SetConsoleMode(hConsoleIn, mode);

        public CONSOLE_SCREEN_BUFFER_INFO GetConsoleScreenBufferInfo() => Kernel32.GetConsoleScreenBufferInfo(hConsoleOut);

        public void SetConsoleTextAttribute(ConsoleBufferAttributes attributes)
        {
            Kernel32.SetConsoleTextAttribute(hConsoleOut, attributes);
        }

        public void SetCursorPosition(int x, int y)
        {
            Kernel32.SetConsoleCursorPosition(hConsoleOut, new COORD
            {
                X = (short) x,
                Y = (short) y
            });
        }

        public void FlushConsoleInputBuffer() => Kernel32.FlushConsoleInputBuffer(hConsoleIn);

        public void AddBreakHandler(ConsoleCtrlHandlerRoutine handler)
        {
            Kernel32.SetConsoleCtrlHandler(handler, true);

            if (handler != null)
                handlers.Add(handler);
        }

        public void RemoveBreakHandler(ConsoleCtrlHandlerRoutine handler)
        {
            Kernel32.SetConsoleCtrlHandler(handler, false);

            if (handler != null)
                handlers.Remove(handler);
        }

        public void SendInput(INPUT[] inputs, bool activateWindow)
        {
            //Do not step here
            if (activateWindow)
                User32.Native.SetForegroundWindow(Kernel32.Native.GetConsoleWindow());

            User32.SendInput(inputs);
        }

        #region LengthInBufferCells

        /// <summary>
        /// From IsConsoleFullWidth in \windows\core\ntcon\server\dbcs.c.
        /// </summary>
        /// <returns></returns>
        internal static int LengthInBufferCells(string str, int offset, bool checkEscapeSequences)
        {
            Debug.Assert(offset >= 0, "offset >= 0");
            Debug.Assert(string.IsNullOrEmpty(str) || (offset < str.Length), "offset < str.Length");

            var escapeSequenceAdjustment = 0;
            if (checkEscapeSequences)
            {
                int i = 0;
                while (i < offset)
                {
                    ControlSequenceLength(str, ref i);
                }

                // If offset != i, we're in the middle of a sequence, which the caller should avoid,
                // but we'll tolerate.
                while (i < str.Length)
                {
                    escapeSequenceAdjustment += ControlSequenceLength(str, ref i);
                }
            }

            int length = 0;
            foreach (char c in str)
            {
                length += LengthInBufferCells(c);
            }

            return length - offset - escapeSequenceAdjustment;
        }

        internal static int LengthInBufferCells(char c)
        {
            // The following is based on http://www.cl.cam.ac.uk/~mgk25/c/wcwidth.c
            // which is derived from https://www.unicode.org/Public/UCD/latest/ucd/EastAsianWidth.txt
            bool isWide = c >= 0x1100 &&
                (c <= 0x115f || /* Hangul Jamo init. consonants */
                 c == 0x2329 || c == 0x232a ||
                 ((uint) (c - 0x2e80) <= (0xa4cf - 0x2e80) &&
                  c != 0x303f) || /* CJK ... Yi */
                 ((uint) (c - 0xac00) <= (0xd7a3 - 0xac00)) || /* Hangul Syllables */
                 ((uint) (c - 0xf900) <= (0xfaff - 0xf900)) || /* CJK Compatibility Ideographs */
                 ((uint) (c - 0xfe10) <= (0xfe19 - 0xfe10)) || /* Vertical forms */
                 ((uint) (c - 0xfe30) <= (0xfe6f - 0xfe30)) || /* CJK Compatibility Forms */
                 ((uint) (c - 0xff00) <= (0xff60 - 0xff00)) || /* Fullwidth Forms */
                 ((uint) (c - 0xffe0) <= (0xffe6 - 0xffe0)));

            // We can ignore these ranges because .Net strings use surrogate pairs
            // for this range and we do not handle surrogate pairs.
            // (c >= 0x20000 && c <= 0x2fffd) ||
            // (c >= 0x30000 && c <= 0x3fffd)
            return 1 + (isWide ? 1 : 0);
        }

        // Return the length of a VT100 control sequence character in str starting
        // at the given offset.
        //
        // This code only handles the following formatting sequences, corresponding to
        // the patterns:
        //     CSI params? 'm'               // SGR: Select Graphics Rendition
        //     CSI params? '#' [{}pq]        // XTPUSHSGR ('{'), XTPOPSGR ('}'), or their aliases ('p' and 'q')
        //
        // Where:
        //     params: digit+ ((';' | ':') params)?
        //     CSI:     C0_CSI | C1_CSI
        //     C0_CSI:  \x001b '['            // ESC '['
        //     C1_CSI:  \x009b
        //
        // There are many other VT100 escape sequences, but these text attribute sequences
        // (color-related, underline, etc.) are sufficient for our formatting system.  We
        // won't handle cursor movements or other attempts at animation.
        //
        // Note that offset is adjusted past the escape sequence, or at least one
        // character forward if there is no escape sequence at the specified position.
        internal static int ControlSequenceLength(string str, ref int offset)
        {
            var start = offset;

            // First, check for the CSI:
            if ((str[offset] == (char) 0x1b) && (str.Length > (offset + 1)) && (str[offset + 1] == '['))
            {
                // C0 CSI
                offset += 2;
            }
            else if (str[offset] == (char) 0x9b)
            {
                // C1 CSI
                offset += 1;
            }
            else
            {
                // No CSI at the current location, so we are done looking, but we still
                // need to advance offset.
                offset += 1;
                return 0;
            }

            if (offset >= str.Length)
            {
                return 0;
            }

            // Next, handle possible numeric arguments:
            char c;
            do
            {
                c = str[offset++];
            }
            while ((offset < str.Length) && (char.IsDigit(c) || (c == ';') || (c == ':')));

            // Finally, handle the command characters for the specific sequences we
            // handle:
            if (c == 'm')
            {
                // SGR: Select Graphics Rendition
                return offset - start;
            }

            // Maybe XTPUSHSGR or XTPOPSGR, but we need to read another char. Offset is
            // already positioned on the next char (or past the end).
            if (offset >= str.Length)
            {
                return 0;
            }

            if (c == '#')
            {
                // '{' : XTPUSHSGR
                // '}' : XTPOPSGR
                // 'p' : alias for XTPUSHSGR
                // 'q' : alias for XTPOPSGR
                c = str[offset++];
                if ((c == '{') ||
                    (c == '}') ||
                    (c == 'p') ||
                    (c == 'q'))
                {
                    return offset - start;
                }
            }

            return 0;
        }

        #endregion

        public void Clear()
        {
            Console.Clear();

            //I think because our handle is different, we need to set our cursor position as well
            SetCursorPosition(0, 0);
        }

        public void Dispose()
        {
            hConsoleOut.Dispose();
            hConsoleIn.Dispose();
        }
    }
}
