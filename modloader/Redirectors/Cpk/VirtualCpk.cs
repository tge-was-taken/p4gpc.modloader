using modloader.Mods;
using PreappPartnersLib.FileSystems;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace modloader.Redirectors.Cpk
{
    public unsafe class VirtualCpk
    {
        private readonly SemanticLogger mLogger;
        private CpkFile mWrapper;

        public CompressedCpkPtr Native { get; private set; }
        public IReadOnlyList<CpkFileEntry> Entries => mWrapper.Entries; 
        public string FileName { get; private set; }

        public VirtualCpk( SemanticLogger logger )
        {
            mLogger = logger;
        }

        public void LoadFromFile( string filePath, Stream stream )
        {
            FileName = Path.GetFileNameWithoutExtension( filePath );

            // Read compressed buffer
            var buffer = (void*)Marshal.AllocHGlobal( (int)stream.Length );
            var bufferSpan = new Span<byte>( buffer, (int)stream.Length );
            stream.Read( bufferSpan );
            Native = new CompressedCpkPtr( buffer );

            // Parse CPK
            mWrapper = new CpkFile();
            mWrapper.Read( bufferSpan, CompressionState.Compressed );
        }

        public void Redirect( ModDb modDb )
        {
            var newPacIndex = Entries.Max( x => x.PacIndex );
            var newFileIndex = Entries.Where( x => x.PacIndex == newPacIndex ).Max( x => x.FileIndex ) + 1;
            var newFileCount = 0;
            var rebuild = false;

            foreach ( var mod in modDb.Mods )
            {
                foreach ( var file in mod.Files )
                {
                    var relPath = file.Substring( file.IndexOf( mod.LoadDirectory ) + mod.LoadDirectory.Length + 1 );
                    var cpkName = relPath.Substring( 0, relPath.IndexOf( '\\' ) );
                    if ( !cpkName.Equals( FileName, StringComparison.OrdinalIgnoreCase ) )
                        continue;

                    var cpkRelPath = file.Substring( file.IndexOf( cpkName ) + cpkName.Length + 1 );
                    var index = mWrapper.Entries.FindIndex( x => x.Path.Equals( cpkRelPath, StringComparison.OrdinalIgnoreCase ) );

                    if ( index == -1 )
                    {
                        // This is a new file
                        mWrapper.Entries.Add( new CpkFileEntry() { Path = cpkRelPath, PacIndex = (short)newPacIndex, FileIndex = (short)newFileIndex++ } );
                        ++newFileCount;
                        rebuild = true;
                    }
                    //else if ( FileName == "sysdat")
                    //{
                    //    mWrapper.Entries[ index ].PacIndex = (short)newPacIndex;
                    //    mWrapper.Entries[ index ].FileIndex = (short)newFileIndex++;
                    //}
                }
            }

            if ( rebuild )
            {
                Rebuild( newFileCount );
            }
        }

        private void Rebuild( int addedFiles )
        {
            // Write uncompressed CPK
            mLogger.Debug( $"{FileName}: Rebuilding" );

            var uncompressedSize = (int)( Native.Header->UncompressedSize + ( addedFiles * CpkEntry.SIZE ) );
            using var tempBuf = MemoryPool<byte>.Shared.Rent( uncompressedSize );
            var tempBufSpan = tempBuf.Memory.Span.Slice( 0, uncompressedSize );
            mWrapper.Write( tempBufSpan );

            // Compress it
            var comBuffer = (void*)Marshal.AllocHGlobal( uncompressedSize );
            var comSize = CpkUtil.CompressCpk( tempBufSpan, new Span<byte>( comBuffer, uncompressedSize ) );

            // Replace old data
            Native.Dispose();
            Native = new CompressedCpkPtr( comBuffer );
        }
    }

    public class CpkRedirectionInfo
    {
        public string FileName { get; }
        public IReadOnlyList<CpkEntryRedirectionInfo> Entries { get; }

        public CpkRedirectionInfo( string fileName, List<CpkEntryRedirectionInfo> entries )
        {
            FileName = fileName;
            Entries = entries;
        }
    }

    public class CpkEntryRedirectionInfo
    {
        public string Path { get; }
        public short FileIndex { get; }
        public short PacIndex { get; }
        public string RedirectedPath { get; }

        public CpkEntryRedirectionInfo( string path, short fileIndex, short pacIndex, string redirectedPath )
        {
            Path = path;
            FileIndex = fileIndex;
            PacIndex = pacIndex;
            RedirectedPath = redirectedPath;
        }
    }
}
