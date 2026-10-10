using System.Diagnostics;

namespace Aeox.Core.Live;

public sealed record BackgroundAppDef(string Name, string[] Processes, bool CloseByDefault, string Note);

public sealed record RunningApp(BackgroundAppDef Def, long MemoryBytes, int Count);

public static class BackgroundApps
{
    public static IReadOnlyList<BackgroundAppDef> All { get; } = new List<BackgroundAppDef>
    {
        new("NZXT CAM", new[] { "NZXT CAM" }, true, "polls every sensor many times a second"),
        new("Armoury Crate", new[] { "ArmouryCrate", "ArmouryCrate.UserSessionHelper", "asus_framework" }, true, "asus rgb and fan app"),
        new("Corsair iCUE", new[] { "iCUE", "Corsair.Service.CpuIdRemote64" }, true, "rgb and sensor polling"),
        new("SignalRGB", new[] { "SignalRgb", "SignalRgbLauncher" }, true, "rgb effects use cpu and gpu"),
        new("OpenRGB", new[] { "OpenRGB" }, true, "rgb control"),
        new("MSI Center", new[] { "MSI.CentralServer", "MSI Center" }, true, "msi utilities"),
        new("Wallpaper Engine", new[] { "wallpaper32", "wallpaper64" }, true, "renders your desktop while you play"),
        new("Overwolf", new[] { "Overwolf", "OverwolfBrowser", "OverwolfHelper", "OverwolfHelper64" }, true, "overlay platform"),
        new("Microsoft Teams", new[] { "ms-teams", "Teams" }, true, "chat app"),
        new("OneDrive", new[] { "OneDrive" }, true, "syncs files in the background"),
        new("Dropbox", new[] { "Dropbox" }, true, "syncs files in the background"),
        new("Google Drive", new[] { "GoogleDriveFS" }, true, "syncs files in the background"),
        new("Adobe Creative Cloud", new[] { "Creative Cloud", "CCXProcess", "CCLibrary", "AdobeIPCBroker", "Adobe Desktop Service" }, true, "adobe background services"),
        new("Phone Link", new[] { "PhoneExperienceHost", "YourPhone" }, true, "windows · phone sync"),
        new("Widgets", new[] { "Widgets", "WidgetService" }, true, "windows · news widgets"),
        new("Cross Device", new[] { "CrossDeviceResume", "CrossDeviceService" }, true, "windows · continue apps from your phone"),
        new("Microsoft Store", new[] { "WinStore.App" }, true, "windows · store app"),
        new("Edge updater", new[] { "MicrosoftEdgeUpdate" }, true, "windows · checks for edge updates"),
        new("Google updater", new[] { "GoogleUpdate", "GoogleUpdater" }, true, "checks for chrome updates"),
        new("iCloud", new[] { "iCloudServices", "iCloudDrive", "iCloudPhotos", "iCloudCKKS" }, true, "syncs files and photos"),
        new("Skype", new[] { "Skype", "SkypeApp", "SkypeBackgroundHost" }, true, "chat app"),
        new("Xbox app", new[] { "XboxPcApp", "XboxPcAppFT", "XboxApp" }, false, "windows · keep it for game pass games"),
        new("Xbox Game Bar", new[] { "GameBar", "GameBarFTServer" }, false, "windows · keep it if you use its overlay or an x3d chip"),
        new("NVIDIA app", new[] { "NVIDIA app", "NVIDIA Overlay" }, false, "closing it stops instant replay and the overlay"),
        new("AMD Software", new[] { "RadeonSoftware", "AMDRSSrcExt" }, false, "closing it stops amd recording and the overlay"),
        new("Logitech G HUB", new[] { "lghub", "lghub_agent", "lghub_system_tray" }, false, "closing it can reset mouse dpi and lighting"),
        new("Razer Synapse", new[] { "RazerAppEngine", "Razer Synapse 3", "Razer Synapse Service Process" }, false, "closing it can reset mouse dpi"),
        new("SteelSeries GG", new[] { "SteelSeriesGG", "SteelSeriesEngine" }, false, "closing it can reset device settings"),
        new("Discord", new[] { "Discord" }, false, "keep it if you use voice chat"),
        new("Spotify", new[] { "Spotify" }, false, "music"),
        new("Medal", new[] { "medal" }, false, "clip recorder"),
        new("WhatsApp", new[] { "WhatsApp", "WhatsApp.Root" }, false, "chat app"),
        new("Google Chrome", new[] { "chrome" }, false, "browser, tabs keep running"),
        new("Microsoft Edge", new[] { "msedge" }, false, "browser, tabs keep running"),
        new("Firefox", new[] { "firefox" }, false, "browser, tabs keep running"),
        new("Opera", new[] { "opera" }, false, "browser, tabs keep running")
    };

    public static IReadOnlyList<RunningApp> Scan()
    {
        var session = Process.GetCurrentProcess().SessionId;
        var byName = Process.GetProcesses()
            .Where(p => p.SessionId == session)
            .GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var result = new List<RunningApp>();
        foreach (var def in All)
        {
            var procs = def.Processes.Where(byName.ContainsKey).SelectMany(n => byName[n]).ToList();
            if (procs.Count == 0) continue;
            long memory = 0;
            foreach (var p in procs)
            {
                try { memory += p.WorkingSet64; } catch (Exception) { }
            }
            result.Add(new RunningApp(def, memory, procs.Count));
        }
        return result.OrderByDescending(r => r.MemoryBytes).ToList();
    }

    public static async Task<int> CloseAsync(IEnumerable<BackgroundAppDef> apps)
    {
        var session = Process.GetCurrentProcess().SessionId;
        var names = apps.SelectMany(a => a.Processes).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targets = Process.GetProcesses().Where(p => p.SessionId == session && names.Contains(p.ProcessName)).ToList();
        foreach (var p in targets)
        {
            try { p.CloseMainWindow(); } catch (Exception) { }
        }
        await Task.Delay(2500);
        var closed = 0;
        foreach (var p in targets)
        {
            try
            {
                if (!p.HasExited) p.Kill();
                closed++;
            }
            catch (Exception)
            {
            }
            finally
            {
                p.Dispose();
            }
        }
        return closed;
    }
}
