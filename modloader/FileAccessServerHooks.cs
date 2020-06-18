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

        public FileAccessServerHooks(IHook<NtCreateFile> createFileHook, IHook<NtReadFile> readFileHook,
            IHook<NtSetInformationFile> setInformationFileHook, IHook<NtQueryInformationFile> queryInformationHook)
        {
            CreateFileHook = createFileHook;
            ReadFileHook = readFileHook;
            SetInformationFIleHook = setInformationFileHook;
            QueryInformationFileHook = queryInformationHook;
        }

        public void Activate()
        {
            CreateFileHook.Activate();
            ReadFileHook.Activate();
            SetInformationFIleHook.Activate();
            QueryInformationFileHook.Activate();
        }

        public void Disable()
        {
            CreateFileHook.Disable();
            ReadFileHook.Disable();
            SetInformationFIleHook.Disable();
            QueryInformationFileHook.Disable();
        }

        public void Enable()
        {
            CreateFileHook.Enable();
            ReadFileHook.Enable();
            SetInformationFIleHook.Enable();
            QueryInformationFileHook.Enable();
        }
    }
}
