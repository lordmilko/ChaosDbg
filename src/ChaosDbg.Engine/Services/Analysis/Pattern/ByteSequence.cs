using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ChaosDbg.Analysis
{
    //Based on DittedBitSequence.java, Pattern.java from Ghidra, licensed under the Apache License.
    //See ThirdPartyNotices.txt for full license notice.

    class ByteSequence
    {
        /// <summary>
        /// Gets the bytes that comprise this byte sequence.
        /// </summary>
        public byte[] Bytes { get; }

        /// <summary>
        /// Gets the masks that describe which bits in each byte in <see cref="Bytes"/> should be used
        /// in comparisons. e.g. if we want to match against the value 0x1. there will be a mask 0xf0 so
        /// that only the high byte is considered.
        /// </summary>
        public byte[] Masks { get; }

        /// <summary>
        /// Gets the index at which the "good" bytes of the pattern begin. Bytes prior to this index will be treated
        /// as junk bytes for the purposes of identifying the starting position of the bytes we want to consume
        /// from the resulting <see cref="ByteMatch"/>.
        /// </summary>
        public int Mark { get; }

        /// <summary>
        /// Gets whether this is a pattern is Xtended Flow Guard aware, wherein the pattern will attempt to skip
        /// over the 8 byte XFG hash that may exist prior to the <see cref="Mark"/> point, between the junk
        /// and the normal start of the function.
        /// </summary>
        public bool Xfg { get; private set; }

        public int Length => Bytes.Length;

        private object[] items;

        public ByteSequence(params byte[] bytes)
        {
            if (bytes.Length == 0)
                throw new ArgumentException("At least 1 byte must be specified", nameof(bytes));

            //Every bit in each byte is valid
            Bytes = bytes;

            Masks = new byte[bytes.Length];

            for (var i = 0; i < bytes.Length; i++)
                Masks[i] = 0xff; //Set a bitmask of all 1's for the 8 bits in this byte
        }

        public ByteSequence(params object[] items)
            : this(string.Join(" ", items))
        {
            this.items = items;
        }

        public ByteSequence(byte[] bytes, byte[] masks, int mark)
        {
            Bytes = bytes;
            Masks = masks;
            Mark = mark;
        }

        internal string debugStr; //temp

        public ByteSequence(string value)
        {
            value = Regex.Replace(value, "0x +", "0x");

            debugStr = value;

            var items = value.Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);

            if (items.Length == 0)
                throw new ArgumentException("At least 1 byte pattern must be specified");

            var bytes = new List<byte>();
            var masks = new List<byte>();

            foreach (var item in items)
            {
                if (item.StartsWith("0x"))
                    ParseHexString(item, bytes, masks);
                else if (item == "*")
                    Mark = bytes.Count;
                else
                    ParseBinaryString(item, bytes, masks);
            }

            Bytes = bytes.ToArray();
            Masks = masks.ToArray();
        }

        private void ParseHexString(string item, List<byte> bytes, List<byte> masks)
        {
            //As a shorthand, hex numbers are represented as one big number. e.g. 0xff10
            //means we want bytes 0xff and 0x10

            if (item.Length % 2 != 0)
                throw new ArgumentException($"Cannot parse hex string '{item}': string length must have an even number of characters.");

            item = item.Substring(2);

            for (var j = 0; j < item.Length; j += 2)
            {
                var s = new string(new[] { item[j], item[j + 1] });

                byte mask = 0xff;

                byte num = 0;

                if (s.Contains("."))
                {
                    if (s == "..")
                        mask = 0;
                    else
                    {
                        if (s[0] == '.')
                        {
                            //0x.1
                            //The lo byte is specified
                            num = byte.Parse(s[1].ToString(), NumberStyles.HexNumber);
                            mask = 0x0f;
                        }
                        else
                        {
                            //0x1.
                            //The hi byte is specified
                            num = byte.Parse(s[0].ToString(), NumberStyles.HexNumber);
                            num <<= 4;
                            mask = 0xf0;
                        }
                    }
                }
                else
                {
                    num = byte.Parse(s, NumberStyles.HexNumber);
                }

                bytes.Add(num);
                masks.Add(mask);
            }
        }

        private void ParseBinaryString(string item, List<byte> bytes, List<byte> masks)
        {
            //Should be a bit sequence
            if (item.Length % 8 != 0)
                throw new ArgumentException($"Cannot parse binary string '{item}': string length must be a multiple of 8. Actual length: {item.Length}.");

            for (var i = 0; i < item.Length;)
            {
                byte number = 0;
                byte mask = 0;

                for (var j = 0; j < 8; j++, i++)
                {
                    switch (item[i])
                    {
                        case '1':
                            number <<= 1; //Left shift it to make room for this bit
                            number |= 1; //Set a bit to one
                            mask <<= 1; //Left shift the mask to make room for this bit
                            mask |= 1; //Signal that this bit should be considered in the mask
                            break;

                        case '0':
                            //Bit is already 0 in number
                            number <<= 1; //Left shift it to make room for the next bit
                            mask <<= 1; //Left shift the mask to make room for this bit
                            mask |= 1; //Signal that this bit should be considered in the mask
                            break;

                        case '.':
                            //Nothing to set in the number or the mask
                            number <<= 1;
                            mask <<= 1;
                            break;

                        default:
                            throw new InvalidOperationException("Encountered invalid character '{}' in binary string '{}'. Binary string must only consist of '1', '0', and '.'");
                    }
                }

                bytes.Add(number);
                masks.Add(mask);
            }
        }

        /// <summary>
        /// Gets whether this <see cref="ByteSequence"/> matches against a specified
        /// <paramref name="value"/> at a specified index after applying that indexes
        /// relevant bitmask.
        /// </summary>
        /// <param name="index">The index of the byte to compare against.</param>
        /// <param name="value">The value to be compared against (after applying the relevant mask for the specified <paramref name="index"/>).</param>
        /// <returns>True if there is a match, otherwise false.</returns>
        public bool HasByte(int index, int value)
        {
            if (index >= Bytes.Length)
                return false;

            var theirValue = value & Masks[index];

            //There's no need to mask Bytes[index]. If we were told to add a byte 0x1. then we will have a byte 0x10
            //and a mask 0xf0. The mask is already applied

            var ourValue = Bytes[index];

            return theirValue == ourValue;
        }

        /// <summary>
        /// Creates a new <see cref="ByteSequence"/> from this object that is XFG aware.<para/>
        /// If <see cref="Mark"/> is 0 (indicating there is no known junk data prior to the start of this function)
        /// this method returns <see langword="null"/>.
        /// </summary>
        /// <returns>If <see cref="Mark"/> is greater than 0, a new <see cref="ByteSequence"/> that is XFG
        /// aware. Otherwise, <see langword="null"/>.</returns>
        public ByteSequence WithXfg()
        {
            if (Xfg)
                throw new InvalidOperationException($"{nameof(ByteSequence)} is already an XFG pattern.");

            /* When you see indirect function pointer calls like __guard_xfg_dispatch_icall_fptr,
             * these are Xtended Flow Guard function calls.
             *
             * When a function can be the target of an indirect jump, an XFG hash is assigned at compile time, and is
             * located directly before the function definition. XFG hashes are always 8 bytes. As such, for any pattern
             * where we try and skip over any junk that may exist prior to the start of the function, we should also generate
             * an XFG compatible pattern where we skip over that junk, the 8 bytes that would contain the XFG hash, and then
             * look for the expected function prolog
             * See also: https://blog.quarkslab.com/how-the-msvc-compiler-generates-xfg-function-prototype-hashes.html#:~:text=XFG%20works%20by%20restricting%20indirect,those%20XFG%20function%20prototype%20hashes.
             */

            if (Mark != 0)
            {
                const int xfgLength = 8;

                //We do not automatically remove repeated junk bytes (e.g. converting 0xCCCCCC into 0xCC) because this can result in additional false positives.
                //As this logic was quite tricky to figure out however, we leave this in in case we might want to provide options around supporting this in
                //the future
                var skipRepeatedJunkBytes = false;

                var offset = skipRepeatedJunkBytes ? (Mark - 1) : 0;

                var newBytes = new byte[Length + xfgLength - offset];
                var newMasks = new byte[Length + xfgLength - offset];

                //When skipping junk bytes, we write a single junk byte, and then skip j to the main content
                var targetMark = skipRepeatedJunkBytes ? 1 : Mark;

                for (int i = 0, j = 0; i < newBytes.Length; i++, j++)
                {
                    //XFG bytes will be 0x.. (so the byte and mask will both be 0)
                    if (i == targetMark)
                        i += xfgLength;

                    newBytes[i] = Bytes[j];
                    newMasks[i] = Masks[j];

                    if (i == 0 && skipRepeatedJunkBytes)
                        j = Mark - 1; //Skip over the rest of the junk bytes. We do -1 because we're going to do j++
                }

                var xfgPattern = new ByteSequence(newBytes, newMasks, Mark + xfgLength - offset)
                {
                    debugStr = debugStr,
                    Xfg = true,
                    items = items
                };

                return xfgPattern;
            }

            return null;
        }

        public bool IsMatch(byte[] bytes)
        {
            if (bytes.Length != Bytes.Length)
                return false;

            for (var i = 0; i < Bytes.Length; i++)
            {
                var mask = Masks[i];
                var ourByte = Bytes[i] & mask;
                var theirByte = bytes[i] & mask;

                if (ourByte != theirByte)
                    return false;
            }

            return true;
        }

        public override string ToString()
        {
            var components = new List<Tuple<string, bool>>();

            for (var i = 0; i < Masks.Length; i++)
            {
                bool shouldTreatDotDotAsHex()
                {
                    if (i < Masks.Length - 1)
                    {
                        var m = Masks[i + 1];

                        return m is not 0xf0 or 0x0f or 0xff;
                    }

                    throw new NotImplementedException($"Don't know whether to treat dot dot as hex when index is {i}/{Masks.Length}");
                }

                var mask = Masks[i];
                var val = Bytes[i];

                if (mask == 0xff)
                    components.Add(Tuple.Create(val.ToString("x2"), true));
                else if (mask == 0xf0)
                    components.Add(Tuple.Create(val.ToString("x").Substring(0, 1) + ".", true));
                else if (mask == 0x0f)
                    components.Add(Tuple.Create("." + val.ToString("x"), true));
                else if (mask == 0 && shouldTreatDotDotAsHex())
                    components.Add(Tuple.Create("..", true));
                else
                {
                    //Binary

                    var binaryMask = 1;
                    var binaryStr = new List<string>();

                    for (var j = 0; j < 8; j++)
                    {
                        var bitMask = mask & binaryMask;

                        if (bitMask == 0)
                            binaryStr.Add(".");
                        else
                        {
                            var bit = (val & bitMask) >> j;

                            binaryStr.Add(bit.ToString());
                        }

                        binaryMask <<= 1;
                    }

                    binaryStr.Reverse();

                    components.Add(Tuple.Create(string.Join(string.Empty, binaryStr), false));
                }
            }

            var builder = new StringBuilder();

            if (items != null)
            {
                builder.Append("[");

                for (var i = 0; i < items.Length; i++)
                {
                    var item = items[i];

                    if (item is string s)
                        builder.Append(s);
                    else if (item is InstrBytes b)
                        builder.Append(b.Name);
                    else
                        throw new NotImplementedException($"Don't know how to process a value of type '{item.GetType().Name}'");

                    if (i < items.Length - 1)
                    {
                        if (items[i].ToString() == "*" || items[i + 1].ToString() == "*")
                            builder.Append(" ");
                        else
                            builder.Append(" / ");
                    }
                }

                builder.Append("] ");

                builder.Clear();
            }

            for (var i = 0; i < components.Count; i++)
            {
                if (components[i].Item2) //IsHex
                {
                    builder.Append("0x");

                    var numAppended = 0;

                    for (var j = i; j < components.Count && components[j].Item2; j++)
                    {
                        //Maybe the next item is a binary string
                        if (numAppended > 0 && components[j].Item1.Contains("."))
                            break;

                        builder.Append(components[j].Item1);
                        numAppended++;

                        if (components[j].Item1.Contains(".") || j + 1 == Mark)
                            break;
                    }

                    if (numAppended > 1)
                        i += numAppended - 1;
                }
                else
                {
                    var numAppended = 0;

                    for (var j = i; j < components.Count && !components[j].Item2; j++)
                    {
                        builder.Append(components[j].Item1);
                        numAppended++;
                    }

                    if (numAppended > 1)
                        i += numAppended - 1;
                }

                if (i < components.Count - 1)
                    builder.Append(" ");

                if (Mark >= 0 && Mark == i + 1)
                    builder.Append("* ");
            }

            return builder.ToString();
        }
    }
}
