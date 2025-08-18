# first open unity and force upm to import all dependencies
$1 -batchmode -quit -nographics -projectPath .
# move package to assets folder because that's the only location Unity can export a package from
mv ./Packages ./Assets
$1 -noUpm -batchmode -quit -nographics -projectPath . -exportPackage Assets/Packages/VirtualizedScrollview ./VirtualizedScrollview.unitypackage
# move the package back
mv ./Assets/Packages ./

# remove .meta files generated from shuffling around the package
rm Packages/VirtualizedScrollview.meta
rm Packages/manifest.json.meta
rm Packages/packages-lock.json.meta

# reimport with Unity to clean up any stale import data e.g., EditorBuildSettings.asset
$1 -batchmode -quit -nographics -projectPath .

# ./export-unitypackage.sh "/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity"