// Copied & adapted from https://github.com/Sewer56/AfsFsRedir.ReloadedII

using System;
using Reloaded.Hooks.Definitions;
using static modloader.Native;

namespace modloader
{
    public class FileAccessServerHooks
    {
        public IHook<NtCreateFile> NtCreateFileHook { get; }
        public IHook<NtReadFile> NtReadFileHook { get; }
        public IHook<NtSetInformationFile> NtSetInformationFileHook { get; }
        public IHook<NtQueryInformationFile> NtQueryInformationFileHook { get; }
        public IHook<CloseHandleDelegate> CloseHandleHook { get; }

        public FileAccessServerHooks(IHook<NtCreateFile> createFileHook, IHook<NtReadFile> readFileHook,
            IHook<NtSetInformationFile> setInformationFileHook, IHook<NtQueryInformationFile> queryInformationHook,
            IHook<CloseHandleDelegate> closeHandleHook)
        {
            NtCreateFileHook = createFileHook;
            NtReadFileHook = readFileHook;
            NtSetInformationFileHook = setInformationFileHook;
            NtQueryInformationFileHook = queryInformationHook;
            CloseHandleHook = closeHandleHook;
        }

        public void Activate()
        {
            NtCreateFileHook.Activate();
            NtReadFileHook.Activate();
            NtSetInformationFileHook.Activate();
            NtQueryInformationFileHook.Activate();
            CloseHandleHook.Activate();
        }

        public void Disable()
        {
            NtCreateFileHook.Disable();
            NtReadFileHook.Disable();
            NtSetInformationFileHook.Disable();
            NtQueryInformationFileHook.Disable();
            CloseHandleHook.Disable();
        }

        public void Enable()
        {
            NtCreateFileHook.Enable();
            NtReadFileHook.Enable();
            NtSetInformationFileHook.Enable();
            NtQueryInformationFileHook.Enable();
            CloseHandleHook.Enable();
        }
    }
}
