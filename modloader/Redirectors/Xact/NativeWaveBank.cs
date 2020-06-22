using System;
using System.Runtime.InteropServices;
using modloader.Formats.Xact;

namespace modloader.Redirectors.Xact
{
    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public unsafe struct NativeWaveBank : IDisposable
    {
        public byte* Ptr;
        public WaveBankHeader* Header => ( WaveBankHeader* )Ptr;
        public WaveBankData* Data => ( WaveBankData* )( Ptr + Header->Segments[( int )WaveBankSegmentIndex.BankData].Offset );
        public WaveBankEntry* Entries => ( WaveBankEntry* )( Ptr + Header->Segments[( int )WaveBankSegmentIndex.EntryMetadata].Offset );
        public WaveBankEntryCompact* CompactEntries => ( WaveBankEntryCompact* )( Ptr + Header->Segments[( int )WaveBankSegmentIndex.EntryMetadata].Offset );
        public byte* SeekTables => ( byte* )( Ptr + Header->Segments[( int )WaveBankSegmentIndex.SeekTables].Offset );
        public byte* EntryNames => ( byte* )( Ptr + Header->Segments[( int )WaveBankSegmentIndex.EntryNames].Offset );
        public byte* EntryWaveData => ( byte* )( Ptr + Header->Segments[( int )WaveBankSegmentIndex.EntryWaveData].Offset );

        public NativeWaveBank( byte* ptr )
        {
            Ptr = ptr;
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal( ( IntPtr )Ptr );
        }
    }
}
