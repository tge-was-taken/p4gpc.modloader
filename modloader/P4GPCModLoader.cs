using modloader.Configuration;
using Reloaded.Mod.Interfaces;

using modloader.Redirectors.DwPack;
using modloader.Redirectors.Xact;
using System;
using System.Diagnostics;
using modloader.Utilities;
using modloader.Mods;
using System.Text;
using modloader.Redirectors.Cpk;

namespace modloader
{
    public unsafe class P4GPCModLoader
    {
        private ILogger mLogger;
        private Config mConfiguration;
        private readonly Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks mHooks;
        private NativeFunctions mNativeFunctions;

        private FileAccessServer mFileAccessServer;
        private DwPackRedirector mDwPackRedirector;
        private XactRedirector mXactRedirector;
        private CpkRedirector mCpkRedirector;

        public P4GPCModLoader( ILogger logger, Config configuration, Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks hooks )
        {
            mLogger = logger;
            mConfiguration = configuration;
            mHooks = hooks;

#if DEBUG
            Debugger.Launch();
#endif

            // Init
            TrySetConsoleEncoding( EncodingCache.ShiftJIS );
            var version = typeof(P4GPCModLoader).Assembly.GetName().Version.ToString();
            version = version.Substring(0, version.LastIndexOf('.'));
            mLogger.WriteLine( $"[modloader] Persona 4 Golden (Steam) Mod loader by TGE (2020) v{version}" );
            mNativeFunctions = NativeFunctions.GetInstance( hooks );
            mFileAccessServer = new FileAccessServer( hooks, mNativeFunctions, logger );

            // Load mods
            var modDb = new ModDb( mConfiguration.ModsDirectory, mConfiguration.EnabledMods );

            // CPK redirector
            mCpkRedirector = new CpkRedirector( mLogger, modDb, configuration );
            mFileAccessServer.AddClient( mCpkRedirector );

            // DW_PACK (PAC) redirector
            mDwPackRedirector = new DwPackRedirector( mLogger, modDb, configuration, mCpkRedirector );
            mFileAccessServer.AddClient( mDwPackRedirector );

            // XACT (XWB, XSB) redirector
            mXactRedirector = new XactRedirector( mLogger, modDb, configuration );
            mFileAccessServer.AddClient( mXactRedirector );
        }

        public void Activate()
        {
            mFileAccessServer.Activate();
        }

        public void TrySetConsoleEncoding( Encoding encoding )
        {
            if ( Native.GetConsoleWindow() != IntPtr.Zero )
            {
                try
                {
                    Console.OutputEncoding = encoding;
                }
                catch ( Exception )
                {
                    // Fails if encoding is not supported by the console
                }
            }
        }
    }
}
