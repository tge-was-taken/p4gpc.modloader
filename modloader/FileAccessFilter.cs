// Copied & adapted from https://github.com/Sewer56/AfsFsRedir.ReloadedII

using System;
using System.IO;
using static modloader.Native;

namespace modloader
{
    public abstract unsafe class FileAccessFilter
    {
        protected FileAccessServerHooks mHooks;

        public FileAccessFilter()
        {
        }

        public void SetHooks( FileAccessServerHooks hooks )
        {
            mHooks = hooks;
        }

        public abstract bool Accept( string newFilePath );
        public abstract bool Accept( IntPtr handle );

        public abstract NtStatus NtQueryInformationFileImpl( IntPtr hfile, out IO_STATUS_BLOCK ioStatusBlock,
            void* fileInformation, uint length, FileInformationClass fileInformationClass );

        public abstract NtStatus NtSetInformationFileImpl( IntPtr hfile, out IO_STATUS_BLOCK ioStatusBlock,
            void* fileInformation, uint length, FileInformationClass fileInformationClass );

        public abstract unsafe NtStatus NtReadFileImpl( IntPtr handle, IntPtr hEvent, IntPtr* apcRoutine, IntPtr* apcContext,
            ref IO_STATUS_BLOCK ioStatus, byte* buffer, uint length, LARGE_INTEGER* byteOffset, IntPtr key );

        public abstract NtStatus NtCreateFileImpl( string newFilePath, out IntPtr handle, FileAccess access, ref OBJECT_ATTRIBUTES objectAttributes,
            ref IO_STATUS_BLOCK ioStatus, ref long allocSize, uint fileAttributes, FileShare share, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength );

        public abstract uint SetFilePointerImpl( IntPtr hFile, int liDistanceToMove, IntPtr lpNewFilePointer, uint dwMoveMethod );

        public abstract bool CloseHandleImpl( IntPtr handle );
    }
}
