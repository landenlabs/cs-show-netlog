@echo off
echo Publishing ShowNetLog as a portable, single-file executable...

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Success! Portable executable created in the 'publish' folder.
    explorer .\publish
) else (
    echo.
    echo Error: Publish failed.
)
pause
