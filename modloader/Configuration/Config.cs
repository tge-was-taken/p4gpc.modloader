using System.Collections.Generic;
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


        [DisplayName( "Mods directory" )]
        [Description( "Path to the directory mods are loaded from." )]
        public string ModsDirectory { get; set; } = "mods";

        [DisplayName( "Enabled mods" )]
        [Description( "List of mods that should be loaded in order (first mod takes priority)" )]
        public List<string> EnabledMods { get; set; }
    }
}
