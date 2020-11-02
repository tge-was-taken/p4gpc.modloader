using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Reloaded.Mod.Interfaces;
using static modloader.Native;
using modloader.Mods;
using System.Runtime.CompilerServices;
using modloader.Utilities;
using Microsoft.Win32.SafeHandles;
using modloader.Configuration;
using PreappPartnersLib.FileSystems;
using modloader.Redirectors.DwPack;

namespace modloader.Redirectors.Cpk
{
    public unsafe class CpkRedirector : FileAccessClient
    {
        private readonly SemanticLogger mLogger;
        private readonly ModDb mModDb;
        private readonly Dictionary<IntPtr, VirtualCpkHandle> mCpkByHandle;
        private readonly Dictionary<string, VirtualCpk> mCpkByName;

        public event EventHandler<VirtualCpk> CpkLoaded;

        public CpkRedirector(ILogger logger, ModDb modDb, Config configuration)
        {
            mLogger = new SemanticLogger( logger, "[modloader:CpkRedirector]", configuration );
            mModDb = modDb;
            mCpkByHandle = new Dictionary<IntPtr, VirtualCpkHandle>();
            mCpkByName = new Dictionary<string, VirtualCpk>( StringComparer.OrdinalIgnoreCase );
        }

        public override bool Accept( string newFilePath )
        {
            return Path.GetExtension(newFilePath)?.Equals(".cpk", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        public override bool Accept( IntPtr handle )
        {
            return mCpkByHandle.ContainsKey( handle );
        }

        public override bool CloseHandleImpl( IntPtr handle )
        {
            mCpkByHandle.Remove( handle );
            return mHooks.CloseHandleHook.OriginalFunction( handle );
        }

        public override Native.NtStatus NtCreateFileImpl( string filePath, out IntPtr handle, FileAccess access,
            ref Native.OBJECT_ATTRIBUTES objectAttributes, ref Native.IO_STATUS_BLOCK ioStatus, ref long allocSize,
            uint fileAttributes, FileShare share, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength )
        {
            var result = mHooks.NtCreateFileHook.OriginalFunction( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize,
                fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength );

            if ( !mCpkByName.TryGetValue( filePath, out var cpk ) )
            {
                mCpkByName[filePath] = cpk = new VirtualCpk( mLogger );

                // Load file
                using ( var fileStream = new FileStream( new SafeFileHandle( handle, true ), FileAccess.Read, 1024 * 1024 ) )
                    cpk.LoadFromFile( filePath, fileStream );

                // Reopen file to reset it
                result = mHooks.NtCreateFileHook.OriginalFunction( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize,
                    fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength );

                mLogger.Debug( $"Registered {filePath}" );

                // Redirect entries to a non-existent pac that will be handled by the DwPack redirector
                cpk.Redirect( mModDb );
            }

            mCpkByHandle[ handle ] = new VirtualCpkHandle() { Instance = cpk };
            mLogger.Debug( $"Hnd {handle} {filePath} handle registered" );
            CpkLoaded?.Invoke( this, cpk );
            return result;
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
            var cpk = mCpkByHandle[ handle ];
            var effOffset = Utils.ResolveReadFileOffset( cpk.FilePointer, byteOffset );
            Unsafe.CopyBlock( buffer, (byte*)cpk.Instance.Native.Ptr + effOffset, length );
            SetBytesRead( handle, (int)cpk.FilePointer, (int)length, ref ioStatus );
            return NtStatus.Success;
        }

        public override unsafe NtStatus NtSetInformationFileImpl( IntPtr hfile, out IO_STATUS_BLOCK ioStatusBlock,
            void* fileInformation, uint length, FileInformationClass fileInformationClass )
        {
            if ( fileInformationClass == FileInformationClass.FilePositionInformation )
            {
                var cpk = mCpkByHandle[hfile];
                cpk.FilePointer = *( long* )fileInformation;
                mLogger.Debug( $"{cpk.Instance.FileName} Hnd: {hfile} SetFilePointer -> 0x{cpk.FilePointer:X8}" );
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
            if ( !mCpkByHandle.TryGetValue( hfile, out var pack ) )
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
