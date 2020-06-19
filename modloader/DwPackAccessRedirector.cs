using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Amicitia.IO.Streams;
using modloader.Compression;
using Reloaded.Mod.Interfaces;
using static modloader.Native;

namespace modloader
{
    public unsafe class DwPackAccessRedirector : FileAccessFilter
    {
        private class VirtualDwPack
        {
            public long FilePointer;
            public string FilePath;
            public string FileName;
            public long OriginalFileSize;
            public long NewFileSize;
            public DwPackHeader Header;
            public long DataStartOffset;
            public List<VirtualDwPackFile> Files;

            public VirtualDwPack()
            {
                Files = new List<VirtualDwPackFile>();
            }
        }

        private unsafe class VirtualDwPackFile
        {
            private readonly VirtualDwPack mPack;
            public DwPackFileEntry OriginalEntry;
            public DwPackFileEntry NewEntry;
            public long EntryOffset { get; private set; }
            public bool IsRedirected { get; private set; }
            public string FilePath { get; private set; }
            public long FileSize { get; private set; }
            public Stream FallbackStream { get; set; }

            public VirtualDwPackFile( VirtualDwPack pack, DwPackFileEntry* entry )
            {
                OriginalEntry = *entry;
                NewEntry = *entry;
                mPack = pack;
            }

            public void Redirect( string newPath )
            {
                IsRedirected = true;
                FilePath = newPath;
                using ( var stream = OpenRead() )
                    FileSize = stream.Length;

                // Patch file size
                NewEntry.CompressedSize = ( int )FileSize;
                NewEntry.UncompressedSize = ( int )FileSize;
                NewEntry.Flags = 0;
            }

            public Stream OpenRead()
                => new FileStream( FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 );
        }

        private static readonly Regex sPacFileNameRegex = new Regex(@".+\d{5}.pac", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly ILogger mLogger;
        private readonly Dictionary<IntPtr, VirtualDwPack> mPacksByHandle;
        private readonly Dictionary<string, VirtualDwPack> mPacksByName;
        private string mLoadDirectory;

        public DwPackAccessRedirector( ILogger logger )
        {
            mLogger = logger;
            mPacksByHandle = new Dictionary<IntPtr, VirtualDwPack>();
            mPacksByName = new Dictionary<string, VirtualDwPack>( StringComparer.OrdinalIgnoreCase );
        }

        public void SetLoadDirectory( string path )
        {
            mLoadDirectory = path;
        }

        public override bool Accept( string newFilePath )
        {
            var fileName = Path.GetFileName( newFilePath );
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
            mPacksByHandle.Remove( handle );
            return mHooks.CloseHandleHook.OriginalFunction( handle );
        }

        public override Native.NtStatus NtCreateFileImpl( string newFilePath, out IntPtr handle, FileAccess access,
            ref Native.OBJECT_ATTRIBUTES objectAttributes, ref Native.IO_STATUS_BLOCK ioStatus, ref long allocSize,
            uint fileAttributes, FileShare share, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength )
        {
            var status = mHooks.CreateFileHook.OriginalFunction( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize,
                fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength );

#if DEBUG
            if ( mPacksByHandle.ContainsKey( handle ) )
            {
                Debug( $"Hnd {handle} File {newFilePath} is already used by another file!!!" );
            }
            else if ( mPacksByHandle.Any( x => x.Value.FilePath.Equals( newFilePath, StringComparison.OrdinalIgnoreCase ) ) )
            {
                var otherHandle = mPacksByHandle.First( x => x.Value.FilePath.Equals( newFilePath, StringComparison.OrdinalIgnoreCase ) ).Key;
                Debug( $"Hnd {otherHandle} for {newFilePath} already exists!!!" );
            }
#endif

            if ( !mPacksByName.TryGetValue( newFilePath, out var pack ) )
            {
                FILE_STANDARD_INFORMATION fileInfo;
                NtQueryInformationFileImpl( handle, out _, &fileInfo, ( uint )sizeof( FILE_STANDARD_INFORMATION ), FileInformationClass.FileStandardInformation );

                mPacksByName[newFilePath] = pack = new VirtualDwPack()
                {
                    FilePath = newFilePath,
                    FileName = Path.GetFileNameWithoutExtension( newFilePath ),
                    OriginalFileSize = fileInfo.EndOfFile,
                    NewFileSize = fileInfo.EndOfFile
                };

                Debug( $"Registered {newFilePath}" );
            }

            mPacksByHandle[handle] = pack;
            Debug( $"Hnd {handle} registered for {newFilePath}" );
            return status;
        }

        private NtStatus ReadHeader( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext,
            ref Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key,
            VirtualDwPack pack, long offset, long effOffset )
        {
            // Header read
            var result = mHooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
            if ( result != NtStatus.Success ) return result;

            var header = (DwPackHeader*)buffer;

            // Copy header       
            if ( pack.Header.Signature == 0 )
            {
                // First access
                pack.Header = *header;
                pack.DataStartOffset = ( long )( sizeof( DwPackHeader ) + ( sizeof( DwPackFileEntry ) * pack.Header.FileCount ) );
                for ( int i = 0; i < pack.Header.FileCount; i++ )
                    pack.Files.Add( null );
                Debug( $"{pack.FileName} Hnd: {handle} DW_PACK header: Index: {pack.Header.Index} FileCount: {pack.Header.FileCount}" );
            }
            else
            {
                // Repeat access
                *header = pack.Header;
            }

            return result;
        }

        private NtStatus ReadEntry( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext,
            ref Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key,
            VirtualDwPack pack, long offset, long effOffset )
        {
            // Entries read
            var entry = (DwPackFileEntry*)buffer;
            var fileIndex = (int)((effOffset - sizeof(DwPackHeader)) / sizeof(DwPackFileEntry));

            if ( fileIndex >= pack.Files.Count )
            {
                Error( $"{pack.FileName} Hnd: {handle} File index out of range!! {fileIndex}" );
            }
            else
            {
                if ( pack.Files[fileIndex] == null )
                {
                    var result = mHooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
                    if ( result != NtStatus.Success ) return result;

                    Debug( $"{pack.FileName} Hnd: {handle} Accessed file entry {entry->Path} Field00: {entry->Field00} Id: {entry->Id} Field104: {entry->Field104}" +
                        $"CompressedSize: {entry->CompressedSize:X8} UncompressedSize: {entry->UncompressedSize:X8} Flags: {entry->Flags} DataOffset: {pack.DataStartOffset + entry->DataOffset:X8}" );

                    var file = pack.Files[fileIndex] = new VirtualDwPackFile( pack, entry );

                    var redirectedFilePath = Path.Combine(mLoadDirectory, pack.FileName, entry->Path);
                    if ( File.Exists( redirectedFilePath ) )
                    {
                        file.Redirect( redirectedFilePath );
                        *entry = pack.Files[fileIndex].NewEntry;

                        //Dump( effOffset, length, buffer );
                        Debug( $"{pack.FileName} Hnd: {handle} Redirected {entry->Path} to {redirectedFilePath}" );
                        Debug( $"Patched entry: Field00: {entry->Field00} Id: {entry->Id} Field104: {entry->Field104} " +
                            $"CompressedSize: {entry->CompressedSize:X8} UncompressedSize: {entry->UncompressedSize:X8} " +
                            $"Flags: {entry->Flags} DataOffset: {pack.DataStartOffset + entry->DataOffset:X8}" );
                    }
                    else
                    {
                        Debug( $"No redirection for {entry->Path} because {redirectedFilePath} does not exist." );
                    }
                }
                else
                {
                    Debug( $"{pack.FileName} Hnd: {handle} Accessed file entry {entry->Path} Field00: {entry->Field00} Id: {entry->Id} Field104: {entry->Field104}" +
                        $"CompressedSize: {entry->CompressedSize:X8} UncompressedSize: {entry->UncompressedSize:X8} Flags: {entry->Flags} DataOffset: {pack.DataStartOffset + entry->DataOffset:X8}" );

                    // Repeat access
                    *entry = pack.Files[fileIndex].NewEntry;
                }
            }

            return NtStatus.Success;
        }

        private NtStatus ReadFile( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext,
            ref Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key,
            VirtualDwPack pack, long offset, long effOffset )
        {
            // File data read
            NtStatus result = NtStatus.Success;
            var handled = false;
            for ( int i = 0; i < pack.Files.Count; i++ )
            {
                var file = pack.Files[i];
                if ( file == null )
                {
                    // Entry has not been read yet
                    continue;
                }

                ref var entry = ref file.NewEntry;
                var dataOffset = pack.DataStartOffset + entry.DataOffset;
                if ( effOffset < dataOffset || effOffset >= ( dataOffset + entry.CompressedSize ) )
                    continue;

                handled = true;
                if ( !file.IsRedirected )
                {
                    Info( $"{pack.FileName} Hnd: {handle} File data access {entry.Path} Offset: 0x{effOffset:X8} Length: 0x{length:X8}" );
                    result = mHooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
                }
                else
                {
                    Info( $"{pack.FileName} Hnd: {handle} File data access {entry.Path} Offset: 0x{effOffset:X8} Length: 0x{length:X8} redirected to {file.FilePath}" );
                    result = NtStatus.Success;

                    if ( length != file.FileSize && length != 0x00300000 )
                    {
                        Error( "Read length doesnt match file size!!" );
                        Debug( $"{pack.FileName} Hnd: {handle} Path {entry.Path}" );
                        Debug( $"Patched entry: Field00: {entry.Field00} Id: {entry.Id} Field104: {entry.Field104} " +
                            $"CompressedSize: {entry.CompressedSize:X8} UncompressedSize: {entry.UncompressedSize:X8} " +
                            $"Flags: {entry.Flags} DataOffset: {pack.DataStartOffset + entry.DataOffset:X8}" );
                    }

                    var relativeDataOffset = effOffset - dataOffset;
                    if ( relativeDataOffset < 0 )
                    {
                        Error( $"{pack.FileName} Hnd: {handle} Offset is before start of data!!!" );
                        continue;
                    }
                    else if ( relativeDataOffset > file.FileSize )
                    {
                        Error( $"{pack.FileName} Hnd: {handle} Offset is after end of data!!!" );
                        continue;
                    }
                    else
                    {
                        Debug( $"{pack.FileName} Hnd: {handle} Reading 0x{length:X8} bytes from redirected file at offset 0x{relativeDataOffset:X8}" );
                    }

                    if ( ( relativeDataOffset + length ) > file.FileSize )
                    {
                        Error( $"{pack.FileName} Hnd: {handle} Redirected file is too small, attempted to read past end of data!!!" );
                        continue;
                    }
                    else
                    {
                        //if ( ioStatus.Information != ( IntPtr )length )
                        //{
                        //    Error( $"{pack.FileName} Hnd: {handle} Pack file read length doesnt match requested read length!! Expected 0x{length:X8}, Actual 0x{ioStatus.Information:X8}" );
                        //}

                        //// Copy the buffer read from the file
                        //var bufferCopy = new byte[length];
                        //fixed ( byte* pBufferCopy = bufferCopy )
                        //    Unsafe.CopyBlock( pBufferCopy, buffer, ( uint )length );

                        //var decompressedBufferStream = new MemoryStream();
                        //Yggdrasil.uncompress( new MemoryStream( bufferCopy ), decompressedBufferStream, file.OriginalEntry.CompressedSize, file.OriginalEntry.UncompressedSize );
                        //var decompressedBuffer = decompressedBufferStream.ToArray();
                    }

                    // Read from redirected file into the buffer
                    using ( var redirectedStream = file.OpenRead() )
                    {
                        redirectedStream.Seek( relativeDataOffset, SeekOrigin.Begin );
                        var readBytes = redirectedStream.Read( new Span<byte>( ( void* )buffer, ( int )length ) );
                        if ( readBytes != length )
                        {
                            Error( $"{pack.FileName} Hnd: {handle} File read length doesnt match requested read length!! Expected 0x{length:X8}, Actual 0x{readBytes:X8}" );
                        }

                        Debug( $"{pack.FileName} Hnd: {handle} Wrote redirected file to buffer" );
                    }

                    // Compare buffers
                    //var isSame = true;
                    //for ( int j = 0; j < length; j++ )
                    //{
                    //    if ( decompressedBuffer[j] != buffer[j])
                    //    {
                    //        Error( $"Buffer contents dont match!!!! 0x{j:X8} O {bufferCopy[j]:X} N {buffer[j]:X}" );
                    //        break;
                    //    }
                    //}

                    offset += length;
                    NtSetInformationFileImpl( handle, out _, &offset, sizeof( long ), FileInformationClass.FilePositionInformation );

                    // Set number of read bytes.
                    ioStatus.Status = 0;
                    ioStatus.Information = new IntPtr( length );
                }
            }

            if ( !handled )
            {
                Error( $"{pack.FileName} Hnd: {handle} Unhandled file data read request!! Offset: 0x{effOffset:X8} Length: 0x{length:X8}" );
                result = mHooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
            }

            return result;
        }

        public override unsafe Native.NtStatus NtReadFileImpl( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext,
            ref Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key )
        {
            NtStatus result;
            var pack = mPacksByHandle[handle];
            var offset = pack.FilePointer;
            var reqOffset = ( byteOffset != null || ( byteOffset != null && byteOffset->HighPart == -1 && byteOffset->LowPart == FILE_USE_FILE_POINTER_POSITION )) ?
                byteOffset->QuadPart : -1;
            var effOffset = reqOffset == -1 ? offset : reqOffset;

            if ( effOffset == 0 && length == sizeof( DwPackHeader ) )
            {
                result = ReadHeader( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key, pack, offset, effOffset );
            }
            else if ( ( effOffset >= sizeof( DwPackHeader ) && effOffset < pack.DataStartOffset ) && length == sizeof( DwPackFileEntry ) )
            {
                result = ReadEntry( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key, pack, offset, effOffset );
            }
            else if ( effOffset >= pack.DataStartOffset )
            {
                result = ReadFile( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key, pack, offset, effOffset );
            }
            else
            {
                Error( $"{pack.FileName} Hnd: {handle} Unexpected read request!! Offset: {effOffset:X8} Length: {length:X8}" );
                result = mHooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
            }

            if ( result != NtStatus.Success )
                Error( $"{pack.FileName} Hnd: {handle} NtReadFile failed with {result}!!!" );

            return result;
        }

        public override unsafe NtStatus NtSetInformationFileImpl( IntPtr hfile, out IO_STATUS_BLOCK ioStatusBlock,
            void* fileInformation, uint length, FileInformationClass fileInformationClass )
        {

            if ( fileInformationClass == FileInformationClass.FilePositionInformation )
            {
                //Debug( $"SetInformationFileImpl(hfile = {hfile}, out ioStatusBlock, fileInformation = *0x{( long )fileInformation:X8} = {*( long* )fileInformation:X8}, " +
                //    $"length = {length}, fileInformationClass = {fileInformationClass}" );
                var pack = mPacksByHandle[hfile];
                pack.FilePointer = *( long* )fileInformation;
                Debug( $"{pack.FileName} Hnd: {hfile} SetFilePointer -> 0x{pack.FilePointer:X8}" );
            }
            else
            {
                Warning( $"SetInformationFileImpl(hfile = {hfile}, out ioStatusBlock, fileInformation = *0x{( long )fileInformation:X8}, " +
                    $"length = {length}, fileInformationClass = {fileInformationClass}" );
            }

            return mHooks.SetInformationFIleHook.OriginalFunction( hfile, out ioStatusBlock, fileInformation, length, fileInformationClass );
        }


        public override unsafe NtStatus NtQueryInformationFileImpl( IntPtr hfile, out Native.IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length, Native.FileInformationClass fileInformationClass )
        {
            return mHooks.QueryInformationFileHook.OriginalFunction( hfile, out ioStatusBlock, fileInformation, length, fileInformationClass );
        }

        public override uint SetFilePointerImpl( IntPtr hFile, int lDistanceToMove, IntPtr lpDistanceToMoveHigh, uint dwMoveMethod )
        {
            //return mHooks.SetFilePointerHook.OriginalFunction( hFile, liDistanceToMove, lpNewFilePointer, dwMoveMethod );
            const uint INVALID_SET_FILE_POINTER = 0xFFFFFFFF;

            // Thanks Microsoft
            var result = mHooks.SetFilePointerHook.OriginalFunction( hFile, lDistanceToMove, lpDistanceToMoveHigh, dwMoveMethod );
            if ( result == INVALID_SET_FILE_POINTER /*|| GetLastError() != 0*/ ) return result;

            var offset = new LARGE_INTEGER() { LowPart = result };
            var pack = mPacksByHandle[hFile];
            if ( lpDistanceToMoveHigh != IntPtr.Zero )
            {
                //Debug( $"SetFilePointerImpl( IntPtr hFile = {hFile}, int lDistanceToMove = 0x{lDistanceToMove:X8}, IntPtr lpDistanceToMoveHigh = 0x{lpDistanceToMoveHigh:X8}, uint dwMoveMethod = {dwMoveMethod})" );
                offset.HighPart = *( int* )lpDistanceToMoveHigh;
            }

            pack.FilePointer = offset.QuadPart;
            //Debug( $"{pack.FileName} Hnd: {hFile} SetFilePointer -> 0x{pack.FilePointer:X8} ({lDistanceToMove:X8}, {dwMoveMethod}" );
            return result;
        }

        private void Dump( long offset, uint length, byte* buffer )
        {
            mHooks.Disable();
            using ( var dump = new FileStream( $"{DateTime.Now}-dump_{offset:X8}_{length:X8}.bin", FileMode.Create ) )
                dump.Write( new ReadOnlySpan<byte>( ( void* )buffer, ( int )length ) );
            mHooks.Enable();
        }

        private void Info( string msg ) => mLogger.WriteLine( $"[modloader:DwPackAccessRedirector] I {msg}" );
        private void Warning( string msg ) => mLogger.WriteLine( $"[modloader:DwPackAccessRedirector] W {msg}", mLogger.ColorYellow );
        private void Error( string msg ) => mLogger.WriteLine( $"[modloader:DwPackAccessRedirector] E {msg}", mLogger.ColorRed );

        [Conditional( "DEBUG" )]
        private void Debug( string msg ) => mLogger.WriteLine( $"[modloader:DwPackAccessRedirector] D {msg}", mLogger.ColorGreen );
    }
}
