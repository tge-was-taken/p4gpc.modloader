using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Reloaded.Mod.Interfaces;
using static modloader.Native;
using modloader.Mods;
using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;
using modloader.Configuration;
using PreappPartnersLib.FileSystems;
using modloader.Redirectors.Cpk;
using System.Runtime.InteropServices;

namespace modloader.Redirectors.DwPack
{
    public unsafe class DwPackRedirector : FileAccessClient
    {
        private static readonly Regex sPacFileNameRegex = new Regex(@".+\d{5}.pac", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly SemanticLogger mLogger;
        private readonly ModDb mModDb;
        private readonly CpkRedirector mCpkRedirector;
        private readonly Dictionary<IntPtr, VirtualDwPackHandle> mPacksByHandle;
        private readonly Dictionary<string, VirtualDwPack> mPacksByName;
        private readonly Dictionary<VirtualDwPack, IntPtr> mHandleByPack;
        private VirtualDwPackEntry mCachedFile;
        private Stream mCachedFileStream;
        private List<VirtualCpk> mLoadedCpks;

        public DwPackRedirector(ILogger logger, ModDb modDb, Config configuration, CpkRedirector cpkRedirector)
        {
            mLogger = new SemanticLogger( logger, "[modloader:DwPackRedirector]", configuration );
            mModDb = modDb;
            mPacksByHandle = new Dictionary<IntPtr, VirtualDwPackHandle>();
            mPacksByName = new Dictionary<string, VirtualDwPack>( StringComparer.OrdinalIgnoreCase );
            mHandleByPack = new Dictionary<VirtualDwPack, IntPtr>();
            mCpkRedirector = cpkRedirector;
            mCpkRedirector.CpkLoaded += OnCpkLoaded;
            mLoadedCpks = new List<VirtualCpk>();
        }

        private void OnCpkLoaded( object sender, VirtualCpk e )
        {
            mLoadedCpks.Add( e );
        }

        public override bool Accept( string newFilePath )
        {
            if ( sPacFileNameRegex.IsMatch( newFilePath ) )
                return true;

            return false;
        }

        public override bool Accept( IntPtr handle )
        {
            return mPacksByHandle.ContainsKey( handle );
        }

        public override bool CloseHandleImpl( IntPtr handle )
        {
            var pack = mPacksByHandle[ handle ];
            mPacksByHandle.Remove( handle );
            mHandleByPack.Remove( pack.Instance );
            return mHooks.CloseHandleHook.OriginalFunction( handle );
        }

        [DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true )]
        private static extern bool CreatePipe( out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, int nSize );

        private IntPtr GenerateHandle()
        {
            CreatePipe( out var temp1, out var temp2, IntPtr.Zero, 0 );
            return temp1;
        }

        public override Native.NtStatus NtCreateFileImpl( string filePath, out IntPtr handle, FileAccess access,
            ref Native.OBJECT_ATTRIBUTES objectAttributes, ref Native.IO_STATUS_BLOCK ioStatus, ref long allocSize,
            uint fileAttributes, FileShare share, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength )
        {
            var result = mHooks.NtCreateFileHook.OriginalFunction( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize,
                fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength );


            if ( !mPacksByName.TryGetValue( filePath, out var pack ) )
            {
                mPacksByName[ filePath ] = pack = new VirtualDwPack( mLogger, filePath );

                var pacIndex = int.Parse( pack.FileName.Substring( pack.FileName.Length - 5, 5 ) );
                var cpkName = pack.FileName.Substring( 0, pack.FileName.Length - 5 );
                var cpk = mLoadedCpks.Find( x => x.FileName.Contains( cpkName ) && x.Entries.Any( y => y.PacIndex == pacIndex ) );
                pack.Cpk = cpk;

                if ( result != NtStatus.ObjectNameNotFound )
                {
                    // Load file
                    using ( var fileStream = new FileStream( new SafeFileHandle( handle, true ), FileAccess.Read, 1024 * 1024 ) )
                        pack.LoadFromFile( filePath, fileStream );

                    //pack.AddNewFiles( cpk );

                    // Reopen file to reset it
                    result = mHooks.NtCreateFileHook.OriginalFunction( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize,
                        fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength );
                }
                else
                {
                    pack.LoadFromCpk( pacIndex, cpk );
                    handle = GenerateHandle();
                    ioStatus.Information = (IntPtr)1;
                    ioStatus.Status = 0;
                    result = NtStatus.Success;
                }

                mLogger.Debug( $"Registered {filePath}" );

                // Entries are redirected as needed to improve startup performance
            }
            else if ( result == NtStatus.ObjectNameNotFound )
            {
                // Find handle from name
                if ( !mHandleByPack.TryGetValue( pack, out handle ) )
                    handle = GenerateHandle();

                ioStatus.Information = (IntPtr)1;
                ioStatus.Status = 0;
                result = NtStatus.Success;
            }

            mPacksByHandle[ handle ] = new VirtualDwPackHandle() { Instance = pack };
            mHandleByPack[ pack ] = handle;
            mLogger.Debug( $"Hnd {handle} {filePath} handle registered" );
            return result;
        }

        private NtStatus ReadFile( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext,
            ref Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key,
            VirtualDwPack pack, long offset, long effOffset )
        {        
            // File data read
            NtStatus result = NtStatus.Success;
            for ( int i = 0; i < pack.Entries.Count; i++ )
            {
                var entry = pack.Entries[i];
                var dataOffset = (pack.Native.Data + entry.Native->DataOffset) - pack.Native.Ptr;
                if ( effOffset < dataOffset || effOffset >= ( dataOffset + entry.Native->CompressedSize ) )
                    continue;

                var fileDataOffset = effOffset - dataOffset;
                var readEndOffset = fileDataOffset + length;
                if ( readEndOffset > entry.Native->CompressedSize )
                    continue;

                // Make sure the file has been redirected
                // This is done as late as possible to improve startup times
                if ( !entry.IsRedirected )
                {
                    mLogger.Info( $"{pack.FileName} Hnd: {handle} {entry.Native->Path} Idx: ({i}) Data access Offset: 0x{effOffset:X8} Length: 0x{length:X8}" );
                    result = mHooks.NtReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
                }
                else
                {
                    mLogger.Info( $"{pack.FileName} Hnd: {handle} {entry.Native->Path} Idx: ({i}) Data access Offset: 0x{effOffset:X8} Length: 0x{length:X8} redirected to {entry.RedirectedFilePath}" );
                    result = NtStatus.Success;

                    if ( fileDataOffset < 0 )
                    {
                        mLogger.Error( $"{pack.FileName} Hnd: {handle} {entry.Native->Path} Idx: ({i}) Offset is before start of data!!!" );
                    }
                    else if ( fileDataOffset > entry.RedirectedFileSize )
                    {
                        mLogger.Error( $"{pack.FileName} Hnd: {handle} {entry.Native->Path} Idx: ({i}) Offset is after end of data!!!" );
                    }

                    mLogger.Debug( $"{pack.FileName} Hnd: {handle} {entry.Native->Path} Idx: ({i}) Reading 0x{length:X8} bytes from redirected file at offset 0x{fileDataOffset:X8}" );

                    // Get cached file stream if the file was previously opened or open a new file
                    Stream redirectedStream;
                    if ( mCachedFile == entry )
                    {
                        redirectedStream = mCachedFileStream;
                    }
                    else
                    {
                        mCachedFileStream?.Close();
                        mCachedFile = entry;
                        mCachedFileStream = redirectedStream = entry.OpenRead();
                    }

                    // Read from redirected file into the buffer
                    try
                    {
                        redirectedStream.Seek( fileDataOffset, SeekOrigin.Begin );
                        var readBytes = redirectedStream.Read( new Span<byte>( ( void* )buffer, ( int )length ) );
                        SetBytesRead( handle, ( int )offset, ( int )length, ref ioStatus );

                        if ( readBytes != length )
                            mLogger.Error( $"{pack.FileName} Hnd: {handle} {entry.Native->Path} Idx: ({i}) File read length doesnt match requested read length!! Expected 0x{length:X8}, Actual 0x{readBytes:X8}" );

                        mLogger.Debug( $"{pack.FileName} Hnd: {handle} {entry.Native->Path} Idx: ({i}) Wrote redirected file to buffer" );
                    }
                    catch ( Exception e )
                    {
                        mLogger.Debug( $"{pack.FileName} Hnd: {handle} Idx: {i} {entry.Native->Path} Unhandled exception thrown during reading {entry.RedirectedFilePath}: {e}" );
                    }
                }

                // Return early, we're done here
                return result;
            }

            mLogger.Error( $"{pack.FileName} Hnd: {handle} Unhandled file data read request!! Offset: 0x{effOffset:X8} Length: 0x{length:X8}" );
            return mHooks.NtReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
        }

        private void SetBytesRead( IntPtr handle, long offset, int length, ref IO_STATUS_BLOCK ioStatus )
        {
            offset += length;
            NtSetInformationFileImpl( handle, out _, &offset, sizeof( long ), FileInformationClass.FilePositionInformation );

            // Set number of read bytes.
            ioStatus.Status = 0;
            ioStatus.Information = new IntPtr( length );
        }

        public override unsafe Native.NtStatus NtReadFileImpl( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext,
            ref Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key )
        {
            NtStatus result;
            var packHandle = mPacksByHandle[handle];
            var pack = packHandle.Instance;
            var offset = packHandle.FilePointer;
            var reqOffset = ( byteOffset != null || ( byteOffset != null && byteOffset->HighPart == -1 && byteOffset->LowPart == FILE_USE_FILE_POINTER_POSITION )) ?
                byteOffset->QuadPart : -1;
            var effOffset = reqOffset == -1 ? offset : reqOffset;
            var dataOffset = pack.Native.Data - pack.Native.Ptr;

            if ( ( effOffset + length ) <= dataOffset )
            {
                // Header read
                if ( effOffset >= sizeof( DwPackHeader ) )
                {
                    // Ensure entry is redirected before entry is read
                    // This improves startup times greatly, as otherwise thousands of redirections could potentially have to be done at 
                    // startup
                    var entryIndex = ( int )( ( effOffset - sizeof( DwPackHeader ) ) / sizeof( DwPackEntry ) );
                    if ( !pack.Entries[ entryIndex ].RedirectAttempted )
                        pack.Entries[ entryIndex ].Redirect( mModDb );
                }

                Unsafe.CopyBlock( buffer, pack.Native.Ptr + effOffset, length );
                SetBytesRead( handle, ( int )effOffset, ( int )length, ref ioStatus );
                result = NtStatus.Success;
            }
            else if ( effOffset >= dataOffset && effOffset < pack.VirtualFileSize )
            {
                result = ReadFile( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key, pack, offset, effOffset );
            }
            else
            {
                mLogger.Error( $"{pack.FileName} Hnd: {handle} Unexpected read request!! Offset: {effOffset:X8} Length: {length:X8}" );
                result = mHooks.NtReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
            }

            if ( result != NtStatus.Success )
                mLogger.Error( $"{pack.FileName} Hnd: {handle} NtReadFile failed with {result}!!!" );

            return result;
        }

        public override unsafe NtStatus NtSetInformationFileImpl( IntPtr hfile, out IO_STATUS_BLOCK ioStatusBlock,
            void* fileInformation, uint length, FileInformationClass fileInformationClass )
        {

            if ( fileInformationClass == FileInformationClass.FilePositionInformation )
            {
                var pack = mPacksByHandle[hfile];
                pack.FilePointer = *( long* )fileInformation;
                mLogger.Debug( $"{pack.Instance.FileName} Hnd: {hfile} SetFilePointer -> 0x{pack.FilePointer:X8}" );
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


        public override unsafe NtStatus NtQueryInformationFileImpl( IntPtr hfile, out Native.IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length, Native.FileInformationClass fileInformationClass )
        {
            var result = mHooks.NtQueryInformationFileHook.OriginalFunction( hfile, out ioStatusBlock, fileInformation, length, fileInformationClass );
            if ( !mPacksByHandle.TryGetValue( hfile, out var pack ) )
                return result;

            if ( fileInformationClass == FileInformationClass.FileStandardInformation )
            {
                var info = (FILE_STANDARD_INFORMATION*)fileInformation;
                info->EndOfFile = uint.MaxValue;
            }
            else
            {
                mLogger.Debug( $"NtQueryInformationFileImpl( IntPtr hfile = {hfile}, out Native.IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, length = {length}, fileInformationClass = {fileInformationClass} )" );
            }

            return result;
        }
    }
}
