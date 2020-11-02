using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using modloader.Redirectors.Cpk;
using modloader.Utilities;
using PreappPartnersLib.FileSystems;

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
        public long RealFileSize { get; private set; }
        public long VirtualFileSize { get; private set; }
        public long DataBaseOffset => Native.Data - Native.Ptr;
        public List<VirtualDwPackEntry> Entries { get; }
        public VirtualCpk Cpk { get; internal set; }

        public VirtualDwPack( SemanticLogger logger, string filePath )
        {
            mLogger = logger;
            mMapper = new MemoryMapper();
            Entries = new List<VirtualDwPackEntry>();
            FilePath = filePath;
            FileName = Path.GetFileNameWithoutExtension( FilePath );
        }

        public void LoadFromFile( string filePath, FileStream fileStream )
        {
            // Get file size
            RealFileSize = fileStream.Length;
            VirtualFileSize = RealFileSize;

            // Read header to determine where the data starts & so we can read all of the headers
            DwPackHeader header = new DwPackHeader();
            fileStream.Read( SpanHelper.AsSpan<DwPackHeader, byte>( ref header ) );

            // Read the rest of the header data
            var length = sizeof( DwPackHeader ) + ( sizeof( DwPackEntry ) * header.FileCount );
            var buffer = (byte*)Marshal.AllocHGlobal( length );
            fileStream.Seek( 0, SeekOrigin.Begin );
            fileStream.Read( new Span<byte>( buffer, length ) );
            Native = new DwPackPtr( buffer );
            LoadEntries();
        }

        private void LoadEntries()
        {
            for ( int i = 0; i < Native.Header->FileCount; i++ )
            {
                var entry = new VirtualDwPackEntry( mLogger, this, Native.Entries + i );
                Entries.Add( entry );
                mMapper.Map( DataBaseOffset + entry.Native->DataOffset, entry.Native->CompressedSize, true );
            }
        }

        public void LoadFromCpk( int index, VirtualCpk cpk )
        {
            var headerSize = sizeof( DwPackHeader ) + ( cpk.Entries.Count * sizeof( DwPackEntry ) );
            Native = new DwPackPtr( (byte*)Marshal.AllocHGlobal( headerSize ) );
            Native.Header->Signature = DwPackHeader.SIGNATURE;
            Native.Header->Field08 = 0;
            Native.Header->FileCount = cpk.Entries.Count;
            Native.Header->Index = index;

            for ( int i = 0; i < cpk.Entries.Count; i++ )
            {
                var entry = Native.Entries + i;
                entry->Field00 = 0;
                entry->Index = (short)i;
                entry->PackIndex = (short)Native.Header->Index;
                entry->Path = cpk.Entries[ i ].Path;
                entry->Field104 = 0;
                entry->Flags = 0;

                // These will be filled in later
                entry->CompressedSize = entry->UncompressedSize = 1;
                entry->DataOffset = 0;
            }

            LoadEntries();
        }

        public void AddNewFiles( VirtualCpk cpk )
        {
            var newFileCount = cpk.Entries.Where( x => x.PacIndex == Native.Header->Index )
                .Max( x => x.FileIndex + 1 );

            if ( newFileCount > Entries.Count )
            {
                var oldHeaderSize = sizeof( DwPackHeader ) + ( Entries.Count * sizeof( DwPackEntry ) );
                var newHeaderSize = sizeof( DwPackHeader ) + ( newFileCount * sizeof( DwPackEntry ) );
                var oldNative = Native;
                
                Native = new DwPackPtr( (byte*)Marshal.AllocHGlobal( newHeaderSize ) );
                Unsafe.CopyBlock( (void*)Native.Ptr, (void*)oldNative.Ptr, (uint)oldHeaderSize );
                Native.Header->FileCount = newFileCount;
                oldNative.Dispose();       

                var dataOffset = Entries.Max( x => x.Native->DataOffset + x.Native->CompressedSize );
                for ( int i = oldNative.Header->FileCount; i < Native.Header->FileCount; i++ )
                {
                    var entry = Native.Entries + i;
                    var cpkEntry = cpk.Entries.First( x => x.PacIndex == Native.Header->Index && x.FileIndex == i );

                    entry->Field00 = 0;
                    entry->Index = (short)i;
                    entry->PackIndex = (short)Native.Header->Index;
                    entry->Path = cpkEntry.Path;
                    entry->Field104 = 0;
                    entry->Flags = 0;

                    // These will be filled in later
                    entry->CompressedSize = entry->UncompressedSize = 1;
                    entry->DataOffset = dataOffset;
                    dataOffset += entry->CompressedSize;
                }

                mMapper = new MemoryMapper();
                Entries.Clear();
                LoadEntries();
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
