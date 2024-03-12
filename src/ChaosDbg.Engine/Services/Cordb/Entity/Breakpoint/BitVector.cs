using System.Collections.Specialized;
using System.Text;

namespace ChaosDbg.Cordb
{
    public struct BitVector
    {
        //BitVector32 is the biggest pain in the ass to work with, requiring "sections" and "masks" when all I want to do
        //is just access particular bits!

        private uint data;

        public BitVector(uint data)
        {
            this.data = data;
        }

        /// <summary>
        /// Gets or sets whether a specified bit is set.
        /// </summary>
        /// <param name="bit">The 0-based bit to set. Unlike <see cref="BitVector32"/>, this is the actual index of a bit, not a bitmask.</param>
        /// <returns>Whether the specified bit is set.</returns>
        public bool this[int bit]
        {
            get
            {
                //BitVector32 confusingly wants you to specify a bit _mask_. We allow specifying an 0-based bit

                var shiftedBit = 1 << bit;

                return (data & shiftedBit) == shiftedBit;
            }
            set
            {
                if (value)
                    data |= (uint) (1 << bit);
                else
                    data &= ~(uint) (1 << bit);
            }
        }

        /// <summary>
        /// Gets or sets whether a specified range of bits are set.
        /// </summary>
        /// <param name="startBit">The first bit of the range.</param>
        /// <param name="endBit">The last bit of the range.</param>
        /// <returns>A value containing the bits in the specified range.</returns>
        public int this[int startBit, int endBit]
        {
            get => GetBits(startBit, endBit);
            set => SetBits(startBit, endBit, value);
        }

        private int GetBits(int startBit, int endBit)
        {
            //Unlike BitVector32, these bits are 0 based

            var mask = CreateMask(startBit, endBit - startBit);

            var result = (int) (data & mask) >> startBit;

            return result;
        }

        private void SetBits(int startBit, int endBit, int newValue)
        {
            var mask = CreateMask(startBit, endBit - startBit);

            //First, clear whatever values may be present
            var newData = (uint) (data & ~mask);

            //Now set the new bits
            newData |= (uint) (newValue << startBit);

            data = newData;
        }

        private int CreateMask(int startBit, int count)
        {
            var mask = 0;

            for (var i = 0; i <= count; i++)
                mask |= 1 << i;

            return mask << startBit;
        }

        public static implicit operator int(BitVector value) => (int) value.data;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("BitVector{");

            var localData = data;

            for (int i = 0; i < 32; i++)
            {
                if ((localData & 0x80000000) != 0)
                {
                    sb.Append("1");
                }
                else
                {
                    sb.Append("0");
                }

                localData <<= 1;
            }

            sb.Append("}");

            return sb.ToString();
        }
    }
}
