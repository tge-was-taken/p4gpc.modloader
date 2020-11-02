using System.IO;
using modloader.Mods;
using PreappPartnersLib.FileSystems;

namespace modloader.Redirectors.DwPack
{
    public unsafe class VirtualDwPackEntry
    {
        private readonly SemanticLogger mLogger;

        public VirtualDwPack Pack { get; private set; }
        public DwPackEntry* Native { get; private set; }    

        public bool RedirectAttempted { get; private set; }
        public bool IsRedirected { get; private set; }
        public string RedirectedFilePath { get; private set; }
        public string RedirectedFileName { get; private set; }
        public long RedirectedFileSize { get; private set; }

        public VirtualDwPackEntry( SemanticLogger logger, VirtualDwPack pack, DwPackEntry* entry )
        {
            mLogger = logger;
            Pack = pack;
            Native = entry;
        }

        public bool Redirect( ModDb modDb )
        {
            RedirectAttempted = true;

            foreach ( var mod in modDb.Mods )
            {
                var pacRedirectFilePath = Path.Combine(mod.LoadDirectory, Pack.FileName, Native->Path);
                if ( mod.Files.Contains( pacRedirectFilePath ) )
                {
                    // Replacement file stored in folder named after pac file
                    Redirect( pacRedirectFilePath );
                    mLogger.Info( $"{Pack.FileName} {Native->Path} Redirected to {pacRedirectFilePath}" );
                    return true;
                }

                if ( Pack.Cpk != null )
                {
                    var cpkRedirectFilePath = Path.Combine( mod.LoadDirectory, Pack.Cpk.FileName, Native->Path );
                    if ( mod.Files.Contains( cpkRedirectFilePath ) )
                    {
                        // Replacement file stored in folder named after cpk file
                        Redirect( cpkRedirectFilePath );
                        mLogger.Info( $"{Pack.FileName} {Native->Path} Redirected to {cpkRedirectFilePath}" );
                        return true;
                    }
                }

                mLogger.Debug( $"No redirection for {Native->Path}." );
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
