<#
  Install-Thesi.ps1 -- richtet die Thesi-Kris-Sprites fuer den Deltarune Mod-Selector ein.

  Was das Skript macht:
    1. kopiert die Thesi-Sprites und das Import-Skript nach <DELTARUNE>\_tools\
    2. holt (falls noetig) den UndertaleModTool CLI ueber den Mod-Selector
    3. baut aus DEINEN eigenen Spieldateien pro Kapitel die Thesi-Variante:
         mods\Thesi Kris (Kapitel N)\chapterN_windows\data.win
       und - falls der Deutsch-Patch eingerichtet ist - zusaetzlich:
         _thesicache\german_chN\data.win

  Es werden keine Spieldateien mitgeliefert und deine Originale nicht veraendert.

  Aufruf (PowerShell, im Ordner "Thesi" dieses Repos):
    powershell -ExecutionPolicy Bypass -File .\Install-Thesi.ps1 -GameDir "D:\SteamLibrary\steamapps\common\DELTARUNE"

  Optional:
    -Chapters 3,4,5     nur bestimmte Kapitel bauen
    -TestDir  C:\Temp\x alle Ausgaben in diesen Ordner statt in den Spielordner schreiben
#>
[CmdletBinding()]
param(
    [string]$GameDir,
    [string[]]$Chapters = @('1', '2', '3', '4', '5'),
    [string]$TestDir
)
$ErrorActionPreference = 'Stop'
# "-Chapters 3,4,5" kommt bei 'powershell -File' als ein Text an -> selbst zerlegen.
$chapterList = @(($Chapters -join ',') -split '[,;\s]+' | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ })
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Fail([string]$msg) {
    Write-Host "[FEHLER] $msg" -ForegroundColor Red
    exit 1
}

if (-not $GameDir) { $GameDir = Read-Host 'Pfad zu deinem DELTARUNE-Ordner (dort liegt DELTARUNE.exe)' }
$GameDir = $GameDir.Trim().Trim('"')
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'DELTARUNE.exe'))) {
    Fail "In '$GameDir' liegt keine DELTARUNE.exe."
}
$psm = Join-Path $GameDir 'ModLoader\ModLoader.psm1'
if (-not (Test-Path -LiteralPath $psm)) {
    Fail 'Der Mod-Selector ist dort noch nicht installiert (ModLoader\ModLoader.psm1 fehlt). Bitte zuerst die Installation aus der README durchfuehren.'
}
if (-not (Test-Path -LiteralPath (Join-Path $here 'ThesiSprites\Sprites'))) {
    Fail "Der Ordner 'ThesiSprites' fehlt neben diesem Skript."
}
if (Get-Process -Name 'DELTARUNE' -ErrorAction SilentlyContinue) {
    Fail 'DELTARUNE laeuft noch. Bitte das Spiel erst beenden.'
}

Import-Module $psm -Force -DisableNameChecking
$cfg = Get-ModLoaderConfig -GameDir $GameDir
# Sicherstellen, dass die Kapitel-Dateien Vanilla sind (z. B. nach einem Absturz).
Restore-Vanilla $cfg

$dest = $GameDir
if ($TestDir) {
    New-Item -ItemType Directory -Path $TestDir -Force | Out-Null
    $dest = (Resolve-Path -LiteralPath $TestDir).Path
    Write-Host "[TEST] Ausgaben gehen nach: $dest" -ForegroundColor Yellow
}
$toolsDst   = Join-Path $dest '_tools'
$spritesDst = Join-Path $toolsDst 'ThesiSprites'
$importer   = Join-Path $toolsDst 'ImportGraphicsCli.csx'

Write-Host '[1/3] Kopiere Thesi-Sprites ...'
New-Item -ItemType Directory -Path $spritesDst -Force | Out-Null
Copy-Item -Path (Join-Path $here 'ThesiSprites\*') -Destination $spritesDst -Recurse -Force
Copy-Item -LiteralPath (Join-Path $here 'ImportGraphicsCli.csx') -Destination $importer -Force

Write-Host '[2/3] Pruefe UndertaleModTool CLI (wird beim ersten Mal geladen, ca. 62 MB) ...'
$cli = Get-UtmtCli $cfg -AllowDownload
if (-not $cli) { Fail 'UndertaleModTool CLI konnte nicht geladen werden (Internetverbindung?).' }

function Build-Variant([string]$src, [string]$sprites, [string]$out, [string]$label) {
    $outDir = Split-Path $out -Parent
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    $tmp = Join-Path $outDir 'thesi_build_temp.win'
    if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force }
    $old = $env:IMPORT_FOLDER
    $env:IMPORT_FOLDER = $sprites
    $start = Get-Date
    try {
        cmd /c "echo. | `"$cli`" load `"$src`" -s `"$importer`" -o `"$tmp`"" | Out-Null
    } catch {
    } finally {
        $env:IMPORT_FOLDER = $old
    }
    if ((Test-Path -LiteralPath $tmp) -and ((Get-Item -LiteralPath $tmp).Length -gt 1MB)) {
        Move-Item -LiteralPath $tmp -Destination $out -Force
        $mb  = [math]::Round((Get-Item -LiteralPath $out).Length / 1MB)
        $sec = [math]::Round(((Get-Date) - $start).TotalSeconds)
        Write-Host "  [OK] $label ($mb MB, $sec s)" -ForegroundColor Green
        return $true
    }
    if (Test-Path -LiteralPath $tmp) { Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue }
    Write-Host "  [FEHLER] $label" -ForegroundColor Red
    return $false
}

# Hilfsskript: listet die Sprites einer data.win auf (Name + Anzahl Frames).
$listCsx = Join-Path $toolsDst 'thesi_list_sprites.csx'
$listCode = @(
    'using System;',
    'using UndertaleModLib;',
    'EnsureDataLoaded();',
    'foreach (var s in Data.Sprites) {',
    '    if (s.Name?.Content != null) Console.WriteLine("SPRITE|" + s.Name.Content + "|" + s.Textures.Count);',
    '}'
)
Set-Content -LiteralPath $listCsx -Value $listCode -Encoding ASCII

function Get-SpriteTable([string]$win) {
    $table = @{}
    $lines = cmd /c "echo. | `"$cli`" load `"$win`" -s `"$listCsx`""
    foreach ($l in $lines) {
        if ($l -like 'SPRITE|*') { $p = $l.Split('|'); $table[$p[1]] = [int]$p[2] }
    }
    return $table
}

# Nur die Frames bereitstellen, die es in diesem Kapitel wirklich gibt.
function Copy-MatchingSprites($table, [string]$spritesDir, [string]$stageDir) {
    if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
    New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
    $count = 0
    foreach ($f in Get-ChildItem -LiteralPath $spritesDir -Filter '*.png') {
        if ($f.BaseName -match '^(.+)_(\d+)$') {
            $base = $Matches[1]; $idx = [int]$Matches[2]
            if ($table.ContainsKey($base) -and $idx -lt $table[$base]) {
                Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $stageDir $f.Name) -Force
                $count++
            }
        }
    }
    return $count
}

Write-Host '[3/3] Baue die Thesi-Varianten (pro Kapitel bis zu einigen Minuten) ...'
$ok = 0; $bad = 0; $german = 0; $germanFound = $false
foreach ($n in $chapterList) {
    $ch = "chapter${n}_windows"
    $vanilla = Join-Path $GameDir "$ch\data.win"
    if (-not (Test-Path -LiteralPath $vanilla)) {
        Write-Host "  [--] Kapitel ${n}: $ch\data.win nicht gefunden, uebersprungen." -ForegroundColor Yellow
        continue
    }
    $sp = Join-Path $spritesDst "ch$n"
    if (-not (Test-Path -LiteralPath $sp)) { $sp = Join-Path $spritesDst 'Sprites' }

    $table = Get-SpriteTable $vanilla
    $stage = Join-Path $toolsDst "thesi_stage_ch$n"
    $frames = Copy-MatchingSprites $table $sp $stage
    if ($frames -eq 0) {
        Write-Host "  [FEHLER] Kapitel ${n}: keine passenden Sprites gefunden." -ForegroundColor Red
        $bad++
        continue
    }
    $sp = $stage

    $out = Join-Path $dest "mods\Thesi Kris (Kapitel $n)\$ch\data.win"
    if (Build-Variant $vanilla $sp $out "Kapitel $n ($frames Frames)") { $ok++ } else { $bad++ }

    $de = Join-Path $GameDir "_germancache\$ch\data.win"
    if (Test-Path -LiteralPath $de) {
        $germanFound = $true
        $outDe = Join-Path $dest "_thesicache\german_ch$n\data.win"
        if (Build-Variant $de $sp $outDe "Kapitel $n + Deutsch-Patch") { $german++ } else { $bad++ }
    }
    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
}

Remove-Item -LiteralPath $listCsx -Force -ErrorAction SilentlyContinue
Write-Host ''
Write-Host "Fertig: $ok Kapitel gebaut, $german davon zusaetzlich mit Deutsch-Patch, $bad Fehler."
if (-not $germanFound) {
    Write-Host 'Hinweis: Kein Deutsch-Patch gefunden - gebaut wurde nur die englische Fassung. Richtest du den Deutsch-Patch spaeter ein, fuehre dieses Skript noch einmal aus.' -ForegroundColor Yellow
}
if ($bad -gt 0) { exit 1 }
