cd ..\..
dotnet publish -f net9.0-windows10.0.19041.0 -c Release --self-contained false -p:RuntimeIdentifierOverride=win-x64 -p:PublishSingleFile=true -p:PublishReadyToRun=true
PAUSE