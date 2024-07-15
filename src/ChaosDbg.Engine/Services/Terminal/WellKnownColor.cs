namespace ChaosDbg.Terminal
{
    /// <summary>
    /// Describes well known COLORREF values found in cmd.exe and powershell.exe
    /// </summary>
    static class WellKnownColor
    {
        public static readonly int Black       = MakeColor(0, 0, 0);
        public static readonly int DarkBlue    = MakeColor(0, 0, 128);
        public static readonly int DarkGreen   = MakeColor(0, 128, 0);
        public static readonly int DarkCyan    = MakeColor(0, 128, 128);
        public static readonly int DarkRed     = MakeColor(128, 0, 0);
        public static readonly int DarkMagenta = MakeColor(128, 0, 128);
        public static readonly int DarkYellow  = MakeColor(128, 128, 0);
        public static readonly int Gray        = MakeColor(192, 192, 192);
        public static readonly int DarkGray    = MakeColor(128, 128, 128);
        public static readonly int Blue        = MakeColor(0, 0, 255);
        public static readonly int Green       = MakeColor(0, 255, 0);
        public static readonly int Cyan        = MakeColor(0, 255, 255);
        public static readonly int Red         = MakeColor(255, 0, 0);
        public static readonly int Magenta     = MakeColor(255, 0, 255);
        public static readonly int Yellow      = MakeColor(255, 255, 0);
        public static readonly int White       = MakeColor(255, 255, 255);

        public static readonly int AltBlue   = MakeColor(1, 36, 86);
        public static readonly int AltWhite  = MakeColor(238, 237, 240);

        static int MakeColor(int r, int g, int b)
        {
            //COLORREF is backwards: 0x00bbggrr
            return b << 16 | g << 8 | r;
        }
    }
}
