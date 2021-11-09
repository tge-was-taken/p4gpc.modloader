// Copied & adapted from https://github.com/Sewer56/AfsFsRedir.ReloadedII

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using modloader.Hooking;
using Reloaded.Hooks.Definitions;
using Reloaded.Mod.Interfaces;
using static modloader.Native;

namespace modloader
{
    public unsafe class FileAccessServer
    {
        /// <summary>
        /// Maps file handles to file paths.
        /// </summary>
        private ConcurrentDictionary<IntPtr, FileInfo> _handleToInfoMap = new ConcurrentDictionary<IntPtr, FileInfo>();
        private List<FileAccessClient> _filters = new List<FileAccessClient>();
        private FileAccessServerHooks _hooks;

        private ILogger _logger;
        private bool _activated;

        public FileAccessServer( IReloadedHooks hookFactory, NativeFunctions functions, ILogger logger )
        {
            _logger = logger;
            _hooks = new FileAccessServerHooks(
                functions.NtCreateFile.Hook( NtCreateFileImpl ),
                functions.NtReadFile.Hook( NtReadFileImpl ),
                functions.NtSetInformationFile.Hook( NtSetInformationFileImpl ),
                functions.NtQueryinformationFile.Hook( NtQueryInformationFileImpl ),
                ImportAddressTableHooker.Hook<CloseHandleDelegate>( hookFactory, "kernel32.dll", "CloseHandle", CloseHandleImpl));
        }

        private bool CloseHandleImpl( IntPtr handle )
        {
#if DEBUG
            try
            {
#endif
                foreach (var filter in _filters)
                {
                    if (filter.Accept(handle))
                        return filter.CloseHandleImpl(handle);
                }
#if  DEBUG
            }
            catch (Exception e)
            {
                _logger.WriteLine($"[{nameof(modloader)}:{nameof(FileAccessServer)}] {nameof(CloseHandleImpl)} Exception Thrown: {e.Message}\nStack Trace: {e.StackTrace}");
            }
#endif
            return _hooks.CloseHandleHook.OriginalFunction(handle);
        }

        public void Activate()
        {
            _activated = true;
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

        public void AddClient( FileAccessClient filter )
        {
            if ( _activated ) throw new InvalidOperationException();

            filter.SetHooks( _hooks );
            _filters.Add( filter );
        }

        public void RemoveFilter( FileAccessClient filter )
        {
            if ( _activated ) throw new InvalidOperationException();

            filter.SetHooks( null );
            _filters.Remove( filter );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private NtStatus NtQueryInformationFileImpl( IntPtr hfile, out IO_STATUS_BLOCK ioStatusBlock, void* fileInformation,
            uint length, FileInformationClass fileInformationClass )
        {
#if DEBUG
            try
            {
#endif
                foreach (var filter in _filters)
                {
                    if (filter.Accept(hfile))
                        return filter.NtQueryInformationFileImpl(hfile, out ioStatusBlock, fileInformation, length, fileInformationClass);
                }
#if DEBUG
            }
            catch (Exception e)
            {
                _logger.WriteLine($"[{nameof(modloader)}:{nameof(FileAccessServer)}] {nameof(CloseHandleImpl)} Exception Thrown: {e.Message}\nStack Trace: {e.StackTrace}");
            }
#endif

            return _hooks.NtQueryInformationFileHook.OriginalFunction(hfile, out ioStatusBlock, fileInformation, length, fileInformationClass);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private NtStatus NtSetInformationFileImpl( IntPtr handle, out IO_STATUS_BLOCK ioStatusBlock, void* fileInformation,
            uint length, FileInformationClass fileInformationClass )
        {
#if DEBUG
            try
            {
#endif
                if (fileInformationClass == FileInformationClass.FilePositionInformation && _handleToInfoMap.ContainsKey(handle))
                    _handleToInfoMap[handle].FilePointer = *(long*)fileInformation;

                foreach (var filter in _filters)
                {
                    if (filter.Accept(handle))
                        return filter.NtSetInformationFileImpl(handle, out ioStatusBlock, fileInformation, length, fileInformationClass);
                }
#if DEBUG
            }
            catch (Exception e)
            {
                _logger.WriteLine($"[{nameof(modloader)}:{nameof(FileAccessServer)}] {nameof(NtSetInformationFileImpl)} Exception Thrown: {e.Message}\nStack Trace: {e.StackTrace}");
            }
#endif
            
            return _hooks.NtSetInformationFileHook.OriginalFunction(handle, out ioStatusBlock, fileInformation, length, fileInformationClass);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private unsafe NtStatus NtReadFileImpl( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext,
            ref IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key )
        {
#if DEBUG
            try
            {
#endif
                foreach (var filter in _filters)
                {
                    if (filter.Accept(handle))
                    {
                        return filter.NtReadFileImpl(handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key);
                    }
                }
#if DEBUG
            }
            catch (Exception e)
            {
                _logger.WriteLine($"[{nameof(modloader)}:{nameof(FileAccessServer)}] {nameof(NtReadFileImpl)} Exception Thrown: Hnd: {handle} {e.Message}\nStack Trace: {e.StackTrace}");
            }
#endif
            
            return _hooks.NtReadFileHook.OriginalFunction(handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private NtStatus NtCreateFileImpl( out IntPtr handle, FileAccess access, ref OBJECT_ATTRIBUTES objectAttributes,
            ref IO_STATUS_BLOCK ioStatus, ref long allocSize, uint fileAttributes, FileShare share, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength )
        {
#if DEBUG
            try
            {
#endif
                string oldFileName = objectAttributes.ObjectName.ToString();
                if (!TryGetFullPath(oldFileName, out var newFilePath))
                    return _hooks.NtCreateFileHook.OriginalFunction(out handle, access, ref objectAttributes, ref ioStatus, ref allocSize,
                        fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength);

                // Blacklist DLLs to prevent JIT from locking when new assemblies used by this method are loaded.
                // Might want to disable some other extensions in the future; but this is just a temporary bugfix.
                if (string.Equals(Path.GetExtension(newFilePath), ".dll", StringComparison.OrdinalIgnoreCase))
                    return NtCreateFileDefault(out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength, newFilePath);

                NtStatus ret;
                foreach (var filter in _filters)
                {
                    if (filter.Accept(newFilePath))
                    {
                        ret = filter.NtCreateFileImpl(newFilePath, out handle, access, ref objectAttributes, ref ioStatus, ref allocSize,
                            fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength);

                        _handleToInfoMap[handle] = new FileInfo(newFilePath, 0);
                        return ret;
                    }
                }

                return NtCreateFileDefault( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength, newFilePath );
#if DEBUG
            }
            catch (Exception e)
            {
                _logger.WriteLine($"[{nameof(modloader)}:{nameof(FileAccessServer)}] {nameof(NtCreateFileImpl)} Exception Thrown: {e.Message}\nStack Trace: {e.StackTrace}");
            }
#endif

            return _hooks.NtCreateFileHook.OriginalFunction( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize, fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength );
        }

        /// <summary>
        /// Default implementation of NtCreateFile when not accepted by any of the filters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        NtStatus NtCreateFileDefault( out IntPtr handle, FileAccess access, ref OBJECT_ATTRIBUTES objectAttributes, ref IO_STATUS_BLOCK ioStatus, ref long allocSize, uint fileAttributes, FileShare fileShare, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength, string newFilePath )
        {
            var ntStatus = _hooks.NtCreateFileHook.OriginalFunction( out handle, access, ref objectAttributes,
                ref ioStatus, ref allocSize,
                fileAttributes, fileShare, createDisposition, createOptions, eaBuffer, eaLength );
            _handleToInfoMap[handle] = new FileInfo( newFilePath, 0 );
            return ntStatus;
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
    }
}
