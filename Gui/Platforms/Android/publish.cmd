cd ..\..
REM Use AndroidPackageFormat=aab for Play Store publishing, or apk for sideloading/testing
dotnet publish -f net9.0-android -c Release -p:AndroidPackageFormat=apk
PAUSE