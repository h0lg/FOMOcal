cd ..\..
REM Use maccatalyst-arm64 on Apple Silicon if preferred.
dotnet publish -f net9.0-maccatalyst -c Release -p:RuntimeIdentifier=maccatalyst-x64
PAUSE