using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Amicitia.IO;
using modloader.Formats.Xact;
using Reloaded.Mod.Interfaces;

namespace modloader.Redirectors.Xact
{
    internal unsafe class VirtualWaveBankEntry : IDisposable
    {
        private readonly SemanticLogger mLogger;

        public VirtualWaveBank WaveBank { get; private set; }
        public void* NativePtr { get; private set; }
        public WaveBankEntry* Native => ( WaveBankEntry* )NativePtr;
        public WaveBankEntryCompact* NativeCompact => ( WaveBankEntryCompact* )NativePtr;
        public bool IsCompact => ( WaveBank.Native.Data->Flags & WaveBankFlags.Compact ) != 0;
        public int Index { get; private set; }
        public string CueName { get; set; }

        public bool IsRedirected { get; private set; }
        public string FilePath { get; private set; }
        public string FileName { get; private set; }
        public long FileSize { get; private set; }

        public VirtualWaveBankEntry( SemanticLogger logger, VirtualWaveBank waveBank, void* entry, int index, string cueName )
        {
            mLogger = logger;
            WaveBank = waveBank;
            NativePtr = entry;
            Index = index;
            CueName = cueName;
        }

        public bool Redirect( string filePath )
        {
            if ( IsCompact )
            {
                mLogger.Error( $"Compact wave format not implemented, unable to redirect to {filePath}" );
                return false;
            }

            var fileSize = 0L;
            using ( var stream = new FileStream( filePath, FileMode.Open, FileAccess.Read, FileShare.Read ) )
                fileSize = stream.Length;

            if ( fileSize == 0 )
            {
                mLogger.Error( $"{filePath} is empty" );
                return false;
            }

            var txthPath = filePath + ".txth";
            if ( !File.Exists( txthPath ) )
            {
                mLogger.Error( $"{filePath} Missing .txth file! Expected location: {txthPath}" );
                return false;
            }

            if ( !ParseTxth( txthPath ) )
            {
                mLogger.Error( $"{txthPath} Failed to parse. Make sure the file is formatted correctly." );
                return false;
            }

            if ( !IsCompact )
            {
                var originalLength = Native->PlayRegion.Length;

                // Try to recalculate the actual size based on the duration
                if (!WaveBankFormatHelper.TryGetSamplesToByteOffset( Native->Format.FormatTag.Get(), Native->Format.BitsPerSample.Get(), Native->FlagsAndDuration.Duration, 
                    Native->Format.CalculatedBlockAlign, Native->Format.Channels, out Native->PlayRegion.Length ))
                {
                    // In case we can't, just accept the file size
                    Native->PlayRegion.Length = (int)fileSize;
                }

                if ( Native->PlayRegion.Length > originalLength )
                {
                    // If the new size exceeds that of the old, allocate new memory for it
                    Native->PlayRegion.Offset = ( int )( WaveBank.AllocateSectionMemory( WaveBankSegmentIndex.EntryWaveData, ( int )Native->PlayRegion.Length ) );
                }
            }
            else
            {
                mLogger.Error( $"Compact wave format not implemented, unable to redirect to {filePath}" );
                return false;
            }

            FilePath = filePath;
            FileName = Path.GetFileNameWithoutExtension( FilePath );
            FileSize = fileSize;
            IsRedirected = true;
            return true;
        }

        private struct ParsedTxth
        {
            public int? Duration;
            public int? Channels;
            public int? SampleRate;
            public int? Interleave;
            public int? LoopStart;
            public int? LoopEnd;
            public WaveBankMiniFormatTag? FormatTag;
            public WaveBankMiniFormatBitDepth? BitDepth;
        }

        private bool ParseTxth( string path )
        {
            using ( var reader = new StreamReader( path ) )
            {
                var txth = new ParsedTxth();

                while ( !reader.EndOfStream )
                {
                    var line = reader.ReadLine();
                    var kvp = line.Split("=", StringSplitOptions.RemoveEmptyEntries);
                    var key = kvp[0].Trim();
                    var value = kvp[1].Trim();
                    int temp = 0;

                    switch ( key )
                    {
                        case "num_samples":
                            if ( !int.TryParse( value, out temp ) ) return false;
                            txth.Duration = temp;
                            break;

                        case "codec":
                            switch ( value )
                            {
                                case "PCM16LE":
                                    txth.FormatTag = WaveBankMiniFormatTag.PCM;
                                    txth.BitDepth = WaveBankMiniFormatBitDepth._16;
                                    break;

                                case "PCM8":
                                    txth.FormatTag = WaveBankMiniFormatTag.PCM;
                                    txth.BitDepth = WaveBankMiniFormatBitDepth._8;
                                    break;

                                case "XMA2":
                                    txth.FormatTag = WaveBankMiniFormatTag.XMA;
                                    break;

                                case "MSADPCM":
                                    txth.FormatTag = WaveBankMiniFormatTag.ADPCM;
                                    break;

                                case "XWMA":
                                    txth.FormatTag = WaveBankMiniFormatTag.WMA;
                                    break;

                                default:
                                    mLogger.Error( $"Unsupported codec ({value})" );
                                    return false;
                            }
                            break;

                        case "channels":
                            if ( !int.TryParse( value, out temp ) ) return false;
                            txth.Channels = temp;
                            break;

                        case "sample_rate":
                            if ( !int.TryParse( value, out temp ) ) return false;
                            txth.SampleRate = temp;
                            break;

                        case "interleave":
                            if ( !int.TryParse( value, out temp ) ) return false;
                            txth.Interleave = temp;
                            break;

                        case "loop_start_sample":
                            if ( !int.TryParse( value, out temp ) ) return false;
                            txth.LoopStart = temp;
                            break;

                        case "loop_end_sample":
                            if ( !int.TryParse( value, out temp ) ) return false;
                            txth.LoopEnd = temp;
                            break;

                        default:
                            mLogger.Error( $"{path} Unrecognized TXTH command: {key} = {value}" );
                            break;
                    }
                }

                if ( !txth.Duration.HasValue )
                {
                    mLogger.Error( $"{path} num_samples is not set!" );
                    return false;
                }

                if ( !txth.FormatTag.HasValue )
                {
                    mLogger.Error( $"{path} codec is not set!" );
                    return false;
                }

                if ( !txth.Channels.HasValue )
                {
                    mLogger.Error( $"{path} channels is not set!" );
                    return false;
                }

                if ( !txth.SampleRate.HasValue )
                {
                    mLogger.Error( $"{path} sample_rate is not set!" );
                    return false;
                }

                if ( !txth.Interleave.HasValue )
                {
                    mLogger.Error( $"{path} interleave is not set!" );
                    return false;
                }

                if ( txth.LoopStart.HasValue && !txth.LoopEnd.HasValue )
                {
                    // Set end loop sample to the duration if it is somehow missing
                    txth.LoopEnd = txth.Duration;
                }

                // Set settings              
                Native->Format.FormatTag.Set( txth.FormatTag.Value );
                Native->Format.BitsPerSample.Set( txth.BitDepth.GetValueOrDefault( 0 ) );
                Native->Format.Channels.Set( txth.Channels.Value );
                Native->Format.SamplesPerSecond.Set( txth.SampleRate.Value );
                Native->Format.CalculatedBlockAlign = txth.Interleave.Value;

                var durationAligned = WaveBankFormatHelper.AlignSamples( Native->Format.FormatTag.Get(), txth.Duration.Value, 
                    Native->Format.CalculatedBlockAlign, Native->Format.Channels );

                if ( txth.LoopStart.HasValue )
                {
                    var startSampleAligned = WaveBankFormatHelper.AlignSamples( Native->Format.FormatTag.Get(), txth.LoopStart.Value,
                        Native->Format.CalculatedBlockAlign, Native->Format.Channels );
                    var startSampleAlignedDiff = startSampleAligned - txth.LoopStart.Value;
                    var endSampleAligned = WaveBankFormatHelper.AlignSamples( Native->Format.FormatTag.Get(), txth.LoopEnd.Value + startSampleAlignedDiff,
                        Native->Format.CalculatedBlockAlign, Native->Format.Channels );
                    var endSampleAlignedOff = endSampleAligned - txth.LoopEnd.Value;

                    if ( startSampleAlignedDiff > 0 ) mLogger.Warning( $"{path} Loop start sample is not aligned properly, it may not loop correctly ingame!" );
                    if ( endSampleAlignedOff > 0 ) mLogger.Warning( $"{path} Loop end sample is not aligned properly, it may not loop correctly ingame!" );

                    Native->LoopRegion.StartSample = startSampleAligned;
                    Native->LoopRegion.TotalSamples = endSampleAligned - startSampleAligned;

                    if ( endSampleAligned > durationAligned )
                        durationAligned = endSampleAligned;
                }
                else
                {
                    Native->LoopRegion.StartSample = 0;
                    Native->LoopRegion.TotalSamples = 0;
                }

                if ( durationAligned > txth.Duration )
                    mLogger.Warning( $"[{path} Duration is not aligned properly, it may not play correctly ingame!" );

                Native->FlagsAndDuration.Duration.Set( ( uint )durationAligned );
            }

            return true;
        }

        public Stream OpenRead()
            => new FileStream( FilePath, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Write, 0x20000, FileOptions.RandomAccess );

        public void Dispose()
        {
            Marshal.FreeHGlobal( ( IntPtr )NativePtr );
        }
    }
}
