using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Amicitia.IO;
using modloader.Formats.Xact;
using Reloaded.Mod.Interfaces;

namespace modloader.Redirectors.Xact
{
    internal unsafe class VirtualWaveBank
    {
        private readonly ILogger mLogger;

        public const long MAX_FILE_SIZE = uint.MaxValue;

        public NativeWaveBank Native { get; private set; }
        public string FilePath { get; private set; }
        public string FileName { get; private set; }
        public long FilePointer { get; set; }
        public long RealFileSize { get; private set; }
        public long VirtualFileSize { get; private set; }
        public List<VirtualWaveBankEntry> Entries { get; }

        public VirtualWaveBank( ILogger logger )
        {
            mLogger = logger;
            Entries = new List<VirtualWaveBankEntry>();
        }

        public void LoadFromFile( string filePath )
        {
            Debugger.Launch();
            FilePath = filePath;
            FileName = Path.GetFileNameWithoutExtension( filePath );
            FilePointer = 0;

            using ( var fileStream = new FileStream( filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.None ) )
            {
                // Get file size
                RealFileSize = fileStream.Length;
                VirtualFileSize = RealFileSize;

                // Read header to determine where the data starts & so we can read all of the headers
                Span<byte> headerSpan = stackalloc byte[sizeof(WaveBankHeader)];
                fileStream.Read( headerSpan );
                var entryWaveDataOffset = 0;
                fixed ( byte* headerBuffer = headerSpan )
                    entryWaveDataOffset = ( ( WaveBankHeader* )headerBuffer )->Segments[( int )WaveBankSegmentIndex.EntryWaveData].Offset;

                // Read the rest of the header data
                var alignedSize = AlignmentHelper.Align( entryWaveDataOffset, 0x1000 );
                var buffer = (byte*)Marshal.AllocHGlobal( alignedSize );
                fileStream.Seek( 0, SeekOrigin.Begin );
                fileStream.Read( new Span<byte>( buffer, alignedSize ) );
                Native = new NativeWaveBank( buffer );
            }

            // Load entries
            for ( int i = 0; i < Native.Data->EntryCount; i++ )
            {
                var entry = Native.Entries + i;
                Entries.Add( new VirtualWaveBankEntry( mLogger, this, entry, i, null ) );
            }
        }

        public long AllocateSectionMemory( WaveBankSegmentIndex segmentIndex, int length )
        {
            var segment = Native.Header->Segments + (int)segmentIndex;
            var origVirtualSize = VirtualFileSize;
            var offset = AlignmentHelper.Align( VirtualFileSize, 0x10000 );
            VirtualFileSize = offset + length;

            var segmentOffset = offset - segment->Offset;
            if ( segmentOffset > uint.MaxValue )
                mLogger.WriteLine( "[modloader:XactRedirector] E: Out of available memory! 4GB address space exhausted", mLogger.ColorRed );

            segment->Length += ( int )( VirtualFileSize - origVirtualSize );
            return segmentOffset;
        }
    }
}
