using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using modloader.Utilities;

namespace modloader.Formats.Xact
{
    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public unsafe struct SoundBankHeader
    {
        public uint Magic;
        public ushort ToolVersion;
        public ushort FormatVersion;
        public ushort Crc;
        public FILETIME LastModified;
        public byte Platform;
        public ushort SimpleCueCount;
        public ushort ComplexCueCount;
        public ushort Field17;
        public ushort TotalCueCount;
        public byte WaveBankCount;
        public ushort SoundCount;
        public ushort CueNameTableLength;
        public ushort Field20;
        public uint SimpleCuesOffset;
        public uint ComplexCuesOffset;
        public uint CueNamesOffset;
        public uint Offset2E;
        public uint VariationTablesOffset;
        public uint Offset36;
        public uint WaveBankNameTableOffset;
        public uint CueNameHashTableOffset;
        public uint CueNameHashValuesOffset;
        public uint SoundsOffset;
        public fixed byte Name[64];
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 5 )]
    public unsafe struct SimpleCue
    {
        public byte Flags;
        public uint SoundOffset;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 8 )]
    public unsafe struct ComplexCueSound
    {
        public uint SoundOffset;
        public uint Field08;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 14 )]
    public unsafe struct ComplexCueParams
    {
        public uint VariationTableOffset;
        public uint TransitionTableOffset;
        public byte InstanceLimit;
        public ushort FadeInSec;
        public ushort FadeOutSec;
        public byte InstanceFlags;
    }

    public enum ComplexCueFlags : byte
    {
        HasSound = 1 << 2
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public unsafe struct ComplexCue
    {
        [FieldOffset(0)] public ComplexCueFlags Flags;
        [FieldOffset(1)] public ComplexCueSound Sound;
        [FieldOffset(1)] public ComplexCueParams Params;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 5 )]
    public unsafe struct WaveVariation
    {
        public ushort TrackIndex;
        public byte WaveBankIndex;
        public byte WeightMin;
        public byte WeightMax;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 6 )]
    public struct SoundVaration
    {
        public uint SoundOffset;
        public byte WeightMin;
        public byte WeightMax;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 10 )]
    public struct SoundVariation2
    {
        public uint SoundOffset;
        public byte WeightMin;
        public byte WeightMax;
        public uint Flags;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 3 )]
    public struct CompactWaveVariation
    {
        public ushort TrackIndex;
        public byte WaveBankIndex;
    }

    public enum VariationType : int
    {
        Wave,
        Sound,
        Sound2,
        CompactWave
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public unsafe struct VariationTable
    {
        public ushort EntryCount;
        public ushort VariationFlags;
        public byte Field03;
        public ushort Field04;
        public byte Field06;

        public VariationType VariationType => ( VariationType )( ( VariationFlags >> 3 ) & 0x7 );
        public void* Entries
        {
            get
            {
                fixed ( VariationTable* pThis = &this )
                    return ( void* )( pThis + 1 );
            }
        }
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 2 )]
    public struct CueNameHashTable
    {
        public ushort Field00;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 6 )]
    public struct CueNameHashValue
    {
        public uint Key;
        public ushort Value;
    }

    public enum EventPlayWaveFlags : byte
    {
        PlayRelease = 1,
        PanEnabled = 2,
        UseCenterSpeaker = 4
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public struct EventPlayWave
    {
        public byte Field00;
        public EventPlayWaveFlags Flags;
        public ushort TrackIndex;
        public byte WaveBankIndex;
        public byte LoopCount;
        public ushort PanAngle;
        public ushort PanArc;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public struct EventPlayWaveVariation
    {
        public EventPlayWave Base;
        public VariationTable VariationTable;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public struct EventPlayWave2
    {
        public byte Field00;
        public EventPlayWaveFlags Flags;
        public ushort TrackIndex;
        public byte WaveBankIndex;
        public byte LoopCount;
        public ushort PanAngle;
        public ushort PanArc;
        public short MinPitch;
        public short MaxPitch;
        public byte MinVolume;
        public byte MaxVolume;
        public float MinFrequency;
        public float MaxFrequency;
        public float MinQ;
        public float MaxQ;
        public byte FieldU1;
        public byte VariationFlags;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public struct EventPlayWaveVariation2
    {
        public EventPlayWave2 Base;
        public VariationTable VariationTable;
    }


    public enum EventType : int
    {
        PlayWave = 1,
        PlayWaveVariation = 3,
        PlayWave2 = 4,
        PlayWaveVariation2 = 6
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public unsafe struct ClipEvent
    {
        public uint EventInfo;
        public ushort RandomOffset;
        public EventType EventType => ( EventType )( EventInfo & 0x1F );
        public void* EventData
        {
            get
            {
                fixed ( ClipEvent* pThis = &this )
                    return ( void* )( pThis + 1 );
            }
        }
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public unsafe struct SoundClipEvents
    {
        public byte EventCount;
        public ClipEvent* Events
        {
            get
            {
                fixed ( SoundClipEvents* pThis = &this )
                    return ( ClipEvent* )( pThis + 1 );
            }
        }
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public unsafe struct SoundClip
    {
        public byte Volume;
        public uint EventsOffset;
        public ushort FilterFlags;
        public ushort FilterFrequency;
    }

    public enum SoundFlags : byte
    {
        Complex = 1,
        Rpc = 0xE,
        Dsp = 0x10
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 3 )]
    public unsafe struct SoundRpcData
    {
        public ushort Length;
        public byte RpcCodeCount;
        public uint* RpcCodes
        {
            get
            {
                fixed ( SoundRpcData* pThis = &this )
                    return ( uint* )( pThis + 1 );
            }
        }
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 3 )]
    public unsafe struct SoundDspData
    {
        public ushort Length;
        public byte DspCodeCount;
        public uint* DspCodes
        {
            get
            {
                fixed ( SoundDspData* pThis = &this )
                    return ( uint* )( pThis + 1 );
            }
        }
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 1 )]
    public unsafe struct SoundComplexData
    {
        public byte ClipCount;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1, Size = 3 )]
    public unsafe struct SoundSimpleData
    {
        public ushort TrackIndex;
        public byte WaveBankIndex;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 9)]
    public unsafe struct Sound
    {
        public SoundFlags Flags;
        public ushort CategoryId;
        public byte Volume;
        public short Pitch;
        public byte Priority;
        public ushort EntryLength;
    }

    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public unsafe struct WaveBankNameTableEntry
    {
        public fixed byte NameBytes[64];

        public string Name
        {
            get
            {
                fixed ( byte* pNameBytes = NameBytes ) return new string( ( sbyte* )pNameBytes );
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct SoundBankPtr
    {
        public byte* Ptr;

        public SoundBankHeader* Header => ( SoundBankHeader* )( Ptr );

        public SimpleCue* SimpleCues => ( SimpleCue* )GetPtr( Header->SimpleCuesOffset );
        public ComplexCue* ComplexCues =>( ComplexCue* )GetPtr( Header->ComplexCuesOffset );
        public sbyte* CueNames => ( sbyte* )GetPtr( Header->CueNamesOffset );
        public VariationTable* VariationTables => ( VariationTable* )GetPtr( Header->VariationTablesOffset );
        public WaveBankNameTableEntry* WaveBankNames => ( WaveBankNameTableEntry* )GetPtr( Header->WaveBankNameTableOffset );
        public CueNameHashTable* CueNameHashTable => ( CueNameHashTable* )GetPtr( Header->CueNameHashTableOffset );
        public CueNameHashValue* CueNameHashValues => ( CueNameHashValue* )GetPtr( Header->CueNameHashValuesOffset );
        public Sound* Sounds => ( Sound* )GetPtr( Header->SoundsOffset );

        public SoundBankPtr(byte* ptr)
        {
            Ptr = ptr;
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal( ( IntPtr )Ptr );
        }

        public void* GetPtr( uint offset ) =>
            ( offset != 0 && offset != uint.MaxValue ) ? ( Ptr + offset ) : null;
    }
}
