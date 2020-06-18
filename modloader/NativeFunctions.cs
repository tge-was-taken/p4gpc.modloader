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
        public IFunction<NtSetInformationFile> SetFilePointer;
        public IFunction<NtQueryInformationFile> GetFileSize;
        public IFunction<FreeConsoleDelegate> FreeConsole;
        public IFunction<CloseHandleDelegate> CloseHandle;

        public NativeFunctions(IntPtr ntCreateFile, IntPtr ntReadFile, IntPtr ntSetInformationFile, 
            IntPtr ntQueryInformationFile, IntPtr freeConsole, IntPtr closeHandle,
            IReloadedHooks hooks)
        {
            NtCreateFile = hooks.CreateFunction<NtCreateFile>((long) ntCreateFile);
            NtReadFile = hooks.CreateFunction<NtReadFile>((long) ntReadFile);
            SetFilePointer = hooks.CreateFunction<NtSetInformationFile>((long) ntSetInformationFile);
            GetFileSize = hooks.CreateFunction<NtQueryInformationFile>((long) ntQueryInformationFile);
            FreeConsole = hooks.CreateFunction<FreeConsoleDelegate>( (long)freeConsole );
            CloseHandle = hooks.CreateFunction<CloseHandleDelegate>( ( long )closeHandle );
        }

        public static NativeFunctions GetInstance(IReloadedHooks hooks)
        {
            if (_instanceMade)
                return _instance;

            var ntdllHandle    = LoadLibraryW("ntdll");
            var ntCreateFilePointer = GetProcAddress(ntdllHandle, "NtCreateFile");
            var ntReadFilePointer = GetProcAddress(ntdllHandle, "NtReadFile");
            var setFilePointer = GetProcAddress(ntdllHandle, "NtSetInformationFile");
            var getFileSize = GetProcAddress(ntdllHandle, "NtQueryInformationFile");

            var kernel32Handle = LoadLibraryW("kernel32");
            var freeConsolePtr = GetProcAddress(kernel32Handle, "FreeConsole");
            var closeHandlePtr = GetProcAddress(kernel32Handle, "CloseHandle");

            _instance = new NativeFunctions(ntCreateFilePointer, ntReadFilePointer, setFilePointer, getFileSize, freeConsolePtr, closeHandlePtr, hooks );
            _instanceMade = true;

            return _instance;
        }
    }
}
