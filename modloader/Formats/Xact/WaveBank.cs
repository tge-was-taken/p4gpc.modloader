using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Amicitia.IO;
using Amicitia.IO.Generics;

namespace modloader.Formats.Xact
{
    public static class Constants
    {
        public const int WAVEBANK_HEADER_SIGNATURE  = 0x444E4257;      // WaveBank  RIFF chunk signature
        public const int WAVEBANK_HEADER_VERSION  = 44;          // Current wavebank file version
        public const int WAVEBANK_BANKNAME_LENGTH = 64;         // Wave bank friendly name length, in characters
        public const int WAVEBANK_ENTRYNAME_LENGTH = 64;         // Wave bank entry friendly name length, in characters
        public const uint WAVEBANK_MAX_DATA_SEGMENT_SIZE = 0xFFFFFFFF;  // Maximum wave bank data segment size, in bytes
        public const uint WAVEBANK_MAX_COMPACT_DATA_SEGMENT_SIZE = 0x001FFFFF;  // Maximum compact wave bank data segment size, in bytes

        //
        // Arbitrary fixed sizes
        //
        public const int WAVEBANKENTRY_XMASTREAMS_MAX  = 3;   // enough for 5.1 channel audio
        public const int WAVEBANKENTRY_XMACHANNELS_MAX = 6;   // enough for 5.1 channel audio (cf. XAUDIOCHANNEL_SOURCEMAX)

        //
        // DVD data sizes
        //

        public const int WAVEBANK_DVD_SECTOR_SIZE = 2048;
        public const int WAVEBANK_DVD_BLOCK_SIZE  = (WAVEBANK_DVD_SECTOR_SIZE * 16);

        //
        // Bank alignment presets
        //

        public const int WAVEBANK_ALIGNMENT_MIN = 4;                          // Minimum alignment
        public const int WAVEBANK_ALIGNMENT_DVD = WAVEBANK_DVD_SECTOR_SIZE;    // DVD-optimized alignment

        public const int XMA_OUTPUT_SAMPLE_BYTES = 2;
        public const int XMA_OUTPUT_SAMPLE_BITS = 16;
        public const int ADPCM_MINIWAVEFORMAT_BLOCKALIGN_CONVERSION_OFFSET = 22;
        public const int MAX_WMA_AVG_BYTES_PER_SEC_ENTRIES = 7;
        public readonly static int[] WMA_AVG_BYTES_PER_SEC = new int[7]
        {
            12000,
            24000,
            4000,
            6000,
            8000,
            20000,
            2500
        };
        public readonly static int[] WMA_BLOCK_ALIGN = new int[17]
        {
            929,
            1487,
            1280,
            2230,
            8917,
            8192,
            4459,
            5945,
            2304,
            1536,
            1485,
            1008,
            2731,
            4096,
            6827,
            5462,
            1280
        };
    }


    //
    // Bank flags
    //

    public enum WaveBankType
    {
        Buffer,
        Streaming,
        Mask
    }


    [Flags]
    public enum WaveBankFlags : uint
    {
        EntryNames = 0x10000,
        Compact = 0x20000,
        SyncDisabled = 0x40000,
        SeekTables = 0x80000,
        Mask = 0xF0000
    }


    //
    // Entry flags
    //
    [Flags]
    public enum WaveBankEntryFlags
    {
        ReadAhead = 0x1,
        LoopCache = 0x2,
        RemoveLoopTail = 0x4,
        IgnoreLoop = 0x8,
    }


    //
    // Entry wave format identifiers
    //

    public enum WaveBankMiniFormatTag
    {
        PCM,
        XMA,
        ADPCM,
        WMA
    }

    public enum WaveBankMiniFormatBitDepth
    {
        _8,
        _16
    }

    //
    // Wave bank segment identifiers
    //
    public enum WaveBankSegmentIndex
    {
        BankData,
        EntryMetadata,
        SeekTables,
        EntryNames,
        EntryWaveData,
        Count
    }

    //
    // Wave bank region in bytes.
    //
    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = WaveBankRegion.SIZE )]
    public struct WaveBankRegion
    {
        public const int SIZE = 8;

        public int Offset;
        public int Length;
    }

    //
    // Wave bank region in samples.
    //
    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 4 )]
    public struct WaveBankSampleRegion
    {
        public const int SIZE = 8;

        public int StartSample;
        public int TotalSamples;
    }

    //
    // Wave bank file header
    //
    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 52 )]
    public unsafe struct WaveBankHeader
    {
        public uint Signature; // File signature
        public uint Version; // Version of the tool that created the file
        public uint HeaderVersion; // Version of the file format
        public fixed byte SegmentsBytes[(int)WaveBankSegmentIndex.Count * WaveBankRegion.SIZE];    // Segment lookup table

        public WaveBankRegion* Segments
        {
            get
            {
                fixed ( byte* pSegmentsBytes = SegmentsBytes )
                    return ( WaveBankRegion* )pSegmentsBytes;
            }
        }
    };

    //
    // Entry compressed data format
    //

    [StructLayout( LayoutKind.Explicit, Pack = 1, Size = 4 )]
    public struct WaveBankMiniWaveFormat
    {
        public const int SIZE = 4;

        [FieldOffset(0)] public BitField<int, WaveBankMiniFormatTag, N0, N1> FormatTag;
        [FieldOffset(0)] public BitField<int, N2, N4> Channels;
        [FieldOffset(0)] public BitField<int, N5, N22> SamplesPerSecond;
        [FieldOffset(0)] public BitField<int, N23, N30> BlockAlign;
        [FieldOffset(0)] public BitField<int, WaveBankMiniFormatBitDepth, N31, N31> BitsPerSample;
        [FieldOffset(0)] public BitField<uint, N0, N31> Value;

        public int CalculatedBitsPerSample
        {
            get
            {
                switch ( FormatTag.Get() )
                {
                    case WaveBankMiniFormatTag.XMA:
                        return 16;
                    case WaveBankMiniFormatTag.ADPCM:
                        return 4;
                    default:
                        return ( BitsPerSample.Get() == WaveBankMiniFormatBitDepth._16 ) ? 16 : 8;
                }
            }
        }

        public int CalculatedBlockAlign
        {
            get
            {
                switch ( FormatTag.Get() )
                {
                    case WaveBankMiniFormatTag.PCM:
                        return BlockAlign;
                    case WaveBankMiniFormatTag.XMA:
                        return ( int )Channels * 16 / 8;
                    case WaveBankMiniFormatTag.ADPCM:
                        return ( ( int )BlockAlign + 22 ) * ( int )Channels;
                    case WaveBankMiniFormatTag.WMA:
                        return ( ( ( int )BlockAlign & 0x1F ) < 17 ) ? Constants.WMA_BLOCK_ALIGN[( int )BlockAlign & 0x1F] : ( ( int )BlockAlign & 0x1F );
                    default:
                        return 0;
                }
            }

            set
            {
                switch ( FormatTag.Get() )
                {
                    case WaveBankMiniFormatTag.PCM:
                        BlockAlign.Set( value );
                        break;
                    case WaveBankMiniFormatTag.XMA:
                        BlockAlign.Set( value / ( 16 / 8 ) );
                        break;
                    case WaveBankMiniFormatTag.ADPCM:
                        BlockAlign.Set( ( value / Channels.Get() ) - 22 );
                        break;
                    case WaveBankMiniFormatTag.WMA:
                        var temp =  Array.IndexOf( Constants.WMA_BLOCK_ALIGN, value );
                        if ( temp != -1 )
                            BlockAlign.Set( temp );
                        else
                            BlockAlign.Set( value );
                        break;
                }
            }
        }

        public int AverageBytesPerSecond
        {
            get
            {
                switch ( FormatTag.Get() )
                {
                    case WaveBankMiniFormatTag.PCM:
                    case WaveBankMiniFormatTag.XMA:
                        return ( int )SamplesPerSecond * CalculatedBlockAlign;
                    case WaveBankMiniFormatTag.ADPCM:
                        {
                            int blockAlign = CalculatedBlockAlign;
                            int samplesPerAdpcmBlock = AdpcmSamplesPerBlock;
                            return blockAlign * ( int )SamplesPerSecond / samplesPerAdpcmBlock;
                        }
                    case WaveBankMiniFormatTag.WMA:
                        {
                            int bytesPerSecIndex = CalculatedBlockAlign >> 5;
                            if ( bytesPerSecIndex < 7 )
                            {
                                return Constants.WMA_AVG_BYTES_PER_SEC[bytesPerSecIndex];
                            }
                            return 0;
                        }
                    default:
                        return 0;
                }
            }
        }

        public int AdpcmSamplesPerBlock
        {
            get
            {
                int blockAlign = CalculatedBlockAlign + 22 + (int)Channels;
                return blockAlign * 2 / ( int )Channels - 12;
            }
        }
    }

    //
    // Entry meta-data
    //

    [StructLayout( LayoutKind.Explicit, Pack = 1, Size = 4 )]
    public struct WaveBankEntryFlagsAndDuration
    {
        public const int SIZE = 4;

        [FieldOffset(0)] public BitField<uint, WaveBankEntryFlags, N0, N3> Flags;
        [FieldOffset(0)] public BitField<uint, N4, N31> Duration;
        [FieldOffset(0)] public uint Value;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 24 )]
    public struct WaveBankEntry
    {
        public WaveBankEntryFlagsAndDuration FlagsAndDuration;
        public WaveBankMiniWaveFormat  Format;         // Entry format.
        public WaveBankRegion          PlayRegion;     // Region within the wave data segment that contains this entry.
        public WaveBankSampleRegion    LoopRegion;     // Region within the wave data (in samples) that should loop.
    }

    //
    // Compact entry meta-data
    //
    [StructLayout( LayoutKind.Explicit, Pack = 1, Size = 4 )]
    public struct WaveBankEntryCompact
    {
        [FieldOffset(0)] public BitField<uint, N0, N20> dwOffset; // Data offset, in sectors
        [FieldOffset(0)] public BitField<uint, N21, N31> dwLengthDeviation; // Data length deviation, in bytes

        public long CalculatedOffset
            => dwOffset * Constants.WAVEBANK_DVD_SECTOR_SIZE;
    };

    //
    // Bank data segment
    //
    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 96 )]
    public unsafe struct WaveBankData
    {
        public const int BANKNAME_LENGTH = 64;

        public WaveBankFlags Flags;                                // Bank flags
        public uint EntryCount;                           // Number of entries in the bank
        public fixed byte BankName[BANKNAME_LENGTH];   // Bank friendly name
        public uint EntryMetaDataElementSize;             // Size of each entry meta-data element, in bytes
        public uint EntryNameElementSize;                 // Size of each entry name element, in bytes
        public uint Alignment;                            // Entry alignment, in bytes
        public WaveBankMiniWaveFormat CompactFormat;                          // Format data for compact bank
        public FILETIME BuildTime;                              // Build timestamp
    };
}
