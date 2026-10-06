# aeox

free fps tools for fortnite and retrac. windows 10/11.

## tools

aeox - the main one. checks what costs you frames, shows the exact lines before it writes anything, and keeps every original value so one button puts it all back. checkup, performance, visuals, system, network, stats per match, and a tray mode that re-applies your settings if the game or a driver resets them.

aeox driver - shows your installed gpu driver next to the newest one and how your fps, hitches and crashes looked on every driver you've played on (read from the game's own logs). release notes and downloads open nvidia's site, nothing gets installed for you.

aeox scan - your pc as a 3d scene. the part holding your fps back lights up, worked out from frame times in your last 10 matches, not from a parts list. save card makes a picture of your setup.

## what it won't do

no game file edits, nothing near the anti-cheat, no engine.ini on live fortnite, no registry placebo, no telemetry.

## build

.net 9 sdk.

```
dotnet test tests/Aeox.Core.Tests
dotnet publish src/Aeox.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```

same for src/Aeox.Driver and src/Aeox.Scan. scan needs the webview2 runtime, which windows 11 already has.

data lives in %LOCALAPPDATA%\Aeox. delete that folder after a restore and it's gone.
