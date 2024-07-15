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

#rm -rf temp

(
    cd build
    jar -cMf OpenTabletDriver.UX.DarkTheme.zip *
)

echo ""
echo "Build complete"
echo ""