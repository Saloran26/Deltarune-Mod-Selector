# 🎮 Deltarune Mod-Selector

Ein In-Game-Mod-Auswahlmenü für **Deltarune** (Steam). Startet das Spiel ganz
normal über Steam — beim Auswählen eines Kapitels erscheint ein Menü, in dem du
zwischen **Vanilla** und deinen installierten **Mods** wählst (Tastatur **oder**
Controller). Ist für ein Kapitel kein Mod installiert, startet es ganz normal.

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
   wieder her. Läuft unsichtbar im Hintergrund.
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
- Kein Mod für das Kapitel → startet direkt normal.

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

---

## Lizenz
MIT — siehe [LICENSE](LICENSE). Betrifft nur den Code dieses Projekts (Loader +
Menü-Patch), nicht Deltarune oder Mods.

## Credits
Erstellt mit Hilfe von **Claude AI** (Anthropic).
