#!/usr/bin/env bash

dotnet publish OpenTabletDriver.UX.DarkTheme/OpenTabletDriver.UX.DarkTheme.csproj -c Debug -o temp

# Check if build folder exists
if [ -d "build" ]; then
    rm -rf build
fi

mkdir build

cp temp/OpenTabletDriver.UX.DarkTheme.dll build
cp temp/OpenTabletDriver.UX.DarkTheme.pdb build
cp temp/OpenTabletDriver.UX.Theming.dll build
cp temp/OpenTabletDriver.UX.Theming.pdb build
cp temp/ReactiveUI.dll build
cp temp/System.Reactive.dll build
cp temp/Splat.dll build

#rm -rf temp

(
    cd build
    jar -cMf OpenTabletDriver.UX.DarkTheme.zip *
)

echo ""
echo "Build complete"
echo ""