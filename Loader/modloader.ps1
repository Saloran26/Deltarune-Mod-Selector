# Deltarune Mod-Selector -- Loader-Einstiegspunkt.
# Wird ueber die Steam-Startoptionen vorgeschaltet:
#   powershell -ExecutionPolicy Bypass -File "<Pfad>\modloader.ps1" %command%
# Steam ersetzt %command% durch den echten Aufruf von DELTARUNE.exe.
param(
    [string]$GameExe,
    [Parameter(ValueFromRemainingArguments=$true)][string[]]$GameArgs
)
$ErrorActionPreference = "Stop"
$GameDir = Split-Path -Parent $MyInvocation.MyCommand.Path
try {
    Import-Module (Join-Path $GameDir "ModLoader\ModLoader.psm1") -Force -DisableNameChecking
    Start-ModLoader -GameDir $GameDir -GameExe $GameExe -GameArgs $GameArgs
} catch {
    # Bei einem Fehler eine kurze Notiz hinterlassen (hilft bei der Fehlersuche).
    $errFile = Join-Path $env:LOCALAPPDATA "DELTARUNE\loader_error.txt"
    try { Set-Content -LiteralPath $errFile -Value ($_.Exception.Message + "`n`n" + $_.ScriptStackTrace) -Encoding utf8 } catch {}
    throw
}
