using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32.SafeHandles;
using modloader.Formats.Xact;
using modloader.Mods;
using modloader.Utilities;
using Reloaded.Mod.Interfaces;
using static modloader.Native;

namespace modloader.Redirectors.Xact
{
    public unsafe class XactRedirector : FileAccessClient
    {
        private struct CacheEntry
        {
            public VirtualWaveBankEntry Entry;
            public int Score;
            public Stream Stream;

            public void Hit() => Score += 1;
            public bool Miss()
            {
                Score -= 1;
                if ( Entry != null && Score <= 0 )
                {
                    // Invalidate
                    Score = 0;
                    Entry = null;
                    Stream.Dispose();
                    Stream = null;
                    return true;
                }

                return false;
            }
        }

        private readonly SemanticLogger mLogger;
        private readonly ModDb mModDb;
        private readonly Dictionary<IntPtr, VirtualWaveBank> mWaveBankByHandle;
        private readonly Dictionary<string, VirtualWaveBank> mWaveBankByName;
        private readonly CacheEntry[] mCache = new CacheEntry[4];
        private readonly Dictionary<IntPtr, VirtualSoundBank> mSoundBankByHandle;
        private readonly Dictionary<string, VirtualSoundBank> mSoundBankByName;

        public XactRedirector(ILogger logger, ModDb modDb)
        {
            mLogger = new SemanticLogger( logger, "[modloader:XactRedirector]" );
            mModDb = modDb;
            mWaveBankByName = new Dictionary<string, VirtualWaveBank>();
            mWaveBankByHandle = new Dictionary<IntPtr, VirtualWaveBank>();
            mSoundBankByName = new Dictionary<string, VirtualSoundBank>();
            mSoundBankByHandle = new Dictionary<IntPtr, VirtualSoundBank>();
        }

        public override bool Accept( string newFilePath )
        {
            var ext = Path.GetExtension( newFilePath );
            return ext.Equals( ".xwb", StringComparison.OrdinalIgnoreCase ) ||
                   ext.Equals( ".xsb", StringComparison.OrdinalIgnoreCase );             
        }

        public override bool Accept( IntPtr handle )
        {
            return mWaveBankByHandle.ContainsKey( handle ) || mSoundBankByHandle.ContainsKey( handle );
        }

        private void Read(IntPtr handle, long offset, int length, byte* buffer)
        {
            var ioStatus = new IO_STATUS_BLOCK();
            mHooks.NtReadFileHook.OriginalFunction( handle, IntPtr.Zero, null, null, ref ioStatus, buffer, ( uint )length, (LARGE_INTEGER*)&offset, IntPtr.Zero );
        }

        public override Native.NtStatus NtCreateFileImpl( string newFilePath, out IntPtr handle, FileAccess access, ref Native.OBJECT_ATTRIBUTES objectAttributes, 
            ref Native.IO_STATUS_BLOCK ioStatus, ref long allocSize, uint fileAttributes, FileShare share, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength )
        {
            var result = mHooks.NtCreateFileHook.OriginalFunction( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes,
                share, createDisposition, createOptions, eaBuffer, eaLength );

            var fileName = Path.GetFileNameWithoutExtension( newFilePath );
            var ext = Path.GetExtension( newFilePath );
            var isWaveBank = ext.Equals( ".xwb", StringComparison.OrdinalIgnoreCase );
            VirtualWaveBank waveBank = null;
            VirtualSoundBank soundBank = null;

            if ( isWaveBank && !mWaveBankByName.TryGetValue( fileName, out waveBank ) )
            {
                // Try get sound bank for cue names
                mSoundBankByName.TryGetValue( fileName, out soundBank );

                // Wave bank
                mWaveBankByName[fileName] = waveBank = new VirtualWaveBank( mLogger );

                // Load wave bank
                using ( var fileStream = new FileStream( new SafeFileHandle( handle, true ), FileAccess.Read, 1024 * 1024 ) )
                    waveBank.LoadFromFile( newFilePath, fileStream );

                // Reopen file to reset it
                result = mHooks.NtCreateFileHook.OriginalFunction( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes,
                    share, createDisposition, createOptions, eaBuffer, eaLength );

                ProcessWaveBankEntries( newFilePath, soundBank, waveBank );
                mLogger.Debug( $"{newFilePath} registered" );
            }
            else if ( !mSoundBankByName.TryGetValue( fileName, out soundBank ))
            {
                // Sound bank
                mSoundBankByName[fileName] = soundBank = new VirtualSoundBank( mLogger );

                // Load wave bank
                using ( var fileStream = new FileStream( new SafeFileHandle( handle, true ), FileAccess.Read, 1024 * 1024 ) )
                    soundBank.LoadFromFile( newFilePath, fileStream );

                // Reopen file to reset it
                result = mHooks.NtCreateFileHook.OriginalFunction( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes,
                    share, createDisposition, createOptions, eaBuffer, eaLength );

                // Find associated wave bank
                if (!mWaveBankByName.TryGetValue(soundBank.Native.WaveBankNames[0].Name, out waveBank))
                {
                    mLogger.Error( $"{newFilePath} Can't find wavebank!" );
                }
                else
                {
                    ProcessWaveBankEntries( newFilePath, soundBank, waveBank );
                }
            }

            if ( isWaveBank )
            {
                mWaveBankByHandle.Add( handle, waveBank );
                mLogger.Debug( $"{waveBank.FileName} Hnd {handle} registered" );
            }
            else
            {
                mSoundBankByHandle.Add( handle, soundBank );
            }


            return result;
        }

        private void ProcessWaveBankEntries( string filePath, VirtualSoundBank soundBank, VirtualWaveBank waveBank )
        {
            bool TryRedirectEntry(Mod mod, int i, string fileName)
            {
                var redirectFilePath = Path.Combine(Path.Combine(mod.LoadDirectory, "SND"), waveBank.FileName, fileName);
                if ( File.Exists( redirectFilePath ) )
                {
                    if ( waveBank.Entries[i].Redirect( redirectFilePath ) )
                        mLogger.Info( $"{waveBank.FileName} Index: {i} Cue: {waveBank.Entries[i].CueName} redirected to {redirectFilePath}" );
                    else
                        mLogger.Error( $"{waveBank.FileName} Index: {i} Cue: {waveBank.Entries[i].CueName} redirecting to {redirectFilePath} failed!" );

                    return true;
                }

                return false;
            }

            for ( int i = 0; i < waveBank.Entries.Count; i++ )
            {
                if ( soundBank != null )
                {
                    waveBank.Entries[i].CueName = soundBank.GetTrackCueName( i, 0 );
                }

                if ( waveBank.Entries[i].IsRedirected ) continue;

                foreach ( var mod in mModDb.Mods )
                {
                    // Try redirect from wave/track index
                    if ( TryRedirectEntry( mod, i, $"{i}.raw" ) ) 
                        break;

                    if ( waveBank.Entries[i].CueName != null )
                    {
                        // Try redirect from cue name
                        if ( TryRedirectEntry( mod, i, waveBank.Entries[i].CueName + ".raw" ) )
                            break;
                    }
                }
            }
        }

        public override bool CloseHandleImpl( IntPtr handle )
        {
            mWaveBankByHandle.Remove( handle );
            mSoundBankByHandle.Remove( handle );
            return mHooks.CloseHandleHook.OriginalFunction( handle );
        }

        private void SetBytesRead( IntPtr handle, int offset, int length, ref IO_STATUS_BLOCK ioStatus )
        {
            offset += length;
            NtSetInformationFileImpl( handle, out _, &offset, sizeof( long ), FileInformationClass.FilePositionInformation );

            // Set number of read bytes.
            ioStatus.Status = 0;
            ioStatus.Information = new IntPtr( length );
        }

        private NtStatus ReadEntryWaveData( VirtualWaveBank waveBank, long absOffset, 
            IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext, ref Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, Native.LARGE_INTEGER* byteOffset, IntPtr key)
        {
            var status = NtStatus.Success;
            var handled = false;

            var segBaseOffset = waveBank.Native.Header->Segments[(int)WaveBankSegmentIndex.EntryWaveData].Offset;
            var segOffset = absOffset - segBaseOffset;
            for ( int i = 0; i < waveBank.Entries.Count; i++ )
            {
                var entry = waveBank.Entries[i];
                if ( segOffset < entry.Native->PlayRegion.Offset || segOffset >= ( entry.Native->PlayRegion.Offset + entry.Native->PlayRegion.Length ) )
                    continue;

                var fileDataOffset = segOffset - entry.Native->PlayRegion.Offset;
                var readEndOffset = fileDataOffset + length;
                var nextDataOffset = i < waveBank.Entries.Count - 1 ? waveBank.Entries[i + 1].Native->PlayRegion.Offset : ( waveBank.VirtualFileSize - segBaseOffset );
                if ( readEndOffset > nextDataOffset )
                    continue;

                handled = true;
                if ( !entry.IsRedirected )
                {
                    // Trigger cache miss
                    for ( int j = 0; j < mCache.Length; j++ )
                    {
                        var cacheEntry = mCache[j].Entry;
                        if ( mCache[j].Miss() ) mLogger.Debug( $"{waveBank.FileName} Hnd: {handle} Index: {j} {cacheEntry.CueName} removed from cache" );
                    }

                    mLogger.Info( $"{waveBank.FileName} Hnd: {handle} Index: {i} {entry.CueName} Data access Offset: 0x{absOffset:X8} Length: 0x{length:X8}" );
                    status = mHooks.NtReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
                }
                else
                {
                    mLogger.Info( $"{waveBank.FileName} Hnd: {handle} Index: {i} {entry.CueName} Data access Offset: 0x{absOffset:X8} Length: 0x{length:X8} redirected to {entry.FilePath}" );
                    status = NtStatus.Success;

                    if ( fileDataOffset < 0 )
                    {
                        mLogger.Error( $"{waveBank.FileName} Hnd: {handle} Index: {i} {entry.CueName} Offset is before start of data!!!" );
                        continue;
                    }
                    else if ( fileDataOffset > entry.FileSize )
                    {
                        mLogger.Error( $"{waveBank.FileName} Hnd: {handle} Index: {i} {entry.CueName} Offset is after end of data!!!" );
                        //continue;
                    }

                    mLogger.Debug( $"{waveBank.FileName} Hnd: {handle} Index: {i} {entry.CueName} Reading 0x{length:X8} bytes from redirected file at offset 0x{fileDataOffset:X8}" );

                    // Get cached file stream if the file was previously opened or open a new file
                    Stream redirectedStream = null;
                    for ( int j = 0; j < mCache.Length; j++ )
                    {
                        if ( mCache[j].Entry == entry )
                        {
                            // Found entry in cache, increase score
                            mCache[j].Hit();
                            mLogger.Debug( $"{waveBank.FileName} Hnd: {handle} Index: {i} {entry.CueName} loaded from cache" );
                            redirectedStream = mCache[j].Stream;
                            break;
                        }
                        else
                        {
                            // Entry is not the one we're looking for, so we lower its score
                            var cacheEntry = mCache[j].Entry;
                            if ( mCache[j].Miss() ) mLogger.Debug( $"{waveBank.FileName} Hnd: {handle} Index: {j} {cacheEntry.CueName} removed from cache" );
                        }
                    }

                    if (redirectedStream == null)
                    {
                        // Wasn't found in cache
                        mLogger.Debug( $"{waveBank.FileName} Hnd: {handle} Index: {i} {entry.CueName} added to cache" );
                        redirectedStream = entry.OpenRead();
                        for ( int j = 0; j < mCache.Length; j++ )
                        {
                            if ( mCache[j].Entry == null )
                            {
                                mCache[j] = new CacheEntry() { Entry = entry, Score = 1, Stream = redirectedStream };
                                break;;
                            }
                        }
                    }

                    // Read from redirected file into the buffer
                    try
                    {
                        redirectedStream.Seek( fileDataOffset, SeekOrigin.Begin );
                        var readBytes = redirectedStream.Read( new Span<byte>( ( void* )buffer, ( int )length ) );
                        SetBytesRead( handle, ( int )waveBank.FilePointer, ( int )length, ref ioStatus );

                        if ( readBytes != length )
                            mLogger.Error( $"{waveBank.FileName} Hnd: {handle} Index: {i} {entry.CueName} File read length doesnt match requested read length!! Expected 0x{length:X8}, Actual 0x{readBytes:X8}" );

                        mLogger.Debug( $"{waveBank.FileName} Hnd: {handle} Index: {i} {entry.CueName} Wrote redirected file to buffer" );
                    }
                    catch ( Exception e )
                    {
                        mLogger.Debug( $"{waveBank.FileName} Hnd: {handle} Index: {i} {entry.CueName} Unhandled exception thrown during reading {entry.FileName}: {e}" );
                    }
                }
            }

            if ( !handled )
            {
                mLogger.Error( $"{waveBank.FileName} Hnd: {handle} Unhandled file data read request!! Offset: 0x{absOffset:X8} Length: 0x{length:X8}" );
                status = mHooks.NtReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
            }

            return status;
        }

        public override unsafe Native.NtStatus NtReadFileImpl( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext, ref Native.IO_STATUS_BLOCK ioStatus, 
            byte* buffer, uint length, Native.LARGE_INTEGER* byteOffset, IntPtr key )
        {
            if ( !mWaveBankByHandle.TryGetValue( handle, out var waveBank ) )
                return mHooks.NtReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );

            var offset = waveBank.FilePointer;
            var reqOffset = ( byteOffset != null || ( byteOffset != null && byteOffset->HighPart == -1 && byteOffset->LowPart == FILE_USE_FILE_POINTER_POSITION )) ?
                byteOffset->QuadPart : -1;
            var effOffset = reqOffset == -1 ? offset : reqOffset;

            var result = NtStatus.Success;
            var waveDataOffset = waveBank.Native.Header->Segments[(int)WaveBankSegmentIndex.EntryWaveData].Offset;
            if ( ( effOffset + length ) <= waveDataOffset )
            {
                // Header read
                Unsafe.CopyBlock( buffer, waveBank.Native.Ptr + effOffset, length );
                SetBytesRead( handle, (int)offset, (int)length, ref ioStatus );
                result = NtStatus.Success;
            }
            else if ( effOffset >= waveDataOffset && effOffset < waveBank.VirtualFileSize )
            {
                if ((waveBank.Native.Data->Flags & WaveBankFlags.Compact) != 0)
                {
                    // TODO: compact format
                    result = mHooks.NtReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
                }
                else
                {
                    ReadEntryWaveData( waveBank, effOffset, handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
                }   
            }
            else
            {
                mLogger.Error( $"{waveBank.FileName} Hnd: {handle} Unexpected read request!! Offset: {effOffset:X8} Length: {length:X8}" );
                result = mHooks.NtReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
            }


            if ( result != NtStatus.Success )
                mLogger.Error( $"{waveBank.FileName} Hnd: {handle} NtReadFile failed with {result}!!!" );

            return result;
        }


        public override unsafe Native.NtStatus NtQueryInformationFileImpl( IntPtr hfile, out Native.IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length,
            Native.FileInformationClass fileInformationClass )
        {
            var result = mHooks.NtQueryInformationFileHook.OriginalFunction( hfile, out ioStatusBlock, fileInformation, length, fileInformationClass );
            if ( !mWaveBankByHandle.TryGetValue( hfile, out var waveBank ) )
                return result;

            if ( fileInformationClass == FileInformationClass.FileStandardInformation )
            {
                var info = (FILE_STANDARD_INFORMATION*)fileInformation;
                info->EndOfFile = waveBank.VirtualFileSize;
            }
            else
            {
                mLogger.Debug( $"NtQueryInformationFileImpl( IntPtr hfile = {hfile}, out Native.IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, length = {length}, fileInformationClass = {fileInformationClass} )" );
            }

            return result;
        }

        public override unsafe Native.NtStatus NtSetInformationFileImpl( IntPtr hfile, out IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length, FileInformationClass fileInformationClass )
        {
            if ( fileInformationClass == FileInformationClass.FilePositionInformation )
            {
                if ( mWaveBankByHandle.TryGetValue( hfile, out var waveBank ) )
                {
                    waveBank.FilePointer = *( long* )fileInformation;
                    mLogger.Debug( $"{waveBank.FileName} Hnd: {hfile} SetFilePointer -> 0x{waveBank.FilePointer:X8}" );
                }
            }
            else
            {
                mLogger.Warning( $"SetInformationFileImpl(hfile = {hfile}, out ioStatusBlock, fileInformation = *0x{( long )fileInformation:X8}, " +
                    $"length = {length}, fileInformationClass = {fileInformationClass}" );
            }

            mHooks.NtSetInformationFileHook.OriginalFunction( hfile, out ioStatusBlock, fileInformation, length, fileInformationClass );

            // Spoof return value as we extend beyond the end of the file
            return NtStatus.Success;
        }
    }
}
