using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Amicitia.IO.Binary;
using modloader.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using static modloader.Native;

namespace modloader
{
    public unsafe class P4GPCModLoader
    {
        private ILogger mLogger;
        private Config mConfiguration;
        private Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks mHooks;
        private NativeFunctions mNativeFunctions;

        private FileAccessServer mFileAccessServer;
        private DwPackAccessRedirector mDwPackRedirector;

        public P4GPCModLoader( ILogger logger, Config configuration, Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks hooks )
        {
            mLogger = logger;
            mConfiguration = configuration;
            mHooks = hooks;

            mNativeFunctions = NativeFunctions.GetInstance( hooks );
            mFileAccessServer = new FileAccessServer( mNativeFunctions );
            mDwPackRedirector = new DwPackAccessRedirector( logger );
            mDwPackRedirector.SetLoadDirectory( mConfiguration.ModDir );
            mFileAccessServer.AddFilter( mDwPackRedirector );
        }
    }
}
