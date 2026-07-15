// ============================================================================
//  InjectModMenu.csx  --  UndertaleModTool script (UTMT v0.9.1.1)
// ============================================================================
//  Injects the in-game mod-selector menu into the DELTARUNE Hub `data.win`.
//
//  What it does:
//    1. Creates a new game object `obj_modmenu` with Create / Step / Draw-GUI
//       events, compiled from tools/mod_menu.gml (inlined below). The menu lets
//       you pick a mod (or Vanilla) AND a save profile: "Standard-Saves" (your
//       real saves) or a named profile (the loader swaps that chapter's saves in
//       and restores your real ones afterwards); "+ New Profile" types a name.
//    2. Hooks `show_transition` inside gml_Object_obj_CHAPTER_SELECT_Create_0:
//       inserts a chapter-mod check right after the function's opening brace.
//    3. Prepends an input guard to the REAL chapter-select input objects
//       gml_Object_obj_ui_choice_Step_0 (confirm / button1_p) and
//       gml_Object_obj_ui_chapter_Step_0 (navigation / button2_p).
//       (obj_CHAPTER_SELECT's own Step event is inert -- just `exit;` -- so a
//        guard there would block nothing.)
//
//  API used (verified against the sample scripts shipped with THIS UTMT build):
//    * UndertaleModLib.Compiler.CodeImportGroup + QueueReplace / QueueFindReplace
//        -> Scripts/UTDR Scripts/Debug.csx           (same game, same tool ver)
//        -> Scripts/Technical Scripts/GameObjectCopyInternal.csx
//    * GetDecompiledText(code)                        -> Debug.csx line ~123
//    * new UndertaleGameObject() + Data.GameObjects.Add
//        -> GameObjectCopyInternal.csx
//    * obj.EventHandlerFor(EventType.X, subtype, Data)
//        -> Scripts/Sample Scripts/MixMod.csx
//    * Data.Strings.MakeString(...)                   -> MixMod.csx / everywhere
//
//  HOW TO RUN:
//    UTMT GUI -> open the Hub data.win -> Scripts -> "Run other script..."
//    -> pick this file. Then File -> Save AS a COPY (e.g. data_patched.win),
//    do NOT overwrite the original on the first pass.
//
//  If the script errors, see the "MANUAL FALLBACK" block at the very bottom;
//  every step here has a hand-apply equivalent you can do in the UTMT GUI.
// ============================================================================

using System;
using UndertaleModLib;
using UndertaleModLib.Models;
using UndertaleModLib.Compiler;

EnsureDataLoaded();

// ----------------------------------------------------------------------------
//  Pre-flight checks
// ----------------------------------------------------------------------------
if (Data.GameObjects.ByName("obj_CHAPTER_SELECT") is null)
{
    ScriptError("obj_CHAPTER_SELECT not found -- is this the Hub data.win?");
    return;
}
if (Data.Code.ByName("gml_Object_obj_CHAPTER_SELECT_Create_0") is not UndertaleCode createCode)
{
    ScriptError("gml_Object_obj_CHAPTER_SELECT_Create_0 not found.");
    return;
}
if (Data.Code.ByName("gml_Object_obj_ui_choice_Step_0") is not UndertaleCode uiChoiceStep)
{
    ScriptError("gml_Object_obj_ui_choice_Step_0 not found -- cannot place confirm guard.");
    return;
}
if (Data.Code.ByName("gml_Object_obj_ui_chapter_Step_0") is not UndertaleCode uiChapterStep)
{
    ScriptError("gml_Object_obj_ui_chapter_Step_0 not found -- cannot place navigation guard.");
    return;
}
if (Data.Code.ByName("gml_Object_obj_ui_version_Draw_0") is not UndertaleCode uiVersionDraw)
{
    ScriptError("gml_Object_obj_ui_version_Draw_0 not found -- cannot add title-screen credit.");
    return;
}
if (Data.GameObjects.ByName("obj_modmenu") != null)
{
    ScriptError("obj_modmenu already exists -- data.win looks already patched. Aborting.");
    return;
}

// ----------------------------------------------------------------------------
//  GML sources (kept in sync with tools/mod_menu.gml).
//  NOTE: C# verbatim strings @"..." -> every literal double-quote is doubled "".
// ----------------------------------------------------------------------------

string CREATE_GML = @"
global.modmenu_open = true;
mm_parent  = global.modmenu_parent;
mm_chapter = global.modmenu_chapter;
mm_index   = 0;
mm_state   = ""select"";
mm_mod     = """";
mm_moddisp = """";
mm_pidx    = 0;
mm_newname = """";
mm_target  = """";
mm_wait    = """";
mm_holdt   = 0;

mm_names = [""Vanilla""];

if (file_exists(""modlist.txt""))
{
    var _f = file_text_open_read(""modlist.txt"");
    while (!file_text_eof(_f))
    {
        var _line = file_text_read_string(_f);
        file_text_readln(_f);
        var _sep = string_pos(""|"", _line);
        if (_sep > 0)
        {
            var _n  = real(string_copy(_line, 1, _sep - 1));
            var _nm = string_copy(_line, _sep + 1, string_length(_line) - _sep);
            if (_n == mm_chapter)
                array_push(mm_names, _nm);
        }
    }
    file_text_close(_f);
}

mm_profiles = [""Standard-Saves""];
if (file_exists(""profiles.txt""))
{
    var _pf = file_text_open_read(""profiles.txt"");
    while (!file_text_eof(_pf))
    {
        var _pl = file_text_read_string(_pf);
        file_text_readln(_pf);
        var _psep = string_pos(""|"", _pl);
        if (_psep > 0)
        {
            var _pn  = real(string_copy(_pl, 1, _psep - 1));
            var _pnm = string_copy(_pl, _psep + 1, string_length(_pl) - _psep);
            if (_pn == mm_chapter)
                array_push(mm_profiles, _pnm);
        }
    }
    file_text_close(_pf);
}
array_push(mm_profiles, ""+ New Profile"");
";

string STEP_GML = @"
var _cancel = button2_p();
if (!_cancel)
{
    if (keyboard_check_pressed(vk_escape))
    {
        _cancel = true;
    }
    else if (instance_exists(obj_gamecontroller))
    {
        var _gp = obj_gamecontroller.gamepad_id;
        if (gamepad_button_check_pressed(_gp, gp_face2) || gamepad_button_check_pressed(_gp, global.button1))
            _cancel = true;
    }
}

if (mm_state == ""select"")
{
    if (up_p())
    {
        mm_index = ((mm_index - 1) + array_length(mm_names)) mod array_length(mm_names);
        audio_play_sound(snd_menumove, 50, 0);
    }
    if (down_p())
    {
        mm_index = (mm_index + 1) mod array_length(mm_names);
        audio_play_sound(snd_menumove, 50, 0);
    }

    if (button1_p())
    {
        audio_play_sound(snd_select, 50, 0);
        // Remember the chosen mod, then move on to the save-profile picker.
        mm_mod     = (mm_index == 0) ? ""vanilla"" : mm_names[mm_index];
        mm_moddisp = mm_names[mm_index];
        mm_pidx = 0;
        mm_state = ""profile"";
    }
    else if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        // Cancel. The chapter-select is in a post-confirm state (input
        // disabled, scroll off, no native ""back""), so we reset it cleanly by
        // reloading the room -- the same mechanism the game uses elsewhere.
        global.modmenu_open = false;
        instance_destroy();
        room_restart();
    }
}
else if (mm_state == ""profile"")
{
    if (up_p())
    {
        mm_pidx = ((mm_pidx - 1) + array_length(mm_profiles)) mod array_length(mm_profiles);
        audio_play_sound(snd_menumove, 50, 0);
    }
    if (down_p())
    {
        mm_pidx = (mm_pidx + 1) mod array_length(mm_profiles);
        audio_play_sound(snd_menumove, 50, 0);
    }

    var _deletable = (mm_pidx > 0) && (mm_pidx < array_length(mm_profiles) - 1);
    var _delkey = keyboard_check_pressed(vk_delete);
    if (!_delkey && instance_exists(obj_gamecontroller))
    {
        var _gpd = obj_gamecontroller.gamepad_id;
        if (gamepad_button_check_pressed(_gpd, gp_face4))
            _delkey = true;
    }

    if (button1_p())
    {
        if (mm_pidx == array_length(mm_profiles) - 1)
        {
            // ""+ New Profile"" -> name it (prefilled with the mod name). Creating
            // does NOT launch; we return to the list afterwards.
            audio_play_sound(snd_menumove, 50, 0);
            mm_newname = mm_moddisp;
            keyboard_string = mm_moddisp;
            mm_state = ""naming"";
        }
        else
        {
            audio_play_sound(snd_select, 50, 0);
            var _prof = (mm_pidx == 0) ? """" : mm_profiles[mm_pidx];
            var _req  = string(mm_chapter) + ""|"" + mm_mod + ""|"" + _prof;
            var _wf = file_text_open_write(""mod_request.txt"");
            file_text_write_string(_wf, _req);
            file_text_close(_wf);
            mm_state = ""waiting"";
        }
    }
    else if (_delkey && _deletable)
    {
        audio_play_sound(snd_menumove, 50, 0);
        mm_target = mm_profiles[mm_pidx];
        mm_holdt  = 0;
        mm_state  = ""confirmdelete"";
    }
    else if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = ""select"";
    }
}
else if (mm_state == ""naming"")
{
    // Free text entry. keyboard_string tracks typed characters (incl. backspace).
    // Confirm is ENTER only and cancel is ESC only, so the Z/X keys used for
    // confirm/back elsewhere can be typed into the name normally.
    mm_newname = keyboard_string;

    if (keyboard_check_pressed(vk_enter))
    {
        var _clean = """";
        var _len = string_length(mm_newname);
        for (var _ci = 1; _ci <= _len; _ci++)
        {
            var _ch = string_char_at(mm_newname, _ci);
            var _o  = ord(_ch);
            var _ok = (_o >= 48 && _o <= 57) || (_o >= 65 && _o <= 90) ||
                      (_o >= 97 && _o <= 122) || _ch == ""-"" || _ch == ""_"" ||
                      (_ch == "" "" && string_length(_clean) > 0);
            if (_ok && string_length(_clean) < 24)
                _clean += _ch;
        }
        while (string_length(_clean) > 0 && string_char_at(_clean, string_length(_clean)) == "" "")
            _clean = string_copy(_clean, 1, string_length(_clean) - 1);

        if (string_length(_clean) > 0)
        {
            audio_play_sound(snd_select, 50, 0);
            // Create the (empty) profile folder via the loader; do NOT launch.
            mm_target = _clean;
            mm_wait   = ""create"";
            var _cf = file_text_open_write(""profile_cmd.txt"");
            file_text_write_string(_cf, ""create|"" + string(mm_chapter) + ""|"" + _clean);
            file_text_close(_cf);
            mm_state = ""profilewait"";
        }
    }
    else if (keyboard_check_pressed(vk_escape))
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = ""profile"";
    }
}
else if (mm_state == ""confirmdelete"")
{
    // Two-step safety: DELETE armed this; now HOLD confirm ~1s to actually delete.
    var _hold = keyboard_check(vk_enter) || keyboard_check(ord(""Z""));
    if (!_hold && instance_exists(obj_gamecontroller))
    {
        var _gph = obj_gamecontroller.gamepad_id;
        if (gamepad_button_check(_gph, gp_face1))
            _hold = true;
    }

    if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = ""profile"";
    }
    else if (_hold)
    {
        mm_holdt += delta_time / 1000;
        if (mm_holdt >= 3000)
        {
            // Heavy ""hit"": the Hub's slash sound, pitched down for impact.
            audio_play_sound(snd_swing, 100, false, 1.2, 0, 0.55);
            mm_wait = ""delete"";
            var _df = file_text_open_write(""profile_cmd.txt"");
            file_text_write_string(_df, ""delete|"" + string(mm_chapter) + ""|"" + mm_target);
            file_text_close(_df);
            mm_state = ""profilewait"";
        }
    }
    else
    {
        mm_holdt = 0;
    }
}
else if (mm_state == ""profilewait"")
{
    if (file_exists(""profile_ready.txt""))
    {
        if (file_exists(""profile_cmd.txt""))
            file_delete(""profile_cmd.txt"");
        file_delete(""profile_ready.txt"");

        mm_profiles = [""Standard-Saves""];
        if (file_exists(""profiles.txt""))
        {
            var _rf = file_text_open_read(""profiles.txt"");
            while (!file_text_eof(_rf))
            {
                var _rl = file_text_read_string(_rf);
                file_text_readln(_rf);
                var _rs = string_pos(""|"", _rl);
                if (_rs > 0)
                {
                    var _rn  = real(string_copy(_rl, 1, _rs - 1));
                    var _rnm = string_copy(_rl, _rs + 1, string_length(_rl) - _rs);
                    if (_rn == mm_chapter)
                        array_push(mm_profiles, _rnm);
                }
            }
            file_text_close(_rf);
        }
        array_push(mm_profiles, ""+ New Profile"");

        mm_pidx = 0;
        if (mm_wait == ""create"")
        {
            for (var _pi = 0; _pi < array_length(mm_profiles); _pi++)
            {
                if (mm_profiles[_pi] == mm_target)
                {
                    mm_pidx = _pi;
                    break;
                }
            }
        }
        if (mm_pidx > array_length(mm_profiles) - 1)
            mm_pidx = array_length(mm_profiles) - 1;

        mm_state = ""profile"";
    }
}
else if (mm_state == ""waiting"")
{
    if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        // Abort the wait -> same clean reset as cancel.
        if (file_exists(""mod_request.txt""))
            file_delete(""mod_request.txt"");
        global.modmenu_open = false;
        instance_destroy();
        room_restart();
    }
    else if (file_exists(""mod_ready.txt""))
    {
        if (file_exists(""mod_request.txt""))
            file_delete(""mod_request.txt"");
        file_delete(""mod_ready.txt"");

        mm_parent.modmenu_done = true;
        global.modmenu_open = false;

        with (mm_parent)
            show_transition();

        instance_destroy();
    }
}
";

string DRAW_GML = @"
var _gw = display_get_gui_width();
var _gh = display_get_gui_height();
var _cx = _gw * 0.5;

// Opaque backdrop so the chapter-select screen behind is fully hidden.
draw_set_alpha(1);
draw_set_color(c_black);
draw_rectangle(0, 0, _gw, _gh, false);

// === Ornate double frame: outer + inner border, corner brackets & accents ===
draw_set_color(c_white);
var _m = 14;
draw_rectangle(_m, _m, _gw - _m, _gh - _m, true);
draw_rectangle(_m + 6, _m + 6, _gw - _m - 6, _gh - _m - 6, true);
var _b = 24;
var _o = _m + 6;
draw_line_width(_o, _o, _o + _b, _o, 3);
draw_line_width(_o, _o, _o, _o + _b, 3);
draw_line_width(_gw - _o, _o, _gw - _o - _b, _o, 3);
draw_line_width(_gw - _o, _o, _gw - _o, _o + _b, 3);
draw_line_width(_o, _gh - _o, _o + _b, _gh - _o, 3);
draw_line_width(_o, _gh - _o, _o, _gh - _o - _b, 3);
draw_line_width(_gw - _o, _gh - _o, _gw - _o - _b, _gh - _o, 3);
draw_line_width(_gw - _o, _gh - _o, _gw - _o, _gh - _o - _b, 3);
var _cs = 3;
draw_rectangle(_m - _cs, _m - _cs, _m + _cs, _m + _cs, false);
draw_rectangle(_gw - _m - _cs, _m - _cs, _gw - _m + _cs, _m + _cs, false);
draw_rectangle(_m - _cs, _gh - _m - _cs, _m + _cs, _gh - _m + _cs, false);
draw_rectangle(_gw - _m - _cs, _gh - _m - _cs, _gw - _m + _cs, _gh - _m + _cs, false);

// Game menu font.
draw_set_font((global.lang == ""en"") ? 2 : 1);
draw_set_halign(fa_center);

// === Header: CHAPTER N with flanking rules + yellow diamond accents ===
var _hy = _gh * 0.12;
var _htxt = ""CHAPTER "" + string(mm_chapter);
draw_set_color(c_white);
draw_text(_cx, _hy, _htxt);
var _hw = string_width(_htxt) / 2;
var _ry = _hy + 13;
draw_line_width(_cx - _hw - 50, _ry, _cx - _hw - 14, _ry, 2);
draw_line_width(_cx + _hw + 14, _ry, _cx + _hw + 50, _ry, 2);
draw_set_color(c_yellow);
var _ds = 4;
var _lx = _cx - _hw - 56;
var _rx = _cx + _hw + 56;
draw_triangle(_lx, _ry - _ds, _lx + _ds, _ry, _lx, _ry + _ds, false);
draw_triangle(_lx, _ry - _ds, _lx - _ds, _ry, _lx, _ry + _ds, false);
draw_triangle(_rx, _ry - _ds, _rx + _ds, _ry, _rx, _ry + _ds, false);
draw_triangle(_rx, _ry - _ds, _rx - _ds, _ry, _rx, _ry + _ds, false);
draw_set_color(c_white);

if (mm_state == ""waiting"")
{
    draw_text(_cx, _gh * 0.45, ""Loading mod..."");
}
else if (mm_state == ""naming"")
{
    draw_text(_cx, _gh * 0.21, ""NAME NEW PROFILE"");
    draw_set_color(c_silver);
    draw_text(_cx, _gh * 0.27, ""Mod: "" + mm_moddisp);
    draw_set_color(c_white);

    // Text field with a blinking caret.
    var _disp = mm_newname;
    if ((current_time div 500) mod 2 == 0)
        _disp += ""|"";
    var _fw = 230;
    draw_set_color(c_yellow);
    draw_rectangle(_cx - _fw, _gh * 0.45 - 4, _cx + _fw, _gh * 0.45 + 26, true);
    draw_set_color(c_white);
    draw_text(_cx, _gh * 0.45, _disp);

    draw_text(_cx, _gh * 0.855, ""Type a name    [Enter] Create    [X]/[Esc] Back"");
}
else if (mm_state == ""confirmdelete"")
{
    var _prog = mm_holdt / 3000;
    if (_prog > 1) _prog = 1;

    var _mag = 10 * _prog;
    var _shx = random_range(-_mag, _mag);
    var _shy = random_range(-_mag, _mag);

    draw_set_color(c_red);
    draw_text(_cx + _shx, _gh * 0.21 + _shy, ""DELETE PROFILE"");
    draw_set_color(c_yellow);
    draw_text(_cx + _shx, _gh * 0.32 + _shy, mm_target);
    draw_set_color(c_white);
    draw_text(_cx + _shx, _gh * 0.40 + _shy, ""This erases its saves. This cannot be undone."");

    var _bw = 220;
    var _bx = _cx + _shx;
    var _by2 = _gh * 0.52 + _shy;
    draw_set_color(c_white);
    draw_rectangle(_bx - _bw, _by2 - 10, _bx + _bw, _by2 + 10, true);
    draw_set_color(c_red);
    draw_rectangle(_bx - _bw + 2, _by2 - 8, _bx - _bw + 2 + (2 * _bw - 4) * _prog, _by2 + 8, false);
    draw_set_color(c_white);

    draw_text(_cx, _gh * 0.855, ""HOLD [Z]/[A] to delete  (3s)     [X]/[Esc] Cancel"");
}
else if (mm_state == ""profilewait"")
{
    draw_text(_cx, _gh * 0.45, ""Please wait..."");
}
else
{
    // ""select"" (mod list) and ""profile"" (save-profile list) share one renderer.
    var _isprofile = (mm_state == ""profile"");
    var _list = _isprofile ? mm_profiles : mm_names;
    var _sel  = _isprofile ? mm_pidx     : mm_index;

    if (_isprofile)
    {
        draw_text(_cx, _gh * 0.21, ""SELECT SAVE PROFILE"");
        draw_set_color(c_silver);
        draw_text(_cx, _gh * 0.265, ""Mod: "" + mm_moddisp);
        draw_set_color(c_white);
    }
    else
    {
        draw_text(_cx, _gh * 0.21, ""SELECT MOD"");
    }

    // Scrolling list: only _maxvis rows visible, selection kept centered.
    var _total = array_length(_list);
    var _rowtop = _gh * (_isprofile ? 0.33 : 0.31);
    var _rowbot = _gh * 0.80;
    var _rowh = 32;
    var _maxvis = floor((_rowbot - _rowtop) / _rowh);
    if (_maxvis < 1)
        _maxvis = 1;

    var _first = 0;
    if (_total > _maxvis)
    {
        _first = _sel - floor(_maxvis / 2);
        if (_first < 0)
            _first = 0;
        if (_first > _total - _maxvis)
            _first = _total - _maxvis;
    }
    var _last = min(_total, _first + _maxvis);

    var _barw = 210;   // half-width of the selection bar
    for (var _i = _first; _i < _last; _i++)
    {
        var _yy = _rowtop + ((_i - _first) * _rowh);
        if (_i == _sel)
        {
            // yellow selection bar + red SOUL heart at its left
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _yy - 3, _cx + _barw, _yy + 24, true);
            draw_sprite_ext(spr_heart, 0, _cx - _barw + 12, _yy + 6, 1, 1, 0, c_white, 1);
            draw_set_color(c_yellow);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _yy, _list[_i]);
    }

    // Scroll arrows when there are more entries above/below the window.
    draw_set_color(c_white);
    if (_first > 0)
        draw_triangle(_cx - 9, _rowtop - 10, _cx + 9, _rowtop - 10, _cx, _rowtop - 22, false);
    if (_last < _total)
    {
        var _by = _rowtop + (_maxvis * _rowh) + 2;
        draw_triangle(_cx - 9, _by, _cx + 9, _by, _cx, _by + 12, false);
    }

    if (_isprofile && (_sel > 0) && (_sel < _total - 1))
        draw_text(_cx, _gh * 0.855, ""[Z] Confirm    [Del]/[Y] Delete    [X] Back"");
    else
        draw_text(_cx, _gh * 0.855, ""[Z]/[Enter] Confirm      [X]/[Shift] Back"");
}

// Credit line -- clear above the bottom frame.
draw_set_color(c_gray);
draw_text(_cx, _gh * 0.90, ""Mod-Selector v1.1.0 by Saloran26  -  made with Claude AI"");

draw_set_halign(fa_left);
draw_set_color(c_white);
";

// The HOOK is inserted right after `show_transition = function()\n{`.
string HOOK_GML = @"
    if (!variable_instance_exists(id, ""modmenu_done"") || !modmenu_done)
    {
        var _mm_count = 0;
        if (file_exists(""modlist.txt""))
        {
            var _mm_f = file_text_open_read(""modlist.txt"");
            while (!file_text_eof(_mm_f))
            {
                var _mm_line = file_text_read_string(_mm_f);
                file_text_readln(_mm_f);
                var _mm_sep = string_pos(""|"", _mm_line);
                if (_mm_sep > 0)
                {
                    if (real(string_copy(_mm_line, 1, _mm_sep - 1)) == _target_chapter)
                        _mm_count += 1;
                }
            }
            file_text_close(_mm_f);
        }

        if (_mm_count > 0)
        {
            global.modmenu_parent  = id;
            global.modmenu_chapter = _target_chapter;
            instance_create_depth(0, 0, -10000, obj_modmenu);
            exit;
        }
    }
";

string GUARD_GML = @"if (variable_global_exists(""modmenu_open"") && global.modmenu_open)
    exit;";

// Title-screen credit: one full-size line directly under "DELTARUNE v22" on the
// left (same left edge x+16, same color/alpha/font/scale already set above it).
string VERSION_ANCHOR = "draw_text_transformed(x + 16, y + 40, _version_text, _scale, _scale, 0);";
string VERSION_CREDIT = VERSION_ANCHOR + "\n"
    + "draw_text_transformed(x + 16, y + 56, \"Mod-Selector v1.1.0 by Saloran26\", _scale, _scale, 0);";

// ----------------------------------------------------------------------------
//  1) Create obj_modmenu and wire its three events.
//     EventHandlerFor(...) creates the event + its code entry and returns it.
// ----------------------------------------------------------------------------
var mm = new UndertaleGameObject();
mm.Name       = Data.Strings.MakeString("obj_modmenu");
mm.Persistent = false;   // recreated per launch; state lives in globals
mm.Visible    = true;    // required for the Draw GUI event to fire
Data.GameObjects.Add(mm);

UndertaleCode mmCreate = mm.EventHandlerFor(EventType.Create, Data);
UndertaleCode mmStep   = mm.EventHandlerFor(EventType.Step, EventSubtypeStep.Step, Data);
UndertaleCode mmDraw   = mm.EventHandlerFor(EventType.Draw, EventSubtypeDraw.DrawGUI, Data);

// ----------------------------------------------------------------------------
//  2) Compile all GML through one import group.
//     obj_modmenu already exists in Data.GameObjects, so the HOOK's reference to
//     it resolves at compile time.
// ----------------------------------------------------------------------------
CodeImportGroup importGroup = new(Data)
{
    ThrowOnNoOpFindReplace = true,   // fail loudly if the hook anchor is missing
    MainThreadAction = MainThreadAction
};

importGroup.QueueReplace(mmCreate, CREATE_GML);
importGroup.QueueReplace(mmStep,   STEP_GML);
importGroup.QueueReplace(mmDraw,   DRAW_GML);

// Hook: insert right after the opening brace of show_transition's body.
// (Regular C# string so \n is a real newline that matches the decompiler output.)
string hookAnchor = "show_transition = function()\n{";
importGroup.QueueFindReplace(createCode, hookAnchor, hookAnchor + "\n" + HOOK_GML);

// Guard: prepend to the REAL input objects so that while the mod menu is open,
// obj_ui_choice (confirm) and obj_ui_chapter (navigation) stop processing input.
// Guarding obj_CHAPTER_SELECT's Step would do nothing -- that event is inert.
string uiChoiceText  = GetDecompiledText(uiChoiceStep);
string uiChapterText = GetDecompiledText(uiChapterStep);
importGroup.QueueReplace(uiChoiceStep,  GUARD_GML + "\n" + uiChoiceText);
importGroup.QueueReplace(uiChapterStep, GUARD_GML + "\n" + uiChapterText);

// Title-screen credit: append a draw line under the version text in obj_ui_version.
importGroup.QueueFindReplace(uiVersionDraw, VERSION_ANCHOR, VERSION_CREDIT);

importGroup.Import();

ChangeSelection(mm);
ScriptMessage(
    "Mod menu injected successfully.\n\n" +
    "- obj_modmenu created (Create / Step / Draw GUI)\n" +
    "- show_transition hooked\n" +
    "- input guards added (obj_ui_choice + obj_ui_chapter)\n" +
    "- title-screen credit added (obj_ui_version)\n\n" +
    "Now: File -> Save As -> data_patched.win (do NOT overwrite the original yet).");


/* ============================================================================
   MANUAL FALLBACK  (do these by hand in the UTMT GUI if the script errored)
   ============================================================================

   The most likely failure points and their hand-fixes:

   (A) EventHandlerFor / EventSubtypeStep.Step overload not found on this build:
       - In the object list, right-click -> add a new object, name it exactly
         `obj_modmenu`, set Visible = checked, Persistent = unchecked.
       - Add three events: Create ; Step (Step) ; Draw (Draw GUI).
       - Paste the matching section from tools/mod_menu.gml into each event's
         code editor (SECTION 1 -> Create, SECTION 2 -> Step, SECTION 3 -> Draw
         GUI) and hit the compile/checkmark button.

   (B) Hook find/replace failed ("no-op" / anchor not found):
       - Open code entry `gml_Object_obj_CHAPTER_SELECT_Create_0`.
       - Find the lines:
             show_transition = function()
             {
       - Paste SECTION 4 (the HOOK snippet) from tools/mod_menu.gml on the line
         directly AFTER the `{`, so it is the first code inside the function body.
       - Compile. (Do NOT put it at the top of the Create event -- it must be
         INSIDE show_transition.)

   (C) Guard replace failed:
       - Open code entry `gml_Object_obj_ui_choice_Step_0` and paste SECTION 5
         (the GUARD snippet) at the very TOP, above the existing code. Compile.
       - Do the SAME for code entry `gml_Object_obj_ui_chapter_Step_0`.
       - (These are the objects that actually read button1_p / button2_p. Do NOT
         use obj_CHAPTER_SELECT_Step_0 -- that event is inert.)

   (D) Compiler complains about undefined variables (global.modmenu_open, etc.)
       on this build: they are plain dynamic vars and normally auto-register.
       If not, this UTMT version needs them declared first -- run once:
           Data.Variables.EnsureDefined("modmenu_open",   UndertaleInstruction.InstanceType.Self, false, Data.Strings, Data);
           Data.Variables.EnsureDefined("modmenu_parent", UndertaleInstruction.InstanceType.Self, false, Data.Strings, Data);
           Data.Variables.EnsureDefined("modmenu_chapter",UndertaleInstruction.InstanceType.Self, false, Data.Strings, Data);
           Data.Variables.EnsureDefined("modmenu_done",   UndertaleInstruction.InstanceType.Self, false, Data.Strings, Data);
       (pattern copied from MixMod.csx) then re-run the compile.

   (E) Draw GUI shows nothing / tiny text: the default font is being used. For an
       authentic look, add `draw_set_font(<a real hub font asset>);` at the top of
       the Draw GUI code -- pick a font name that exists in this data.win's Fonts
       list (e.g. one starting with `fnt_`).

   (F) Title-screen credit find/replace failed:
       - Open code entry `gml_Object_obj_ui_version_Draw_0`.
       - Find the line that draws the version:
             draw_text_transformed(x + 16, y + 40, _version_text, _scale, _scale, 0);
       - Paste this line directly AFTER it, then compile:
             draw_text_transformed(x + 16, y + 56, "Mod-Selector by Saloran26", _scale, _scale, 0);

   After any manual fix: File -> Save As -> data_patched.win.
   ============================================================================ */
