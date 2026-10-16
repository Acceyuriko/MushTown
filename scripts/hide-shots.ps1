# Hides game windows from outside. Baseline exes predate the in-app
# headless loop, so their shots would flash black windows.
# Use: run as a job around a baseline batch, then stop it.
#   $w = Start-Job -ScriptBlock { & scripts/hide-shots.ps1 180 }
#   ... baseline shots ...
#   Stop-Job $w; Remove-Job $w
param([int]$Seconds = 120, [string]$ProcessName = 'MushTown.Client')
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public static class WHide {
    public delegate bool EWP(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EWP f, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    public static HashSet<uint> Pids = new HashSet<uint>();
    public static bool HideOne(IntPtr h, IntPtr l) {
        uint p; GetWindowThreadProcessId(h, out p);
        if (Pids.Contains(p) && IsWindowVisible(h)) ShowWindow(h, 0);
        return true;
    }
    public static void Sweep() { EnumWindows(HideOne, IntPtr.Zero); }
}
'@
$end = (Get-Date).AddSeconds($Seconds)
while ((Get-Date) -lt $end) {
    [WHide]::Pids.Clear()
    foreach ($p in Get-Process -Name $ProcessName -ErrorAction SilentlyContinue) {
        [WHide]::Pids.Add([uint]$p.Id) | Out-Null
    }
    [WHide]::Sweep()
    Start-Sleep -Milliseconds 25
}
