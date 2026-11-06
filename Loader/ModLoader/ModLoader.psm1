# Installed Mod-Selector version. The self-updater compares this against the
# latest GitHub release tag. Bump it whenever the shipped files change.
$script:SELECTOR_VERSION = '1.5.1'

if (-not ([System.Management.Automation.PSTypeName]'WinHelper').Type) {
    $winHelperDef = @'
using System;
using System.Text;
using System.Runtime.InteropServices;

public class WinHelper {
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public const int SW_RESTORE = 9;
    public const byte VK_CONTROL = 0x11;
    public const byte VK_MENU    = 0x12; // Alt
    public const byte VK_S       = 0x53; // 'S'
    public const uint KEYEVENTF_KEYUP = 0x0002;

    public static IntPtr FindDeltaruneWindow() {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, lParam) => {
            if (!IsWindowVisible(hWnd)) return true;
            StringBuilder sbTitle = new StringBuilder(256);
            GetWindowText(hWnd, sbTitle, 256);
            string title = sbTitle.ToString();

            StringBuilder sbClass = new StringBuilder(256);
            GetClassName(hWnd, sbClass, 256);
            string cls = sbClass.ToString();

            if (cls.Contains("YYGameMakerYY") || title.IndexOf("DELTARUNE", StringComparison.OrdinalIgnoreCase) >= 0) {
                found = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static bool TriggerLssHotkey(IntPtr hWnd) {
        if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd)) {
            return false;
        }
        ShowWindow(hWnd, SW_RESTORE);
        SetForegroundWindow(hWnd);
        System.Threading.Thread.Sleep(250);
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
        keybd_event(VK_S, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(50);
        keybd_event(VK_S, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        return true;
    }
}
'@
    Add-Type -TypeDefinition $winHelperDef -ErrorAction SilentlyContinue
}

function Get-ModLoaderConfig {
    param([Parameter(Mandatory)][string]$GameDir)
    $signal = Join-Path $env:LOCALAPPDATA "DELTARUNE"
    if (-not (Test-Path $signal)) { New-Item -ItemType Directory -Path $signal -Force | Out-Null }
    $vanilla = Join-Path $GameDir "_vanilla"
    if (-not (Test-Path $vanilla)) { New-Item -ItemType Directory -Path $vanilla -Force | Out-Null }
    [ordered]@{
        GameDir          = $GameDir
        ModsDir          = Join-Path $GameDir "mods"
        VanillaDir       = $vanilla
        SignalDir        = $signal
        ManifestPath     = Join-Path $vanilla "applied.json"
        # --- Save-Profile support -------------------------------------------
        # DELTARUNE reads/writes its saves (filechN_*) directly from SaveDir
        # (== SignalDir == %LOCALAPPDATA%\DELTARUNE). Profiles are named copies
        # of a chapter's save files; the loader swaps them in before launch and
        # persists them back + restores the user's real saves afterwards.
        SaveDir          = $signal
        ProfilesDir      = Join-Path $GameDir "_saveprofiles"      # per-chapter subfolders: ch<N>\<Name>\
        SaveBackupDir    = Join-Path $GameDir "_savebackup"        # user's real saves parked here while a profile is active
        ProfileListPath  = Join-Path $signal "profiles.txt"        # written by loader; read by the in-game menu
        ThesiUnlockedPath= Join-Path $signal "thesi_unlocked.txt"  # "1"/"0"; unlocked via secret code in menu
        ThesiOverlayPath = Join-Path $signal "thesi_overlay.txt"   # "1"/"0"; toggled in settings menu
        ThesiCacheDir    = Join-Path $GameDir "_thesicache"        # cached patched mods for instant loading
        ThesiSpritesDir  = Join-Path (Join-Path $GameDir "_tools") "ThesiSprites"
        # --- 60 FPS Overhaul & Framerate Mode support ------------------------
        Fps60PatchPath   = Join-Path $signal "fps60_patch.txt"     # legacy "1"/"0"; synced with fps_mode.txt
        FramerateModePath= Join-Path $signal "fps_mode.txt"        # "lossless"/"engine"/"off"
        LsfgFpsPath      = Join-Path $signal "lsfg_fps.txt"        # "60"/"90"/"120"/"144"/"240"
        LsfgModePath     = Join-Path $signal "lsfg_mode.txt"       # "balanced"/"performance"
        LsfgFlowPath     = Join-Path $signal "lsfg_flow.txt"       # "100"/"75"/"50"
        LssPromptedPath  = Join-Path $signal "lss_prompted.txt"    # "1" once first-run question has been answered
        Fps60CacheDir    = Join-Path $GameDir "_fpscache"          # pre-patched 60 FPS data for instant loading
        Fps60PatchesDir  = Join-Path (Join-Path $GameDir "_tools") "60fps_patches"
        # --- German Translation Patch support --------------------------------
        GermanPatchPath  = Join-Path $signal "german_patch.txt"    # "1"/"0"; toggled in settings menu
        GermanCacheDir   = Join-Path $GameDir "_germancache"       # pre-patched German data for instant loading
        GermanPatchesDir = Join-Path (Join-Path $GameDir "_tools") "GermanPatches"
        # --- Mod Menu Language support --------------------------------------
        MenuLangPath     = Join-Path $signal "menu_lang.txt"       # "en"/"de"; toggled in settings menu
        # --- Self-update / auto-repatch support -----------------------------
        DataWin          = Join-Path $GameDir "data.win"
        DataWinBackup    = Join-Path $GameDir "data.win.BACKUP"    # clean vanilla the menu was patched from
        ToolsDir         = Join-Path $GameDir "_tools"
        UtmtDir          = Join-Path (Join-Path $GameDir "_tools") "UTMT_CLI"   # cached UndertaleModTool CLI
        InjectorPath     = Join-Path $GameDir "ModLoader\InjectModMenu.csx"     # local copy used to re-patch
        # Auto-Update state as plain-text flag files so the in-game menu (GML,
        # which resolves bare filenames to this same folder) can toggle it too.
        AutoUpdatePath   = Join-Path $signal "autoupdate.txt"   # "1"/"0"; toggled in-menu with [U]
        LastCheckPath    = Join-Path $signal "lastcheck.txt"    # ISO datetime of last GitHub check
        RepoOwner        = "Saloran26"
        RepoName         = "Deltarune-Mod-Selector"
        PatchMarker      = "SELECT SAVE PROFILE"                                # present iff data.win is patched
        UtmtUrl          = "https://github.com/UnderminersTeam/UndertaleModTool/releases/download/0.9.1.2/UTMT_CLI_v0.9.1.2-Windows.zip"
        # --- Mod Importer & Update support -----------------------------------
        ModsImportDir          = Join-Path $GameDir "mods\_import"
        ModsImportProcessedDir = Join-Path $GameDir "mods\_import\_processed"
        ModUpdatesPath         = Join-Path $signal "mod_updates.txt"
    }
}

function Backup-FileIfNeeded {
    param([Parameter(Mandatory)]$cfg, [Parameter(Mandatory)][string]$RelPath)
    $src = Join-Path $cfg.GameDir $RelPath
    $dst = Join-Path $cfg.VanillaDir $RelPath
    if (Test-Path $dst) { return $true }          # bereits gesichert -> existierte im Original
    if (-not (Test-Path $src)) { return $false }  # existiert im Original nicht
    $dstDir = Split-Path $dst -Parent
    if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
    Copy-Item -LiteralPath $src -Destination $dst -Force
    return $true
}

function Get-ModMetadata {
    param([Parameter(Mandatory)][string]$ModPath)
    $jsonPath = Join-Path $ModPath "mod.json"
    if (Test-Path $jsonPath) {
        try {
            return (Get-Content -LiteralPath $jsonPath -Raw -Encoding utf8 | ConvertFrom-Json)
        } catch { return $null }
    }
    return $null
}

function Set-ModMetadata {
    param([Parameter(Mandatory)][string]$ModPath, [Parameter(Mandatory)]$Metadata)
    $jsonPath = Join-Path $ModPath "mod.json"
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $json = $Metadata | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($jsonPath, $json, $utf8)
}

function Get-InstalledMods {
    param([Parameter(Mandatory)]$cfg)
    if (-not (Test-Path $cfg.ModsDir)) { return @() }
    $result = @()
    foreach ($dir in Get-ChildItem -LiteralPath $cfg.ModsDir -Directory) {
        if ($dir.Name.StartsWith('_')) { continue } # skip _import, _processed etc.
        $files = Get-ChildItem -LiteralPath $dir.FullName -Recurse -File
        $rel = @()
        foreach ($f in $files) {
            $r = $f.FullName.Substring($dir.FullName.Length).TrimStart('\','/')
            if ($r -eq 'mod.json' -or $r -like '*.md' -or ($r -like '*.txt' -and $r -notmatch '(?i)lang')) { continue }
            $rel += $r
        }
        $chapter = 0
        foreach ($r in $rel) {
            if ($r -match '(?i)(^|[\\/])chapter([0-9]+)_windows[\\/]') { $chapter = [int]$Matches[2]; break }
        }
        $meta = Get-ModMetadata $dir.FullName
        $result += [pscustomobject]@{
            Name     = $dir.Name
            Chapter  = $chapter
            Path     = $dir.FullName
            Files    = $rel
            Metadata = $meta
        }
    }
    return $result
}

function Write-ModList {
    param([Parameter(Mandatory)]$cfg, [Parameter(Mandatory)][AllowEmptyCollection()][array]$mods)
    $lines = $mods | Where-Object { $_.Chapter -gt 0 } |
        Sort-Object Chapter, Name | ForEach-Object { "$($_.Chapter)|$($_.Name)" }
    $path = Join-Path $cfg.SignalDir "modlist.txt"
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, (($lines -join "`n") + "`n"), $utf8)
}

# ============================================================================
#  Save-Profile handling
# ============================================================================
#  A "profile" is a named copy of ONE chapter's save files (filech<N>_*),
#  stored in _saveprofiles\ch<N>\<Name>\. "Standard-Saves" (empty profile name)
#  means: play on the user's real saves, no swapping.
#
#  While a profile is active, the user's real chapter saves are parked in
#  _savebackup\ together with a pending.json marker { chapter, profile }. The
#  marker doubles as crash-recovery: if the loader dies mid-session, the next
#  launch finds pending.json and safely persists progress + restores the real
#  saves via Swap-OutProfile.

function Get-ChapterSaveFiles {
    param([Parameter(Mandatory)]$cfg, [Parameter(Mandatory)][int]$Chapter)
    if ($Chapter -le 0) { return @() }
    if (-not (Test-Path $cfg.SaveDir)) { return @() }
    @(Get-ChildItem -LiteralPath $cfg.SaveDir -File -Filter "filech${Chapter}_*" -ErrorAction SilentlyContinue)
}

function Write-ProfileList {
    param([Parameter(Mandatory)]$cfg)
    $lines = @()
    if (Test-Path $cfg.ProfilesDir) {
        foreach ($chDir in (Get-ChildItem -LiteralPath $cfg.ProfilesDir -Directory -ErrorAction SilentlyContinue)) {
            if ($chDir.Name -notmatch '^ch([0-9]+)$') { continue }
            $ch = [int]$Matches[1]
            foreach ($pDir in (Get-ChildItem -LiteralPath $chDir.FullName -Directory -ErrorAction SilentlyContinue)) {
                $lines += "$ch|$($pDir.Name)"
            }
        }
    }
    $lines = $lines | Sort-Object
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($cfg.ProfileListPath, (($lines -join "`n") + "`n"), $utf8)
}

function Get-ProfileDir {
    param([Parameter(Mandatory)]$cfg, [Parameter(Mandatory)][int]$Chapter, [Parameter(Mandatory)][string]$Profile)
    Join-Path (Join-Path $cfg.ProfilesDir "ch$Chapter") $Profile
}

function Swap-InProfile {
    param([Parameter(Mandatory)]$cfg, [Parameter(Mandatory)][int]$Chapter, [string]$Profile = "")
    if ($Chapter -le 0 -or [string]::IsNullOrWhiteSpace($Profile)) { return }
    # 1) Park the user's real chapter saves + write the recovery marker.
    if (Test-Path $cfg.SaveBackupDir) { Remove-Item -LiteralPath $cfg.SaveBackupDir -Recurse -Force }
    New-Item -ItemType Directory -Path $cfg.SaveBackupDir -Force | Out-Null
    foreach ($f in (Get-ChapterSaveFiles $cfg $Chapter)) {
        Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $cfg.SaveBackupDir $f.Name) -Force
    }
    [pscustomobject]@{ chapter = $Chapter; profile = $Profile } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $cfg.SaveBackupDir "pending.json") -Encoding utf8
    # 2) Clear the live chapter saves.
    foreach ($f in (Get-ChapterSaveFiles $cfg $Chapter)) { Remove-Item -LiteralPath $f.FullName -Force }
    # 3) Copy the profile's saves into place (an empty/new profile => fresh start).
    $pdir = Get-ProfileDir $cfg $Chapter $Profile
    if (Test-Path $pdir) {
        foreach ($f in (Get-ChildItem -LiteralPath $pdir -File -Filter "filech${Chapter}_*" -ErrorAction SilentlyContinue)) {
            Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $cfg.SaveDir $f.Name) -Force
        }
    } else {
        New-Item -ItemType Directory -Path $pdir -Force | Out-Null
    }
}

function Swap-OutProfile {
    param([Parameter(Mandatory)]$cfg)
    $marker = Join-Path $cfg.SaveBackupDir "pending.json"
    if (-not (Test-Path $marker)) { return }   # no profile active -> nothing to do
    $info    = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    $ch      = [int]$info.chapter
    $profile = [string]$info.profile
    $pdir    = Get-ProfileDir $cfg $ch $profile
    if (-not (Test-Path $pdir)) { New-Item -ItemType Directory -Path $pdir -Force | Out-Null }
    # 1) Persist the just-played saves back into the profile (replace old copy).
    foreach ($old in (Get-ChildItem -LiteralPath $pdir -File -Filter "filech${ch}_*" -ErrorAction SilentlyContinue)) {
        Remove-Item -LiteralPath $old.FullName -Force
    }
    foreach ($f in (Get-ChapterSaveFiles $cfg $ch)) {
        Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $pdir $f.Name) -Force
    }
    # 2) Clear the live chapter saves, then restore the user's real saves.
    foreach ($f in (Get-ChapterSaveFiles $cfg $ch)) { Remove-Item -LiteralPath $f.FullName -Force }
    foreach ($b in (Get-ChildItem -LiteralPath $cfg.SaveBackupDir -File -Filter "filech${ch}_*" -ErrorAction SilentlyContinue)) {
        Copy-Item -LiteralPath $b.FullName -Destination (Join-Path $cfg.SaveDir $b.Name) -Force
    }
    Remove-Item -LiteralPath $cfg.SaveBackupDir -Recurse -Force
}

function New-Profile {
    param([Parameter(Mandatory)]$cfg, [Parameter(Mandatory)][int]$Chapter, [string]$Name = "")
    if ($Chapter -le 0 -or [string]::IsNullOrWhiteSpace($Name)) { return }
    $pdir = Get-ProfileDir $cfg $Chapter $Name
    if (-not (Test-Path $pdir)) { New-Item -ItemType Directory -Path $pdir -Force | Out-Null }
    Write-ProfileList $cfg
}

function Remove-Profile {
    param([Parameter(Mandatory)]$cfg, [Parameter(Mandatory)][int]$Chapter, [string]$Name = "")
    if ($Chapter -le 0 -or [string]::IsNullOrWhiteSpace($Name)) { return }
    $pdir = Get-ProfileDir $cfg $Chapter $Name
    if (Test-Path $pdir) { Remove-Item -LiteralPath $pdir -Recurse -Force }
    Write-ProfileList $cfg
}

function Read-ProfileCmd {
    # In-game profile management: the menu writes "action|N|Name" (create/delete).
    param([Parameter(Mandatory)]$cfg)
    $p = Join-Path $cfg.SignalDir "profile_cmd.txt"
    if (-not (Test-Path $p)) { return $null }
    $line = (Get-Content $p -Raw -Encoding UTF8).Trim()
    Remove-Item $p -Force
    $parts = $line.Split('|', 3)
    if ($parts.Count -lt 3) { return $null }
    [pscustomobject]@{ Action = $parts[0].Trim(); Chapter = [int]$parts[1]; Name = $parts[2].Trim() }
}

function Set-ProfileReady {
    param([Parameter(Mandatory)]$cfg)
    Set-Content (Join-Path $cfg.SignalDir "profile_ready.txt") "ok" -Encoding utf8
}

function Read-ModToolsCmd {
    param([Parameter(Mandatory)]$cfg)
    $p = Join-Path $cfg.SignalDir "mod_tools_cmd.txt"
    if (-not (Test-Path $p)) { return $null }
    $line = (Get-Content $p -Raw -Encoding UTF8).Trim()
    Remove-Item $p -Force
    return $line
}

function Set-ModToolsResult {
    param(
        [Parameter(Mandatory)]$cfg,
        [string]$Line1,
        [string]$Line2 = "",
        [string]$Line3 = ""
    )
    $content = "$Line1`r`n$Line2`r`n$Line3"
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Join-Path $cfg.SignalDir "mod_tools_result.txt"), $content, $utf8)
    [System.IO.File]::WriteAllText((Join-Path $cfg.SignalDir "mod_tools_ready.txt"), "ok", $utf8)
}

function Restore-Vanilla {
    param([Parameter(Mandatory)]$cfg)
    if (-not (Test-Path $cfg.ManifestPath)) {
        return
    }
    try {
        $entries = Get-Content $cfg.ManifestPath -Raw | ConvertFrom-Json
        foreach ($e in $entries) {
            $target = Join-Path $cfg.GameDir $e.path
            if ($e.existedInVanilla) {
                $backup = Join-Path $cfg.VanillaDir $e.path
                if (Test-Path $backup) {
                    try { Copy-Item -LiteralPath $backup -Destination $target -Force -ErrorAction SilentlyContinue } catch {}
                }
            } else {
                if (Test-Path $target) {
                    try { Remove-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue } catch {}
                }
            }
        }
    } catch {}
    finally {
        try { Remove-Item $cfg.ManifestPath -Force -ErrorAction SilentlyContinue } catch {}
    }
}

function Apply-Mod {
    param([Parameter(Mandatory)]$cfg, [Parameter(Mandatory)]$mod)
    Restore-Vanilla $cfg
    # Phase 1: back up originals and record the FULL manifest BEFORE copying,
    # so a mid-copy failure is still fully recoverable on the next launch.
    $manifest = @()
    foreach ($rel in $mod.Files) {
        $existed = Backup-FileIfNeeded $cfg $rel
        $manifest += [pscustomobject]@{ path = $rel; existedInVanilla = $existed }
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $cfg.ManifestPath -Encoding utf8
    # Phase 2: copy mod files into place.
    foreach ($rel in $mod.Files) {
        $src = Join-Path $mod.Path $rel
        $dst = Join-Path $cfg.GameDir $rel
        $dstDir = Split-Path $dst -Parent
        if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
        Copy-Item -LiteralPath $src -Destination $dst -Force
    }
}

function Clear-Signals {
    param([Parameter(Mandatory)]$cfg)
    foreach ($n in "mod_request.txt","mod_ready.txt","profile_cmd.txt","profile_ready.txt","mod_tools_cmd.txt","mod_tools_ready.txt","mod_tools_result.txt") {
        $p = Join-Path $cfg.SignalDir $n
        if (Test-Path $p) { Remove-Item $p -Force }
    }
}

function Read-Request {
    param([Parameter(Mandatory)]$cfg)
    $p = Join-Path $cfg.SignalDir "mod_request.txt"
    if (-not (Test-Path $p)) { return $null }
    $line = (Get-Content $p -Raw -Encoding UTF8).Trim()
    Remove-Item $p -Force
    # Format: "N|ModName" (legacy) or "N|ModName|ProfileName" (empty profile = Standard saves).
    $parts = $line.Split('|', 3)
    if ($parts.Count -lt 2) { return $null }
    $profile = ""
    if ($parts.Count -ge 3) { $profile = $parts[2].Trim() }
    [pscustomobject]@{ Chapter = [int]$parts[0]; Name = $parts[1]; Profile = $profile }
}

function Set-Ready {
    param([Parameter(Mandatory)]$cfg)
    Set-Content (Join-Path $cfg.SignalDir "mod_ready.txt") "ok" -Encoding utf8
}

function Get-ThesiUnlockedSetting {
    param([Parameter(Mandatory)]$cfg)
    if (-not (Test-Path $cfg.ThesiUnlockedPath)) { return $false }
    try {
        return ((Get-Content -LiteralPath $cfg.ThesiUnlockedPath -Raw).Trim() -eq '1')
    } catch { return $false }
}

function Get-ThesiOverlaySetting {
    param([Parameter(Mandatory)]$cfg)
    if (-not (Get-ThesiUnlockedSetting $cfg)) { return $false }
    if (-not (Test-Path $cfg.ThesiOverlayPath)) { return $false }
    try {
        return ((Get-Content -LiteralPath $cfg.ThesiOverlayPath -Raw).Trim() -eq '1')
    } catch { return $false }
}

function Set-ThesiOverlaySetting {
    param([Parameter(Mandatory)]$cfg, [bool]$Enabled)
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try { [System.IO.File]::WriteAllText($cfg.ThesiOverlayPath, ($(if ($Enabled) { '1' } else { '0' })), $utf8) } catch {}
}

function Apply-ThesiOverlay {
    param(
        [Parameter(Mandatory)]$cfg,
        [Parameter(Mandatory)][int]$Chapter,
        $Mod = $null,
        [switch]$IsFps60
    )
    if (-not (Get-ThesiOverlaySetting $cfg)) { return }
    if ($Chapter -le 0) { return }

    # If the chosen mod is already Thesi Kris, it already has the textures!
    if ($Mod -and $Mod.Name -like "*Thesi Kris*") { return }

    $chFolder = "chapter${Chapter}_windows"
    $liveWin = Join-Path (Join-Path $cfg.GameDir $chFolder) "data.win"
    if (-not (Test-Path $liveWin)) { return }

    if (-not (Test-Path $cfg.ThesiCacheDir)) { New-Item -ItemType Directory -Path $cfg.ThesiCacheDir -Force | Out-Null }
    $modKey = if ($IsFps60) { "fps60" } elseif ($Mod) { $Mod.Name -replace '[^a-zA-Z0-9_-]', '_' } else { "vanilla" }
    $cacheDir = Join-Path $cfg.ThesiCacheDir "${modKey}_ch${Chapter}"
    $cachedWin = Join-Path $cacheDir "data.win"

    # 1) If cached version exists and is valid, copy instantly:
    if (Test-Path $cachedWin) {
        $sourceWin = if ($IsFps60) {
            Join-Path (Join-Path $cfg.Fps60CacheDir $chFolder) "data.win"
        } elseif ($Mod) {
            Join-Path (Join-Path $Mod.Path $chFolder) "data.win"
        } else {
            Join-Path (Join-Path $cfg.VanillaDir $chFolder) "data.win"
        }
        $cacheValid = $true
        if ($sourceWin -and (Test-Path $sourceWin)) {
            if ((Get-Item $sourceWin).LastWriteTimeUtc -gt (Get-Item $cachedWin).LastWriteTimeUtc) {
                $cacheValid = $false
            }
        }
        if ($cacheValid) {
            Copy-Item -LiteralPath $cachedWin -Destination $liveWin -Force
            return
        }
    }

    # 2) If not cached, patch liveWin with Thesi sprites and cache it:
    $cli = Get-UtmtCli $cfg -AllowDownload
    $importer = Join-Path $cfg.ToolsDir "ImportGraphicsCli.csx"
    $chSprites = Join-Path $cfg.ThesiSpritesDir "ch${Chapter}"
    $spritesDir = if (Test-Path $chSprites) { $chSprites } else { Join-Path $cfg.ThesiSpritesDir "Sprites" }
    if (-not $cli -or -not (Test-Path $importer) -or -not (Test-Path $spritesDir)) { return }

    if (-not (Test-Path $cacheDir)) { New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null }
    $tmpWin = Join-Path $cacheDir "patch_temp.win"
    if (Test-Path $tmpWin) { Remove-Item -LiteralPath $tmpWin -Force }

    $oldEnv = $env:IMPORT_FOLDER
    $env:IMPORT_FOLDER = $spritesDir
    try {
        cmd /c "echo. | `"$cli`" load `"$liveWin`" -s `"$importer`" -o `"$tmpWin`"" | Out-Null
    } catch {}
    finally {
        $env:IMPORT_FOLDER = $oldEnv
    }

    if (Test-Path $tmpWin) {
        Move-Item -LiteralPath $tmpWin -Destination $cachedWin -Force
        Copy-Item -LiteralPath $cachedWin -Destination $liveWin -Force
    }
}

function Get-FramerateModeSetting {
    param([Parameter(Mandatory)]$cfg)
    if (Test-Path $cfg.FramerateModePath) {
        try {
            $val = (Get-Content -LiteralPath $cfg.FramerateModePath -Raw).Trim().ToLower()
            if ($val -in @('lossless', 'engine', 'off')) { return $val }
        } catch {}
    }
    # Fallback to legacy fps60_patch.txt
    if (Test-Path $cfg.Fps60PatchPath) {
        try {
            if ((Get-Content -LiteralPath $cfg.Fps60PatchPath -Raw).Trim() -eq '1') {
                return 'engine'
            }
        } catch {}
    }
    return 'off'
}

function Set-FramerateModeSetting {
    param([Parameter(Mandatory)]$cfg, [string]$Mode = 'off')
    $Mode = $Mode.ToLower()
    if ($Mode -notin @('lossless', 'engine', 'off')) { $Mode = 'off' }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try { [System.IO.File]::WriteAllText($cfg.FramerateModePath, $Mode, $utf8) } catch {}
    # Keep legacy fps60_patch.txt in sync
    try { [System.IO.File]::WriteAllText($cfg.Fps60PatchPath, ($(if ($Mode -eq 'engine') { '1' } else { '0' })), $utf8) } catch {}
}

function Get-LsfgFpsSetting {
    param([Parameter(Mandatory)]$cfg)
    if (Test-Path $cfg.LsfgFpsPath) {
        try {
            $val = (Get-Content -LiteralPath $cfg.LsfgFpsPath -Raw).Trim()
            if ($val -in @('60', '90', '120', '144', '240')) { return $val }
        } catch {}
    }
    return '60'
}

function Set-LsfgFpsSetting {
    param([Parameter(Mandatory)]$cfg, [string]$Fps = '60')
    if ($Fps -notin @('60', '90', '120', '144', '240')) { $Fps = '60' }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try { [System.IO.File]::WriteAllText($cfg.LsfgFpsPath, $Fps, $utf8) } catch {}
}

function Get-LsfgModeSetting {
    param([Parameter(Mandatory)]$cfg)
    if (Test-Path $cfg.LsfgModePath) {
        try {
            $val = (Get-Content -LiteralPath $cfg.LsfgModePath -Raw).Trim().ToLower()
            if ($val -in @('balanced', 'performance')) { return $val }
        } catch {}
    }
    return 'balanced'
}

function Set-LsfgModeSetting {
    param([Parameter(Mandatory)]$cfg, [string]$Mode = 'balanced')
    $Mode = $Mode.ToLower()
    if ($Mode -notin @('balanced', 'performance')) { $Mode = 'balanced' }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try { [System.IO.File]::WriteAllText($cfg.LsfgModePath, $Mode, $utf8) } catch {}
}

function Get-LsfgFlowSetting {
    param([Parameter(Mandatory)]$cfg)
    if (Test-Path $cfg.LsfgFlowPath) {
        try {
            $val = (Get-Content -LiteralPath $cfg.LsfgFlowPath -Raw).Trim()
            if ($val -in @('100', '75', '50')) { return $val }
        } catch {}
    }
    return '100'
}

function Set-LsfgFlowSetting {
    param([Parameter(Mandatory)]$cfg, [string]$Flow = '100')
    if ($Flow -notin @('100', '75', '50')) { $Flow = '100' }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try { [System.IO.File]::WriteAllText($cfg.LsfgFlowPath, $Flow, $utf8) } catch {}
}

function Get-Fps60PatchSetting {
    param([Parameter(Mandatory)]$cfg)
    return (Get-FramerateModeSetting $cfg) -eq 'engine'
}

function Set-Fps60PatchSetting {
    param([Parameter(Mandatory)]$cfg, [bool]$Enabled)
    Set-FramerateModeSetting $cfg -Mode ($(if ($Enabled) { 'engine' } else { 'off' }))
}

function Find-LosslessScalingExe {
    param($cfg = $null)
    # 1. Check running process
    $p = Get-Process -Name "*LosslessScaling*" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p -and $p.Path -and (Test-Path $p.Path)) { return $p.Path }

    # 2. Check well-known default Steam locations
    $candidates = @(
        "C:\Program Files (x86)\Steam\steamapps\common\Lossless Scaling\LosslessScaling.exe",
        "C:\Program Files\Steam\steamapps\common\Lossless Scaling\LosslessScaling.exe"
    )
    if ($cfg -and $cfg.ToolsDir) {
        $candidates += Join-Path $cfg.ToolsDir "Lossless Scaling\LosslessScaling.exe"
    }
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }

    # 3. Parse Steam libraryfolders.vdf
    $vdfPaths = @(
        "C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf",
        "C:\Program Files\Steam\steamapps\libraryfolders.vdf"
    )
    foreach ($vdf in $vdfPaths) {
        if (Test-Path $vdf) {
            try {
                $content = Get-Content -LiteralPath $vdf -Raw -ErrorAction SilentlyContinue
                $matches = [regex]::Matches($content, '"path"\s+"([^"]+)"')
                foreach ($m in $matches) {
                    $lib = $m.Groups[1].Value.Replace('\\', '\')
                    $candidate = Join-Path $lib "steamapps\common\Lossless Scaling\LosslessScaling.exe"
                    if (Test-Path $candidate) { return $candidate }
                }
            } catch {}
        }
    }
    return $null
}

function Set-LosslessScalingProfile {
    param(
        [Parameter(Mandatory)]$cfg,
        [string]$TargetFps = $null,
        [string]$Mode = $null,
        [string]$Flow = $null
    )
    try {
        if (-not $TargetFps) { $TargetFps = Get-LsfgFpsSetting $cfg }
        if (-not $Mode)      { $Mode = Get-LsfgModeSetting $cfg }
        if (-not $Flow)      { $Flow = Get-LsfgFlowSetting $cfg }

        $lssDir = Join-Path $env:LOCALAPPDATA "Lossless Scaling"
        if (-not (Test-Path $lssDir)) { New-Item -ItemType Directory -Path $lssDir -Force | Out-Null }
        $lssConfig = Join-Path $lssDir "Settings.xml"
        $xml = $null
        if (Test-Path $lssConfig) {
            try {
                [xml]$xml = Get-Content -LiteralPath $lssConfig -Raw
            } catch {
                $xml = $null
            }
        }
        if (-not $xml -or -not $xml.Settings) {
            [xml]$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<Settings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <MinimizeToTray>true</MinimizeToTray>
  <CloseToTray>true</CloseToTray>
  <GameProfiles />
</Settings>
"@
        }

        $settingsNode = $xml.Settings

        function _Set-LssSubnode([System.Xml.XmlElement]$parent, [string]$name, [string]$val) {
            $sub = $parent[$name]
            if (-not $sub) {
                $sub = $parent.OwnerDocument.CreateElement($name)
                [void]$parent.AppendChild($sub)
            }
            $sub.InnerText = $val
        }

        _Set-LssSubnode $settingsNode "MinimizeToTray" "true"
        _Set-LssSubnode $settingsNode "CloseToTray" "true"
        _Set-LssSubnode $settingsNode "Hotkey" "S"
        _Set-LssSubnode $settingsNode "HotkeyModifierKeys" "Alt Control"

        $profilesNode = $settingsNode.GameProfiles
        if (-not $profilesNode) {
            $profilesNode = $xml.CreateElement("GameProfiles")
            [void]$settingsNode.AppendChild($profilesNode)
        }

        # Crucial: Disable FrameGeneration and AutoScale on "Standard" profile so LSS NEVER hooks any other app!
        $standardProfile = $profilesNode.Profile | Where-Object { $_.Title -eq "Standard" } | Select-Object -First 1
        if ($standardProfile) {
            _Set-LssSubnode $standardProfile "AutoScale" "false"
            _Set-LssSubnode $standardProfile "AutoScaleDelay" "0"
            _Set-LssSubnode $standardProfile "FrameGeneration" "Off"
            _Set-LssSubnode $standardProfile "ScalingType" "Off"
        }

        $deltaruneProfile = $profilesNode.Profile | Where-Object { $_.Title -eq "DELTARUNE" -or $_.Path -like "*DELTARUNE*" -or $_.Filter -eq "DELTARUNE.exe" } | Select-Object -First 1
        if (-not $deltaruneProfile) {
            $deltaruneProfile = $xml.CreateElement("Profile")
            [void]$profilesNode.AppendChild($deltaruneProfile)
        }

        _Set-LssSubnode $deltaruneProfile "Title" "DELTARUNE"
        _Set-LssSubnode $deltaruneProfile "Path" "DELTARUNE.exe;*DELTARUNE*"
        # We hook deterministically via hotkey once DELTARUNE window is in foreground -> AutoScale must be false!
        _Set-LssSubnode $deltaruneProfile "AutoScale" "false"
        _Set-LssSubnode $deltaruneProfile "AutoScaleDelay" "0"
        _Set-LssSubnode $deltaruneProfile "ScalingMode" "Auto"
        _Set-LssSubnode $deltaruneProfile "ScalingFitMode" "AspectRatio"
        _Set-LssSubnode $deltaruneProfile "ScalingType" "Off"
        _Set-LssSubnode $deltaruneProfile "FrameGeneration" "LSFG3"
        _Set-LssSubnode $deltaruneProfile "LSFGSize" ($(if ($Mode -eq 'performance') { "PERFORMANCE" } else { "BALANCED" }))
        _Set-LssSubnode $deltaruneProfile "LSFGFlowScale" $Flow
        _Set-LssSubnode $deltaruneProfile "SyncMode" "DEFAULT"
        _Set-LssSubnode $deltaruneProfile "MaxFrameLatency" "3"
        _Set-LssSubnode $deltaruneProfile "GsyncSupport" "true"
        _Set-LssSubnode $deltaruneProfile "CaptureApi" "DXGI"
        _Set-LssSubnode $deltaruneProfile "QueueTarget" "1"
        _Set-LssSubnode $deltaruneProfile "DrawFps" "true"
        _Set-LssSubnode $deltaruneProfile "ClipCursor" "true"

        switch ($TargetFps) {
            "60"  { _Set-LssSubnode $deltaruneProfile "LSFG3Mode1" "FIXED"; _Set-LssSubnode $deltaruneProfile "LSFG3Multiplier" "2"; _Set-LssSubnode $deltaruneProfile "LSFG3Target" "60" }
            "90"  { _Set-LssSubnode $deltaruneProfile "LSFG3Mode1" "FIXED"; _Set-LssSubnode $deltaruneProfile "LSFG3Multiplier" "3"; _Set-LssSubnode $deltaruneProfile "LSFG3Target" "90" }
            "120" { _Set-LssSubnode $deltaruneProfile "LSFG3Mode1" "FIXED"; _Set-LssSubnode $deltaruneProfile "LSFG3Multiplier" "4"; _Set-LssSubnode $deltaruneProfile "LSFG3Target" "120" }
            "144" { _Set-LssSubnode $deltaruneProfile "LSFG3Mode1" "ADAPTIVE"; _Set-LssSubnode $deltaruneProfile "LSFG3Multiplier" "4"; _Set-LssSubnode $deltaruneProfile "LSFG3Target" "144" }
            "240" { _Set-LssSubnode $deltaruneProfile "LSFG3Mode1" "ADAPTIVE"; _Set-LssSubnode $deltaruneProfile "LSFG3Multiplier" "4"; _Set-LssSubnode $deltaruneProfile "LSFG3Target" "240" }
            default { _Set-LssSubnode $deltaruneProfile "LSFG3Mode1" "FIXED"; _Set-LssSubnode $deltaruneProfile "LSFG3Multiplier" "2"; _Set-LssSubnode $deltaruneProfile "LSFG3Target" "60" }
        }

        $xml.Save($lssConfig)
    } catch {}
}

$script:StartedLosslessScaling = $false
$script:StartedLosslessBridge  = $false

function Start-LosslessScalingIfEnabled {
    param([Parameter(Mandatory)]$cfg)
    try {
        $mode = Get-FramerateModeSetting $cfg
        if ($mode -ne 'lossless') { return }

        $running = Get-Process -Name "*LosslessScaling*" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($running) {
            $script:StartedLosslessScaling = $true
            return
        }

        $lssExe = Find-LosslessScalingExe $cfg
        if (-not $lssExe) { return }

        Set-LosslessScalingProfile $cfg

        # Launch directly with -StartMinimized so Lossless Scaling starts 100% headless in the system tray without opening any GUI window
        $lssWorkingDir = Split-Path -Path $lssExe -Parent
        $null = Start-Process -FilePath $lssExe -ArgumentList "-StartMinimized" -WorkingDirectory $lssWorkingDir -WindowStyle Minimized -PassThru
        $script:StartedLosslessScaling = $true
    } catch {}
}

function Stop-LosslessScalingIfStarted {
    try {
        cmd /c "taskkill /f /im LosslessBridge.exe /im LosslessScaling.exe >nul 2>nul"
    } catch {}
    try {
        Get-Process -Name "LosslessBridge" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    } catch {}
    try {
        Get-Process -Name "*LosslessScaling*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    } catch {}
    $script:StartedLosslessBridge  = $false
    $script:StartedLosslessScaling = $false
}

function Invoke-LosslessScalingFirstRunCheck {
    param([Parameter(Mandatory)]$cfg)
    if (Test-Path $cfg.LssPromptedPath) { return }

    if (-not [Environment]::UserInteractive) {
        try { Set-Content -LiteralPath $cfg.LssPromptedPath -Value "1" -Encoding utf8 } catch {}
        return
    }

    try {
        Add-Type -AssemblyName System.Windows.Forms
        $promptMsg = "Moechtest du Lossless Scaling (LSFG) fuer fluessige 60, 120 oder 240 FPS auf allen Mods nutzen?`n`nFalls du Lossless Scaling auf Steam besitzt, richtet der ModLoader es vollautomatisch und headless im Hintergrund ein.`n`n(Diese Frage erscheint nur einmal beim ersten Start. Du kannst die Erkennung jederzeit im In-Game-Menue unter 'Mod-Verwaltung' starten.)"
        $res = [System.Windows.Forms.MessageBox]::Show(
            $promptMsg,
            "Deltarune Mod-Selector - Lossless Scaling",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Question
        )

        if ($res -eq [System.Windows.Forms.DialogResult]::Yes) {
            $lssExe = Find-LosslessScalingExe $cfg
            if ($lssExe) {
                Set-LosslessScalingProfile $cfg -TargetFps "60"
                Set-FramerateModeSetting $cfg -Mode "lossless"
                Set-LsfgFpsSetting $cfg -Fps "60"
                [System.Windows.Forms.MessageBox]::Show(
                    "Lossless Scaling wurde erfolgreich erkannt und fuer DELTARUNE eingerichtet!`n`nIm Spiel laeuft die Zwischenbildberechnung nun automatisch mit maximaler Bildqualitaet (LSFG 2.3 Quality). Du kannst die Ziel-FPS (bis zu 240 FPS) jederzeit im Spielmenue anpassen.",
                    "Lossless Scaling eingerichtet",
                    [System.Windows.Forms.MessageBoxButtons]::OK,
                    [System.Windows.Forms.MessageBoxIcon]::Information
                )
            } else {
                [System.Windows.Forms.MessageBox]::Show(
                    "Lossless Scaling wurde in deiner Steam-Bibliothek noch nicht gefunden.`n`nSobald du es auf Steam besitzt, kannst du es im In-Game-Menue unter 'Mod-Verwaltung & Updates' jederzeit automatisch einrichten lassen.",
                    "Lossless Scaling nicht gefunden",
                    [System.Windows.Forms.MessageBoxButtons]::OK,
                    [System.Windows.Forms.MessageBoxIcon]::Information
                )
            }
        }
    } catch {}
    finally {
        try { Set-Content -LiteralPath $cfg.LssPromptedPath -Value "1" -Encoding utf8 } catch {}
    }
}

function Get-GermanPatchSetting {
    param([Parameter(Mandatory)]$cfg)
    if (-not (Test-Path $cfg.GermanPatchPath)) { return $true }
    try {
        return ((Get-Content -LiteralPath $cfg.GermanPatchPath -Raw).Trim() -eq '1')
    } catch { return $true }
}

function Set-GermanPatchSetting {
    param([Parameter(Mandatory)]$cfg, [bool]$Enabled)
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try { [System.IO.File]::WriteAllText($cfg.GermanPatchPath, ($(if ($Enabled) { '1' } else { '0' })), $utf8) } catch {}
}

function Get-MenuLangSetting {
    param([Parameter(Mandatory)]$cfg)
    if (-not (Test-Path $cfg.MenuLangPath)) { return "en" }
    try {
        $val = (Get-Content -LiteralPath $cfg.MenuLangPath -Raw).Trim().ToLower()
        if ($val -eq 'de') { return 'de' }
        return 'en'
    } catch { return "en" }
}

function Set-MenuLangSetting {
    param([Parameter(Mandatory)]$cfg, [string]$Lang = "en")
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try { [System.IO.File]::WriteAllText($cfg.MenuLangPath, $Lang.ToLower(), $utf8) } catch {}
}

function Apply-ChapterVariant {
    param(
        [Parameter(Mandatory)]$cfg,
        [Parameter(Mandatory)][int]$Chapter,
        [string]$ModName = 'vanilla'
    )
    Restore-Vanilla $cfg
    if ($Chapter -le 0) { return }

    $framerateMode = Get-FramerateModeSetting $cfg
    $fps60Active = ($framerateMode -eq 'engine')
    $thesiActive = Get-ThesiOverlaySetting $cfg
    $germanActive = Get-GermanPatchSetting $cfg

    $chFolder = "chapter${Chapter}_windows"
    $liveDir = Join-Path $cfg.GameDir $chFolder
    if (-not (Test-Path $liveDir)) { return }

    # 1) If a custom mod is selected (not 'vanilla')
    if ($ModName -ne 'vanilla') {
        $mod = @(Get-InstalledMods $cfg) | Where-Object { $_.Name -eq $ModName -and $_.Chapter -eq $Chapter } | Select-Object -First 1
        if ($mod) {
            Apply-Mod $cfg $mod
            $modKey = $mod.Name -replace '[^a-zA-Z0-9_-]', '_'
            $targetWin = Join-Path (Join-Path $cfg.GameDir $chFolder) "data.win"

            if ($germanActive -and $thesiActive) {
                $cachedGermanThesiWin = Join-Path (Join-Path $cfg.ThesiCacheDir "${modKey}_german_ch${Chapter}") "data.win"
                if (Test-Path $cachedGermanThesiWin) {
                    Copy-Item -LiteralPath $cachedGermanThesiWin -Destination $targetWin -Force
                } else {
                    Apply-ThesiOverlay $cfg $Chapter $mod
                }
            }
            elseif ($germanActive -and -not $thesiActive) {
                $cachedGermanWin = Join-Path (Join-Path $cfg.GermanCacheDir "${modKey}_ch${Chapter}") "data.win"
                if (Test-Path $cachedGermanWin) {
                    Copy-Item -LiteralPath $cachedGermanWin -Destination $targetWin -Force
                }
            }
            elseif (-not $germanActive -and $thesiActive) {
                Apply-ThesiOverlay $cfg $Chapter $mod
            }

            # Optional Chapter 1 German translation for custom mods that don't supply custom text
            if ($germanActive -and $Chapter -eq 1) {
                $targetLang = Join-Path (Join-Path (Join-Path $cfg.GameDir $chFolder) "lang") "lang_en.json"
                $modLang = Join-Path (Join-Path $mod.Path $chFolder) "lang\lang_en.json"
                if (-not (Test-Path $modLang)) {
                    $gLang = Join-Path (Join-Path (Join-Path $cfg.GermanCacheDir "chapter1_windows") "lang") "lang_en.json"
                    if (Test-Path $gLang) {
                        $targetLangDir = Split-Path $targetLang -Parent
                        if (-not (Test-Path $targetLangDir)) { New-Item -ItemType Directory -Path $targetLangDir -Force | Out-Null }
                        Copy-Item -LiteralPath $gLang -Destination $targetLang -Force
                    }
                }
            }
        }
        return
    }

    # 2) If 60 FPS Overhaul is active for vanilla:
    $fpsChapterWin = Join-Path (Join-Path $cfg.Fps60CacheDir $chFolder) "data.win"
    $fpsGermanWin = Join-Path (Join-Path $cfg.Fps60CacheDir "german_ch${Chapter}") "data.win"
    $fpsThesiWin = Join-Path (Join-Path $cfg.ThesiCacheDir "fps60_ch${Chapter}") "data.win"
    $fpsGermanThesiWin = Join-Path (Join-Path $cfg.ThesiCacheDir "fps60_german_ch${Chapter}") "data.win"

    if ($fps60Active -and (Test-Path $fpsChapterWin)) {
        # Determine which 60 FPS build to use based on German and Thesi flags:
        $srcWin = $null
        if ($germanActive -and $thesiActive) {
            if (Test-Path $fpsGermanThesiWin) {
                $srcWin = $fpsGermanThesiWin
            } elseif (Test-Path $fpsGermanWin) {
                $srcWin = $fpsGermanWin
            } else {
                $srcWin = $fpsChapterWin
            }
        }
        elseif ($germanActive -and -not $thesiActive) {
            if (Test-Path $fpsGermanWin) {
                $srcWin = $fpsGermanWin
            } else {
                $srcWin = $fpsChapterWin
            }
        }
        elseif (-not $germanActive -and $thesiActive) {
            if (Test-Path $fpsThesiWin) {
                $srcWin = $fpsThesiWin
            } else {
                $srcWin = $fpsChapterWin
            }
        }
        else {
            $srcWin = $fpsChapterWin
        }

        $filesToApply = @(
            [pscustomobject]@{ RelPath = "$chFolder\data.win"; Src = $srcWin }
        )
        if ($germanActive -and $Chapter -eq 1) {
            $gLang = Join-Path (Join-Path (Join-Path $cfg.GermanCacheDir "chapter1_windows") "lang") "lang_en.json"
            if (Test-Path $gLang) {
                $filesToApply += [pscustomobject]@{ RelPath = "chapter1_windows\lang\lang_en.json"; Src = $gLang }
            }
        }
        if ($germanActive -and $Chapter -eq 5) {
            $gVid = Join-Path (Join-Path (Join-Path $cfg.GermanCacheDir "chapter5_windows") "vid") "ch5_intro_en.mp4"
            if (Test-Path $gVid) {
                $filesToApply += [pscustomobject]@{ RelPath = "chapter5_windows\vid\ch5_intro_en.mp4"; Src = $gVid }
            }
        }

        $manifest = @()
        foreach ($f in $filesToApply) {
            $existed = Backup-FileIfNeeded $cfg $f.RelPath
            $manifest += [pscustomobject]@{ path = $f.RelPath; existedInVanilla = $existed }
            $dst = Join-Path $cfg.GameDir $f.RelPath
            $dstDir = Split-Path $dst -Parent
            if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
            Copy-Item -LiteralPath $f.Src -Destination $dst -Force
        }
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $cfg.ManifestPath -Encoding utf8
        return
    }

    # 3) If 'vanilla' (30 FPS) is selected:
    # The German patch is optional. If there is no German build for this chapter,
    # behave as if the toggle were off, so the Thesi overlay still applies.
    $germanChapterWin = Join-Path (Join-Path $cfg.GermanCacheDir $chFolder) "data.win"
    if ($germanActive -and -not (Test-Path $germanChapterWin)) { $germanActive = $false }
    # German + Thesi requested but the combined build is missing -> keep the language.
    if ($germanActive -and $thesiActive) {
        $germanThesiWin = Join-Path (Join-Path $cfg.ThesiCacheDir "german_ch${Chapter}") "data.win"
        if (-not (Test-Path $germanThesiWin)) { $thesiActive = $false }
    }
    # State 1: Both German + Thesi active (fast-path: cached pre-patched combination)
    if ($germanActive -and $thesiActive) {
        $thesiGermanWin = Join-Path (Join-Path $cfg.ThesiCacheDir "german_ch${Chapter}") "data.win"
        $filesToApply = @()
        if (Test-Path $thesiGermanWin) {
            $filesToApply += [pscustomobject]@{ RelPath = "$chFolder\data.win"; Src = $thesiGermanWin }
        }
        if ($Chapter -eq 1) {
            $gLang = Join-Path (Join-Path (Join-Path $cfg.GermanCacheDir "chapter1_windows") "lang") "lang_en.json"
            if (Test-Path $gLang) {
                $filesToApply += [pscustomobject]@{ RelPath = "chapter1_windows\lang\lang_en.json"; Src = $gLang }
            }
        }
        if ($Chapter -eq 5) {
            $gVid = Join-Path (Join-Path (Join-Path $cfg.GermanCacheDir "chapter5_windows") "vid") "ch5_intro_en.mp4"
            if (Test-Path $gVid) {
                $filesToApply += [pscustomobject]@{ RelPath = "chapter5_windows\vid\ch5_intro_en.mp4"; Src = $gVid }
            }
        }
        $manifest = @()
        foreach ($f in $filesToApply) {
            $existed = Backup-FileIfNeeded $cfg $f.RelPath
            $manifest += [pscustomobject]@{ path = $f.RelPath; existedInVanilla = $existed }
            $dst = Join-Path $cfg.GameDir $f.RelPath
            $dstDir = Split-Path $dst -Parent
            if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
            Copy-Item -LiteralPath $f.Src -Destination $dst -Force
        }
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $cfg.ManifestPath -Encoding utf8
        return
    }

    # State 2: German active, Thesi disabled (fast-path: pure German translation)
    if ($germanActive -and -not $thesiActive) {
        $germanWin = Join-Path (Join-Path $cfg.GermanCacheDir $chFolder) "data.win"
        $filesToApply = @()
        if (Test-Path $germanWin) {
            $filesToApply += [pscustomobject]@{ RelPath = "$chFolder\data.win"; Src = $germanWin }
        }
        if ($Chapter -eq 1) {
            $gLang = Join-Path (Join-Path (Join-Path $cfg.GermanCacheDir "chapter1_windows") "lang") "lang_en.json"
            if (Test-Path $gLang) {
                $filesToApply += [pscustomobject]@{ RelPath = "chapter1_windows\lang\lang_en.json"; Src = $gLang }
            }
        }
        if ($Chapter -eq 5) {
            $gVid = Join-Path (Join-Path (Join-Path $cfg.GermanCacheDir "chapter5_windows") "vid") "ch5_intro_en.mp4"
            if (Test-Path $gVid) {
                $filesToApply += [pscustomobject]@{ RelPath = "chapter5_windows\vid\ch5_intro_en.mp4"; Src = $gVid }
            }
        }
        $manifest = @()
        foreach ($f in $filesToApply) {
            $existed = Backup-FileIfNeeded $cfg $f.RelPath
            $manifest += [pscustomobject]@{ path = $f.RelPath; existedInVanilla = $existed }
            $dst = Join-Path $cfg.GameDir $f.RelPath
            $dstDir = Split-Path $dst -Parent
            if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
            Copy-Item -LiteralPath $f.Src -Destination $dst -Force
        }
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $cfg.ManifestPath -Encoding utf8
        return
    }

    # State 3: German disabled, Thesi active (fast-path: English Thesi Kris mod)
    if (-not $germanActive -and $thesiActive) {
        $thesiMod = @(Get-InstalledMods $cfg) | Where-Object { $_.Name -like "*Thesi Kris*" -and $_.Chapter -eq $Chapter } | Select-Object -First 1
        if ($thesiMod) {
            Apply-Mod $cfg $thesiMod
        } else {
            Restore-Vanilla $cfg
        }
        return
    }

    # State 4: Both disabled -> pure English vanilla
    Restore-Vanilla $cfg
}

# ============================================================================
#  Self-update + auto re-patch
# ============================================================================
#  On every launch the loader can (a) re-apply the in-game menu if a DELTARUNE
#  update reverted data.win to vanilla, and (b) update the Mod-Selector itself
#  from its GitHub releases. Both need UndertaleModTool to re-patch data.win;
#  it is downloaded once and cached in <GameDir>\_tools\UTMT_CLI. All of this is
#  strictly best-effort: any failure is swallowed so the game still launches.

function Get-SelectorVersion { return $script:SELECTOR_VERSION }

function Compare-SemVer {
    param([string]$A, [string]$B)
    $pa = @(); foreach ($p in (($A -replace '^[vV]+', '').Trim() -split '[.\-+]')) { if ($p -match '^\d+$') { $pa += [int]$p } else { break } }
    $pb = @(); foreach ($p in (($B -replace '^[vV]+', '').Trim() -split '[.\-+]')) { if ($p -match '^\d+$') { $pb += [int]$p } else { break } }
    while ($pa.Count -lt 3) { $pa += 0 }
    while ($pb.Count -lt 3) { $pb += 0 }
    for ($i = 0; $i -lt 3; $i++) {
        if ($pa[$i] -lt $pb[$i]) { return -1 }
        if ($pa[$i] -gt $pb[$i]) { return 1 }
    }
    return 0
}

function Test-DataWinPatched {
    param([Parameter(Mandatory)][string]$Path, [string]$Marker = "SELECT SAVE PROFILE")
    if (-not (Test-Path $Path)) { return $false }
    try {
        # Latin1 maps bytes 1:1 to chars, so IndexOf finds the ASCII marker fast.
        $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::GetEncoding('ISO-8859-1'))
        return $text.Contains($Marker)
    } catch { return $false }
}

function Get-SelectorSettings {
    param([Parameter(Mandatory)]$cfg)
    $auto = $false
    if (Test-Path $cfg.AutoUpdatePath) {
        try { $auto = ((Get-Content -LiteralPath $cfg.AutoUpdatePath -Raw).Trim() -eq '1') } catch {}
    }
    $last = ''
    if (Test-Path $cfg.LastCheckPath) {
        try { $last = (Get-Content -LiteralPath $cfg.LastCheckPath -Raw).Trim() } catch {}
    }
    return [pscustomobject]@{ autoUpdate = $auto; lastCheck = $last }
}

function Set-SelectorSettings {
    param([Parameter(Mandatory)]$cfg, [Parameter(Mandatory)]$Settings)
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    try { [System.IO.File]::WriteAllText($cfg.AutoUpdatePath, ($(if ($Settings.autoUpdate) { '1' } else { '0' })), $utf8) } catch {}
    try { [System.IO.File]::WriteAllText($cfg.LastCheckPath, [string]$Settings.lastCheck, $utf8) } catch {}
}

function Get-LatestSelectorRelease {
    param([Parameter(Mandatory)]$cfg)
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $url = "https://api.github.com/repos/$($cfg.RepoOwner)/$($cfg.RepoName)/releases/latest"
        $headers = @{ 'User-Agent' = 'DeltaruneModSelector'; 'Accept' = 'application/vnd.github+json' }
        $r = Invoke-RestMethod -Uri $url -Headers $headers -TimeoutSec 8 -ErrorAction Stop
        $zip = $null
        foreach ($a in $r.assets) { if ($a.name -like '*.zip') { $zip = $a.browser_download_url; break } }
        return [pscustomobject]@{ Version = ($r.tag_name -replace '^[vV]', ''); Tag = $r.tag_name; ZipUrl = $zip }
    } catch { return $null }
}

function Get-UtmtCli {
    # Returns path to UndertaleModCli.exe, downloading+caching it if -AllowDownload.
    param([Parameter(Mandatory)]$cfg, [switch]$AllowDownload)
    if (Test-Path $cfg.UtmtDir) {
        $found = Get-ChildItem -LiteralPath $cfg.UtmtDir -Recurse -Filter UndertaleModCli.exe -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    if (-not $AllowDownload) { return $null }
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        if (-not (Test-Path $cfg.ToolsDir)) { New-Item -ItemType Directory -Path $cfg.ToolsDir -Force | Out-Null }
        $zip = Join-Path $cfg.ToolsDir "UTMT_CLI.zip"
        $old = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $cfg.UtmtUrl -OutFile $zip -TimeoutSec 600 -UseBasicParsing
        $ProgressPreference = $old
        if (Test-Path $cfg.UtmtDir) { Remove-Item -LiteralPath $cfg.UtmtDir -Recurse -Force }
        New-Item -ItemType Directory -Path $cfg.UtmtDir -Force | Out-Null
        Expand-Archive -LiteralPath $zip -DestinationPath $cfg.UtmtDir -Force
        Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
        $found = Get-ChildItem -LiteralPath $cfg.UtmtDir -Recurse -Filter UndertaleModCli.exe -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { return $found.FullName }
    } catch {}
    return $null
}

function Invoke-DataWinRepatch {
    # Re-applies the in-game menu to data.win from a clean vanilla using the local
    # InjectModMenu.csx + cached UTMT. Returns $true on success.
    param([Parameter(Mandatory)]$cfg)
    if (-not (Test-Path $cfg.InjectorPath)) { return $false }
    $cli = Get-UtmtCli $cfg -AllowDownload
    if (-not $cli) { return $false }
    if (Test-DataWinPatched $cfg.DataWin $cfg.PatchMarker) {
        # data.win is already patched -> the clean vanilla is the backup
        if (-not (Test-Path $cfg.DataWinBackup)) { return $false }
    } else {
        # data.win is vanilla (DELTARUNE update reverted it) -> refresh backup from it
        Copy-Item -LiteralPath $cfg.DataWin -Destination $cfg.DataWinBackup -Force
    }
    if (-not (Test-Path $cfg.ToolsDir)) { New-Item -ItemType Directory -Path $cfg.ToolsDir -Force | Out-Null }
    $out = Join-Path $cfg.ToolsDir "data_patched.win"
    if (Test-Path $out) { Remove-Item -LiteralPath $out -Force }
    try { cmd /c "echo. | `"$cli`" load `"$($cfg.DataWinBackup)`" -s `"$($cfg.InjectorPath)`" -o `"$out`"" | Out-Null } catch { return $false }
    if (-not (Test-Path $out)) { return $false }
    if (-not (Test-DataWinPatched $out $cfg.PatchMarker)) { Remove-Item -LiteralPath $out -Force -ErrorAction SilentlyContinue; return $false }
    Copy-Item -LiteralPath $out -Destination $cfg.DataWin -Force
    Remove-Item -LiteralPath $out -Force -ErrorAction SilentlyContinue
    return $true
}

function Invoke-SelectorSelfUpdate {
    # Downloads the release ZIP, replaces loader + injector, then re-patches.
    param([Parameter(Mandatory)]$cfg, [Parameter(Mandatory)]$Release)
    if (-not $Release.ZipUrl) { return $false }
    if (-not (Test-Path $cfg.ToolsDir)) { New-Item -ItemType Directory -Path $cfg.ToolsDir -Force | Out-Null }
    $tmp = Join-Path $cfg.ToolsDir ("update_" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tmp -Force | Out-Null
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $zip = Join-Path $tmp "release.zip"
        $old = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $Release.ZipUrl -OutFile $zip -TimeoutSec 120 -UseBasicParsing
        $ProgressPreference = $old
        Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force
        $root = Get-ChildItem -LiteralPath $tmp -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'Loader') } | Select-Object -First 1
        if (-not $root) { return $false }
        $r = $root.FullName
        $mlSrc = Join-Path $r 'Loader\ModLoader\ModLoader.psm1'
        $psSrc = Join-Path $r 'Loader\modloader.ps1'
        $csSrc = Join-Path $r 'Patch\InjectModMenu.csx'
        # Replace loader + injector; leave mods/, _saveprofiles, settings untouched.
        if (Test-Path $mlSrc) { Copy-Item -LiteralPath $mlSrc -Destination (Join-Path $cfg.GameDir 'ModLoader\ModLoader.psm1') -Force }
        if (Test-Path $psSrc) { Copy-Item -LiteralPath $psSrc -Destination (Join-Path $cfg.GameDir 'modloader.ps1') -Force }
        if (Test-Path $csSrc) { Copy-Item -LiteralPath $csSrc -Destination $cfg.InjectorPath -Force }
        return (Invoke-DataWinRepatch $cfg)
    } catch { return $false }
    finally { if (Test-Path $tmp) { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue } }
}

function Show-SelectorDialog {
    param([string]$Text, [string]$Title = "Deltarune Mod-Selector", [string]$Buttons = "YesNo", [string]$Icon = "Question")
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        return [System.Windows.Forms.MessageBox]::Show($Text, $Title,
            [System.Windows.Forms.MessageBoxButtons]::$Buttons,
            [System.Windows.Forms.MessageBoxIcon]::$Icon)
    } catch { return [System.Windows.Forms.DialogResult]::None }
}

function Invoke-UpdateCheck {
    # Orchestrates version-update and post-DELTARUNE-update re-patch. Best-effort.
    param([Parameter(Mandatory)]$cfg)
    try {
        $settings = Get-SelectorSettings $cfg
        $auto = [bool]$settings.autoUpdate
        $needRepatch = -not (Test-DataWinPatched $cfg.DataWin $cfg.PatchMarker)

        # Throttle the network check to once every 4h, unless a re-patch is needed.
        $doCheck = $true
        if ($settings.lastCheck) {
            try { if (((Get-Date) - [datetime]$settings.lastCheck) -lt [timespan]::FromHours(4)) { $doCheck = $false } } catch {}
        }
        $release = $null
        if ($doCheck -or $needRepatch) {
            $release = Get-LatestSelectorRelease $cfg
            $settings.lastCheck = (Get-Date).ToString('o')
            Set-SelectorSettings $cfg $settings
        }

        $newVersion = ($release -and $release.Version -and (Compare-SemVer (Get-SelectorVersion) $release.Version) -lt 0)
        $utmtMissing = -not (Get-UtmtCli $cfg)

        if ($newVersion) {
            $go = $auto
            if (-not $auto) {
                $msg = "Eine neue Version des Mod-Selectors ist verfuegbar:`n`n" +
                       "   Installiert:  v$(Get-SelectorVersion)`n" +
                       "   Verfuegbar:   v$($release.Version)`n`n" +
                       "[Ja] = jetzt aktualisieren`n" +
                       "[Nein] = diesmal ueberspringen`n" +
                       "[Abbrechen] = ab jetzt immer automatisch aktualisieren"
                $res = Show-SelectorDialog -Text $msg -Buttons "YesNoCancel"
                if ($res -eq [System.Windows.Forms.DialogResult]::Yes) { $go = $true }
                elseif ($res -eq [System.Windows.Forms.DialogResult]::Cancel) { $go = $true; $settings.autoUpdate = $true; Set-SelectorSettings $cfg $settings }
            }
            if ($go) {
                if (-not $auto -and $utmtMissing) { Show-SelectorDialog -Text "Erstmalig wird ein Hilfstool (~62 MB) geladen. Das kann eine Minute dauern - bitte warten." -Buttons "OK" -Icon "Information" | Out-Null }
                $ok = Invoke-SelectorSelfUpdate $cfg $release
                if (-not $auto) {
                    if ($ok) { Show-SelectorDialog -Text "Aktualisiert auf v$($release.Version). Viel Spass!" -Buttons "OK" -Icon "Information" | Out-Null }
                    else     { Show-SelectorDialog -Text "Update fehlgeschlagen - der Selector laeuft mit der bisherigen Version weiter." -Buttons "OK" -Icon "Warning" | Out-Null }
                }
                return
            }
        }

        # No (accepted) version update -> if DELTARUNE reverted data.win, re-patch.
        if (-not (Test-DataWinPatched $cfg.DataWin $cfg.PatchMarker)) {
            $go = $auto
            if (-not $auto) {
                $res = Show-SelectorDialog -Text "Deltarune wurde aktualisiert und hat das Mod-Menue entfernt.`n`nMod-Selector-Menue jetzt neu installieren?" -Buttons "YesNo"
                $go = ($res -eq [System.Windows.Forms.DialogResult]::Yes)
            }
            if ($go) {
                if (-not $auto -and $utmtMissing) { Show-SelectorDialog -Text "Erstmalig wird ein Hilfstool (~62 MB) geladen. Das kann eine Minute dauern - bitte warten." -Buttons "OK" -Icon "Information" | Out-Null }
                $ok = Invoke-DataWinRepatch $cfg
                if (-not $auto) {
                    if ($ok) { Show-SelectorDialog -Text "Mod-Selector-Menue wurde neu installiert." -Buttons "OK" -Icon "Information" | Out-Null }
                    else     { Show-SelectorDialog -Text "Neu-Installation fehlgeschlagen (UTMT/Netzwerk?)." -Buttons "OK" -Icon "Warning" | Out-Null }
                }
            }
        }
    } catch {
        try { Add-Content -LiteralPath (Join-Path $cfg.SignalDir 'loader_error.txt') -Value ("Update-Check: " + $_.Exception.Message) -Encoding utf8 } catch {}
    }
}

function Get-GameBananaModInfo {
    param([Parameter(Mandatory)][Alias('ModId')][string]$IdOrUrl)
    $mid = 0
    if ($IdOrUrl -match '(?i)gamebanana\.com/mods/([0-9]+)') {
        $mid = [int]$Matches[1]
    } elseif ($IdOrUrl -match '^[0-9]+$') {
        $mid = [int]$IdOrUrl
    } else {
        throw "Ungueltige GameBanana ID oder URL: '$IdOrUrl'"
    }

    $url = "https://gamebanana.com/apiv11/Mod/$mid`?_csvProperties=_sName,_sVersion,_aFiles,_tsDateUpdated,_aSubmitter,_sText,_sProfileUrl"
    $headers = @{ 'User-Agent' = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) DeltaruneModSelector/1.4.0' }
    try {
        $res = Invoke-RestMethod -Uri $url -Headers $headers -ErrorAction Stop
        if (-not $res._sName) { return $null }
        return [pscustomobject]@{
            Id          = $mid
            Name        = $res._sName
            Version     = $res._sVersion
            DateUpdated = [int64]$res._tsDateUpdated
            Author      = $res._aSubmitter._sName
            Url         = if ($res._sProfileUrl) { $res._sProfileUrl } else { "https://gamebanana.com/mods/$mid" }
            Files       = @($res._aFiles | ForEach-Object {
                [pscustomobject]@{
                    FileId      = $_._idRow
                    FileName    = $_._sFile
                    DownloadUrl = $_._sDownloadUrl
                    FileSize    = [int64]$_._nFilesize
                    DateAdded   = [int64]$_._tsDateAdded
                    Description = $_._sDescription
                }
            })
        }
    } catch {
        Write-Warning "Fehler beim Abrufen von GameBanana Mod ${mid}: $($_.Exception.Message)"
        return $null
    }
}

function Get-ModUpdates {
    param([Parameter(Mandatory)]$cfg)
    $installed = Get-InstalledMods $cfg
    $updates = @()
    foreach ($mod in $installed) {
        $meta = $mod.Metadata
        if ($meta -and $meta.gamebanana_id) {
            $info = Get-GameBananaModInfo $meta.gamebanana_id
            if ($info) {
                $targetFile = $null
                if ($meta.file_name) {
                    $targetFile = $info.Files | Where-Object { $_.FileName -eq $meta.file_name } | Select-Object -First 1
                }
                if (-not $targetFile) {
                    $targetFile = $info.Files | Sort-Object DateAdded -Descending | Select-Object -First 1
                }

                $hasUpdate = $false
                if ($targetFile -and $meta.file_timestamp -and ($targetFile.DateAdded -gt $meta.file_timestamp)) {
                    $hasUpdate = $true
                }
                if ($info.Version -and $meta.version -and ((Compare-SemVer $meta.version $info.Version) -lt 0)) {
                    $hasUpdate = $true
                }
                $updates += [pscustomobject]@{
                    ModName        = $mod.Name
                    Chapter        = $mod.Chapter
                    Path           = $mod.Path
                    HasUpdate      = $hasUpdate
                    CurrentVersion = if ($meta.version) { $meta.version } else { "?" }
                    NewVersion     = if ($info.Version) { $info.Version } else { "-" }
                    CurrentDate    = if ($meta.file_timestamp) { [DateTimeOffset]::FromUnixTimeSeconds($meta.file_timestamp).LocalDateTime.ToString('dd.MM.yyyy') } else { "-" }
                    NewDate        = if ($targetFile) { [DateTimeOffset]::FromUnixTimeSeconds($targetFile.DateAdded).LocalDateTime.ToString('dd.MM.yyyy') } else { "-" }
                    LatestFileName = if ($targetFile) { $targetFile.FileName } else { "-" }
                    DownloadUrl    = if ($targetFile) { $targetFile.DownloadUrl } else { $null }
                    GameBananaId   = $meta.gamebanana_id
                    GameBananaUrl  = $info.Url
                }
            }
        } else {
            $updates += [pscustomobject]@{
                ModName        = $mod.Name
                Chapter        = $mod.Chapter
                Path           = $mod.Path
                HasUpdate      = $false
                CurrentVersion = "-"
                NewVersion     = "-"
                CurrentDate    = "-"
                NewDate        = "-"
                LatestFileName = "-"
                DownloadUrl    = $null
                GameBananaId   = $null
                GameBananaUrl  = $null
            }
        }
    }
    return $updates
}

function Update-DeltaruneMod {
    param(
        [Parameter(Mandatory)]$cfg,
        [Parameter(Mandatory)][string]$ModName
    )
    $modDir = Join-Path $cfg.ModsDir $ModName
    if (-not (Test-Path $modDir)) {
        throw "Mod-Ordner '$ModName' existiert nicht!"
    }
    $meta = Get-ModMetadata $modDir
    if (-not $meta -or -not $meta.gamebanana_id) {
        throw "Mod '$ModName' hat keine verknuepfte GameBanana ID in mod.json!"
    }
    $info = Get-GameBananaModInfo $meta.gamebanana_id
    if (-not $info) {
        throw "Konnte Mod-Informationen fuer ID $($meta.gamebanana_id) nicht von GameBanana abrufen."
    }
    $latestFile = $info.Files | Sort-Object DateAdded -Descending | Select-Object -First 1
    if (-not $latestFile) {
        throw "Keine Downloaddateien auf GameBanana gefunden."
    }

    # Backup des bisherigen Mod-Ordners
    $backupDir = Join-Path $cfg.GameDir "_mod_backups"
    if (-not (Test-Path $backupDir)) { New-Item -ItemType Directory -Path $backupDir -Force | Out-Null }
    $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $modBackup = Join-Path $backupDir "${ModName}_bak_${stamp}"
    Copy-Item -LiteralPath $modDir -Destination $modBackup -Recurse -Force

    # Herunterladen und Ausfuehren ueber Import-Mod.ps1
    $importerPs1 = Join-Path $cfg.GameDir "Import-Mod.ps1"
    if (-not (Test-Path $importerPs1)) {
        $importerPs1 = Join-Path (Split-Path $cfg.GameDir) "Import-Mod.ps1"
    }
    if (Test-Path $importerPs1) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $importerPs1 -Source "https://gamebanana.com/mods/$($meta.gamebanana_id)" -ModName $ModName -Force
    }

    # Caches fuer diesen Mod zuruecksetzen
    $modKey = $ModName -replace '[^a-zA-Z0-9_-]', '_'
    Get-ChildItem -Path $cfg.ThesiCacheDir, $cfg.GermanCacheDir -Filter "${modKey}_*" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
}

function Invoke-AutoImportFolder {
    param([Parameter(Mandatory)]$cfg)
    $importDir = $cfg.ModsImportDir
    if (-not (Test-Path $importDir)) {
        New-Item -ItemType Directory -Path $importDir -Force | Out-Null
        $readme = "Lege hier heruntergeladene Mod-Dateien (.zip, .rar, .7z oder .xdelta) ab.`nDer Mod-Loader importiert sie beim Spielstart automatisch in deinen 'mods'-Ordner!"
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText((Join-Path $importDir "HIER_MOD_DATEIEN_ABLEGEN.txt"), $readme, $utf8)
        return
    }

    $files = Get-ChildItem -LiteralPath $importDir -File | Where-Object {
        $_.Extension -match '(?i)^\.(zip|rar|7z|xdelta)$'
    }
    if ($files.Count -eq 0) { return }

    $importerPs1 = Join-Path $cfg.GameDir "Import-Mod.ps1"
    $procDir = $cfg.ModsImportProcessedDir
    if (-not (Test-Path $procDir)) { New-Item -ItemType Directory -Path $procDir -Force | Out-Null }

    foreach ($f in $files) {
        try {
            if (Test-Path $importerPs1) {
                & powershell -NoProfile -ExecutionPolicy Bypass -File $importerPs1 -Source $f.FullName -Force
                $dest = Join-Path $procDir $f.Name
                if (Test-Path $dest) { Remove-Item $dest -Force }
                Move-Item -LiteralPath $f.FullName -Destination $dest -Force
            }
        } catch {
            try { Add-Content -LiteralPath (Join-Path $cfg.SignalDir 'loader_error.txt') -Value ("Auto-Import '$($f.Name)': " + $_.Exception.Message) -Encoding utf8 } catch {}
        }
    }
}

function Start-ModLoader {
    param(
        [Parameter(Mandatory)][string]$GameDir,
        [string]$GameExe,
        [string[]]$GameArgs = @()
    )
    $cfg = Get-ModLoaderConfig $GameDir
    Clear-Signals $cfg
    # Crash recovery: if a previous session left a profile swapped in, persist its
    # progress and restore the user's real saves BEFORE anything else touches them.
    Swap-OutProfile $cfg
    Restore-Vanilla $cfg
    Invoke-AutoImportFolder $cfg
    Write-ModList $cfg (@(Get-InstalledMods $cfg))
    Write-ProfileList $cfg
    if ([string]::IsNullOrWhiteSpace($GameExe)) { return }

    # Before launching: self-update the selector and/or re-apply the menu if a
    # DELTARUNE update reverted data.win. Best-effort; never blocks the launch.
    try { Invoke-UpdateCheck $cfg } catch {}

    # First-run prompt & startup for Lossless Scaling (best-effort; never blocks game launch)
    try { Invoke-LosslessScalingFirstRunCheck $cfg } catch {}

    $startParams = @{ FilePath = $GameExe; WorkingDirectory = $GameDir; PassThru = $true }
    if ($GameArgs -and $GameArgs.Count -gt 0) { $startParams.ArgumentList = $GameArgs }
    $null = Start-Process @startParams

    # WICHTIG: Deltarunes game_change startet bei JEDEM Kapitelwechsel einen
    # neuen Prozess. Deshalb ueberwachen wir NICHT einen einzelnen PID, sondern
    # ob ueberhaupt noch ein Deltarune-Prozess laeuft.
    $sawGame           = $false
    $goneCount         = 0
    $lastSyncedFps     = Get-LsfgFpsSetting $cfg
    $lastSyncedMode    = Get-LsfgModeSetting $cfg
    $lastSyncedFlow    = Get-LsfgFlowSetting $cfg
    $chapterActive     = $false
    $waitingForChapter = $false
    $chapterLaunchTime = [DateTime]::MinValue
    $hookedLss         = $false

    # Do not start Lossless Scaling in the Hub menu
    Stop-LosslessScalingIfStarted

    try {
        while ($true) {
            # 1. Process Aliveness Check (first priority!)
            $alive = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like '*ELTARUNE*' })
            if ($alive.Count -gt 0) {
                $sawGame = $true
                $goneCount = 0
            } elseif ($sawGame) {
                $goneCount++
                # DELTARUNE is NOT running!
                # Instantly kill Lossless Scaling and cancel active chapter state so it NEVER tries to hook another window!
                Stop-LosslessScalingIfStarted
                $chapterActive = $false
                $hookedLss = $false

                if (-not $waitingForChapter) {
                    # User closed the game (Alt+F4 or Quit to Desktop).
                    # Exit cleanly within ~750ms (5 ticks).
                    if ($goneCount -ge 5) { break }
                } else {
                    # We are in the middle of a chapter transition (~game_change).
                    # Allow up to 12 seconds (~80 ticks) for the new chapter process to launch.
                    if ($goneCount -ge 80) { break }
                }
                Start-Sleep -Milliseconds 150
                continue
            }

            # 2. In-Game Mod Requests
            $req = Read-Request $cfg
            if ($req) {
                # A new request means the previous chapter is done -> persist its
                # profile (if any) and restore the real saves before we re-swap.
                Swap-OutProfile $cfg
                Apply-ChapterVariant $cfg $req.Chapter $req.Name
                # Empty profile == "Standard-Saves" -> play on the real saves, no swap.
                if ($req.Chapter -gt 0 -and -not [string]::IsNullOrWhiteSpace($req.Profile)) {
                    Swap-InProfile $cfg $req.Chapter $req.Profile
                }
                Write-ProfileList $cfg
                Set-Ready $cfg
                # Reset goneCount so chapter transitions have plenty of time without killing the loader
                $goneCount = 0
                if ($req.Chapter -gt 0) {
                    $waitingForChapter = $true
                    $chapterActive = $false
                    $chapterLaunchTime = [DateTime]::UtcNow
                    $hookedLss = $false
                    # Cleanly stop any previous LSS instance so DXGI swapchains are not blocked
                    Stop-LosslessScalingIfStarted
                }
            }

            # 3. Profile Commands
            # In-game profile management (create / delete a profile folder).
            $pcmd = Read-ProfileCmd $cfg
            if ($pcmd) {
                # Reach a clean save state first (persist + restore any active swap).
                Swap-OutProfile $cfg
                if ($pcmd.Action -eq 'delete') {
                    Remove-Profile $cfg $pcmd.Chapter $pcmd.Name
                } elseif ($pcmd.Action -eq 'create') {
                    New-Profile $cfg $pcmd.Chapter $pcmd.Name
                }
                Set-ProfileReady $cfg
            }

            # 4. Mod Tools Commands
            # In-game mod tools (check updates / process auto-import / scan lossless scaling).
            $tcmd = Read-ModToolsCmd $cfg
            if ($tcmd) {
                if ($tcmd -eq 'check_updates') {
                    try {
                        $updates = @(Get-ModUpdates $cfg)
                        $withUpdates = @($updates | Where-Object { $_.HasUpdate })
                        $gbCount = @($updates | Where-Object { $_.GameBananaId }).Count
                        if ($gbCount -eq 0) {
                            Set-ModToolsResult $cfg "Keine GameBanana-Mods gefunden." "Fuege Mods mit Import-Mod.bat hinzu" "(Option [2] fuer GameBanana-Verknuepfung)"
                        } elseif ($withUpdates.Count -gt 0) {
                            $names = ($withUpdates | ForEach-Object { "$($_.ModName) ($($_.NewVersion))" }) -join ", "
                            Set-ModToolsResult $cfg "$($withUpdates.Count) Mod-Update(s) verfuegbar!" "$names" "Zum Aktualisieren Import-Mod.bat [3] starten."
                        } else {
                            Set-ModToolsResult $cfg "Alle $gbCount GameBanana-Mods aktuell!" "Keine neuen Updates vorhanden." "Deine Mods sind auf dem neuesten Stand."
                        }
                    } catch {
                        Set-ModToolsResult $cfg "Fehler beim Update-Check" $_.Exception.Message "Bitte Internetverbindung pruefen."
                    }
                } elseif ($tcmd -eq 'auto_import') {
                    try {
                        Invoke-AutoImportFolder $cfg
                        $mods = @(Get-InstalledMods $cfg)
                        Write-ModList $cfg $mods
                        Set-ModToolsResult $cfg "Auto-Import durchgefuehrt!" "$($mods.Count) Mods jetzt installiert." "Mod-Liste wurde aktualisiert."
                    } catch {
                        Set-ModToolsResult $cfg "Fehler beim Auto-Import" $_.Exception.Message "Pruefe Ordner mods\_import."
                    }
                } elseif ($tcmd -eq 'scan_lss') {
                    try {
                        $lssExe = Find-LosslessScalingExe $cfg
                        if ($lssExe) {
                            Set-LosslessScalingProfile $cfg
                            Set-FramerateModeSetting $cfg -Mode 'lossless'
                            Set-ModToolsResult $cfg "Lossless Scaling eingerichtet!" "Profil 'DELTARUNE' (LSFG 3) aktiv." "Framerate-Modus: [Lossless Scaling] aktiviert."
                        } else {
                            Set-ModToolsResult $cfg "Lossless Scaling nicht gefunden!" "Bitte ueber Steam installieren." "Danach hier erneut suchen lassen."
                        }
                    } catch {
                        Set-ModToolsResult $cfg "Fehler beim Einrichten von LSS" $_.Exception.Message "Bitte Pfade pruefen."
                    }
                }
            }

            # 5. Framerate / LSFG Settings Sync
            # If in-game menu changed target FPS or mode, update profile
            $curMode     = Get-FramerateModeSetting $cfg
            $curFps      = Get-LsfgFpsSetting $cfg
            $curLsfgMode = Get-LsfgModeSetting $cfg
            $curLsfgFlow = Get-LsfgFlowSetting $cfg
            if ($curMode -eq 'lossless') {
                if ($lastSyncedFps -ne $curFps -or $lastSyncedMode -ne $curLsfgMode -or $lastSyncedFlow -ne $curLsfgFlow) {
                    Set-LosslessScalingProfile $cfg -TargetFps $curFps -Mode $curLsfgMode -Flow $curLsfgFlow
                    $lastSyncedFps  = $curFps
                    $lastSyncedMode = $curLsfgMode
                    $lastSyncedFlow = $curLsfgFlow
                }
            }

            # 6. Chapter Launch & LSS Hooking
            # Wait ~3.5 seconds after chapter launch for GameMaker to create and show the new window,
            # then start Lossless Scaling headless and send Ctrl + Alt + S to DELTARUNE!
            if ($waitingForChapter) {
                $elapsedSec = ([DateTime]::UtcNow - $chapterLaunchTime).TotalSeconds
                if ($elapsedSec -ge 3.5) {
                    $hwnd = [WinHelper]::FindDeltaruneWindow()
                    if ($hwnd -ne [IntPtr]::Zero) {
                        $waitingForChapter = $false
                        $chapterActive = $true
                        if ($curMode -eq 'lossless' -and -not $hookedLss) {
                            Start-LosslessScalingIfEnabled $cfg
                            Start-Sleep -Milliseconds 1200
                            $targetHwnd = [WinHelper]::FindDeltaruneWindow()
                            if ($targetHwnd -ne [IntPtr]::Zero) {
                                [WinHelper]::TriggerLssHotkey($targetHwnd)
                                $hookedLss = $true
                            }
                        }
                    } elseif ($elapsedSec -ge 12.0) {
                        # Timeout waiting for chapter window
                        $waitingForChapter = $false
                    }
                }
            }

            Start-Sleep -Milliseconds 150
        }
    } finally {
        Stop-LosslessScalingIfStarted
        # Persist the active profile + restore the user's real saves, then vanilla.
        Swap-OutProfile $cfg
        Restore-Vanilla $cfg
        Clear-Signals $cfg
    }
}

Export-ModuleMember -Function Get-ModLoaderConfig, Backup-FileIfNeeded, Get-InstalledMods, Write-ModList, Restore-Vanilla, Apply-Mod, Clear-Signals, Read-Request, Set-Ready, Start-ModLoader, Get-ChapterSaveFiles, Write-ProfileList, Get-ProfileDir, Swap-InProfile, Swap-OutProfile, New-Profile, Remove-Profile, Read-ProfileCmd, Set-ProfileReady, Read-ModToolsCmd, Set-ModToolsResult, Get-SelectorVersion, Compare-SemVer, Test-DataWinPatched, Get-SelectorSettings, Set-SelectorSettings, Get-LatestSelectorRelease, Get-UtmtCli, Invoke-DataWinRepatch, Invoke-SelectorSelfUpdate, Invoke-UpdateCheck, Get-ThesiOverlaySetting, Set-ThesiOverlaySetting, Apply-ThesiOverlay, Get-Fps60PatchSetting, Set-Fps60PatchSetting, Get-GermanPatchSetting, Set-GermanPatchSetting, Get-MenuLangSetting, Set-MenuLangSetting, Apply-ChapterVariant, Get-ModMetadata, Set-ModMetadata, Get-GameBananaModInfo, Get-ModUpdates, Update-DeltaruneMod, Invoke-AutoImportFolder, Get-FramerateModeSetting, Set-FramerateModeSetting, Get-LsfgFpsSetting, Set-LsfgFpsSetting, Get-LsfgModeSetting, Set-LsfgModeSetting, Get-LsfgFlowSetting, Set-LsfgFlowSetting, Find-LosslessScalingExe, Set-LosslessScalingProfile, Start-LosslessScalingIfEnabled, Stop-LosslessScalingIfStarted, Invoke-LosslessScalingFirstRunCheck
