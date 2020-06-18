using System;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using modloader.Configuration;
using modloader.Configuration.Implementation;
using modloader.Compression;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace modloader
{
    public class Program : IMod
    {
        /// <summary>
        /// Your mod if from ModConfig.json, used during initialization.
        /// </summary>
        private const string MyModId = "p4gpc-modloader";

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
            //var stream = new BitStream(Endianness.Big);
            //stream.WriteBit( true );
            //stream.WriteBit( true );
            //Debug.Assert( stream.Position == 0 );
            //var tempx = stream.ReadByte();
            //Debug.Assert( tempx == 0xC0 );
            //Debug.Assert( stream.Position == 1 );
            //stream.WriteBit( true );
            //stream.WriteBit( false );
            //stream.WriteByte( 1 );
            //Debug.Assert( stream.Position == 2 );
            //stream.Position = 1;
            //Debug.Assert( stream.ReadBit() );
            //Debug.Assert( !stream.ReadBit() );
            //Debug.Assert( stream.ReadBit() );
            //return;

            //using ( var cpkStream = File.OpenRead( @"D:\Games\PC\SteamLibrary\steamapps\common\Day\data.cpk" ) )
            //using ( var cpkReader = new BinaryReader(cpkStream)) 
            //using ( var decCpkStream = File.Create( @"D:\Games\PC\SteamLibrary\steamapps\common\Day\data.cpk.dec" ) )
            //using ( var decCpkStream2 = File.Create( @"D:\Games\PC\SteamLibrary\steamapps\common\Day\data.cpk.dec2" ) )
            //{
            //    cpkReader.BaseStream.Seek( 4, SeekOrigin.Current );
            //    var compressedSize = cpkReader.ReadInt32();
            //    var decompressedSize = cpkReader.ReadInt32();
            //    var isCompressed = cpkReader.ReadInt32();

            //    // Compressed file header
            //    var magic = cpkReader.ReadInt32();
            //    var chunkCount = cpkReader.ReadInt32();
            //    var chunkSize = cpkReader.ReadInt32();
            //    var headerSize = cpkReader.ReadInt32();
            //    for ( int i = 0; i < chunkCount; i++ )
            //    {
            //        var chunkUncompressedSize = cpkReader.ReadInt32();
            //        var chunkCompressedSize = cpkReader.ReadInt32();
            //        var dataOffset = cpkReader.ReadInt32();
            //        var next = cpkReader.BaseStream.Position;
            //        cpkStream.Seek( ( headerSize + 0x10 ) + dataOffset, SeekOrigin.Begin );
            //        Yggdrasil.uncompress( cpkStream, decCpkStream, chunkCompressedSize, chunkUncompressedSize, true );
            //        cpkReader.BaseStream.Seek( next, SeekOrigin.Begin );

            //        decCpkStream.Position = 0;
            //        var comDecCpkStream = Yggdrasil.compress( decCpkStream, 0, ( int )decCpkStream.Length, true );
            //        using ( var temp3 = File.Create("temp3"))
            //        {
            //            comDecCpkStream.Position = 0;
            //            comDecCpkStream.CopyTo( temp3 );
            //            comDecCpkStream.Position = 0;
            //        }

            //        var temp = new MemoryStream();
            //        comDecCpkStream.Position = 0;
            //        Yggdrasil.uncompress( comDecCpkStream, temp, ( int )comDecCpkStream.Length, ( int )decCpkStream.Length );

            //        var temp2 = new MemoryStream();
            //        decCpkStream.Position = 0;
            //        decCpkStream.CopyTo( temp2 );
            //        temp.Position = 0;
            //        temp.CopyTo( decCpkStream2 );
            //        //Debug.Assert( temp.ToArray().SequenceEqual( temp2.ToArray() ) );
                    
            //        return;
            //    }
            //}
        }
    }
}
