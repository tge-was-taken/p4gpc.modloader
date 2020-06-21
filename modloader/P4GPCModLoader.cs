using modloader.Configuration;
using Reloaded.Mod.Interfaces;

namespace modloader
{
    public unsafe class P4GPCModLoader
    {
        private ILogger mLogger;
        private Config mConfiguration;
        private readonly Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks mHooks;
        private NativeFunctions mNativeFunctions;

        private FileAccessServer mFileAccessServer;
        private DwPackAccessRedirector mDwPackRedirector;

        public P4GPCModLoader( ILogger logger, Config configuration, Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks hooks )
        {
            mLogger = logger;
            mConfiguration = configuration;
            mHooks = hooks;

            mLogger.WriteLine( "[modloader] Persona 4 Golden (Steam) Mod loader by TGE (2020) v1.0.0" );
            mNativeFunctions = NativeFunctions.GetInstance( hooks );
            mFileAccessServer = new FileAccessServer( mNativeFunctions );
            mDwPackRedirector = new DwPackAccessRedirector( logger );
            mDwPackRedirector.SetLoadDirectory( mConfiguration.LoadDirectory );
            mFileAccessServer.AddFilter( mDwPackRedirector );
            mFileAccessServer.Activate();
        }
    }
}
