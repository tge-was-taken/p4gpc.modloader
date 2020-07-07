using System;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using modloader.Configuration;
using modloader.Configuration.Implementation;

namespace modloader
{
    public class Program : IMod
    {
        /// <summary>
        /// Your mod if from ModConfig.json, used during initialization.
        /// </summary>
        private const string MyModId = "p4gpc.modloader";

        /// <summary>
        /// Used for writing text to the console window.
        /// </summary>
        private ILogger _logger;

        /// <summary>
        /// Provides access to the mod loader API.
        /// </summary>
        private IModLoader _modLoader;

        /// <summary>
        /// Stores the contents of your mod's configuration. Automatically updated by template.
        /// </summary>
        private Config _configuration;

        /// <summary>
        /// An interface to Reloaded's the function hooks/detours library.
        /// See: https://github.com/Reloaded-Project/Reloaded.Hooks
        ///      for documentation and samples. 
        /// </summary>
        private IReloadedHooks _hooks;

        private P4GPCModLoader _P4GPCModLoader;

        /// <summary>
        /// Entry point for your mod.
        /// </summary>
        public void Start( IModLoaderV1 loader )
        {
            _modLoader = ( IModLoader )loader;
            _logger = ( ILogger )_modLoader.GetLogger();
            _modLoader.GetController<IReloadedHooks>().TryGetTarget( out _hooks );

            // Your config file is in Config.json.
            // Need a different name, format or more configurations? Modify the `Configurator`.
            // If you do not want a config, remove Configuration folder and Config class.
            var configurator = new Configurator(_modLoader.GetDirectoryForModId(MyModId));
            _configuration = configurator.GetConfiguration<Config>( 0 );
            _configuration.ConfigurationUpdated += OnConfigurationUpdated;

            /* Your mod code starts here. */
            _P4GPCModLoader = new P4GPCModLoader( _logger, _configuration, _hooks );
            _P4GPCModLoader.Activate();
        }

        private void OnConfigurationUpdated( IConfigurable obj )
        {
            /*
                This is executed when the configuration file gets updated by the user
                at runtime.
            */

            // Replace configuration with new.
            _configuration = ( Config )obj;
            _logger.WriteLine( $"[{MyModId}] Config Updated: Applying" );

            // Apply settings from configuration.
            // ... your code here.
        }

        /* Mod loader actions. */
        public void Suspend()
        {
            /*  Some tips if you wish to support this (CanSuspend == true)
             
                A. Undo memory modifications.
                B. Deactivate hooks. (Reloaded.Hooks Supports This!)
            */
        }

        public void Resume()
        {
            /*  Some tips if you wish to support this (CanSuspend == true)
             
                A. Redo memory modifications.
                B. Re-activate hooks. (Reloaded.Hooks Supports This!)
            */
        }

        public void Unload()
        {
            /*  Some tips if you wish to support this (CanUnload == true).
             
                A. Execute Suspend(). [Suspend should be reusable in this method]
                B. Release any unmanaged resources, e.g. Native memory.
            */
        }

        /*  If CanSuspend == false, suspend and resume button are disabled in Launcher and Suspend()/Resume() will never be called.
            If CanUnload == false, unload button is disabled in Launcher and Unload() will never be called.
        */
        public bool CanUnload() => false;
        public bool CanSuspend() => false;

        /* Automatically called by the mod loader when the mod is about to be unloaded. */
        public Action Disposing { get; }

        /* Contains the Types you would like to share with other mods.
           If you do not want to share any types, please remove this method and the
           IExports interface.
        
           Inter Mod Communication: https://github.com/Reloaded-Project/Reloaded-II/blob/master/Docs/InterModCommunication.md
        */
        public Type[] GetTypes() => new Type[0];

        /* This is a dummy for R2R (ReadyToRun) deployment.
           For more details see: https://github.com/Reloaded-Project/Reloaded-II/blob/master/Docs/ReadyToRun.md
        */
        public static void Main() 
        {
        }
    }
}
