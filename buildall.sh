#!/bin/bash
set -e

dotnet publish MHSClientAvalonia.Browser
cp -r MHSClientAvalonia.Browser/bin/Release/net9.0-browser/publish/wwwroot/* SecuritySystem/www/modernclient/
cp SecuritySystem/www/modernclient/*.js SecuritySystem/www/modernclient/_framework/

dotnet publish MHSClientAvalonia.Desktop --runtime linux-x64
cp -r MHSClientAvalonia.Desktop/bin/Release/net9.0/linux-x64/publish/MHSClientAvalonia.Desktop SecuritySystem/www/client/linux64/

dotnet publish MHSClientAvalonia.Desktop --runtime win-x64
cp -r MHSClientAvalonia.Desktop/bin/Release/net9.0/win-x64/publish/MHSClientAvalonia.Desktop.exe SecuritySystem/www/client/win64/

#dotnet publish MHSClientAvalonia.Android

dotnet publish SecuritySystem --runtime linux-arm
