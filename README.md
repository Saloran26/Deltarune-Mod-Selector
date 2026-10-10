# 🎮 Deltarune Mod-Selector

Ein In-Game-Mod-Auswahlmenü für **Deltarune** (Steam). Startet das Spiel ganz
normal über Steam – beim Auswählen eines Kapitels erscheint ein Menü, in dem du
zwischen **Vanilla** und deinen installierten **Mods** wählst (Tastatur **oder**
Controller). Ist für ein Kapitel kein Mod installiert, startet es ganz normal.

Nach der Mod-Auswahl wählst du zusätzlich ein **Save-Profil**: entweder deine
normalen **Standard-Saves** oder ein eigenes benanntes Profil (eigene 3
Speicherplätze für dieses Kapitel). Deine echten Spielstände bleiben dabei
geschützt – Profil-Fortschritt landet im Profil, deine Standard-Saves werden nach
dem Spielen automatisch wiederhergestellt.

Neu in **v1.3.0**: ein **Einstellungsmenü** direkt im Spiel (Menüsprache
Deutsch/Englisch, Deutsch-Patch an/aus, Auto-Update an/aus, Credits) – und ein
gut verstecktes Geheimnis. 👀

Neue Mods hinzufügen = einen Ordner in `mods/` legen. Fertig. Deine
Original-Spieldateien werden **nie dauerhaft** verändert – bei jedem Start wird
sauber auf Vanilla zurückgesetzt.

> ℹ️ *English:* An in-game mod selector for Deltarune on Steam. Launch normally
> via Steam, pick a chapter, then choose Vanilla or one of your mods
> (keyboard/controller). Drop a folder into `mods/` to add a mod. Your original
> game files are never permanently modified. v1.3.0 adds an in-game settings
> menu (menu language, German patch toggle, auto-update). Full guide below (German).

---

## ⚠️ Wichtig / Rechtliches

- Funktioniert nur mit deinem **eigenen, über Steam gekauften** Deltarune.
- Dieses Repository enthält **keine** Spieldateien, **keine** fremden Mods und
  **keine** Übersetzung. Deltarune gehört Toby Fox.
- Enthalten sind nur: der Loader, das Menü-Skript und die Thesi-Kris-Sprites
  (Fan-Sprite-Edits, siehe [Credits](#credits)).
- Alles, was von anderen stammt, holst du dir selbst aus der jeweiligen Quelle –
  die nächste Tabelle zeigt, was das ist und wo es hingehört.

---

## 📦 Was du von anderen brauchst

| Was | Von wem | Nötig? | Wohin damit |
| :-- | :-- | :-- | :-- |
| **Deltarune** (5-Kapitel-Version) | Toby Fox, über Steam | **Pflicht** | ist dein Spielordner |
| **UndertaleModTool** | [UnderminersTeam](https://github.com/UnderminersTeam/UndertaleModTool/releases) | **Pflicht** für den Menü-Einbau | einmal von Hand benutzen (Schritt 2); danach lädt der Loader die CLI-Version selbst nach `_tools\UTMT_CLI\` |
| **Mods** (z. B. Kaizo Knight, Hardtype, Roaring Edition, Boss Rush) | die jeweiligen Mod-Autoren | optional | je ein Ordner in `mods\` – siehe [Einen Mod hinzufügen](#-einen-mod-hinzufügen) |
| **Deutsch-Patch** | [GERMANRUNE](https://gamebanana.com/mods/629265) (Chexle / GermanRune-Team) | optional | nach `_germancache\` – siehe [Deutsch-Patch einfügen](#-deutsch-patch-einfügen) |
| **xdelta3** oder **DeltaPatcher** | jeweilige Autoren | nur für den Deutsch-Patch | irgendwo auf deinem PC |

---

## Wie es funktioniert (kurz)

Zwei Teile arbeiten zusammen:

1. **Loader** (`Loader/`) – ein kleines PowerShell-Skript, das über die
   Steam-Startoptionen vorgeschaltet wird. Es scannt deinen `mods/`-Ordner,
   tauscht beim Mod-Start die passenden Dateien ein und stellt danach Vanilla
   wieder her. Er verwaltet außerdem die **Save-Profile** und spielt – je nach
   Einstellung – die deutsche Fassung ein. Läuft unsichtbar im Hintergrund.
2. **Menü-Patch** (`Patch/`) – wird einmalig mit *UndertaleModTool* in die
   Spieldatei `data.win` eingebaut und zeigt das Auswahlmenü beim Kapitelstart.

Die beiden reden über kleine Signaldateien in `%LOCALAPPDATA%\DELTARUNE`
miteinander.

---

## 📥 Installation

### Voraussetzungen
- **Deltarune** über Steam – die **5-Kapitel-Version**. Deine Version steht im
  **Titelbildschirm** unten. Der Loader ist versionsunabhängig; nur der
  Menü-Patch kann bei anderen Versionen ein Nachziehen brauchen.
- **UndertaleModTool** (kostenlos): https://github.com/UnderminersTeam/UndertaleModTool/releases
- Windows 10/11 (PowerShell ist bereits dabei).

### Wo ist mein Deltarune-Ordner?
Steam → Rechtsklick auf Deltarune → **Verwalten → Lokale Dateien durchsuchen**.
Darin liegen `DELTARUNE.exe`, `data.win`, `chapter1_windows`, …

### Schritt 1 – Loader hineinkopieren
Kopiere in deinen Deltarune-Ordner (neben `DELTARUNE.exe`):
- aus `Loader/` die Datei `modloader.ps1`
- aus `Loader/` den Ordner `ModLoader`
- aus `Patch/` die Datei `InjectModMenu.csx` **in den Ordner `ModLoader`**
  (der Loader braucht sie dort, um das Menü nach einem Deltarune-Update selbst
  neu einzubauen)

### Schritt 2 – Menü einbauen (UndertaleModTool)
1. **Sichere zuerst dein Original:** Kopiere `data.win` → `data.win.BACKUP`.
2. UndertaleModTool starten → `File → Open` → deine **`data.win`** (die kleine
   im Hauptordner, ca. 3 MB – **nicht** die in `chapter3_windows`!).
3. `Scripts → Run other script...` → `Patch/InjectModMenu.csx`.
4. Erfolgsmeldung abwarten → `File → Save As...` → über deine `data.win` speichern.

> Meldet das Skript „already patched“, ist die `data.win` schon gepatcht – nimm
> dein Backup und fang neu an. (Notfall-Handanleitung steht unten in
> `InjectModMenu.csx`.)

### Schritt 3 – Steam-Startoptionen
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

### Update von einer älteren Version
Einfach starten – der Selector fragt selbst, ob er sich aktualisieren soll, und
baut das Menü danach neu ein.

---

## ➕ Einen Mod hinzufügen

Mods stammen von ihren jeweiligen Autoren und sind hier **nicht** enthalten. Lade
den Mod bei seinem Autor herunter und lege ihn so ab (kein UndertaleModTool nötig):

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

> Kommt ein Mod als `.xdelta`-Patch, wende ihn zuerst mit xdelta3 oder
> DeltaPatcher auf eine **Kopie** deiner `chapterN_windows\data.win` an und lege
> das Ergebnis wie oben in den Mod-Ordner.

---

## 🇩🇪 Deutsch-Patch einfügen

Die deutsche Übersetzung ist [**GERMANRUNE**](https://gamebanana.com/mods/629265) von Chexle und dem
GermanRune-Team und ist hier **nicht** enthalten. Der Mod-Selector schaltet sie nur an und aus. So richtest du sie ein:

1. Lade das Patch-Paket bei GameBanana herunter: https://gamebanana.com/mods/629265
   Darin liegen je Kapitel
   eine `KapitelN_Deutsch_….xdelta` sowie die Ordner `chapter1_windows\lang\`
   und `chapter5_windows\vid\`.
2. Lege in deinem Deltarune-Ordner den Ordner `_germancache` an.
3. Wende für jedes Kapitel den Patch auf deine **originale**
   `chapterN_windows\data.win` an und speichere das Ergebnis als
   `_germancache\chapterN_windows\data.win`. Mit xdelta3 zum Beispiel:
   ```
   xdelta3.exe -d -s "chapter1_windows\data.win" "Kapitel1_Deutsch_v1.0.4.xdelta" "_germancache\chapter1_windows\data.win"
   ```
   (Den Zielordner vorher anlegen. Mit DeltaPatcher geht es genauso: Original
   wählen, Patch wählen, als neue Datei speichern.)
4. Kopiere aus dem Paket zusätzlich:
   - `chapter1_windows\lang\lang_en.json` → `_germancache\chapter1_windows\lang\lang_en.json`
   - `chapter5_windows\vid\ch5_intro_en.mp4` → `_germancache\chapter5_windows\vid\ch5_intro_en.mp4`

Danach sieht es so aus:
```
_germancache\
├─ chapter1_windows\  data.win  +  lang\lang_en.json
├─ chapter2_windows\  data.win
├─ chapter3_windows\  data.win
├─ chapter4_windows\  data.win
└─ chapter5_windows\  data.win  +  vid\ch5_intro_en.mp4
```

Im Spiel schaltest du die Übersetzung im Einstellungsmenü unter
**Deutsch-Patch (Spiel)** an oder aus. Den Launcher-Patch aus dem Paket
(`Launcher_Deutsch_….xdelta`) brauchst du nicht – die Haupt-`data.win` gehört
dem Menü-Patch.

> ⚠️ Die Einstellung **Deutsch-Patch (Spiel)** steht standardmäßig auf **AN**.
> Hast du den Patch nicht eingerichtet, läuft das Spiel einfach auf Englisch.

---

## 🕹️ Spielen
- Normal über Steam starten.
- Kapitel wählen → wenn Mods da sind, erscheint das Menü:
  **↑/↓** wählen · **Z / Enter** Bestätigen · **X / Shift** (Controller-B) Zurück.
- **1. Mod wählen** (Vanilla oder ein Mod).
- **2. Save-Profil wählen:**
  - **Standard-Saves** – spielt auf deinen ganz normalen Spielständen weiter (**Z** startet).
  - **ein vorhandenes Profil** – **Z** lädt dessen 3 Speicherplätze und startet.
  - **+ Neues Profil** – Name eintippen (der Mod-Name ist als Vorschlag schon
    vorausgefüllt), **Enter** legt ein neues, leeres Profil an. Es wird **nicht**
    sofort gestartet – du landest wieder in der Liste mit dem neuen Profil markiert
    und startest es dann bewusst mit **Z**.
  - **Profil löschen:** markiertes Profil → **Entf** (Controller **Y**) → dann zur
    Sicherheit **Z / A ~3 s gedrückt halten** (der Bildschirm wackelt, Balken füllt
    sich); Loslassen oder **X** bricht ab.
- Kein Mod für das Kapitel → startet direkt normal (ohne Profil-Auswahl).

## ⚙️ Einstellungsmenü (ab v1.3.0)
Im Mod-Menü mit **S** oder **Tab** öffnen (Controller: **Select/Back** oder **L1**).

| Einstellung | Bedeutung |
| :-- | :-- |
| **Menü-Sprache** | Sprache des Mod-Menüs: English (Standard) oder Deutsch |
| **Deutsch-Patch (Spiel)** | spielt die deutsche Fassung ein, falls eingerichtet (siehe oben) |
| **Auto-Update** | sucht beim Start nach neuen Selector-Versionen auf GitHub |
| **Credits & Danksagung** | wer was gemacht hat – lies sie ruhig einmal genau |

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

## 🔄 Auto-Update & Selbst-Reparatur
Der Loader prüft **bei jedem Start** (vor dem Spiel) zwei Dinge:

1. **Neue Selector-Version?** Gibt es auf GitHub ein neueres Release, erscheint ein
   Dialog: **Ja** = jetzt aktualisieren · **Nein** = überspringen · **Abbrechen** =
   ab jetzt immer automatisch. Beim Update lädt der Loader das Release, ersetzt seine
   eigenen Dateien und baut das Menü neu ein.
2. **Deltarune-Update erkannt?** Wenn Steam die `data.win` durch eine neue Vanilla
   ersetzt hat (Menü weg), bietet der Loader an, das Menü **automatisch neu
   einzupatchen** – kein manuelles UndertaleModTool mehr nötig.

Zum Patchen wird der **UndertaleModTool CLI** einmalig (~62 MB) von den offiziellen
Releases geladen und dauerhaft in `<DELTARUNE>\_tools\UTMT_CLI\` gecacht.

**Auto-Update ein-/ausschalten:** im Einstellungsmenü unter **Auto-Update**.
Ist es aktiv, passiert alles **ohne Nachfrage** still vor dem Spielstart.

---

<details>
<summary>🤫 <b>Spoiler: das Geheimnis im Menü</b> (aufklappen auf eigene Gefahr)</summary>

### 💗 Der Thesi-Modus

Im Menü schläft ein Name mit fünf Buchstaben. Tippst du im Mod-Menü **`thesi`**
(Controller: **5× Y / Dreieck**), wird der **Thesi-Modus** freigeschaltet: Kris
bekommt zweifarbige Haare in Türkis und Hot Pink – in Kapitel 1 bis 5. Auch das
SOUL-Herz im Menü trägt dann beide Farben.

Nach dem Freischalten erscheinen im Einstellungsmenü zwei neue Einträge:

| Einstellung | Bedeutung |
| :-- | :-- |
| **Thesi-Kris Texturen** | Thesi-Haare an/aus |
| **Thesi-Modus sperren / zurücksetzen** | versteckt den Modus wieder |

#### Thesi-Sprites einrichten

Die Sprites liegen in `Thesi/ThesiSprites/`. Damit das Spiel sie zeigt, müssen sie
einmal in **deine eigenen** Kapitel-Dateien eingebaut werden – das erledigt ein
Skript (fertige Spieldateien dürfen hier nicht liegen):

1. Mod-Selector wie oben installieren.
2. Wenn du auf Deutsch spielen willst: **zuerst** den Deutsch-Patch einrichten
   (siehe oben), damit das Skript auch die deutsche Thesi-Fassung baut.
3. Deltarune beenden, im Ordner `Thesi` eine PowerShell öffnen und ausführen:
   ```
   powershell -ExecutionPolicy Bypass -File .\Install-Thesi.ps1 -GameDir "D:\SteamLibrary\steamapps\common\DELTARUNE"
   ```
   (Pfad an deinen Deltarune-Ordner anpassen.)

Das Skript
- kopiert die Sprites nach `<DELTARUNE>\_tools\ThesiSprites\` und das
  Import-Skript nach `<DELTARUNE>\_tools\`,
- baut pro Kapitel `mods\Thesi Kris (Kapitel N)\chapterN_windows\data.win`,
- baut – falls `_germancache` vorhanden ist – zusätzlich
  `_thesicache\german_chN\data.win`.

Nur einzelne Kapitel bauen: `-Chapters 3,4,5` anhängen.

Wählst du einen **anderen Mod** (z. B. Kaizo Knight) und der Thesi-Modus ist an,
baut der Loader die Thesi-Sprites beim ersten Start selbst in diesen Mod ein und
merkt sich das Ergebnis in `_thesicache\`. Das dauert beim ersten Mal etwas.

> ⚠️ **Ohne Deutsch-Patch:** Stelle **Deutsch-Patch (Spiel)** auf **AUS**. Steht
> es auf AN und es gibt keine deutsche Thesi-Fassung, bleibt Kris im Vanilla-Look.

> Nach einem **Deltarune-Update** das Skript einfach noch einmal ausführen.

</details>

---

## 🛠️ Fehlerbehebung
- **Startet über Steam nicht / Fenster blitzt nur auf:** Pfad in den
  Startoptionen prüfen (kein doppeltes `DELTARUNE`); liegen `modloader.ps1` und
  `ModLoader\` neben `DELTARUNE.exe`? Datei
  `%LOCALAPPDATA%\DELTARUNE\loader_error.txt` verrät Fehler.
- **Menü erscheint nicht:** Ist die `data.win` die gepatchte? (Nach Update neu
  patchen.) Gibt es Mods für das Kapitel?
- **Mod fehlt im Menü:** Enthält der Mod-Ordner einen `chapterN_windows`-Unterordner
  und liegt er direkt unter `mods\`?
- **Einstellungsmenü fehlt:** Die `data.win` ist noch eine alte gepatchte Version –
  den Menü-Patch (**Schritt 2**) einmal neu einbauen oder das Update annehmen.
- **Spiel bleibt Englisch, obwohl Deutsch-Patch AN ist:** In `_germancache\` fehlt
  die Datei für dieses Kapitel (siehe [Deutsch-Patch einfügen](#-deutsch-patch-einfügen)).
- **Meine echten Saves sind weg / verändert:** Beim nächsten normalen Start stellt
  der Loader sie automatisch wieder her (auch nach einem Absturz). Ein wegge-
  sicherter Stand liegt notfalls in `_savebackup\` im Deltarune-Ordner.

---

## Lizenz
MIT – siehe [LICENSE](LICENSE). Betrifft den Code dieses Projekts (Loader,
Menü-Patch, `Install-Thesi.ps1`), nicht Deltarune, fremde Mods oder Übersetzungen.

`Thesi/ImportGraphicsCli.csx` ist eine angepasste Fassung des
ImportGraphics-Skripts aus dem UndertaleModTool (Texture-Packer von Samuel Roy)
und steht wie das UndertaleModTool unter der GPL-3.0.

Die Sprites in `Thesi/ThesiSprites/` sind Fan-Edits von Deltarune-Sprites.
Deltarune und seine Figuren gehören Toby Fox.

## Credits
- **Mod-Selector, Menü & Thesi-Sprites Kapitel 3–5:** Saloran26
- **Thesi-Kris-Sprites Kapitel 1 & 2:** basieren auf den Original-Sprites von
  **KajaKay_** (mit Erlaubnis verwendet)
- **Deutsche Übersetzung:** [GERMANRUNE](https://gamebanana.com/mods/629265) von Chexle und dem GermanRune-Team
  (nicht in diesem Repository enthalten)
- **UndertaleModTool:** UnderminersTeam
- **Deltarune:** Toby Fox

Erstellt mit Hilfe von **Claude AI** (Anthropic).
