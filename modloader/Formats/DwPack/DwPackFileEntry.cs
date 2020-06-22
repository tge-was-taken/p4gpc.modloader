using System;
using System.Runtime.InteropServices;
using System.Text;

namespace modloader.Formats.DwPack
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x120)]
    public unsafe struct DwPackFileEntry
    {
        public int Field00;
        public int Id;
        public fixed byte PathBytes[260];
        public int Field104;
        public int CompressedSize;
        public int UncompressedSize;
        public int Flags;
        public int DataOffset;

        public string Path
        {
            get
            {
                fixed ( byte* pathBytes = PathBytes )
                    return new string( ( sbyte* )pathBytes );
            }
            set
            {
                fixed ( byte* pathBytes = PathBytes )
                    Encoding.ASCII.GetBytes( value.AsSpan(), new Span<byte>( ( void* )pathBytes, 260 ) );
            }
        }
    }
}
