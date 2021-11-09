# Project Output Paths
$modOutputPath = "TempBuild"
$solutionName = "modloader.csproj"
$publishName = "Mod.zip"
$publishDirectory = "Publish"

[Environment]::CurrentDirectory = $PWD

# Clean anything in existing Release directory.
Remove-Item $modOutputPath -Recurse
Remove-Item $publishDirectory -Recurse
New-Item $modOutputPath -ItemType Directory
New-Item $publishDirectory -ItemType Directory

# Build
dotnet restore $solutionName
dotnet clean $solutionName
dotnet publish $solutionName -c Release --self-contained false -o "$modOutputPath/"

# Cleanup Unnecessary Files
Get-ChildItem $modOutputPath -Include *.exe -Recurse | Remove-Item -Force -Recurse
Get-ChildItem $modOutputPath -Include *.pdb -Recurse | Remove-Item -Force -Recurse
Get-ChildItem $modOutputPath -Include *.xml -Recurse | Remove-Item -Force -Recurse

# Compress
Add-Type -A System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($modOutputPath, "$publishDirectory/$publishName")

# Cleanup After Build
Remove-Item $modOutputPath -Recurse
