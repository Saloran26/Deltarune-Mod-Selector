function Get-ModLoaderConfig {
    param([Parameter(Mandatory)][string]$GameDir)
    $signal = Join-Path $env:LOCALAPPDATA "DELTARUNE"
    if (-not (Test-Path $signal)) { New-Item -ItemType Directory -Path $signal -Force | Out-Null }
    $vanilla = Join-Path $GameDir "_vanilla"
    if (-not (Test-Path $vanilla)) { New-Item -ItemType Directory -Path $vanilla -Force | Out-Null }
    [ordered]@{
        GameDir         = $GameDir
        ModsDir         = Join-Path $GameDir "mods"
        VanillaDir      = $vanilla
        SignalDir       = $signal
        ManifestPath    = Join-Path $vanilla "applied.json"
        # --- Save-Profile support -------------------------------------------
        # DELTARUNE reads/writes its saves (filechN_*) directly from SaveDir
        # (== SignalDir == %LOCALAPPDATA%\DELTARUNE). Profiles are named copies
        # of a chapter's save files; the loader swaps them in before launch and
        # persists them back + restores the user's real saves afterwards.
        SaveDir         = $signal
        ProfilesDir     = Join-Path $GameDir "_saveprofiles"      # per-chapter subfolders: ch<N>\<Name>\
        SaveBackupDir   = Join-Path $GameDir "_savebackup"        # user's real saves parked here while a profile is active
        ProfileListPath = Join-Path $signal "profiles.txt"        # written by loader; read by the in-game menu
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

function Get-InstalledMods {
    param([Parameter(Mandatory)]$cfg)
    if (-not (Test-Path $cfg.ModsDir)) { return @() }
    $result = @()
    foreach ($dir in Get-ChildItem -LiteralPath $cfg.ModsDir -Directory) {
        $files = Get-ChildItem -LiteralPath $dir.FullName -Recurse -File
        $rel = @()
        foreach ($f in $files) {
            $rel += $f.FullName.Substring($dir.FullName.Length).TrimStart('\','/')
        }
        $chapter = 0
        foreach ($r in $rel) {
            if ($r -match '(?i)(^|[\\/])chapter([0-9]+)_windows[\\/]') { $chapter = [int]$Matches[2]; break }
        }
        $result += [pscustomobject]@{
            Name = $dir.Name; Chapter = $chapter; Path = $dir.FullName; Files = $rel
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

function Restore-Vanilla {
    param([Parameter(Mandatory)]$cfg)
    if (-not (Test-Path $cfg.ManifestPath)) {
        if (Test-Path $cfg.VanillaDir) {
            $backups = Get-ChildItem -LiteralPath $cfg.VanillaDir -Recurse -File -ErrorAction SilentlyContinue
            foreach ($b in $backups) {
                if ($b.Name -eq 'applied.json') { continue }
                $rel = $b.FullName.Substring($cfg.VanillaDir.Length).TrimStart('\','/')
                $target = Join-Path $cfg.GameDir $rel
                $tdir = Split-Path $target -Parent
                if (-not (Test-Path $tdir)) { New-Item -ItemType Directory -Path $tdir -Force | Out-Null }
                Copy-Item -LiteralPath $b.FullName -Destination $target -Force
            }
        }
        return
    }
    $entries = Get-Content $cfg.ManifestPath -Raw | ConvertFrom-Json
    foreach ($e in $entries) {
        $target = Join-Path $cfg.GameDir $e.path
        if ($e.existedInVanilla) {
            $backup = Join-Path $cfg.VanillaDir $e.path
            if (Test-Path $backup) { Copy-Item -LiteralPath $backup -Destination $target -Force }
        } else {
            if (Test-Path $target) { Remove-Item -LiteralPath $target -Force }
        }
    }
    Remove-Item $cfg.ManifestPath -Force
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
    foreach ($n in "mod_request.txt","mod_ready.txt","profile_cmd.txt","profile_ready.txt") {
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
    Write-ModList $cfg (@(Get-InstalledMods $cfg))
    Write-ProfileList $cfg
    if ([string]::IsNullOrWhiteSpace($GameExe)) { return }

    $startParams = @{ FilePath = $GameExe; WorkingDirectory = $GameDir; PassThru = $true }
    if ($GameArgs -and $GameArgs.Count -gt 0) { $startParams.ArgumentList = $GameArgs }
    $null = Start-Process @startParams

    # WICHTIG: Deltarunes game_change startet bei JEDEM Kapitelwechsel einen
    # neuen Prozess. Deshalb ueberwachen wir NICHT einen einzelnen PID, sondern
    # ob ueberhaupt noch ein Deltarune-Prozess laeuft. Erst wenn ~3s lang keiner
    # mehr da ist (echtes Beenden), raeumen wir auf. So ueberlebt der Loader die
    # Kapitelwechsel und bedient auch weitere Mod-Anfragen.
    $sawGame = $false
    $goneCount = 0
    try {
        while ($true) {
            $req = Read-Request $cfg
            if ($req) {
                # A new request means the previous chapter is done -> persist its
                # profile (if any) and restore the real saves before we re-swap.
                Swap-OutProfile $cfg
                if ($req.Name -eq 'vanilla') {
                    Restore-Vanilla $cfg
                } else {
                    $mod = @(Get-InstalledMods $cfg) | Where-Object { $_.Name -eq $req.Name -and $_.Chapter -eq $req.Chapter } | Select-Object -First 1
                    if ($mod) { Apply-Mod $cfg $mod } else { Restore-Vanilla $cfg }
                }
                # Empty profile == "Standard-Saves" -> play on the real saves, no swap.
                if ($req.Chapter -gt 0 -and -not [string]::IsNullOrWhiteSpace($req.Profile)) {
                    Swap-InProfile $cfg $req.Chapter $req.Profile
                }
                Write-ProfileList $cfg
                Set-Ready $cfg
            }

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

            $alive = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like '*ELTARUNE*' })
            if ($alive.Count -gt 0) { $sawGame = $true; $goneCount = 0 }
            elseif ($sawGame) { $goneCount++ }
            if ($sawGame -and $goneCount -ge 20) { break }  # ~3s ohne Prozess = beendet

            Start-Sleep -Milliseconds 150
        }
    } finally {
        # Persist the active profile + restore the user's real saves, then vanilla.
        Swap-OutProfile $cfg
        Restore-Vanilla $cfg
        Clear-Signals $cfg
    }
}

Export-ModuleMember -Function Get-ModLoaderConfig, Backup-FileIfNeeded, Get-InstalledMods, Write-ModList, Restore-Vanilla, Apply-Mod, Clear-Signals, Read-Request, Set-Ready, Start-ModLoader, Get-ChapterSaveFiles, Write-ProfileList, Get-ProfileDir, Swap-InProfile, Swap-OutProfile, New-Profile, Remove-Profile, Read-ProfileCmd, Set-ProfileReady
