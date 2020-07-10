using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Amicitia.IO;
using Microsoft.Win32.SafeHandles;
using modloader.Formats.DwPack;
using modloader.Utilities;
using Reloaded.Mod.Interfaces;

namespace modloader.Redirectors.DwPack
{
    public unsafe class VirtualDwPack
    {
        public const long MAX_FILE_SIZE = uint.MaxValue;

        private readonly SemanticLogger mLogger;
        private MemoryMapper mMapper;

        public DwPackPtr Native { get; private set; }
        public string FilePath { get; private set; }
        public string FileName { get; private set; }
        public long FilePointer { get; set; }    
        public long RealFileSize { get; private set; }
        public long VirtualFileSize { get; private set; }
        public long DataBaseOffset => Native.Data - Native.Ptr;
        public List<VirtualDwPackEntry> Entries { get; }

        public VirtualDwPack( SemanticLogger logger )
        {
            mLogger = logger;
            mMapper = new MemoryMapper();
            Entries = new List<VirtualDwPackEntry>();
        }

        public void LoadFromFile( string filePath, FileStream fileStream )
        {
            FilePath = filePath;
            FileName = Path.GetFileNameWithoutExtension( FilePath );
            FilePointer = 0;

            // Get file size
            RealFileSize = fileStream.Length;
            VirtualFileSize = RealFileSize;

            // Read header to determine where the data starts & so we can read all of the headers
            DwPackHeader header = new DwPackHeader();
            fileStream.Read( SpanHelper.AsSpan<DwPackHeader, byte>( ref header ) );

            // Read the rest of the header data
            var length = sizeof(DwPackHeader) + (sizeof(DwPackEntry) * header.FileCount);
            var buffer = (byte*)Marshal.AllocHGlobal( length );
            fileStream.Seek( 0, SeekOrigin.Begin );
            fileStream.Read( new Span<byte>( buffer, length ) );
            Native = new DwPackPtr( buffer );

            for ( int i = 0; i < Native.Header->FileCount; i++ )
            {
                var entry = new VirtualDwPackEntry( mLogger, this, Native.Entries + i, i );
                Entries.Add( entry );
                mMapper.Map( DataBaseOffset + entry.Native->DataOffset, entry.Native->CompressedSize, true );
            }
        }

        public long ReallocateFileData( int dataOffset, int length )
        {
            var offset = mMapper.Reallocate( DataBaseOffset + dataOffset, length );
            var oldVirtualFileSize = VirtualFileSize;
            VirtualFileSize = Math.Max( VirtualFileSize, offset + length );
            if ( VirtualFileSize > MAX_FILE_SIZE )
                mLogger.Error( "Out of available memory! 4GB address space exhausted" );
            else if ( VirtualFileSize > oldVirtualFileSize )
                mLogger.Info( $"{FileName} Virtual size increased to 0x{VirtualFileSize:X8}" );

            return ( long )( offset - DataBaseOffset );
        }
    }
}
