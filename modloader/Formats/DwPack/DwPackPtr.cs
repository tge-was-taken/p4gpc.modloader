using System;
using System.Runtime.InteropServices;

namespace modloader.Formats.DwPack
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct DwPackPtr : IDisposable
    {
        public byte* Ptr;

        public DwPackHeader* Header => ( DwPackHeader* )( Ptr );
        public DwPackEntry* Entries => ( DwPackEntry* )( Header + 1 );
        public byte* Data => ( byte* )( Entries + Header->FileCount );

        public DwPackPtr(byte* ptr)
        {
            Ptr = ptr;
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal( ( IntPtr )Ptr );
        }
    }
}
