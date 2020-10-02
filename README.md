[![Build status](https://ci.appveyor.com/api/projects/status/n0gyja1foykuwmbo?svg=true)](https://ci.appveyor.com/project/TGEnigma/p4gpc-modloader)

# p4gpc.modloader
Mod loader for the Steam version of Persona 4 Golden

# Download
Stable releases can be found [here](https://github.com/TGEnigma/p4gpc.modloader/releases/).

Development releases can be found [here](https://ci.appveyor.com/project/TGEnigma/p4gpc-modloader/build/artifacts).

# Dependencies
* Windows 7/8.1/10 with latest updates
* .NET Core 3.1 Desktop Runtime x64 AND x86
* Visual C++ Redist 2015/17/19 (x64) and (x86)
* Reloaded II by Sewer56: https://github.com/Reloaded-Project/Reloaded-II/releases

# Building
Note: skip this section if you're installing an existing build from the releases page.
* Install Reloaded II (see dependencies)
* Add an environment variable named RELOADEDIIMODS set to the Mods folder used by Reloaded.\* 
* Log off and back on to apply the changes
* Open the solution in Visual Studio 2019 and build as usual.

By default this is a folder called `Mods` in the root of the Reloaded installation folder, however can be overwritten in `%AppData%/Reloaded-Mod-Loader-II/ReloadedII.json`.

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
The following instructions assume the default paths, however they can be changed in the config.

## Mod management
Mods can be seperated from each other by creating a folder inside the "mods" folder (p4g install path/mods) with the name of your mod.
You can enable them by adding their (case insensitive) name to the 'EnabledMods' list either directly by editing Config.json or through Reloaded. The mod loader will give the highest priority to the entries at the top of the list, meaning that mods below it can't override files replaced by the topmost one. 

## PAC file replacement
Modified files are placed in a folder named after the PAC file inside the mod folder. The mods are located in the "mods" folder in P4G installation directory by default. 

To replace title\logo.bin inside data00003.pac for example, you would place it at (p4g install path)/mods/(mod name)/data00003/title/logo.bin

## XWB file replacement
Modified files are placed in a folder named after the XWB file inside the 'SND' folder inside the mod folder. The mods are located in the "mods" folder in P4G installation directory by default.

Sound (track) replacements consist out of a RAW file and a TXTH file. The RAW file contains the raw, unformatted audio data whilst the TXTH is a text based format that describes how the data should be loaded. 
TXTH is supported by vgmstream, and can be used by playing the RAW file (make sure the TXTH file name is <filename>.raw.txth). Use this to test if the audio plays correctly!

A tool such as [this one](https://github.com/jpmac26/P4G_PC_Music_Converter) can be used to create these files.

Tracks can be replaced by either their cue name or their index:
* To replace BGM055 (cue name, main battle theme) inside BGM.XWB, you would place the the RAW file at "(p4g install path)/mods/(mod name)/SND/BGM/BGM055.raw", and the TXTH file at "(p4g install path)/mods/(mod name)/SND/BGM/BGM055.raw.txth".
* To replace track index 20 (base 0, title screen bgm) inside BGM.XWB, you would place the RAW file at "(p4g install path)/mods/(mod name)/SND/BGM/20.raw", and the TXTH file at "(p4g install path)/mods/(mod name)/SND/BGM/20.raw.txth".

### Technical info
The RAW file contains the raw PCM data, which would be the contents of the WAV 'data' chunk.
The TXTH file is based on the format described [here](https://github.com/losnoco/vgmstream/blob/master/doc/TXTH.md) however the loader only implements the necessary subset of options, being:
* num_samples = (integer)
* codec = PCM16LE|PCM8|XMA1|XMA2|MSADPCM|WMA
* channels = (integer)
* sample_rate = (integer)
* interleave = (integer)
* loop_start_sample = (integer)
* loop_end_sample = (integer)

## Advanced usage
Modified files can be loaded without the need of a mod folder if placed in the root of the mods directory. These files take priority over other mods.

# Notes
If the console is enabled in Reloaded you can see which files are accessed during gameplay. 
Unless you need this info, I'd recommend you to disable the console for performance reasons as it causes stuttering especially during movies.
