using System.Runtime.InteropServices;

namespace modloader
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x14)]
    public unsafe struct LiveDwPackHeader
    {
        public ulong Signature; // DW_PACK\0
        public int Field08;
        public int FileCount;
        public int Index;

        public LiveDwPackFileEntry* Files
        {
            get
            {
                fixed ( LiveDwPackHeader* pThis = &this ) 
                    return ( LiveDwPackFileEntry* )( pThis + sizeof( LiveDwPackHeader ) );
            }
        }
    }
}
