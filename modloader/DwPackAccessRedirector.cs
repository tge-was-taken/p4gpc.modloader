using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
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
            public LiveDwPackHeader Header;
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
            public LiveDwPackFileEntry OriginalEntry;
            public LiveDwPackFileEntry NewEntry;
            public long EntryOffset { get; private set; }
            public bool IsRedirected { get; private set; }
            public string FilePath { get; private set; }
            public long FileSize { get; private set; }

            public VirtualDwPackFile( VirtualDwPack pack, LiveDwPackFileEntry* entry )
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
                //NewEntry.DataOffset = ( int )( mPack.NewFileSize - mPack.DataStartOffset );
                //mPack.NewFileSize += FileSize;
                NewEntry.CompressedSize = ( int )FileSize;
                NewEntry.UncompressedSize = ( int )FileSize;
                NewEntry.Flags = 0;
            }

            public Stream OpenRead()
                => new FileStream( FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 );
        }

        private static readonly List<string> sPacFileNames = new List<string>()
        {
            "data00000.pac",
            "data00001.pac",
            "data00002.pac",
            "data00003.pac",
            "data00004.pac",
            "data00005.pac",
            "data00006.pac",
            "movie00000.pac",
            "movie00000.pac",
            "movie00000.pac",
            "sysdat00000.pac"
        };

        private readonly ILogger mLogger;
        private Dictionary<IntPtr, VirtualDwPack> mPacksByHandle;
        private Dictionary<string, VirtualDwPack> mPacksByName;
        private string mLoadDirectory;
        private byte[] mScratchBuffer = new byte[1024 * 1024];

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
            if ( sPacFileNames.Contains( fileName, StringComparer.OrdinalIgnoreCase ) )
                return true;

            return false;
        }

        public override bool Accept( IntPtr handle )
        {
            return mPacksByHandle.ContainsKey( handle );
        }

        public override unsafe NtStatus SetInformationFileImpl( IntPtr hfile, out IO_STATUS_BLOCK ioStatusBlock,
            void* fileInformation, uint length, FileInformationClass fileInformationClass )
        {
            
            if ( fileInformationClass == FileInformationClass.FilePositionInformation )
            {
                //Log( $"SetInformationFileImpl(hfile = {hfile}, out ioStatusBlock, fileInformation = *0x{( long )fileInformation:X8} = {*(long*)fileInformation:X8}, " +
                //    $"length = {length}, fileInformationClass = {fileInformationClass}" );
                mPacksByHandle[hfile].FilePointer = *( long* )fileInformation;
            }
            else
            {
                Info( $"SetInformationFileImpl(hfile = {hfile}, out ioStatusBlock, fileInformation = *0x{( long )fileInformation:X8}, " +
                    $"length = {length}, fileInformationClass = {fileInformationClass}" );
            }

            return mHooks.SetInformationFIleHook.OriginalFunction( hfile, out ioStatusBlock, fileInformation, length, fileInformationClass );
        }

        public override NtStatus NtCloseImpl( IntPtr handle )
        {
            if ( mPacksByHandle.Remove( handle, out var file ) )
            {
                //Log( $"Hnd {handle} File {file.FilePath} NtClose" );
            }

            return NtStatus.Success;
        }

        public override Native.NtStatus NtCreateFileImpl( string newFilePath, out IntPtr handle, FileAccess access,
            ref Native.OBJECT_ATTRIBUTES objectAttributes, ref Native.IO_STATUS_BLOCK ioStatus, ref long allocSize,
            uint fileAttributes, FileShare share, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength )
        {
            var status = mHooks.CreateFileHook.OriginalFunction( out handle, access, ref objectAttributes, ref ioStatus, ref allocSize,
                fileAttributes, share, createDisposition, createOptions, eaBuffer, eaLength );

            //Log( $"Hnd {handle} File {newFilePath} CreateFile" );

            if ( mPacksByHandle.ContainsKey( handle ) )
            {
                Error( $"Hnd {handle} File {newFilePath} is already used by another file!!!" );
            }
            else if ( mPacksByHandle.Any( x => x.Value.FilePath.Equals( newFilePath, StringComparison.OrdinalIgnoreCase ) ) )
            {
                var otherHandle = mPacksByHandle.First( x => x.Value.FilePath.Equals( newFilePath, StringComparison.OrdinalIgnoreCase ) ).Key;
                Error( $"Hnd {otherHandle} for {newFilePath} already exists!!!" );
            }

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

                Info( $"Registered {newFilePath}" );
            }

            mPacksByHandle[handle] = pack;
            Info( $"Hnd {handle} registerd for {newFilePath}" );
            return status;
        }

        private NtStatus ReadHeader( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext,
            ref Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key,
            VirtualDwPack pack, long offset, long effOffset )
        {
            // Header read
            //Log( $"{packInfo.FileName} Hnd: {handle} Intercepted header read" );
            var result = mHooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
            if ( result != NtStatus.Success ) return result;

            var header = (LiveDwPackHeader*)buffer;

            // Copy header       
            if ( pack.Header.Signature == 0 )
            {
                // First access
                pack.Header = *header;
                pack.DataStartOffset = ( long )( sizeof( LiveDwPackHeader ) + ( sizeof( LiveDwPackFileEntry ) * pack.Header.FileCount ) );
                for ( int i = 0; i < pack.Header.FileCount; i++ )
                    pack.Files.Add( null );
                //Log( $"{packInfo.FileName} Hnd: {handle} DW_PACK header: Index: {packInfo.Header.Index} FileCount: {packInfo.Header.FileCount}" );
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
            Debug( $"{pack.FileName} Hnd: {handle} Intercepted entry read" );
            var entry = (LiveDwPackFileEntry*)buffer;
            var fileIndex = (int)((effOffset - sizeof(LiveDwPackHeader)) / sizeof(LiveDwPackFileEntry));

            if ( fileIndex >= pack.Files.Count )
            {
                Error( $"{pack.FileName} Hnd: {handle} File index out of range!! {fileIndex}" );
            }
            else
            {
                Debug( $"{pack.FileName} Hnd: {handle} Accessed file entry {entry->Path} Field00: {entry->Field00} Id: {entry->Id} Field104: {entry->Field104}" +
                        $"CompressedSize: {entry->CompressedSize:X8} UncompressedSize: {entry->UncompressedSize:X8} Flags: {entry->Flags} DataOffset: {pack.DataStartOffset + entry->DataOffset:X8}" );

                if ( pack.Files[fileIndex] == null )
                {
                    var result = mHooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
                    if ( result != NtStatus.Success ) return result;
                    var file = pack.Files[fileIndex] = new VirtualDwPackFile( pack, entry );

                    //mHooks.Disable();
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
                        //// DEBUG
                        //if ( entry->Id == 0x11A4B )
                        //{
                        //    // redirect fc_01_02.amd to fc_02_10.amd
                        //    entry->CompressedSize = 0x25A6E;
                        //    entry->UncompressedSize = 0x2B660;
                        //    entry->DataOffset = 0x53F52669;
                        //    //var temp = *entry;
                        //    //var mem = new Reloaded.Memory.Sources.ExternalMemory(Process.GetCurrentProcess());
                        //    //mem.Write( ( IntPtr )buffer, ref temp );

                        //    Log( $"{pack.FileName} Hnd: {handle} Redirected {entry->Path} to {redirectedFilePath}" );
                        //    Log($"Patched entry: Field00: {entry->Field00} Id: {entry->Id} Field104: {entry->Field104} " +
                        //        $"CompressedSize: {entry->CompressedSize:X8} UncompressedSize: {entry->UncompressedSize:X8} " +
                        //        $"Flags: {entry->Flags} DataOffset: {pack.DataStartOffset + entry->DataOffset:X8}" );
                        //}

                        //Log( $"{packContext.FileName} Hnd: {handle} {redirectedFilePath} does not exist", Color.Yellow );
                    }
                    //mHooks.Enable();
                }
                else
                {
                    // Repeat access
                    *entry = pack.Files[fileIndex].NewEntry;
                    Debug( $"{entry->Path} Repeat Access!!" );

                    //Log( $"Field00: {entry->Field00} Id: {entry->Id} Path: {entry->Path} Field104: {entry->Field104}" +
                    //    $"CompressedSize: {entry->CompressedSize} UncompressedSize: {entry->UncompressedSize} Flags: {entry->Flags} DataOffset: {packContext.DataStartOffset + entry->DataOffset}" );
                }
            }

            return NtStatus.Success;
        }

        private NtStatus ReadFile( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext,
            ref Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key,
            VirtualDwPack pack, long offset, long effOffset )
        {
            //Log( $"{pack.FileName} Hnd: {handle} Unhandled file data read request!! Offset: 0x{effOffset:X8} Length: 0x{length:X8}", mLogger.ColorRed );
            //return mHooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );

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
                    Info( $"{pack.FileName} Hnd: {handle} Accessing file data {entry.Path} Offset: 0x{effOffset:X8} Length: 0x{length:X8}" );
                    result = mHooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
                }
                else
                {
                    Info( $"{pack.FileName} Hnd: {handle} Accessing file data {entry.Path} Offset: 0x{effOffset:X8} Length: 0x{length:X8} redirected to {file.FilePath}" );
                    result = NtStatus.Success;

                    if ( length != file.FileSize )
                    {
                        Debug( "Length doesnt match file size, Dumping entry data!!" );
                        Debug( $"{pack.FileName} Hnd: {handle} Path {entry.Path}" );
                        Debug( $"Patched entry: Field00: {entry.Field00} Id: {entry.Id} Field104: {entry.Field104} " +
                            $"CompressedSize: {entry.CompressedSize:X8} UncompressedSize: {entry.UncompressedSize:X8} " +
                            $"Flags: {entry.Flags} DataOffset: {pack.DataStartOffset + entry.DataOffset:X8}" );
                        //Dump( entryOffset, ( uint )( sizeof( LiveDwPackFileEntry ) ), buffer );
                    }

                    var relativeDataOffset = effOffset - dataOffset;
                    if ( relativeDataOffset < 0 )
                    {
                        Error( $"{pack.FileName} Hnd: {handle} Attempt to read before start of data!!!" );
                        continue;
                    }
                    else
                    {
                        Info( $"{pack.FileName} Hnd: {handle} Reading 0x{length:X8} bytes from redirected file at offset 0x{relativeDataOffset:X8}" );
                    }

                    if ( ( relativeDataOffset + length ) > file.FileSize )
                    {
                        Error( $"{pack.FileName} Hnd: {handle} Redirected file is too small, attempted to read past end of data!!!" );
                        continue;
                    }

                    //mHooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );

                    // Read from redirected file into the buffer
                    using ( var fileStream = file.OpenRead() )
                    {
                        fileStream.Seek( relativeDataOffset, SeekOrigin.Begin );
                        fileStream.Read( new Span<byte>( ( void* )buffer, ( int )length ) );
                        Debug( $"{pack.FileName} Hnd: {handle} Wrote redirected file to buffer" );
                    }

                    offset += length;
                    SetInformationFileImpl( handle, out _, &offset, sizeof( long ), FileInformationClass.FilePositionInformation );

                    // Set number of read bytes.
                    ioStatus.Status = 0;
                    ioStatus.Information = new IntPtr( length );
                }
            }

            if ( !handled )
            {
                Error( $"{pack.FileName} Hnd: {handle} Unhandled file data read request!! Offset: 0x{effOffset:X8} Length: 0x{length:X8}");
                result = mHooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
            }

            return result;
        }

        public override unsafe Native.NtStatus NtReadFileImpl( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext,
            ref Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key )
        {
            //return mHooks.ReadFileHook.OriginalFunction( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key );
            NtStatus result;
            var pack = mPacksByHandle[handle];
            var offset = pack.FilePointer;
            var reqOffset = ( byteOffset != null || ( byteOffset != null && byteOffset->HighPart == -1 && byteOffset->LowPart == FILE_USE_FILE_POINTER_POSITION )) ?
                byteOffset->QuadPart : -1;
            var effOffset = reqOffset == -1 ? offset : reqOffset;

            if ( effOffset == 0 && length == sizeof( LiveDwPackHeader ) )
            {
                result = ReadHeader( handle, hEvent, apcRoutine, apcContext, ref ioStatus, buffer, length, byteOffset, key, pack, offset, effOffset );
            }
            else if ( ( effOffset >= sizeof( LiveDwPackHeader ) && effOffset < pack.DataStartOffset ) && length == sizeof( LiveDwPackFileEntry ) )
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

        public override unsafe NtStatus NtQueryInformationFileImpl( IntPtr hfile, out Native.IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length, Native.FileInformationClass fileInformationClass )
        {
            Info( $"Hnd: {hfile} QueryInformationFileHook({hfile}, out ioStatusBlock, {( long )fileInformation:X8}, {length}, {fileInformationClass}) unimplemented!" );
            return mHooks.QueryInformationFileHook.OriginalFunction( hfile, out ioStatusBlock, fileInformation, length, fileInformationClass );
            //var result = mHooks.QueryInformationFileHook.OriginalFunction( hfile, out ioStatusBlock, fileInformation, length, fileInformationClass );
            //if ( !mPacksByHandle.ContainsKey( hfile ) )
            //    return result;

            //var pack = mPacksByHandle[hfile];
            //if ( fileInformationClass == FileInformationClass.FileStandardInformation )
            //{
            //    var information = (FILE_STANDARD_INFORMATION*)fileInformation;
            //    var oldSize = information->EndOfFile;
            //    information->EndOfFile = pack.NewFileSize;

            //    //Log( $"{pack.FileName} Hnd: {hfile} File Size Override | Old: {oldSize:X8}, New: {information->EndOfFile:X8}" );
            //}

            //return result;
        }

        private void Dump( long offset, uint length, byte* buffer )
        {
            mHooks.Disable();
            using ( var dump = new FileStream( $"{DateTime.Now}-dump_{offset:X8}_{length:X8}.bin", FileMode.Create ) )
                dump.Write( new ReadOnlySpan<byte>( ( void* )buffer, ( int )length ) );
            mHooks.Enable();
        }

        private void Info( string msg ) => mLogger.WriteLine( $"[modloader:DwPackAccessRedirector] I {msg}" );
        private void Error( string msg ) => mLogger.WriteLine( $"[modloader:DwPackAccessRedirector] E {msg}", mLogger.ColorRed );
        private void Debug( string msg ) => mLogger.WriteLine( $"[modloader:DwPackAccessRedirector] D {msg}", mLogger.ColorGreen );
    }
}
