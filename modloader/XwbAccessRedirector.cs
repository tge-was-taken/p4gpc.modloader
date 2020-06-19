using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using modloader.XACT;
using Reloaded.Mod.Interfaces;

namespace modloader
{
    public class XwbAccessRedirector : FileAccessFilter
    {
        private readonly ILogger mLogger;

        private class VirtualXwb
        {
            public WaveBankHeader Header;
            public WaveBankData Data;
        }

        public XwbAccessRedirector(ILogger logger)
        {
            mLogger = logger;
        }

        public override bool Accept( string newFilePath )
        {
            return Path.GetExtension( newFilePath ).Equals( ".xwb", StringComparison.OrdinalIgnoreCase );
        }

        public override bool Accept( IntPtr handle )
        {
            throw new NotImplementedException();
        }

        public override Native.NtStatus NtCreateFileImpl( string newFilePath, out IntPtr handle, FileAccess access, ref Native.OBJECT_ATTRIBUTES objectAttributes, ref Native.IO_STATUS_BLOCK ioStatus, ref long allocSize, uint fileAttributes, FileShare share, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength )
        {
            throw new NotImplementedException();
        }

        public override unsafe Native.NtStatus NtQueryInformationFileImpl( IntPtr hfile, out Native.IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length, Native.FileInformationClass fileInformationClass )
        {
            throw new NotImplementedException();
        }

        public override unsafe Native.NtStatus NtReadFileImpl( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext, ref Native.IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, Native.LARGE_INTEGER* byteOffset, IntPtr key )
        {
            throw new NotImplementedException();
        }

        public override unsafe Native.NtStatus NtSetInformationFileImpl( IntPtr hfile, out Native.IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length, Native.FileInformationClass fileInformationClass )
        {
            throw new NotImplementedException();
        }

        private void Info( string msg ) => mLogger.WriteLine( $"[modloader:XwbAccessRedirector] I {msg}" );
        private void Warning( string msg ) => mLogger.WriteLine( $"[modloader:XwbAccessRedirector] W {msg}", mLogger.ColorYellow );
        private void Error( string msg ) => mLogger.WriteLine( $"[modloader:XwbAccessRedirector] E {msg}", mLogger.ColorRed );

        [Conditional( "DEBUG" )]
        private void Debug( string msg ) => mLogger.WriteLine( $"[modloader:XwbAccessRedirector] D {msg}", mLogger.ColorGreen );

        public override uint SetFilePointerImpl( IntPtr hFile, int liDistanceToMove, IntPtr lpNewFilePointer, uint dwMoveMethod )
        {
            throw new NotImplementedException();
        }

        public override bool CloseHandleImpl( IntPtr handle )
        {
            throw new NotImplementedException();
        }
    }
}
