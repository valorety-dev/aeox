# aeox

free fps tools for fortnite, valorant, cs2 and 20 other games. one app, windows 10/11.

## what's in it

tweaks - checks what costs you frames, shows the exact lines before it writes anything, and keeps every original value so one button puts it all back. fortnite (live, retrac and the other og projects), valorant, cs2, apex, cod, overwatch, the finals, marvel rivals, pubg and more. windows and internet tweaks picked by what your pc actually has.

live check - while you play it checks the game runs on the right gpu, on the x3d cache cores if you have them, and whether you're cpu or gpu bound.

background - scans what's running on your pc and lets you close what you don't need before a match.

driver - your gpu driver next to the newest one and how your fps, hitches and crashes looked on every driver you've played on.

scan - your pc as a 3d scene. the part holding your fps back lights up, worked out from frame times in your last matches.

aim - aim trainer with your real in-game sens, raw mouse input, gridshot, sixshot, spidershot, precision, tracking and more. builds a map from your weak spots. crosshair maker with valorant codes.

updates itself when a new release is out.

## what it won't do

no game file edits, nothing near the anti-cheat, no engine.ini on live fortnite, no registry placebo, no telemetry.

## build

.net 9 sdk.

```
dotnet test tests/Aeox.Core.Tests
dotnet publish src/Aeox.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -p:DebugType=none -o dist
```

scan and aim need the webview2 runtime, which windows 11 already has.

data lives in %LOCALAPPDATA%\Aeox. delete that folder after a restore and it's gone.
