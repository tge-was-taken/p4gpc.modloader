using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using modloader.Utilities;

namespace modloader.Formats.DwPack
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x120)]
    public unsafe struct DwPackEntry
    {
        public const int PATH_LENGTH = 260;

        public int Field00;
        public int Id;
        public fixed byte PathBytes[PATH_LENGTH];
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
                    return EncodingCache.ShiftJIS.GetString( NativeStringHelper.AsSpan( pathBytes ) );
            }
            set
            {
                fixed ( byte* pathBytes = PathBytes )
                {
                    Unsafe.InitBlock( pathBytes, 0, PATH_LENGTH );
                    EncodingCache.ShiftJIS.GetBytes( value.AsSpan(), new Span<byte>( ( void* )pathBytes, PATH_LENGTH ) );
                }
            }
        }
    }
}
