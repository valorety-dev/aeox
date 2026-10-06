# aeox

FPS tweaker for Fortnite and Retrac. Windows 10/11, free.

It checks what costs you frames, shows the exact lines it's about to write before it writes them, and saves every original value so you can put everything back with one button.

## what's in it

- **checkup**: refresh rate, RAM speed, power plan, GPU assignment, optimizer leftovers, background apps, Wi-Fi
- **performance**: uncapped fps, vsync off, reflex, low presets, grass off, settings lock
- **visuals**: window mode, stretched res, render scale, performance mode (live fortnite)
- **system**: dedicated gpu, fullscreen optimizations, game mode, raw mouse, power plan
- **network**: pings your router and the match server so you know if it's your wi-fi
- **stats**: fps / stutter / ping per match, read from the game's own logs
- **tray**: puts your settings back if the game or a driver resets them

## what it doesn't do

No game file edits, nothing near the anti-cheat, no Engine.ini on live Fortnite, no registry placebo, no telemetry.

## build

Needs the .NET 9 SDK.

```
dotnet test tests/Aeox.Core.Tests
dotnet publish src/Aeox.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```

`dist/Aeox.exe` is the whole app.

## layout

- `src/Aeox.Core`: settings files, change engine, backups, checks, stats
- `src/Aeox.App`: the WPF app
- `tests/Aeox.Core.Tests`: apply/restore round trips on temp files
- `website`: aeox site, static

Aeox keeps its data in `%LOCALAPPDATA%\Aeox`. Delete that folder after a restore and it's gone.

Michroma font by the Michroma Project Authors, SIL Open Font License 1.1.

Not affiliated with Epic Games or Retrac.

made by valorety · [buy me a coffee](https://buymeacoffee.com/valorety)
