using modloader.Configuration;
using Reloaded.Mod.Interfaces;

using modloader.Redirectors.DwPack;
using modloader.Redirectors.Xact;
using System;
using modloader.Utilities;
using modloader.Mods;
using System.Diagnostics;

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

        public P4GPCModLoader( ILogger logger, Config configuration, Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks hooks )
        {
            mLogger = logger;
            mConfiguration = configuration;
            mHooks = hooks;

#if DEBUG
            Debugger.Launch();
#endif

            // Init
            if ( Native.GetConsoleWindow() != IntPtr.Zero )
                Console.OutputEncoding = EncodingCache.ShiftJIS;

            mLogger.WriteLine( "[modloader] Persona 4 Golden (Steam) Mod loader by TGE (2020) v1.2.0a" );
            mNativeFunctions = NativeFunctions.GetInstance( hooks );
            mFileAccessServer = new FileAccessServer( hooks, mNativeFunctions );

            // Load mods
            var modDb = new ModDb( mConfiguration.ModsDirectory, mConfiguration.EnabledMods );

            // DW_PACK (PAC) redirector
            mDwPackRedirector = new DwPackRedirector( logger, modDb );
            mFileAccessServer.AddClient( mDwPackRedirector );

            // XACT (XWB, XSB) redirector
            mXactRedirector = new XactRedirector( logger, modDb );
            mFileAccessServer.AddClient( mXactRedirector );

            mFileAccessServer.Activate();
        }
    }
}
