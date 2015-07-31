using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace mlconverter
{
    class Endian
    {
        public static uint SwapEndianU32(uint unsignedInteger) // Unsigned 32
        {
            return ((unsignedInteger & 0x000000ff) << 24) +
                   ((unsignedInteger & 0x0000ff00) << 8) +
                   ((unsignedInteger & 0x00ff0000) >> 8) +
                   ((unsignedInteger & 0xff000000) >> 24);
        }

        public static ushort SwapEndianU16(ushort unsignedShort) // Unsigned 16
        {
            int swapped = ((unsignedShort & 0x00ff) << 8) + ((unsignedShort & 0xff00) >> 8);
            return (ushort)swapped;
        }

        public static ulong SwapEndianU64(ulong unsignedLong) // Unsigned 64
        {
            return (ulong)(((SwapEndianU32((uint)unsignedLong) & 0xffffffffL) << 0x20) |
                            (SwapEndianU32((uint)(unsignedLong >> 0x20)) & 0xffffffffL));

        }

        public static int SwapEndian32(int signedInteger) // Unsigned 32
        {
            return ((int)(signedInteger & 0x000000ff) << 24) +
                   ((int)(signedInteger & 0x0000ff00) << 8) +
                   ((int)(signedInteger & 0x00ff0000) >> 8) +
                   ((int)(signedInteger & 0xff000000) >> 24);
        }

        public static short SwapEndian16(short signedShort) // Unsigned 16
        {
            int swapped = ((signedShort & 0x00ff) << 8) + ((signedShort & 0xff00) >> 8);
            return (short)swapped;
        }

        public static long SwapEndian64(long signedLong) // Unsigned 64
        {
            return (long)(((SwapEndianU32((uint)signedLong) & 0xffffffffL) << 0x20) |
                            (SwapEndianU32((uint)(signedLong >> 0x20)) & 0xffffffffL));
        }
    }
}
