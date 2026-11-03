<#
  Import-Mod.ps1 -- Mod Importer & Update Manager fuer Deltarune Mod Selector v1.4.0
  
  Ermoeglicht einfaches Installieren und Aktualisieren von Deltarune-Mods:
    - ZIP, RAR, 7Z Archive
    - XDELTA Patches
    - Ganze Mod-Ordner
    - Direkt von GameBanana per Link oder ID
    - Automatische Kapitel-Erkennung (1-5)
    - Pruefung auf Mod-Updates & 1-Klick Aktualisierung
#>
[CmdletBinding()]
param(
    [string]$Source,
    [string]$GameDir,
    [string]$ModName,
    [int]$Chapter = 0,
    [switch]$CheckUpdates,
    [switch]$AutoImport,
    [switch]$Force
)

# -----------------------------------------------------------------------------
# 1. Pfad-Ermittlung & ModLoader Modul laden
# -----------------------------------------------------------------------------
if (-not $GameDir) {
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot "DELTARUNE.exe")) {
        $GameDir = $PSScriptRoot
    } elseif (Test-Path -LiteralPath "D:\SteamLibrary\steamapps\common\DELTARUNE\DELTARUNE.exe") {
        $GameDir = "D:\SteamLibrary\steamapps\common\DELTARUNE"
    } else {
        $GameDir = Read-Host "Pfad zum DELTARUNE-Ordner eingeben"
    }
}
$GameDir = $GameDir.Trim().Trim('"', "'")
if (-not (Test-Path -LiteralPath (Join-Path $GameDir "DELTARUNE.exe"))) {
    Write-Host "[FEHLER] In '$GameDir' wurde keine DELTARUNE.exe gefunden." -ForegroundColor Red
    exit 1
}

$psm = Join-Path $GameDir "ModLoader\ModLoader.psm1"
if (-not (Test-Path $psm)) {
    $psm = Join-Path $PSScriptRoot "Loader\ModLoader\ModLoader.psm1"
}
if (-not (Test-Path $psm)) {
    Write-Host "[FEHLER] ModLoader.psm1 nicht gefunden!" -ForegroundColor Red
    exit 1
}

Import-Module $psm -Force -DisableNameChecking
$cfg = Get-ModLoaderConfig $GameDir

# -----------------------------------------------------------------------------
# 2. Hilfsfunktionen fuer Archiv-Entpackung & xdelta
# -----------------------------------------------------------------------------
function Extract-ArchiveFile {
    param([string]$ArchiveFile, [string]$DestDir)
    if (-not (Test-Path $DestDir)) { New-Item -ItemType Directory -Path $DestDir -Force | Out-Null }
    $ext = [System.IO.Path]::GetExtension($ArchiveFile).ToLower()
    $sevenZip = "C:\Program Files\7-Zip\7z.exe"
    
    if ($ext -eq '.zip') {
        try {
            Expand-Archive -LiteralPath $ArchiveFile -DestinationPath $DestDir -Force -ErrorAction Stop
            return $true
        } catch {}
    }
    
    if (Test-Path $sevenZip) {
        & $sevenZip x "$ArchiveFile" "-o$DestDir" -y | Out-Null
        if ($LASTEXITCODE -eq 0) { return $true }
    }
    
    try {
        & tar.exe -xf "$ArchiveFile" -C "$DestDir" 2>$null
        if ($LASTEXITCODE -eq 0) { return $true }
    } catch {}
    
    return $false
}

function Detect-XDeltaChapter {
    param(
        [Parameter(Mandatory)][System.IO.FileInfo]$XDeltaFile,
        [string]$SearchDir,
        [string]$ModName = "",
        [int]$DefaultChapter = 0
    )
    if ($DefaultChapter -ge 1 -and $DefaultChapter -le 5) {
        return $DefaultChapter
    }
    
    # 1. Aus dem Dateinamen des xdelta-Patches
    if ($XDeltaFile.Name -match '(?i)(?:ch|kapitel|chapter)\s*([1-5])') {
        return [int]$Matches[1]
    }
    
    # 2. Aus dem Mod-Namen oder Archiv-Namen
    if ($ModName -match '(?i)(?:ch|kapitel|chapter)\s*([1-5])') {
        return [int]$Matches[1]
    }
    
    if ($SearchDir -and (Test-Path $SearchDir)) {
        # 3. Pruefen auf Save-Dateien wie filech3_0
        $saveFile = Get-ChildItem -Path $SearchDir -Recurse -File | 
            Where-Object { $_.Name -match '(?i)^filech([1-5])_[0-9]+$' } | Select-Object -First 1
        if ($saveFile -and $saveFile.Name -match '(?i)^filech([1-5])_') {
            return [int]$Matches[1]
        }
        
        # 4. In Text-/Readme-Dateien nach "chapter3_windows" oder "chapter 3" suchen
        $txtFiles = Get-ChildItem -Path $SearchDir -Recurse -File | 
            Where-Object { $_.Extension -match '(?i)^\.(txt|md|ini)$' }
        foreach ($tf in $txtFiles) {
            try {
                $content = Get-Content -LiteralPath $tf.FullName -Raw -ErrorAction SilentlyContinue
                if ($content -match '(?i)chapter([1-5])_windows') {
                    return [int]$Matches[1]
                }
                if ($content -match '(?i)(?:chapter|kapitel)\s*([1-5])') {
                    return [int]$Matches[1]
                }
            } catch {}
        }
    }
    
    return 0
}

function Detect-ArchiveChapterAndRoot {
    param([string]$ExtractedDir)
    # 1. Pruefen ob chapter[1-5]_windows Ordner existiert
    $chDir = Get-ChildItem -Path $ExtractedDir -Recurse -Directory | 
        Where-Object { $_.Name -match '(?i)^chapter([1-5])_windows$' } | Select-Object -First 1
    if ($chDir) {
        $ch = [int]($chDir.Name -replace '(?i)^chapter([1-5])_windows$', '$1')
        return [pscustomobject]@{
            Type        = "FolderLayout"
            Chapter     = $ch
            ModRoot     = $chDir.Parent.FullName
            HasChFolder = $true
        }
    }
    
    # 2. Pruefen auf lose data.win
    $dataWin = Get-ChildItem -Path $ExtractedDir -Recurse -File -Filter "data.win" | Select-Object -First 1
    if ($dataWin) {
        return [pscustomobject]@{
            Type        = "DataWin"
            Chapter     = 0
            ModRoot     = $dataWin.Directory.FullName
            HasChFolder = $false
            DataWinPath = $dataWin.FullName
        }
    }

    # 3. Pruefen auf .xdelta Patch(es)
    $xdeltaFiles = @(Get-ChildItem -Path $ExtractedDir -Recurse -File | Where-Object { $_.Extension -match '(?i)^\.xdelta$' })
    if ($xdeltaFiles.Count -gt 0) {
        return [pscustomobject]@{
            Type        = "XDelta"
            Chapter     = 0
            ModRoot     = $ExtractedDir
            XDeltaFiles = $xdeltaFiles
        }
    }
    
    return $null
}

# -----------------------------------------------------------------------------
# 3. Haupt-Import-Routine
# -----------------------------------------------------------------------------
function Execute-Import {
    param(
        [Parameter(Mandatory)][string]$SourceInput,
        [string]$NameOverride,
        [int]$TargetChapter = 0,
        [switch]$Silent,
        [switch]$Force
    )
    
    $SourceInput = $SourceInput.Trim().Trim('"', "'")
    $gbMeta = $null
    $fileToProcess = $SourceInput
    $tempDir = $null
    
    # A) GameBanana URL oder ID
    if ($SourceInput -match '(?i)gamebanana\.com/mods/([0-9]+)' -or ($SourceInput -match '^[0-9]+$' -and -not (Test-Path -LiteralPath $SourceInput))) {
        Write-Host "`n[GameBanana] Rufe Mod-Informationen ab..." -ForegroundColor Cyan
        $gbMeta = Get-GameBananaModInfo $SourceInput
        if (-not $gbMeta) {
            Write-Host "[FEHLER] Konnte GameBanana Mod nicht abrufen." -ForegroundColor Red
            return $false
        }
        
        Write-Host "Mod: $($gbMeta.Name) von $($gbMeta.Author) (v$($gbMeta.Version))" -ForegroundColor Green
        if ($gbMeta.Files.Count -eq 0) {
            Write-Host "[FEHLER] Keine Downloaddateien fuer diesen Mod verfuegbar." -ForegroundColor Red
            return $false
        }
        
        $selectedFile = $null
        if ($gbMeta.Files.Count -eq 1) {
            $selectedFile = $gbMeta.Files[0]
        } else {
            Write-Host "`nMehrere Dateien verfuegbar:" -ForegroundColor Yellow
            for ($i = 0; $i -lt $gbMeta.Files.Count; $i++) {
                $f = $gbMeta.Files[$i]
                $mb = [math]::Round($f.FileSize / 1MB, 1)
                Write-Host "  [$($i+1)] $($f.FileName) ($mb MB)"
            }
            if (-not $Silent) {
                $idx = Read-Host "Waehle eine Datei (1-$($gbMeta.Files.Count))"
                $num = 0
                if ([int]::TryParse($idx, [ref]$num) -and $num -ge 1 -and $num -le $gbMeta.Files.Count) {
                    $selectedFile = $gbMeta.Files[$num - 1]
                } else {
                    $selectedFile = $gbMeta.Files[0]
                }
            } else {
                $selectedFile = $gbMeta.Files[0]
            }
        }
        
        $tempDir = Join-Path $env:TEMP ("dr_import_" + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        $dlPath = Join-Path $tempDir $selectedFile.FileName
        
        Write-Host "Lade '$($selectedFile.FileName)' herunter..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $selectedFile.DownloadUrl -OutFile $dlPath -UserAgent 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) DeltaruneModSelector/1.4.0' -UseBasicParsing
        $fileToProcess = $dlPath
        if (-not $NameOverride) { $NameOverride = $gbMeta.Name }
    }
    
    if (-not (Test-Path -LiteralPath $fileToProcess)) {
        Write-Host "[FEHLER] Datei oder Ordner '$fileToProcess' nicht gefunden." -ForegroundColor Red
        return $false
    }
    
    # B) Mod-Namen festlegen
    if (-not $NameOverride) {
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($fileToProcess)
        # Bereinigen von Versionsnummern / Endungen am Ende
        $clean = $baseName -replace '(?i)[_-]v?[0-9]+(\.[0-9]+)*$', ''
        $clean = $clean -replace '[_-]', ' '
        $clean = $clean.Trim()
        $NameOverride = if ($clean) { $clean } else { $baseName }
    }
    $NameOverride = $NameOverride -replace '[\\/:*?"<>|]', ''
    
    $finalModDir = Join-Path $cfg.ModsDir $NameOverride
    if (Test-Path $finalModDir) {
        if (-not $Force -and -not $Silent) {
            $ans = Read-Host "Mod '$NameOverride' existiert bereits. Ueberschreiben? (J/N)"
            if ($ans -notmatch '(?i)^[jy]') {
                Write-Host "Abgebrochen." -ForegroundColor Yellow
                return $false
            }
        }
    }
    
    # C) Verarbeiten nach Dateityp
    $ext = [System.IO.Path]::GetExtension($fileToProcess).ToLower()
    $installedCh = $TargetChapter
    
    # Fall 1: xdelta Patch
    if ($ext -eq '.xdelta') {
        if ($installedCh -le 0) {
            $parentDir = Split-Path $fileToProcess -Parent
            $installedCh = Detect-XDeltaChapter -XDeltaFile (Get-Item -LiteralPath $fileToProcess) -SearchDir $parentDir -ModName $NameOverride -DefaultChapter $TargetChapter
            if ($installedCh -le 0 -and -not $Silent) {
                Write-Host "`nxdelta-Patch gefunden: $([System.IO.Path]::GetFileName($fileToProcess))" -ForegroundColor Yellow
                $chIn = Read-Host "Fuer welches Kapitel (1-5) ist dieser Patch?"
                [int]::TryParse($chIn, [ref]$installedCh) | Out-Null
            }
        }
        if ($installedCh -lt 1 -or $installedCh -gt 5) {
            Write-Host "[FEHLER] Ungueltiges Kapitel ($installedCh). Abbruch." -ForegroundColor Red
            return $false
        }
        
        $vanillaWin = Join-Path (Join-Path $cfg.VanillaDir "chapter${installedCh}_windows") "data.win"
        if (-not (Test-Path $vanillaWin)) {
            $vanillaWin = Join-Path (Join-Path $cfg.GameDir "chapter${installedCh}_windows") "data.win"
        }
        $xdeltaExe = Join-Path $cfg.ToolsDir "xdelta3-3.1.0-x86_64.exe"
        if (-not (Test-Path $xdeltaExe)) {
            Write-Host "[FEHLER] xdelta3-3.1.0-x86_64.exe in _tools nicht gefunden." -ForegroundColor Red
            return $false
        }
        
        $targetChDir = Join-Path $finalModDir "chapter${installedCh}_windows"
        New-Item -ItemType Directory -Path $targetChDir -Force | Out-Null
        $targetWin = Join-Path $targetChDir "data.win"
        
        Write-Host "Wende xdelta-Patch auf Kapitel $installedCh an..." -ForegroundColor Cyan
        & $xdeltaExe -d -s $vanillaWin $fileToProcess $targetWin 2>&1 | Out-Null
        if (-not (Test-Path $targetWin) -or (Get-Item $targetWin).Length -lt 1000000) {
            Write-Host "[FEHLER] xdelta Patch-Anwendung fehlgeschlagen!" -ForegroundColor Red
            return $false
        }
        Write-Host "Patch erfolgreich angewendet: chapter${installedCh}_windows\data.win" -ForegroundColor Green

        # Begleitdateien im gleichen Verzeichnis pruefen (z.B. Audio, DLL)
        $parentDir = Split-Path $fileToProcess -Parent
        if ($parentDir -and (Test-Path $parentDir)) {
            $extraAudio = Get-ChildItem -Path $parentDir -Recurse -File | Where-Object { $_.Extension -match '(?i)^\.(ogg|wav|mp3)$' }
            if ($extraAudio.Count -gt 0) {
                $musDir = Join-Path $finalModDir "mus"
                if (-not (Test-Path $musDir)) { New-Item -ItemType Directory -Path $musDir -Force | Out-Null }
                foreach ($af in $extraAudio) {
                    Copy-Item -LiteralPath $af.FullName -Destination (Join-Path $musDir $af.Name) -Force
                    Write-Host "  + Musik: mus\$($af.Name)" -ForegroundColor DarkGray
                }
            }
            $extraDlls = Get-ChildItem -Path $parentDir -Recurse -File | Where-Object { $_.Extension -match '(?i)^\.dll$' }
            foreach ($df in $extraDlls) {
                Copy-Item -LiteralPath $df.FullName -Destination (Join-Path $targetChDir $df.Name) -Force
                Write-Host "  + DLL: $($df.Name)" -ForegroundColor DarkGray
            }
        }
    }
    # Fall 2: Archiv (.zip, .rar, .7z)
    elseif ($ext -match '^\.(zip|rar|7z)$') {
        if (-not $tempDir) {
            $tempDir = Join-Path $env:TEMP ("dr_import_" + [guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        }
        $extDir = Join-Path $tempDir "extracted"
        Write-Host "Entpacke Archiv..." -ForegroundColor Cyan
        $ok = Extract-ArchiveFile -ArchiveFile $fileToProcess -DestDir $extDir
        if (-not $ok) {
            Write-Host "[FEHLER] Konnte Archiv nicht entpacken." -ForegroundColor Red
            return $false
        }
        
        $layout = Detect-ArchiveChapterAndRoot $extDir
        if (-not $layout) {
            Write-Host "[FEHLER] Keine DELTARUNE-Mod-Dateien (chapterX_windows, data.win oder *.xdelta) im Archiv gefunden." -ForegroundColor Red
            return $false
        }
        
        if ($layout.Type -eq "FolderLayout") {
            $installedCh = $layout.Chapter
            New-Item -ItemType Directory -Path $finalModDir -Force | Out-Null
            Copy-Item -Path (Join-Path $layout.ModRoot "*") -Destination $finalModDir -Recurse -Force
        } elseif ($layout.Type -eq "DataWin") {
            # Lose data.win
            if ($installedCh -le 0) {
                if ($fileToProcess -match '(?i)(?:ch|kapitel|chapter)\s*([1-5])') {
                    $installedCh = [int]$Matches[1]
                } elseif (-not $Silent) {
                    $chIn = Read-Host "Zu welchem Kapitel (1-5) gehoert dieser Mod?"
                    [int]::TryParse($chIn, [ref]$installedCh) | Out-Null
                }
            }
            if ($installedCh -lt 1 -or $installedCh -gt 5) {
                Write-Host "[FEHLER] Ungueltiges Kapitel. Abbruch." -ForegroundColor Red
                return $false
            }
            $targetChDir = Join-Path $finalModDir "chapter${installedCh}_windows"
            New-Item -ItemType Directory -Path $targetChDir -Force | Out-Null
            Copy-Item -LiteralPath $layout.DataWinPath -Destination (Join-Path $targetChDir "data.win") -Force
            # Weitere Ordner (z.B. mus, vid, lang) mitsichern
            foreach ($item in Get-ChildItem -LiteralPath $layout.ModRoot) {
                if ($item.Name -ne "data.win") {
                    Copy-Item -LiteralPath $item.FullName -Destination $finalModDir -Recurse -Force
                }
            }
        } elseif ($layout.Type -eq "XDelta") {
            $xdeltaExe = Join-Path $cfg.ToolsDir "xdelta3-3.1.0-x86_64.exe"
            if (-not (Test-Path $xdeltaExe)) {
                Write-Host "[FEHLER] xdelta3-3.1.0-x86_64.exe in _tools nicht gefunden." -ForegroundColor Red
                return $false
            }

            New-Item -ItemType Directory -Path $finalModDir -Force | Out-Null
            $patchedAny = $false

            foreach ($xf in $layout.XDeltaFiles) {
                $detCh = Detect-XDeltaChapter -XDeltaFile $xf -SearchDir $extDir -ModName $NameOverride -DefaultChapter $installedCh
                if ($detCh -le 0 -and -not $Silent) {
                    Write-Host "`nxdelta-Patch gefunden: $($xf.Name)" -ForegroundColor Yellow
                    $chIn = Read-Host "Fuer welches Kapitel (1-5) ist dieser Patch?"
                    [int]::TryParse($chIn, [ref]$detCh) | Out-Null
                }
                if ($detCh -lt 1 -or $detCh -gt 5) {
                    Write-Host "[WARNUNG] Ungueltiges oder unbekanntes Kapitel fuer '$($xf.Name)'. Ueberspringe..." -ForegroundColor Yellow
                    continue
                }

                $installedCh = $detCh
                $vanillaWin = Join-Path (Join-Path $cfg.VanillaDir "chapter${detCh}_windows") "data.win"
                if (-not (Test-Path $vanillaWin)) {
                    $vanillaWin = Join-Path (Join-Path $cfg.GameDir "chapter${detCh}_windows") "data.win"
                }
                if (-not (Test-Path $vanillaWin)) {
                    Write-Host "[FEHLER] Vanilla data.win fuer Kapitel $detCh nicht gefunden!" -ForegroundColor Red
                    continue
                }

                $targetChDir = Join-Path $finalModDir "chapter${detCh}_windows"
                New-Item -ItemType Directory -Path $targetChDir -Force | Out-Null
                $targetWin = Join-Path $targetChDir "data.win"

                Write-Host "Wende xdelta-Patch '$($xf.Name)' auf Kapitel $detCh an..." -ForegroundColor Cyan
                & $xdeltaExe -d -s $vanillaWin $xf.FullName $targetWin 2>&1 | Out-Null
                if ((Test-Path $targetWin) -and (Get-Item $targetWin).Length -gt 1000000) {
                    Write-Host "Patch erfolgreich angewendet: chapter${detCh}_windows\data.win" -ForegroundColor Green
                    $patchedAny = $true
                } else {
                    Write-Host "[FEHLER] xdelta Patch-Anwendung fehlgeschlagen fuer '$($xf.Name)'!" -ForegroundColor Red
                }
            }

            if (-not $patchedAny) {
                Write-Host "[FEHLER] Es konnte kein xdelta-Patch erfolgreich angewendet werden." -ForegroundColor Red
                return $false
            }

            # Begleitdateien kopieren:
            # 1. Musik / Audio (in mus/)
            $audioFiles = Get-ChildItem -Path $extDir -Recurse -File | 
                Where-Object { $_.Extension -match '(?i)^\.(ogg|wav|mp3)$' }
            if ($audioFiles.Count -gt 0) {
                $musDir = Join-Path $finalModDir "mus"
                if (-not (Test-Path $musDir)) { New-Item -ItemType Directory -Path $musDir -Force | Out-Null }
                foreach ($af in $audioFiles) {
                    Copy-Item -LiteralPath $af.FullName -Destination (Join-Path $musDir $af.Name) -Force
                    Write-Host "  + Musik: mus\$($af.Name)" -ForegroundColor DarkGray
                }
            }

            # 2. Videos (in vid/)
            $videoFiles = Get-ChildItem -Path $extDir -Recurse -File | 
                Where-Object { $_.Extension -match '(?i)^\.(mp4|bik)$' }
            if ($videoFiles.Count -gt 0) {
                $vidDir = Join-Path (Join-Path $finalModDir "chapter${installedCh}_windows") "vid"
                if (-not (Test-Path $vidDir)) { New-Item -ItemType Directory -Path $vidDir -Force | Out-Null }
                foreach ($vf in $videoFiles) {
                    Copy-Item -LiteralPath $vf.FullName -Destination (Join-Path $vidDir $vf.Name) -Force
                    Write-Host "  + Video: vid\$($vf.Name)" -ForegroundColor DarkGray
                }
            }

            # 3. DLLs (in chapterX_windows/)
            $dllFiles = Get-ChildItem -Path $extDir -Recurse -File | 
                Where-Object { $_.Extension -match '(?i)^\.dll$' }
            foreach ($df in $dllFiles) {
                Copy-Item -LiteralPath $df.FullName -Destination (Join-Path (Join-Path $finalModDir "chapter${installedCh}_windows") $df.Name) -Force
                Write-Host "  + DLL: $($df.Name)" -ForegroundColor DarkGray
            }

            # 4. Sprachen (lang/)
            $langFiles = Get-ChildItem -Path $extDir -Recurse -File | 
                Where-Object { $_.Name -match '(?i)^lang_.*\.json$' }
            if ($langFiles.Count -gt 0) {
                $langDir = Join-Path (Join-Path $finalModDir "chapter${installedCh}_windows") "lang"
                if (-not (Test-Path $langDir)) { New-Item -ItemType Directory -Path $langDir -Force | Out-Null }
                foreach ($lf in $langFiles) {
                    Copy-Item -LiteralPath $lf.FullName -Destination (Join-Path $langDir $lf.Name) -Force
                    Write-Host "  + Sprache: lang\$($lf.Name)" -ForegroundColor DarkGray
                }
            }

            # 5. Speichern von Saves falls vorhanden
            $saveFiles = Get-ChildItem -Path $extDir -Recurse -File | 
                Where-Object { $_.Name -match '(?i)^filech[1-5]_[0-9]+$' }
            if ($saveFiles.Count -gt 0) {
                $savesDir = Join-Path $finalModDir "_saves"
                if (-not (Test-Path $savesDir)) { New-Item -ItemType Directory -Path $savesDir -Force | Out-Null }
                foreach ($sf in $saveFiles) {
                    Copy-Item -LiteralPath $sf.FullName -Destination (Join-Path $savesDir $sf.Name) -Force
                    Write-Host "  + Spielstand: _saves\$($sf.Name)" -ForegroundColor DarkGray
                }
            }
        }
    }
    # Fall 3: Ganzer Ordner
    elseif ((Get-Item -LiteralPath $fileToProcess) -is [System.IO.DirectoryInfo]) {
        $layout = Detect-ArchiveChapterAndRoot $fileToProcess
        if ($layout -and $layout.HasChFolder) {
            $installedCh = $layout.Chapter
            New-Item -ItemType Directory -Path $finalModDir -Force | Out-Null
            Copy-Item -Path (Join-Path $layout.ModRoot "*") -Destination $finalModDir -Recurse -Force
        } else {
            if ($installedCh -le 0) {
                if ($fileToProcess -match '(?i)(?:ch|kapitel|chapter)\s*([1-5])') {
                    $installedCh = [int]$Matches[1]
                } elseif (-not $Silent) {
                    $chIn = Read-Host "Zu welchem Kapitel (1-5) gehoert dieser Mod-Ordner?"
                    [int]::TryParse($chIn, [ref]$installedCh) | Out-Null
                }
            }
            if ($installedCh -lt 1 -or $installedCh -gt 5) {
                Write-Host "[FEHLER] Ungueltiges Kapitel. Abbruch." -ForegroundColor Red
                return $false
            }
            $targetChDir = Join-Path $finalModDir "chapter${installedCh}_windows"
            New-Item -ItemType Directory -Path $targetChDir -Force | Out-Null
            Copy-Item -Path (Join-Path $fileToProcess "*") -Destination $targetChDir -Recurse -Force
        }
    }
    
    # D) Metadaten mod.json anlegen/aktualisieren
    $meta = @{
        name         = $NameOverride
        chapter      = $installedCh
        installed_at = (Get-Date).ToString("o")
    }
    if ($gbMeta) {
        $meta.source        = "gamebanana"
        $meta.gamebanana_id = $gbMeta.Id
        $meta.author        = $gbMeta.Author
        $meta.version       = $gbMeta.Version
        $meta.gamebanana_url= $gbMeta.Url
        if ($selectedFile) {
            $meta.file_name      = $selectedFile.FileName
            $meta.file_timestamp = $selectedFile.DateAdded
        }
    }
    Set-ModMetadata -ModPath $finalModDir -Metadata $meta
    
    # E) Bereinigung
    if ($tempDir -and (Test-Path $tempDir)) {
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    # F) Mod-Liste aktualisieren
    Write-ModList $cfg (Get-InstalledMods $cfg)
    
    Write-Host "`n[ERFOLG] Mod '$NameOverride' (Kapitel $installedCh) wurde erfolgreich importiert!" -ForegroundColor Green
    Write-Host "Pfad: $finalModDir" -ForegroundColor Gray
    return $true
}

# -----------------------------------------------------------------------------
# 4. Mod-Updates Pruefen & Anzeigen
# -----------------------------------------------------------------------------
function Show-ModUpdatesMenu {
    Write-Host "`nPruefe installierte Mods auf Updates..." -ForegroundColor Cyan
    $updates = Get-ModUpdates $cfg
    
    Write-Host "`n-------------------------------------------------------------------------------------------------" -ForegroundColor Gray
    Write-Host ("{0,-26} {1,-8} {2,-20} {3,-12} {4,-12}" -f "MOD-NAME", "KAPITEL", "STATUS", "INSTALLIERT", "NEUESTE VERSION") -ForegroundColor White
    Write-Host "-------------------------------------------------------------------------------------------------" -ForegroundColor Gray
    
    $upCount = 0
    $upIndex = 1
    $upList = @()
    
    foreach ($m in $updates) {
        $stat = "[AKTUELL]"
        $statColor = "Green"
        if ($m.HasUpdate) {
            $stat = "[UPDATE VERFUEGBAR]"
            $statColor = "Yellow"
            $upCount++
            $upList += $m
        } elseif (-not $m.GameBananaId) {
            $stat = "[MANUELLER MOD]"
            $statColor = "DarkGray"
        }
        
        Write-Host ("{0,-26} {1,-8} " -f $m.ModName, "Ch $($m.Chapter)") -NoNewline
        Write-Host ("{0,-20} " -f $stat) -ForegroundColor $statColor -NoNewline
        Write-Host ("{0,-12} {1,-12}" -f $m.CurrentVersion, $m.NewVersion)
    }
    Write-Host "-------------------------------------------------------------------------------------------------" -ForegroundColor Gray
    
    if ($upCount -eq 0) {
        Write-Host "Alle Mods sind auf dem neuesten Stand!" -ForegroundColor Green
        return
    }
    
    Write-Host "`nEs gibt $upCount verfuegbare(s) Mod-Update(s)!" -ForegroundColor Yellow
    Write-Host "[A] Alle verfuegbaren Updates jetzt herunterladen & installieren" -ForegroundColor Cyan
    Write-Host "[1-$upCount] Einzelnen Mod aktualisieren" -ForegroundColor Cyan
    Write-Host "[Z] Zurueck" -ForegroundColor Gray
    
    $choice = Read-Host "`nDeine Auswahl"
    $selNum = 0
    if ($choice -match '(?i)^a') {
        foreach ($u in $upList) {
            Write-Host "`nAktualisiere $($u.ModName)..." -ForegroundColor Cyan
            Update-DeltaruneMod -cfg $cfg -ModName $u.ModName
        }
    } elseif ([int]::TryParse($choice, [ref]$selNum) -and $selNum -ge 1 -and $selNum -le $upList.Count) {
        $target = $upList[$selNum - 1]
        Write-Host "`nAktualisiere $($target.ModName)..." -ForegroundColor Cyan
        Update-DeltaruneMod -cfg $cfg -ModName $target.ModName
    }
}

# -----------------------------------------------------------------------------
# 5. Interaktives Hauptmenue
# -----------------------------------------------------------------------------
function Show-MainMenu {
    while ($true) {
        Clear-Host
        Write-Host "================================================================" -ForegroundColor Cyan
        Write-Host "       DELTARUNE MOD IMPORTER & UPDATE MANAGER (v1.4.0)         " -ForegroundColor Yellow
        Write-Host "================================================================" -ForegroundColor Cyan
        Write-Host " [1] Mod aus Datei oder Ordner importieren (.zip, .rar, .7z, .xdelta)" -ForegroundColor White
        Write-Host " [2] Mod direkt von GameBanana installieren (Link oder ID)" -ForegroundColor White
        Write-Host " [3] Alle installierten Mods auf Updates pruefen" -ForegroundColor White
        Write-Host " [4] Auto-Import Ordner verarbeiten (mods\_import)" -ForegroundColor White
        Write-Host " [5] Installierte Mods auflisten" -ForegroundColor White
        Write-Host " [0] Beenden" -ForegroundColor Gray
        Write-Host "================================================================" -ForegroundColor Cyan
        Write-Host "TIPP: Du kannst Dateien (.zip, .xdelta) auch direkt per Drag&Drop" -ForegroundColor DarkGray
        Write-Host "      auf 'Import-Mod.bat' ziehen!" -ForegroundColor DarkGray
        Write-Host "================================================================" -ForegroundColor Cyan
        
        $sel = Read-Host "`nWaehle eine Option (0-5)"
        switch ($sel) {
            "1" {
                Write-Host "`nGib den Pfad zur Mod-Datei/Ordner ein (oder druecke ENTER fuer Dateidialog):" -ForegroundColor Cyan
                $inPath = Read-Host "Pfad"
                if (-not $inPath) {
                    Add-Type -AssemblyName System.Windows.Forms
                    $ofd = New-Object System.Windows.Forms.OpenFileDialog
                    $ofd.Title = "Waehle eine Deltarune-Mod-Datei"
                    $ofd.Filter = "Deltarune Mods (*.zip;*.rar;*.7z;*.xdelta)|*.zip;*.rar;*.7z;*.xdelta|Alle Dateien (*.*)|*.*"
                    if ($ofd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
                        $inPath = $ofd.FileName
                    }
                }
                if ($inPath) {
                    Execute-Import -SourceInput $inPath
                }
                Read-Host "`nDruecke ENTER um fortzufahren..."
            }
            "2" {
                Write-Host "`nGib den GameBanana Mod-Link oder die ID ein:" -ForegroundColor Cyan
                Write-Host "Beispiel: https://gamebanana.com/mods/557348 oder 557348" -ForegroundColor DarkGray
                $gbInput = Read-Host "Link/ID"
                if ($gbInput) {
                    Execute-Import -SourceInput $gbInput
                }
                Read-Host "`nDruecke ENTER um fortzufahren..."
            }
            "3" {
                Show-ModUpdatesMenu
                Read-Host "`nDruecke ENTER um fortzufahren..."
            }
            "4" {
                Write-Host "`nPruefe Ordner 'mods\_import'..." -ForegroundColor Cyan
                Invoke-AutoImportFolder $cfg
                Read-Host "`nDruecke ENTER um fortzufahren..."
            }
            "5" {
                Write-Host "`nInstallierte Mods in '$($cfg.ModsDir)':" -ForegroundColor Cyan
                $all = Get-InstalledMods $cfg
                foreach ($m in ($all | Sort-Object Chapter, Name)) {
                    $ver = if ($m.Metadata -and $m.Metadata.version) { "v$($m.Metadata.version)" } else { "" }
                    $src = if ($m.Metadata -and $m.Metadata.gamebanana_id) { "[GameBanana #$($m.Metadata.gamebanana_id)]" } else { "" }
                    Write-Host "  * Kapitel $($m.Chapter): $($m.Name) $ver $src" -ForegroundColor White
                }
                Read-Host "`nDruecke ENTER um fortzufahren..."
            }
            "0" {
                return
            }
        }
    }
}

# -----------------------------------------------------------------------------
# 6. Ausfuehrungseinstieg (CLI, Drag & Drop oder Interaktiv)
# -----------------------------------------------------------------------------
if ($CheckUpdates) {
    Show-ModUpdatesMenu
    exit 0
}

if ($AutoImport) {
    Invoke-AutoImportFolder $cfg
    exit 0
}

if ($Source) {
    $ok = Execute-Import -SourceInput $Source -NameOverride $ModName -TargetChapter $Chapter -Force:$Force
    if (-not $ok) { exit 1 }
    if (-not $Force -and [Environment]::UserInteractive) {
        Write-Host "`nDruecke eine beliebige Taste zum Schliessen..." -ForegroundColor Gray
        try { $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") } catch {}
    }
    exit 0
}

# Wenn ohne Parameter gestartet -> Interaktives Menue
Show-MainMenu
