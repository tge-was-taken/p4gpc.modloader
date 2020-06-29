using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using modloader.Formats.DwPack;
using modloader.Mods;
using Reloaded.Mod.Interfaces;

namespace modloader.Redirectors.DwPack
{
    public unsafe class VirtualDwPackEntry
    {
        private readonly ILogger mLogger;

        public VirtualDwPack Pack { get; private set; }
        public DwPackEntry* Native { get; private set; }    
        public int Index { get; private set; }

        public bool RedirectAttempted { get; private set; }
        public bool IsRedirected { get; private set; }
        public string RedirectedFilePath { get; private set; }
        public string RedirectedFileName { get; private set; }
        public long RedirectedFileSize { get; private set; }

        public VirtualDwPackEntry( ILogger logger, VirtualDwPack pack, DwPackEntry* entry, int index )
        {
            mLogger = logger;
            Pack = pack;
            Native = entry;
            Index = index;
        }

        public void EnsureRedirected( ModDb modDb )
        {
            if ( !RedirectAttempted )
                TryRedirect( modDb );
        }

        public bool TryRedirect( ModDb modDb )
        {
            RedirectAttempted = true;

            foreach ( var mod in modDb.Mods )
            {
                var redirectedFilePath = Path.Combine(mod.LoadDirectory, Pack.FileName, Native->Path);
                if ( mod.Files.Contains( redirectedFilePath ) )
                {
                    Redirect( redirectedFilePath );
                    mLogger.WriteLine( $"[modloader:DwPackRedirector] I: {Pack.FileName} {Native->Path} Redirected to {redirectedFilePath}" );
                    return true;
                }
                else
                {
#if DEBUG
                    mLogger.WriteLine( $"[modloader:DwPackRedirector] D: No redirection for {Native->Path} because {redirectedFilePath} does not exist." );
#endif
                }
            }

            return false;
        }

        public void Redirect( string newPath )
        {
            RedirectedFilePath = newPath;
            using ( var stream = OpenRead() )
                RedirectedFileSize = stream.Length;

            // Patch file size         
            var originalSize = Native->CompressedSize;
            Native->CompressedSize = ( int )RedirectedFileSize;
            Native->UncompressedSize = ( int )RedirectedFileSize;
            Native->Flags = 0;

            if ( Native->CompressedSize > originalSize )
            {
                // Patch data offset
                Native->DataOffset = ( int )Pack.ReallocateFileData( Native->DataOffset, Native->CompressedSize );
            }

            IsRedirected = true;
        }

        public Stream OpenRead()
            => new FileStream( RedirectedFilePath, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Write | FileShare.Delete, 0x300_000, FileOptions.RandomAccess );
    }

}
