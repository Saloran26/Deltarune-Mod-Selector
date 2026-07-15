# 🎮 Deltarune Mod-Selector

Ein In-Game-Mod-Auswahlmenü für **Deltarune** (Steam). Startet das Spiel ganz
normal über Steam — beim Auswählen eines Kapitels erscheint ein Menü, in dem du
zwischen **Vanilla** und deinen installierten **Mods** wählst (Tastatur **oder**
Controller). Ist für ein Kapitel kein Mod installiert, startet es ganz normal.

Nach der Mod-Auswahl wählst du zusätzlich ein **Save-Profil**: entweder deine
normalen **Standard-Saves** oder ein eigenes benanntes Profil (eigene 3
Speicherplätze für dieses Kapitel). Deine echten Spielstände bleiben dabei
geschützt — Profil-Fortschritt landet im Profil, deine Standard-Saves werden nach
dem Spielen automatisch wiederhergestellt.

Neue Mods hinzufügen = einen Ordner in `mods/` legen. Fertig. Deine
Original-Spieldateien werden **nie dauerhaft** verändert — bei jedem Start wird
sauber auf Vanilla zurückgesetzt.

> ℹ️ *English:* An in-game mod selector for Deltarune on Steam. Launch normally
> via Steam, pick a chapter, then choose Vanilla or one of your mods
> (keyboard/controller). Drop a folder into `mods/` to add a mod. Your original
> game files are never permanently modified. Full guide below (German).

---

## ⚠️ Wichtig / Rechtliches

- Funktioniert nur mit deinem **eigenen, über Steam gekauften** Deltarune.
- Dieses Repository enthält **keine** Spieldateien und **keine** Mods — nur den
  Loader und das Menü-Skript. Deltarune gehört Toby Fox. Mods bringst du selbst
  mit (aus legalen Quellen).

---

## Wie es funktioniert (kurz)

Zwei Teile arbeiten zusammen:

1. **Loader** (`Loader/`) — ein kleines PowerShell-Skript, das über die
   Steam-Startoptionen vorgeschaltet wird. Es scannt deinen `mods/`-Ordner,
   tauscht beim Mod-Start die passenden Dateien ein und stellt danach Vanilla
   wieder her. Er verwaltet außerdem die **Save-Profile**: er sichert deine
   echten Spielstände weg, spielt das gewählte Profil ein und stellt deine Saves
   nach dem Beenden wieder her. Läuft unsichtbar im Hintergrund.
2. **Menü-Patch** (`Patch/`) — wird einmalig mit *UndertaleModTool* in die
   Spieldatei `data.win` eingebaut und zeigt das Auswahlmenü beim Kapitelstart.

Die beiden reden über kleine Signaldateien in `%LOCALAPPDATA%\DELTARUNE`
miteinander.

---

## 📦 Installation

### Voraussetzungen
- **Deltarune** über Steam — die **5-Kapitel-Version, `DELTARUNE v22` (Stand 09.07.2026)**.
  Deine Version steht im **Titelbildschirm** unten. Der Loader ist versionsunabhängig;
  nur der Menü-Patch kann bei anderen Versionen ein Nachziehen brauchen.
- **UndertaleModTool** (kostenlos): https://github.com/UnderminersTeam/UndertaleModTool/releases
- Windows 10/11 (PowerShell ist bereits dabei).

### Wo ist mein Deltarune-Ordner?
Steam → Rechtsklick auf Deltarune → **Verwalten → Lokale Dateien durchsuchen**.
Darin liegen `DELTARUNE.exe`, `data.win`, `chapter1_windows`, …

### Schritt 1 — Loader hineinkopieren
Kopiere aus `Loader/` **beides** in deinen Deltarune-Ordner (neben `DELTARUNE.exe`):
- die Datei `modloader.ps1`
- den Ordner `ModLoader`

### Schritt 2 — Menü einbauen (UndertaleModTool)
1. **Sichere zuerst dein Original:** Kopiere `data.win` → `data.win.BACKUP`.
2. UndertaleModTool starten → `File → Open` → deine **`data.win`** (die kleine
   im Hauptordner, ca. 3 MB — **nicht** die in `chapter3_windows`!).
3. `Scripts → Run other script...` → `Patch/InjectModMenu.csx`.
4. Erfolgsmeldung abwarten → `File → Save As...` → über deine `data.win` speichern.

> Meldet das Skript „already patched", ist die `data.win` schon gepatcht — nimm
> dein Backup und fang neu an. (Notfall-Handanleitung steht unten in
> `InjectModMenu.csx`.)

### Schritt 3 — Steam-Startoptionen
Steam → Rechtsklick auf Deltarune → **Eigenschaften → Startoptionen**:
```
powershell -ExecutionPolicy Bypass -File "DEIN\DELTARUNE\ORDNER\modloader.ps1" %command%
```
Beispiel:
```
powershell -ExecutionPolicy Bypass -File "D:\SteamLibrary\steamapps\common\DELTARUNE\modloader.ps1" %command%
```
⚠️ Der Pfad darf `DELTARUNE` nur **einmal** enthalten (kein doppeltes
`DELTARUNE\DELTARUNE`), sonst startet das Spiel nicht.

---

## ➕ Einen Mod hinzufügen

Kein UndertaleModTool nötig — nur Dateien kopieren:

1. In deinem Deltarune-Ordner den Ordner `mods\` öffnen (ggf. neu anlegen).
2. Darin einen Ordner erstellen; **der Ordnername ist der Menü-Name** (z. B.
   `Kaizo Knight`).
3. Die Mod-Dateien **genau so verschachtelt wie im Spiel** hineinlegen:

```
mods\
└─ Kaizo Knight\
   ├─ chapter3_windows\
   │  └─ data.win            ← die gemodete Kapitel-3-Datei
   └─ mus\
      └─ kaizoknight.ogg     ← Zusatzdatei (falls der Mod eine braucht)
```

Der Loader erkennt am Ordner `chapterN_windows` automatisch das Kapitel. Fertig.

---

## 🎮 Spielen
- Normal über Steam starten.
- Kapitel wählen → wenn Mods da sind, erscheint das Menü:
  **↑/↓** wählen · **Z / Enter** Bestätigen · **X / Shift** (Controller-B) Zurück.
- **1. Mod wählen** (Vanilla oder ein Mod).
- **2. Save-Profil wählen:**
  - **Standard-Saves** — spielt auf deinen ganz normalen Spielständen weiter (**Z** startet).
  - **ein vorhandenes Profil** — **Z** lädt dessen 3 Speicherplätze und startet.
  - **+ New Profile** — Name eintippen (der Mod-Name ist als Vorschlag schon
    vorausgefüllt), **Enter** legt ein neues, leeres Profil an. Es wird **nicht**
    sofort gestartet — du landest wieder in der Liste mit dem neuen Profil markiert
    und startest es dann bewusst mit **Z**.
  - **Profil löschen:** markiertes Profil → **Entf** (Controller **Y**) → dann zur
    Sicherheit **Z / A ~3 s gedrückt halten** (der Bildschirm wackelt, Balken füllt
    sich); Loslassen oder **X** bricht ab. Beim Löschen ertönt ein wuchtiger Hit.
- Kein Mod für das Kapitel → startet direkt normal (ohne Profil-Auswahl).

## 💾 Save-Profile
Ein Profil ist ein eigener Satz der **3 Speicherplätze eines Kapitels**. So kannst
du z. B. einen Mod ausprobieren, ohne deinen echten Spielstand zu überschreiben,
oder mehrere getrennte Durchläufe parallel führen.

- Deine **Standard-Saves** sind immer geschützt: sobald du ein Profil spielst,
  werden sie weggesichert und nach dem Beenden automatisch zurückgespielt.
- Fortschritt in einem Profil wird beim Verlassen des Kapitels **in das Profil**
  gespeichert (nicht in deine echten Saves).
- Profile liegen im Deltarune-Ordner unter `_saveprofiles\ch<N>\<Name>\` und sind
  pro Kapitel getrennt. **Löschen** geht direkt im Menü (Entf/Y + Halten) oder indem
  du den Profil-Ordner löschst.
- Profile sind unabhängig vom Mod: dasselbe Profil funktioniert mit Vanilla und
  jedem Mod desselben Kapitels.

## 🔄 Nach einem Deltarune-Update
Steam ersetzt die `data.win` durch das Original → **Schritt 2** einmal
wiederholen. Loader und Mods bleiben erhalten.

## 🛠️ Fehlerbehebung
- **Startet über Steam nicht / Fenster blitzt nur auf:** Pfad in den
  Startoptionen prüfen (kein doppeltes `DELTARUNE`); liegen `modloader.ps1` und
  `ModLoader\` neben `DELTARUNE.exe`? Datei
  `%LOCALAPPDATA%\DELTARUNE\loader_error.txt` verrät Fehler.
- **Menü erscheint nicht:** Ist die `data.win` die gepatchte? (Nach Update neu
  patchen.) Gibt es Mods für das Kapitel?
- **Mod fehlt im Menü:** Enthält der Mod-Ordner einen `chapterN_windows`-Unterordner
  und liegt er direkt unter `mods\`?
- **Profil-Auswahl fehlt / kein „SELECT SAVE PROFILE":** Die `data.win` ist noch
  die alte gepatchte Version — nach diesem Update den Menü-Patch (**Schritt 2**)
  einmal neu einbauen.
- **Meine echten Saves sind weg / verändert:** Beim nächsten normalen Start stellt
  der Loader sie automatisch wieder her (auch nach einem Absturz). Ein wegge-
  sicherter Stand liegt notfalls in `_savebackup\` im Deltarune-Ordner.

---

## Lizenz
MIT — siehe [LICENSE](LICENSE). Betrifft nur den Code dieses Projekts (Loader +
Menü-Patch), nicht Deltarune oder Mods.

## Credits
Erstellt mit Hilfe von **Claude AI** (Anthropic).
