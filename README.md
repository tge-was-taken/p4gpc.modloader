# p4gpc-modloader
Mod loader for the Steam version of Persona 4 Golden

# Dependencies
Latest .NET Core Runtime: https://dotnet.microsoft.com/download/dotnet-core/current/runtime
Reloaded II by Sewer56: https://github.com/Reloaded-Project/Reloaded-II/releases

# Building
Install Reloaded II from here: https://github.com/Reloaded-Project/Reloaded-II/releases
Add an environment variable to your path named RELOADEDII set to the root of the Reloaded installation folder
Relog to apply the changes
Open the solution in Visual Studio 2019 and build as usual.

# Usage
Modified files are placed in a folder named after the PAC file inside the 'mods' folder located in the P4G installation directory by default. 
To replace title\logo.bin inside data00003.pac for example, you would place it at <p4g install path>\mods\data00003\title\logo.bin
This can be changed in the ModConfig.json

# Notes
If the console is enabled in Reloaded you can see which files are accessed during gameplay. 
Unless you need this info, I'd recommend you to disable the console for performance reasons as it causes stuttering.

