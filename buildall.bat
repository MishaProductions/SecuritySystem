@echo off

md SecuritySystem\www\modernclient\
dotnet publish MHSClientAvalonia.Browser
xcopy /Y /e /k /h /i "MHSClientAvalonia.Browser\bin\Release\net9.0-browser\publish\wwwroot" "SecuritySystem\www\modernclient\"
REM xcopy /Y SecuritySystem\www\modernclient\*.js SecuritySystem\www\modernclient\_framework\

dotnet publish MHSClientAvalonia.Desktop --runtime linux-x64
xcopy /Y /e /k /h /i MHSClientAvalonia.Desktop\bin\Release\net9.0\linux-x64\publish\MHSClientAvalonia.Desktop SecuritySystem\www\client\linux64\

dotnet publish MHSClientAvalonia.Desktop --runtime win-x64
xcopy /Y /e /k /h /i MHSClientAvalonia.Desktop\bin\Release\net9.0-windows10.0.17763.0\publish\win-x64\MHSClientAvalonia.Desktop.exe SecuritySystem\www\client\win64\

REM dotnet publish MHSClientAvalonia.Android

dotnet publish SecuritySystem --runtime linux-arm
