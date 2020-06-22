using System.Runtime.InteropServices;

namespace modloader.Formats.DwPack
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x14)]
    public unsafe struct DwPackHeader
    {
        public ulong Signature; // DW_PACK\0
        public int Field08;
        public int FileCount;
        public int Index;

        public DwPackFileEntry* Files
        {
            get
            {
                fixed ( DwPackHeader* pThis = &this ) 
                    return ( DwPackFileEntry* )( pThis + sizeof( DwPackHeader ) );
            }
        }
    }
}
