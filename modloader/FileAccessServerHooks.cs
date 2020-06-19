// Copied & adapted from https://github.com/Sewer56/AfsFsRedir.ReloadedII

using System;
using Reloaded.Hooks.Definitions;
using static modloader.Native;

namespace modloader
{
    public class FileAccessServerHooks
    {
        public IHook<NtCreateFile> CreateFileHook { get; }
        public IHook<NtReadFile> ReadFileHook { get; }
        public IHook<NtSetInformationFile> SetInformationFIleHook { get; }
        public IHook<NtQueryInformationFile> QueryInformationFileHook { get; }
        public IHook<SetFilePointer> SetFilePointerHook { get; }
        public IHook<CloseHandleDelegate> CloseHandleHook { get; }

        public FileAccessServerHooks(IHook<NtCreateFile> createFileHook, IHook<NtReadFile> readFileHook,
            IHook<NtSetInformationFile> setInformationFileHook, IHook<NtQueryInformationFile> queryInformationHook,
            IHook<SetFilePointer> setFilePointerHook, IHook<CloseHandleDelegate> closeHandleHook)
        {
            CreateFileHook = createFileHook;
            ReadFileHook = readFileHook;
            SetInformationFIleHook = setInformationFileHook;
            QueryInformationFileHook = queryInformationHook;
            SetFilePointerHook = setFilePointerHook;
            CloseHandleHook = closeHandleHook;
        }

        public void Activate()
        {
            CreateFileHook.Activate();
            ReadFileHook.Activate();
            SetInformationFIleHook.Activate();
            QueryInformationFileHook.Activate();
            SetFilePointerHook.Activate();
            CloseHandleHook.Activate();
        }

        public void Disable()
        {
            CreateFileHook.Disable();
            ReadFileHook.Disable();
            SetInformationFIleHook.Disable();
            QueryInformationFileHook.Disable();
            SetFilePointerHook.Disable();
            CloseHandleHook.Disable();
        }

        public void Enable()
        {
            CreateFileHook.Enable();
            ReadFileHook.Enable();
            SetInformationFIleHook.Enable();
            QueryInformationFileHook.Enable();
            SetFilePointerHook.Enable();
            CloseHandleHook.Enable();
        }
    }
}
