using System;

namespace modloader.Mods
{
    public class Mod
    {
        public string Name { get; }
        public string LoadDirectory { get; }

        public Mod( string name, string loadDirectory )
        {
            Name = name;
            LoadDirectory = loadDirectory;
        }
    }
}
