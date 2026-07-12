function Get-ModLoaderConfig {
    param([Parameter(Mandatory)][string]$GameDir)
    $signal = Join-Path $env:LOCALAPPDATA "DELTARUNE"
    if (-not (Test-Path $signal)) { New-Item -ItemType Directory -Path $signal -Force | Out-Null }
    $vanilla = Join-Path $GameDir "_vanilla"
    if (-not (Test-Path $vanilla)) { New-Item -ItemType Directory -Path $vanilla -Force | Out-Null }
    [ordered]@{
        GameDir      = $GameDir
        ModsDir      = Join-Path $GameDir "mods"
        VanillaDir   = $vanilla
        SignalDir    = $signal
        ManifestPath = Join-Path $vanilla "applied.json"
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
    foreach ($n in "mod_request.txt","mod_ready.txt") {
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
    $parts = $line.Split('|', 2)
    if ($parts.Count -lt 2) { return $null }
    [pscustomobject]@{ Chapter = [int]$parts[0]; Name = $parts[1] }
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
    Restore-Vanilla $cfg
    Write-ModList $cfg (@(Get-InstalledMods $cfg))
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
                if ($req.Name -eq 'vanilla') {
                    Restore-Vanilla $cfg
                } else {
                    $mod = @(Get-InstalledMods $cfg) | Where-Object { $_.Name -eq $req.Name -and $_.Chapter -eq $req.Chapter } | Select-Object -First 1
                    if ($mod) { Apply-Mod $cfg $mod } else { Restore-Vanilla $cfg }
                }
                Set-Ready $cfg
            }

            $alive = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like '*ELTARUNE*' })
            if ($alive.Count -gt 0) { $sawGame = $true; $goneCount = 0 }
            elseif ($sawGame) { $goneCount++ }
            if ($sawGame -and $goneCount -ge 20) { break }  # ~3s ohne Prozess = beendet

            Start-Sleep -Milliseconds 150
        }
    } finally {
        Restore-Vanilla $cfg
        Clear-Signals $cfg
    }
}

Export-ModuleMember -Function Get-ModLoaderConfig, Backup-FileIfNeeded, Get-InstalledMods, Write-ModList, Restore-Vanilla, Apply-Mod, Clear-Signals, Read-Request, Set-Ready, Start-ModLoader
