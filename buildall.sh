#!/bin/bash
set -e

#rm -rf MHSClientAvalonia/bin/
#rm -rf MHSClientAvalonia/obj/
#rm -rf MHSClientAvalonia.Android/bin/
#rm -rf MHSClientAvalonia.Android/obj/
#rm -rf MHSClientAvalonia.Browser/bin/
#rm -rf MHSClientAvalonia.Browser/obj/
rm -rf MHSClientAvalonia.Desktop/bin/
rm -rf MHSClientAvalonia.Desktop/obj/

dotnet publish MHSClientAvalonia.Browser
cp -r MHSClientAvalonia.Browser/bin/Release/net9.0-browser/publish/wwwroot/* SecuritySystem/www/modernclient/
cp SecuritySystem/www/modernclient/*.js SecuritySystem/www/modernclient/_framework/

dotnet publish MHSClientAvalonia.Desktop --runtime linux-x64
cp -r MHSClientAvalonia.Desktop/bin/Release/net9.0/linux-x64/publish/MHSClientAvalonia.Desktop SecuritySystem/www/client/linux64/

dotnet publish MHSClientAvalonia.Desktop --runtime win-x64
cp -r MHSClientAvalonia.Desktop/bin/Release/net9.0/win-x64/publish/MHSClientAvalonia.Desktop.exe SecuritySystem/www/client/win64/

dotnet publish MHSClientAvalonia.Android
cp -r MHSClientAvalonia.Android/bin/Release/net9.0-android/publish/com.mikhailproductions.mhs-Signed.apk SecuritySystem/www/client/android/

dotnet publish SecuritySystem --runtime linux-arm
