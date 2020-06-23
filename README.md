[![Build status](https://ci.appveyor.com/api/projects/status/n0gyja1foykuwmbo?svg=true)](https://ci.appveyor.com/project/TGEnigma/p4gpc-modloader)

# p4gpc.modloader
Mod loader for the Steam version of Persona 4 Golden

# Dependencies
* Latest .NET Core Runtime: https://dotnet.microsoft.com/download/dotnet-core/current/runtime
* Reloaded II by Sewer56: https://github.com/Reloaded-Project/Reloaded-II/releases

# Building
Note: skip this section if you're installing an existing build from the releases page.
* Install Reloaded II (see dependencies)
* Add an environment variable to your path named RELOADEDII set to the root of the Reloaded installation folder
* Relog to apply the changes
* Open the solution in Visual Studio 2019 and build as usual.

# Installation
* Download & install Reloaded II (see dependencies)
* Unzip the latest release of the loader into the Mods folder of Reloaded
* Run Reloaded-II.exe
* Go to 'Add application' and add p4g.exe
* Go to 'Download mods' and download "reloaded.universal.steamhook"
* Go to 'Manage mods', select Steam Hook and check the checkbox next to p4g.exe
* Press the P4G icon in the sidebar and enable Steam Hook & Mod Loader
* Press launch application
* If you would prefer to not have to use the launcher every time you want to launch the game, 
press the 'Create Shortcut' button to create a shortcut that does this automatically.

# Usage
Modified files are placed in a folder named after the PAC file inside the 'mods' folder located in the P4G installation directory by default. 

To replace title\logo.bin inside data00003.pac for example, you would place it at <p4g install path>\mods\data00003\title\logo.bin
  
This can be changed using the 'Configure Mod' option in Reloaded.

# Notes
If the console is enabled in Reloaded you can see which files are accessed during gameplay. 
Unless you need this info, I'd recommend you to disable the console for performance reasons as it causes stuttering especially during movies.

