using modloader.Configuration;
using Reloaded.Mod.Interfaces;

using modloader.Redirectors.DwPack;
using modloader.Redirectors.Xact;
using System;
using modloader.Utilities;

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

            Console.OutputEncoding = EncodingCache.ShiftJIS;
            mLogger.WriteLine( "[modloader] Persona 4 Golden (Steam) Mod loader by TGE (2020) v1.0.0" );
            mNativeFunctions = NativeFunctions.GetInstance( hooks );
            mFileAccessServer = new FileAccessServer( hooks, mNativeFunctions );

            // DW_PACK (PAC) redirector
            mDwPackRedirector = new DwPackRedirector( logger, mConfiguration.LoadDirectory );
            mFileAccessServer.AddFilter( mDwPackRedirector );

            // XACT (XWB, XSB) redirector
            mXactRedirector = new XactRedirector( logger, mConfiguration.SoundLoadDirectory );
            mFileAccessServer.AddFilter( mXactRedirector );

            mFileAccessServer.Activate();
        }
    }
}
