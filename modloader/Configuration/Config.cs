using System.ComponentModel;
using modloader.Configuration.Implementation;

namespace modloader.Configuration
{
    public class Config : Configurable<Config>
    {
        /*
            User Properties:
                - Please put all of your configurable properties here.
                - Tip: Consider using the various available attributes https://stackoverflow.com/a/15051390/11106111
        
            By default, configuration saves as "Config.json" in mod folder.    
            Need more config files/classes? See Configuration.cs
        */


        [DisplayName( "Load directory" )]
        [Description( "Path to the directory files are loaded from." )]
        public string LoadDirectory { get; set; } = "mods";

        [DisplayName( "Sound load directory" )]
        [Description( "Path to the directory sound files are loaded from." )]
        public string SoundLoadDirectory { get; set; } = "mods/SND";
    }
}
