using System;
using System.Collections.Generic;
using System.IO;

namespace modloader.Mods
{
    public class Mod
    {
        public string Name { get; }
        public string LoadDirectory { get; }
        public HashSet<string> Files { get; }

        public Mod( string name, string loadDirectory )
        {
            Name = name;
            LoadDirectory = loadDirectory;
            Files = new HashSet<string>( Directory.EnumerateFiles( LoadDirectory, "*.*", SearchOption.AllDirectories ) );
        }
    }
}
