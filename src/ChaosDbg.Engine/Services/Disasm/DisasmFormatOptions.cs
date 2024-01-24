using System;

namespace ChaosDbg.Disasm
{
    public class DisasmFormatOptions
    {
        /// <summary>
        /// Gets the default format where neither <see cref="IP"/> or <see cref="Bytes"/> are specified.
        /// </summary>
        public static readonly DisasmFormatOptions Default = new DisasmFormatOptions();

        /// <summary>
        /// Gets the DbgEng format where <see cref="IP"/> and <see cref="Bytes"/> are specified.
        /// </summary>
        public static readonly DisasmFormatOptions DbgEng = new()
        {
            IP = true,
            Bytes = true
        };

        /// <summary>
        /// Specifies that the instruction should be formatted with IP information.
        /// </summary>
        public bool IP { get; set; }

        /// <summary>
        /// Specifies that the instruction should be formatted with byte information.
        /// </summary>
        public bool Bytes { get; set; }

        public DisasmFormatOptions()
        {
        }

        private DisasmFormatOptions(DisasmFormatOptions format)
        {
            if (format == null)
                throw new ArgumentNullException(nameof(format));

            IP = format.IP;
            Bytes = format.Bytes;
        }

        public DisasmFormatOptions WithIP(bool ip)
        {
            var newFormat = new DisasmFormatOptions(this)
            {
                IP = ip
            };

            return newFormat;
        }

        public DisasmFormatOptions WithBytes(bool bytes)
        {
            var newFormat = new DisasmFormatOptions(this)
            {
                Bytes = bytes
            };

            return newFormat;
        }
    }
}
