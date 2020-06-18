using System.IO;
using Reloaded.Mod.Interfaces;

namespace modloader.Configuration.Implementation
{
    public class Configurator : IConfigurator
    {
        /* For latest documentation:
            - See the interface! (Go To Definition) or if not available
            - Google the Source Code!
        */

        /// <summary>
        /// Full path to the mod folder.
        /// </summary>
        public string ModFolder { get; private set; }

        /// <summary>
        /// Returns a list of configurations.
        /// </summary> 
        public IUpdatableConfigurable[] Configurations => _configurations ?? MakeConfigurations();
        private IUpdatableConfigurable[] _configurations;

        private IUpdatableConfigurable[] MakeConfigurations()
        {
            _configurations = new IUpdatableConfigurable[]
            {
                // Add more configurations here if needed.
                Configurable<Config>.FromFile(Path.Combine(ModFolder, "Config.json"), "Default Config")
            };

            // Add self-updating to configurations.
            for ( int x = 0; x < Configurations.Length; x++ )
            {
                var xCopy = x;
                Configurations[x].ConfigurationUpdated += configurable =>
                {
                    Configurations[xCopy] = configurable;
                };
            }

            return _configurations;
        }

        public Configurator() { }
        public Configurator( string modDirectory ) : this()
        {
            ModFolder = modDirectory;
        }

        /* Configurator */

        /// <summary>
        /// Gets an individual user configuration.
        /// </summary>
        public TType GetConfiguration<TType>( int index ) => ( TType )Configurations[index];

        /* IConfigurator. */

        /// <summary>
        /// Sets the mod directory for the Configurator.
        /// </summary>
        public void SetModDirectory( string modDirectory ) => ModFolder = modDirectory;

        /// <summary>
        /// Returns a list of user configurations.
        /// </summary>
        public IConfigurable[] GetConfigurations() => Configurations;

        /// <summary>
        /// Allows for custom launcher/configurator implementation.
        /// If you have your own configuration program/code, run that code here and return true, else return false.
        /// </summary>
        public bool TryRunCustomConfiguration() => false;
    }
}
