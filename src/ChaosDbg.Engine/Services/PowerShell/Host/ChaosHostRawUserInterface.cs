using System;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using ChaosDbg.Terminal;
using ChaosLib;

namespace ChaosDbg.PowerShell.Host
{
    /// <summary>
    /// Defines the lowest-level user interface functions that an interactive application hosting PowerShell <see cref="Runspace"/> can
    /// choose to implement if it wants to support any cmdlet that does character-mode interaction with the user.
    /// </summary>
    class ChaosHostRawUserInterface : PSHostRawUserInterface
    {
        /// <summary>
        /// Gets or sets the color used to render the background behind characters on the screen buffer. Each character cell in the screen
        /// buffer can have a separate background color.
        /// </summary>
        public override ConsoleColor BackgroundColor
        {
            get
            {
                var bufferInfo = terminal.GetConsoleScreenBufferInfo();

                var color = (ConsoleColor) ((int) bufferInfo.wAttributes & 0xf0);

                return color;
            }
            set
            {
                var bufferInfo = terminal.GetConsoleScreenBufferInfo();

                var attribs = bufferInfo.wAttributes;

                attribs &= (ConsoleBufferAttributes) ~0xf0;

                // C#'s bitwise-or sign-extends to 32 bits.
                attribs = (ConsoleBufferAttributes) (((uint) (ushort) attribs) | ((uint) (ushort) ((short) value << 4)));

                terminal.SetConsoleTextAttribute(attribs);
            }
        }

        /// <summary>
        /// Gets or sets the current size of the screen buffer, measured in character cells.
        /// </summary>
        public override Size BufferSize
        {
            get
            {
                var bufferInfo = terminal.GetConsoleScreenBufferInfo();

                return new Size(bufferInfo.dwSize.X, bufferInfo.dwSize.Y);
            }
            set => throw new NotImplementedException();
        }

        /// <summary>
        /// Gets or sets the cursor position in the screen buffer. The view window always adjusts it's location over the screen buffer
        /// such that the cursor is always visible.
        /// </summary>
        public override Coordinates CursorPosition
        {
            get
            {
                var info = terminal.GetConsoleScreenBufferInfo();

                return new Coordinates(info.dwCursorPosition.X, info.dwCursorPosition.Y);
            }
            set
            {
                terminal.SetCursorPosition(value.X < 0 ? 0 : value.X, value.Y < 0 ? 0 : value.Y);
            }
        }

        /// <summary>
        /// Gets or sets the cursor size as a percentage 0..100.
        /// </summary>
        public override int CursorSize
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        /// <summary>
        /// Gets or sets the color used to render characters on the screen buffer. Each character cell in the screen buffer can have
        /// a separate foreground color.
        /// </summary>
        public override ConsoleColor ForegroundColor
        {
            get
            {
                var bufferInfo = terminal.GetConsoleScreenBufferInfo();

                var color = (ConsoleColor) ((int) bufferInfo.wAttributes & 0x0f);

                return color;
            }
            set
            {
                var bufferInfo = terminal.GetConsoleScreenBufferInfo();

                var attribs = bufferInfo.wAttributes;

                attribs &= (ConsoleBufferAttributes) ~0x0f;

                // C#'s bitwise-or sign-extends to 32 bits.
                attribs = (ConsoleBufferAttributes) (((uint) (ushort) attribs) | ((uint) (ushort) value));

                terminal.SetConsoleTextAttribute(attribs);
            }
        }

        /// <summary>
        /// A non-blocking call to examine if a keystroke is waiting in the input buffer.
        /// </summary>
        public override bool KeyAvailable => throw new NotImplementedException();

        /// <summary>
        /// Gets the largest window possible for the current font and display hardware, ignoring the current buffer dimensions.
        /// In other words, the dimensions of the largest window that could be rendered in the current display, if the buffer was
        /// at least as large.
        /// </summary>
        public override Size MaxPhysicalWindowSize => throw new NotImplementedException();

        /// <summary>
        /// Gets the size of the largest window possible for the current buffer, current font, and current display hardware.
        /// The view window cannot be larger than the screen buffer or the current display (the display the window is rendered on).
        /// </summary>
        public override Size MaxWindowSize => throw new NotImplementedException();

        /// <summary>
        /// Gets or sets position of the view window relative to the screen buffer, in characters. (0,0) is the upper left of the screen buffer.
        /// </summary>
        public override Coordinates WindowPosition
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        /// <summary>
        /// Gets or sets the current view window size, measured in character cells. The window size cannot be larger than the dimensions returned
        /// by <see cref="MaxPhysicalWindowSize"/>.
        /// </summary>
        public override Size WindowSize
        {
            get
            {
                var buffer = terminal.GetConsoleScreenBufferInfo();

                var windowWidth = buffer.srWindow.Right - buffer.srWindow.Left + 1;
                var windowHeight = buffer.srWindow.Bottom - buffer.srWindow.Top + 1;

                return windowWidth == 0 || windowHeight == 0
                    ? new Size(80, 40)
                    : new Size(windowWidth, windowHeight);
            }
            set => throw new NotImplementedException();
        }

        /// <summary>
        /// Gets or sets the titlebar text of the current view window.
        /// </summary>
        public override string WindowTitle { get; set; }

        private ITerminal terminal;

        public ChaosHostRawUserInterface(ITerminal terminal)
        {
            this.terminal = terminal;
        }

        public void ClearKeyCache()
        {
            //throw new NotImplementedException();
        }

        public override KeyInfo ReadKey(ReadKeyOptions options)
        {
            throw new NotImplementedException();
        }

        public override void FlushInputBuffer()
        {
            throw new NotImplementedException();
        }

        public override BufferCell[,] GetBufferContents(Rectangle rectangle)
        {
            throw new NotImplementedException();
        }

        public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill)
        {
            throw new NotImplementedException();
        }

        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents)
        {
            throw new NotImplementedException();
        }

        public override void SetBufferContents(Rectangle rectangle, BufferCell fill)
        {
            throw new NotImplementedException();
        }
    }
}
