cd ..\..
dotnet publish -f net10.0-windows10.0.19041.0 -c Release --no-self-contained -p:RuntimeIdentifierOverride=win-x64 -p:PublishSingleFile=true -p:PublishReadyToRun=true
PAUSE