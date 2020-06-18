// Copied & adapted from https://github.com/Sewer56/AfsFsRedir.ReloadedII

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory;
using static modloader.Native;

namespace modloader
{
    public unsafe class FileAccessServer
    {
        /// <summary>
        /// Maps file handles to file paths.
        /// </summary>
        private ConcurrentDictionary<IntPtr, FileInfo> _handleToInfoMap = new ConcurrentDictionary<IntPtr, FileInfo>();
        private List<FileAccessFilter> _filters = new List<FileAccessFilter>();
        private FileAccessServerHooks _hooks;

        private object _createLock = new object();
        private object _getInfoLock = new object();
        private object _setInfoLock = new object();
        private object _readLock = new object();
        private object _filtersLock = new object();

        private object _lock = new object();

        public FileAccessServer( NativeFunctions functions )
        {
            _hooks = new FileAccessServerHooks(
                functions.NtCreateFile.Hook( NtCreateFileImpl ),
                functions.NtReadFile.Hook( NtReadFileImpl ),
                functions.SetFilePointer.Hook( SetInformationFileImpl ),
                functions.GetFileSize.Hook( QueryInformationFileImpl ) );

            // TODO: Hook NtClose
            // Problem: Native->Managed Transition hits NtClose in .NET Core, so our hook code is never hit.
            // Problem: NtClose needs synchronization.
            // Solution: Write custom ASM to solve the problem, see NtClose branch.
        }

        public void Activate()
        {
            _hooks.Activate();
        }

        public void Enable()
        {
            _hooks.Enable();
        }

        public void Disable()
        {
            _hooks.Disable();
        }

        public void AddFilter( FileAccessFilter filter )
        {
            //lock ( _filtersLock )
            {
                filter.SetHooks( _hooks );
                _filters.Add( filter );
            }
        }

        public void RemoveFilter( FileAccessFilter filter )
        {
            //lock ( _filtersLock )
            {
                filter.SetHooks( null );
                _filters.Remove( filter );
            }
        }

        private NtStatus QueryInformationFileImpl( IntPtr hfile, out IO_STATUS_BLOCK ioStatusBlock, void* fileInformation,
            uint length, FileInformationClass fileInformationClass )
        {
            lock ( _getInfoLock )
            {
                //lock ( _filtersLock )
                {
                    foreach ( var filter in _filters )
                    {
                        if ( filter.Accept( hfile ) )
                            return filter.NtQueryInformationFileImpl( hfile, out ioStatusBlock, fileInformation, length, fileInformationClass );
                    }
                }

                return _hooks.QueryInformationFileHook.OriginalFunction( hfile, out ioStatusBlock, fileInformation, length, fileInformationClass );
            }
        }

        private NtStatus SetInformationFileImpl( IntPtr handle, out IO_STATUS_BLOCK ioStatusBlock, void* fileInformation,
            uint length, FileInformationClass fileInformationClass )
        {
            lock ( _setInfoLock )
            {
                if ( fileInformationClass == FileInformationClass.FilePositionInformation )
                    _handleToInfoMap[handle].FilePointer = *( long* )fileInformation;

                //lock ( _filtersLock )
                {
                    foreach ( var filter in _filters )
                    {
                        if ( filter.Accept( handle ) )
                            return filter.SetInformationFileImpl( handle, out ioStatusBlock, fileInformation, length, fileInformationClass );
                    }
                }

                return _hooks.SetInformationFIleHook.OriginalFunction( handle, out ioStatusBlock, fileInformation, length, fileInformationClass );
            }
        }

        private unsafe NtStatus NtReadFileImpl( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext,
            ref IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key )
        {
            lock ( _readLock )
            {
                //lock ( _filtersLock )
                {
                    foreach ( var filter in _filters )
                    {
                        if ( filter.Accept( handle ) )
                            return filter.NtReadFileImpl( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
                    }
                }

                var result = _hooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );

#if DEBUG
                if ( _handleToInfoMap.TryGetValue( handle, out var file ) )
                {
                    var offset = file.FilePointer;
                    var reqOffset = ( byteOffset != null || ( byteOffset != null && byteOffset->HighPart == -1 && byteOffset->LowPart == FILE_USE_FILE_POINTER_POSITION )) ?
                    byteOffset->QuadPart : -1;
                    var effOffset = reqOffset == -1 ? offset : reqOffset;
                    if ( length == sizeof( DwPackFileEntry ) )
                    {
                        Console.WriteLine( $"[modloader:FileAccessServer] Hnd: {handle} File: {Path.GetFileName( file.FilePath )} Unhandled read of entry {( ( DwPackFileEntry* )buffer )->Path} at 0x{effOffset:X8}", Color.Red );
                    }
                }
#endif

                return result;
            }
        }

        private NtStatus NtCreateFileImpl( out IntPtr handle, FileAccess access, ref OBJECT_ATTRIBUTES objectAttributes,
            ref IO_STATUS_BLOCK ioStatus, ref long allocSize, uint fileAttributes, FileShare share, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength )
        {
            void RegisterFileHandle( IntPtr handle, string newFilePath )
            {
                _handleToInfoMap[handle] = new FileInfo( newFilePath, 0 );
            }

            lock ( _createLock )
            {
                string oldFileName = objectAttributes.ObjectName.ToString();
                if ( !TryGetFullPath( oldFileName, out var newFilePath ) )
                    return _hooks.CreateFileHook.OriginalFunction( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize,
                        fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength );

                NtStatus ret;
                //lock ( _filtersLock )
                {
                    foreach ( var filter in _filters )
                    {
                        if ( filter.Accept( newFilePath ) )
                        {
                            ret = filter.NtCreateFileImpl( newFilePath, out handle, access, ref objectAttributes, ref ioStatus, ref allocSize,
                                fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength );

                            RegisterFileHandle( handle, newFilePath );
                            return ret;
                        }
                    }

                    ret = _hooks.CreateFileHook.OriginalFunction( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize,
                        fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength );
                    RegisterFileHandle( handle, newFilePath );
                    return ret;
                }
            }
        }

        /// <summary>
        /// Tries to resolve a given file path from NtCreateFile to a full file path.
        /// </summary>
        private bool TryGetFullPath( string oldFilePath, out string newFilePath )
        {
            if ( oldFilePath.StartsWith( "\\??\\", StringComparison.InvariantCultureIgnoreCase ) )
                oldFilePath = oldFilePath.Replace( "\\??\\", "" );

            if ( !String.IsNullOrEmpty( oldFilePath ) )
            {
                newFilePath = Path.GetFullPath( oldFilePath );
                return true;
            }

            newFilePath = oldFilePath;
            return false;
        }

        public delegate bool OnCreateFileDelegate( IntPtr handle, string filePath );
        public delegate bool OnReadFileDelegate( IntPtr handle, byte* buffer, uint length, long offset, out int numReadBytes );
        public delegate int OnGetFileSizeDelegate( IntPtr handle );
    }
}
