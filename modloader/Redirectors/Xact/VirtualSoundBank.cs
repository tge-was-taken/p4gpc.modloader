using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using modloader.Formats.Xact;
using Reloaded.Mod.Interfaces;

namespace modloader.Redirectors.Xact
{
    public unsafe class VirtualSoundBank : IDisposable
    {
        private readonly ILogger mLogger;
        private readonly Dictionary<int, string> mTrackIndexToCueName;

        public SoundBankPtr Native { get; private set; }

        public VirtualSoundBank( ILogger logger )
        {
            mLogger = logger;
            mTrackIndexToCueName = new Dictionary<int, string>();
        }

        public void LoadFromFile( string filePath )
        {
            using ( var fileStream = File.OpenRead( filePath ) )
            {
                var length = (int)fileStream.Length;
                var buffer = ( byte* )Marshal.AllocHGlobal( length );
                fileStream.Read( new Span<byte>( buffer, length ) );
                Native = new SoundBankPtr( buffer );
                CacheCueNames();
            }
        }

        public void Dispose()
        {
            Native.Dispose();
        }

        public string GetCueName( int cueIndex )
        {
            var p = Native.CueNames;
            for ( int i = 0; i < cueIndex; i++ )
            {
                while ( *p++ != 0 ) ;
                p++;
            }

            return new string( --p );
        }

        public string GetTrackCueName( int trackIndex, int waveBankIndex )
        {
            mTrackIndexToCueName.TryGetValue( trackIndex, out var name );
            return name;
        }

        private string[] GetAllCueNames()
        {
            var cueNames = new string[Native.Header->TotalCueCount];
            var p = Native.CueNames;
            for ( int i = 0; i < cueNames.Length; i++ )
            {
                cueNames[i] = new string( ( sbyte* )p );
                while ( *p++ != 0 ) ;
            }

            return cueNames;
        }

        private void CacheCueNames()
        {
            var cueNames = GetAllCueNames();
            var processed = new HashSet<IntPtr>();

            for ( int i = 0; i < Native.Header->SimpleCueCount; i++ )
            {
                var cueIndex = i;
                var cue = Native.SimpleCues + i;
                var sound = (Sound*)Native.GetPtr(cue->SoundOffset);
                if ( sound == null ) continue;

                if ( !processed.Contains( ( IntPtr )sound ) )
                {
                    processed.Add( ( IntPtr )sound );
                    CacheCueNames( cueNames, processed, cueIndex, sound );
                }
            }

            for ( int i = 0; i < Native.Header->ComplexCueCount; i++ )
            {
                var cueIndex = Native.Header->SimpleCueCount + i;
                var cue = Native.ComplexCues + i;
                if ( ( cue->Flags & ComplexCueFlags.HasSound ) != 0 )
                {
                    var sound = (Sound*)Native.GetPtr(cue->Sound.SoundOffset);
                    if ( sound == null ) continue;

                    if ( !processed.Contains( ( IntPtr )sound ) )
                    {
                        processed.Add( ( IntPtr )sound );
                        CacheCueNames( cueNames, processed, cueIndex, sound );
                    }
                }
                else
                {
                    var variationTable = (VariationTable*)Native.GetPtr(cue->Params.VariationTableOffset);
                    if ( variationTable == null ) continue;

                    if ( !processed.Contains( ( IntPtr )variationTable ) )
                    {
                        processed.Add( ( IntPtr )variationTable );
                        CacheCueNames( cueNames, processed, cueIndex, variationTable );
                    }
                }
            }
        }

        private void CacheCueNames( string[] cueNames, HashSet<IntPtr> processed, int cueIndex, Sound* sound)
        {
            if ( ( sound->Flags & SoundFlags.Complex ) != 0 )
            {
                var data = (SoundComplexData*)(sound + 1);
                var clipsOffset = (IntPtr)(data + 1);
                if ( ( sound->Flags & SoundFlags.Rpc ) != 0 )
                {
                    var rpc = (SoundRpcData*)clipsOffset;
                    clipsOffset += rpc->Length;
                }

                if ( ( sound->Flags & SoundFlags.Dsp ) != 0 )
                {
                    var dsp = (SoundDspData*)clipsOffset;
                    clipsOffset += dsp->Length;
                }

                var clips = (SoundClip*)(clipsOffset);
                for ( int j = 0; j < data->ClipCount; j++ )
                {
                    var clip = clips + j;
                    var events = (SoundClipEvents*)Native.GetPtr(clips->EventsOffset);
                    for ( int k = 0; k < events->EventCount; k++ )
                    {
                        var evt = events->Events + k;
                        switch ( evt->EventType )
                        {
                            case EventType.PlayWave:
                                {
                                    var evtData = (EventPlayWave*)evt->EventData;
                                    mTrackIndexToCueName[evtData->TrackIndex] = cueNames[ cueIndex ];
                                }
                                break;
                            case EventType.PlayWave2:
                                {
                                    var evtData = (EventPlayWave2*)evt->EventData;
                                    mTrackIndexToCueName[evtData->TrackIndex] = cueNames[ cueIndex ];
                                }
                                break;
                            case EventType.PlayWaveVariation:
                                {
                                    var evtData = (EventPlayWaveVariation*)evt->EventData;
                                    mTrackIndexToCueName[evtData->Base.TrackIndex] = cueNames[ cueIndex ];
                                    CacheCueNames( cueNames, processed, cueIndex, &evtData->VariationTable );
                                }
                                break;
                            case EventType.PlayWaveVariation2:
                                {
                                    var evtData = (EventPlayWaveVariation2*)evt->EventData;
                                    mTrackIndexToCueName[evtData->Base.TrackIndex] = cueNames[ cueIndex ];
                                    CacheCueNames( cueNames, processed, cueIndex, &evtData->VariationTable );
                                }
                                break;
                            default:
                                //mLogger.WriteLine( $"Unhandled event type!!! {evt->EventType}", mLogger.ColorRed );
                                break;
                        }
                    }
                }
            }
            else
            {
                var data = (SoundSimpleData*)(sound + 1);
                mTrackIndexToCueName[data->TrackIndex] = cueNames[ cueIndex ];
            }
        }

        private void CacheCueNames( string[] cueNames, HashSet<IntPtr> processed, int cueIndex, VariationTable* variationTable )
        {
            for ( int j = 0; j < variationTable->EntryCount; j++ )
            {
                switch ( variationTable->VariationType )
                {
                    case VariationType.Wave:
                    case VariationType.CompactWave:
                        {
                            var variation = (WaveVariation*)variationTable->Entries + j;
                            mTrackIndexToCueName[variation->TrackIndex] = cueNames[ cueIndex ];
                        }
                        break;
                    case VariationType.Sound:
                    case VariationType.Sound2:
                        {
                            var variation = (SoundVaration*)variationTable->Entries + j;
                            var sound = (Sound*)Native.GetPtr(variation->SoundOffset);
                            if ( sound == null ) continue;

                            if ( !processed.Contains( ( IntPtr )sound ) )
                            {
                                processed.Add( ( IntPtr )sound );
                                CacheCueNames( cueNames, processed, cueIndex, sound );                 
                            }
                        }
                        break;
                }
            }
        }
    }
}
