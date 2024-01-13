namespace ChaosDbg.Disasm
{
    public class DisasmFormatOptions
    {
        public static readonly DisasmFormatOptions Default = new DisasmFormatOptions();

        /// <summary>
        /// Specifies that the instruction should be formatted without IP or byte information.
        /// </summary>
        public bool Simple { get; set; }
    }
}
