// Copied & adapted from https://github.com/Sewer56/AfsFsRedir.ReloadedII

using System;
using Reloaded.Hooks.Definitions;
using static modloader.Native;

namespace modloader
{
    public struct NativeFunctions
    {
        private static bool _instanceMade;
        private static NativeFunctions _instance;

        public IFunction<NtCreateFile> NtCreateFile;
        public IFunction<NtReadFile> NtReadFile;
        public IFunction<NtSetInformationFile> NtSetInformationFile;
        public IFunction<NtQueryInformationFile> NtQueryinformationFile;
        public IFunction<SetFilePointer> SetFilePointer;
        public IFunction<CloseHandleDelegate> CloseHandle;

        public NativeFunctions(IntPtr ntCreateFile, IntPtr ntReadFile, IntPtr ntSetInformationFile, 
            IntPtr ntQueryInformationFile, IntPtr setFilePointer, IntPtr closeHandle,
            IReloadedHooks hooks)
        {
            NtCreateFile = hooks.CreateFunction<NtCreateFile>((long) ntCreateFile);
            NtReadFile = hooks.CreateFunction<NtReadFile>((long) ntReadFile);
            NtSetInformationFile = hooks.CreateFunction<NtSetInformationFile>((long) ntSetInformationFile);
            NtQueryinformationFile = hooks.CreateFunction<NtQueryInformationFile>((long) ntQueryInformationFile);
            SetFilePointer = hooks.CreateFunction<SetFilePointer>( ( long )setFilePointer );
            CloseHandle = hooks.CreateFunction<CloseHandleDelegate>( ( long )closeHandle );
        }

        public static NativeFunctions GetInstance(IReloadedHooks hooks)
        {
            if (_instanceMade)
                return _instance;

            var ntdllHandle    = LoadLibraryW("ntdll");
            var ntCreateFilePointer = GetProcAddress(ntdllHandle, "NtCreateFile");
            var ntReadFilePointer = GetProcAddress(ntdllHandle, "NtReadFile");
            var ntSetInformationFilePtr = GetProcAddress(ntdllHandle, "NtSetInformationFile");
            var ntQueryInformationFilePtr = GetProcAddress(ntdllHandle, "NtQueryInformationFile");

            var kernel32Handle = LoadLibraryW("kernel32");
            var setFilePointerPtr = GetProcAddress(kernel32Handle, "SetFilePointer");
            var closeHandlePtr = GetProcAddress(kernel32Handle, "CloseHandle");

            _instance = new NativeFunctions(ntCreateFilePointer, ntReadFilePointer, ntSetInformationFilePtr, ntQueryInformationFilePtr, setFilePointerPtr, closeHandlePtr, hooks );
            _instanceMade = true;

            return _instance;
        }
    }
}
