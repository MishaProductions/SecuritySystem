#!/bin/bash
set -e

rm -rf MHSClientAvalonia/bin/
rm -rf MHSClientAvalonia/obj/
rm -rf MHSClientAvalonia.Android/bin/
rm -rf MHSClientAvalonia.Android/obj/
rm -rf MHSClientAvalonia.Browser/bin/
rm -rf MHSClientAvalonia.Browser/obj/
rm -rf MHSClientAvalonia.Desktop/bin/
rm -rf MHSClientAvalonia.Desktop/obj/
rm -rf SecuritySystem/www/client/linux64
rm -rf SecuritySystem/www/client/win64
rm -rf SecuritySystem/www/client/android

mkdir -p SecuritySystem/www/client/linux64
mkdir -p SecuritySystem/www/client/win64
mkdir -p SecuritySystem/www/client/android

echo building browser UI
dotnet publish MHSClientAvalonia.Browser
cp -r MHSClientAvalonia.Browser/bin/Release/net9.0-browser/publish/wwwroot/* SecuritySystem/www/modernclient/
cp SecuritySystem/www/modernclient/*.js SecuritySystem/www/modernclient/_framework/

echo building desktop client for linux
dotnet publish MHSClientAvalonia.Desktop --runtime linux-x64
cp -r MHSClientAvalonia.Desktop/bin/Release/net9.0/linux-x64/publish/MHSClientAvalonia.Desktop SecuritySystem/www/client/linux64/

#echo building desktop client for windows
#dotnet publish MHSClientAvalonia.Desktop --runtime win-x64
#cp -r MHSClientAvalonia.Desktop/bin/Release/net9.0/win-x64/publish/MHSClientAvalonia.Desktop.exe SecuritySystem/www/client/win64/

echo building desktop client for android
dotnet publish MHSClientAvalonia.Android
cp -r MHSClientAvalonia.Android/bin/Release/net9.0-android/publish/com.mikhailproductions.mhs-Signed.apk SecuritySystem/www/client/android/

echo building controller
dotnet publish SecuritySystem --runtime linux-arm

echo compilation finished
