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
The following instructions assume the default paths, however they can be changed in the config.

## PAC file replacement
Modified files are placed in a folder named after the PAC file inside the 'mods' folder located in the P4G installation directory by default. 

To replace title\logo.bin inside data00003.pac for example, you would place it at <p4g install path>\mods\data00003\title\logo.bin

## XWB file replacement
Modified files are placed in a folder named after the XWB file inside the 'mods\SND' folder located in the P4G installation directory by default.

Sound replacements consist out of a RAW file and a TXTH file. The RAW file contains the raw, unformatted audio data whilst the TXTH is a text based format that describes how the data should be loaded. 
TXTH is supported by vgmstream, and can be used by playing the RAW file (make sure the TXTH file name is <filename>.raw.txth). Use this to test if the audio plays correctly!

A tool such as [this one](https://github.com/jpmac26/P4G_PC_Music_Converter) can be used to create these files.

To replace wave index 20 (base 0, title screen bgm) inside BGM.XWB, you would place the RAW file at "<p4g install path>\mods\SND\BGM\20.raw", and the TXTH file at "<p4g install path>\mods\SND\BGM\20.raw.txth".

### Technical info
The RAW file contains the raw PCM data, which would be the contents of the WAV 'data' chunk.
The TXTH file is based on the format described [here](https://github.com/losnoco/vgmstream/blob/master/doc/TXTH.md) however the loader only implements the necessary subset of options, being:
* num_samples = (integer)
* codec = PCM16LE|PCM8|XMA1|XMA2|MSADPCM|WMA
* channels = (integer)
* sample_rate = (integer)
* interleave = (integer) (always 140 for MSADPCM)
* loop_start_sample = (integer)
* loop_end_sample = (integer)

# Notes
If the console is enabled in Reloaded you can see which files are accessed during gameplay. 
Unless you need this info, I'd recommend you to disable the console for performance reasons as it causes stuttering especially during movies.
