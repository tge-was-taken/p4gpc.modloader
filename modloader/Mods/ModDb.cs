using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace modloader.Mods
{
    public class ModDb
    {
        private List<Mod> mMods;

        public IReadOnlyList<Mod> Mods => mMods;

        public ModDb( string directoryPath, List<string> modLoadList )
        {
            mMods = new List<Mod>();

            Directory.CreateDirectory( directoryPath );
            foreach ( var dir in Directory.EnumerateDirectories( directoryPath ) )
            {
                var name = Path.GetFileName( dir );
                if ( modLoadList.Contains( name, StringComparer.InvariantCultureIgnoreCase ) )
                    mMods.Add( new Mod( name, dir ) );
            }

            mMods.Sort((x, y) => modLoadList.IndexOf(x.Name).CompareTo(modLoadList.IndexOf(y.Name)));
            mMods.Insert( 0, new Mod( "<global>", directoryPath ) );
        }
    }
}
