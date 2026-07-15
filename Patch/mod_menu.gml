// ============================================================================
//  DELTARUNE Mod-Selector -- In-Game Mod-Menu (GML truth source)
// ============================================================================
//  This file is the human-readable "source of truth" for all GML that gets
//  injected into the Hub `data.win` by `tools/InjectModMenu.csx`.
//
//  Target: DELTARUNE full release Hub (obj_CHAPTER_SELECT), GMS 2.3+ / LTS.
//  Injector: UndertaleModTool v0.9.1.1 (Underanalyzer compiler).
//
//  Loader contract (signal files live in %LOCALAPPDATA%\DELTARUNE, which is
//  exactly where GML bare filenames resolve -- see findings hub-chapter-select):
//    * modlist.txt      -> written by loader; lines "N|Name" (N = chapter int).
//    * profiles.txt      -> written by loader; lines "N|ProfileName" (save profiles
//                           that already exist for chapter N).
//    * mod_request.txt   -> WE write it; one line "N|Name|Profile". Name is a mod
//                           name or "vanilla"; Profile is empty for "Standard-Saves"
//                           (real saves, no swap) or a profile name to swap in.
//    * mod_ready.txt     -> loader writes "ok" when files are swapped in; we poll
//                           file_exists("mod_ready.txt").
//
//  All identifiers used below are confirmed against Export_Code/:
//    button1_p(), button2_p(), up_p(), down_p()   (GlobalScript inputs)
//    _target_chapter, show_transition()           (obj_CHAPTER_SELECT Create_0)
//  ...and standard GameMaker builtins (file_text_*, file_exists, file_delete,
//  instance_create_depth, instance_destroy, array_push, array_length, draw_*).
//
//  IMPORTANT GML ORDERING GOTCHA
//  -----------------------------
//  instance_create_depth() runs the target's Create event SYNCHRONOUSLY before
//  it returns. So `o.mm_chapter = ...` set on the returned id would happen AFTER
//  Create already ran -> mm_chapter would be undefined inside Create. Therefore
//  the HOOK passes the chapter/parent through GLOBALS set BEFORE creation, and
//  the Create event reads those globals. (Do NOT rely on post-create instance
//  assignment for values Create needs.)
// ============================================================================


// ============================================================================
//  SECTION 1 -- obj_modmenu : CREATE event
//  Code entry: gml_Object_obj_modmenu_Create_0
// ============================================================================
global.modmenu_open = true;
mm_parent  = global.modmenu_parent;   // the obj_CHAPTER_SELECT instance id
mm_chapter = global.modmenu_chapter;  // int chapter number chosen in the hub
mm_index   = 0;
// Flow of states:
//   "select"        -> pick a mod (or Vanilla)
//   "profile"       -> pick a save profile (Standard / existing / + new)
//   "naming"        -> type a name for a brand-new profile
//   "confirmdelete" -> hold-to-confirm deleting the highlighted profile
//   "profilewait"   -> wait for the loader to create/delete a profile folder
//   "waiting"       -> request written, waiting for the loader to swap files & launch
mm_state   = "select";
mm_mod     = "";       // mod chosen in "select" ("vanilla" or a mod name, for the request)
mm_moddisp = "";       // mod display label (index 0 -> "Vanilla"), used in UI + name suggestion
mm_pidx    = 0;        // index into mm_profiles while in "profile"
mm_newname = "";       // name being typed in "naming"
mm_target  = "";       // profile name being created/deleted (for the "profilewait" refresh)
mm_wait    = "";       // "create" or "delete" -> how "profilewait" should re-select afterwards
mm_holdt   = 0;        // accumulated hold time (ms) in "confirmdelete"

// mm_names = ["Vanilla", ...mods for this chapter]
mm_names = ["Vanilla"];

if (file_exists("modlist.txt"))
{
    var _f = file_text_open_read("modlist.txt");
    while (!file_text_eof(_f))
    {
        var _line = file_text_read_string(_f);
        file_text_readln(_f);
        var _sep = string_pos("|", _line);
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
// If modlist.txt is missing, mm_names stays ["Vanilla"] -> menu still usable.

// mm_profiles = ["Standard-Saves", ...saved profiles for this chapter, "+ New Profile"]
// "Standard-Saves" (index 0) plays on the real saves; the loader treats an empty
// profile field as "no swap". The last entry starts the name-entry flow.
mm_profiles = ["Standard-Saves"];
if (file_exists("profiles.txt"))
{
    var _pf = file_text_open_read("profiles.txt");
    while (!file_text_eof(_pf))
    {
        var _pl = file_text_read_string(_pf);
        file_text_readln(_pf);
        var _psep = string_pos("|", _pl);
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
array_push(mm_profiles, "+ New Profile");


// ============================================================================
//  SECTION 2 -- obj_modmenu : STEP event
//  Code entry: gml_Object_obj_modmenu_Step_0
// ============================================================================
// Robust "cancel / back" detection. button2_p() is the game's normal cancel
// (keyboard X / controller B via the input system). We ALSO poll the gamepad
// hardware directly and accept Escape, so back works with every controller /
// Steam Input setup regardless of context quirks.
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

if (mm_state == "select")
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
        mm_mod     = (mm_index == 0) ? "vanilla" : mm_names[mm_index];
        mm_moddisp = mm_names[mm_index];   // display label (index 0 -> "Vanilla")
        mm_pidx = 0;
        mm_state = "profile";
    }
    else if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        // Cancel. After confirming a chapter the select screen has input
        // disabled and no native "back", so we reset it cleanly by reloading
        // the room (the same mechanism the game uses elsewhere). This
        // guarantees a fully interactive chapter select again.
        global.modmenu_open = false;
        instance_destroy();
        room_restart();
    }
}
else if (mm_state == "profile")
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

    // A profile row (not "Standard-Saves" at 0, not "+ New Profile" at the end)
    // can be deleted. Deleting is deliberately two-step: press the DELETE key
    // (keyboard Del / controller Y) to arm, then HOLD confirm in "confirmdelete".
    var _deletable = (mm_pidx > 0) && (mm_pidx < array_length(mm_profiles) - 1);
    var _delkey = keyboard_check_pressed(vk_delete);
    if (!_delkey && instance_exists(obj_gamecontroller))
    {
        var _gpd = obj_gamecontroller.gamepad_id;
        if (gamepad_button_check_pressed(_gpd, gp_face4))   // Y / triangle
            _delkey = true;
    }

    if (button1_p())
    {
        if (mm_pidx == array_length(mm_profiles) - 1)
        {
            // "+ New Profile" -> type a name, pre-filled with the mod name as a
            // suggestion (editable). We do NOT launch here: creating returns to
            // this list so the profile can be reviewed / loaded / deleted.
            audio_play_sound(snd_menumove, 50, 0);
            mm_newname = mm_moddisp;
            keyboard_string = mm_moddisp;   // prefill the text field with the suggestion
            mm_state = "naming";
        }
        else
        {
            audio_play_sound(snd_select, 50, 0);
            // Profile index 0 == "Standard-Saves" -> empty profile field (no swap).
            var _prof = (mm_pidx == 0) ? "" : mm_profiles[mm_pidx];
            var _req  = string(mm_chapter) + "|" + mm_mod + "|" + _prof;
            var _wf = file_text_open_write("mod_request.txt");
            file_text_write_string(_wf, _req);
            file_text_close(_wf);
            mm_state = "waiting";
        }
    }
    else if (_delkey && _deletable)
    {
        audio_play_sound(snd_menumove, 50, 0);
        mm_target = mm_profiles[mm_pidx];   // remember which profile to delete
        mm_holdt  = 0;
        mm_state  = "confirmdelete";
    }
    else if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = "select";   // back to the mod list
    }
}
else if (mm_state == "naming")
{
    // Free text entry. keyboard_string tracks typed characters (incl. backspace).
    // Confirm is ENTER only and cancel is ESC only, so the letters Z/X used for
    // confirm/back elsewhere can be typed into the name normally.
    mm_newname = keyboard_string;

    if (keyboard_check_pressed(vk_enter))
    {
        // Sanitize to a safe folder name: keep letters/digits/space/-/_ only,
        // drop leading spaces, then trim trailing spaces and cap the length.
        var _clean = "";
        var _len = string_length(mm_newname);
        for (var _ci = 1; _ci <= _len; _ci++)
        {
            var _ch = string_char_at(mm_newname, _ci);
            var _o  = ord(_ch);
            var _ok = (_o >= 48 && _o <= 57) || (_o >= 65 && _o <= 90) ||
                      (_o >= 97 && _o <= 122) || _ch == "-" || _ch == "_" ||
                      (_ch == " " && string_length(_clean) > 0);
            if (_ok && string_length(_clean) < 24)
                _clean += _ch;
        }
        while (string_length(_clean) > 0 && string_char_at(_clean, string_length(_clean)) == " ")
            _clean = string_copy(_clean, 1, string_length(_clean) - 1);

        if (string_length(_clean) > 0)
        {
            audio_play_sound(snd_select, 50, 0);
            // Ask the loader to CREATE the (empty) profile folder. We do NOT
            // launch -- afterwards we return to the profile list with the new
            // profile selected, so the user chooses when to actually play it.
            mm_target = _clean;
            mm_wait   = "create";
            var _cf = file_text_open_write("profile_cmd.txt");
            file_text_write_string(_cf, "create|" + string(mm_chapter) + "|" + _clean);
            file_text_close(_cf);
            mm_state = "profilewait";
        }
        // Empty/invalid name -> stay in naming (nothing written).
    }
    else if (keyboard_check_pressed(vk_escape))
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = "profile";   // back to the profile list
    }
}
else if (mm_state == "confirmdelete")
{
    // Two-step safety: DELETE was already pressed to get here; now the confirm
    // button must be HELD ~1s (progress bar fills). Releasing resets the bar;
    // cancel (X / B / Esc) aborts entirely. Only then is the profile removed.
    var _hold = keyboard_check(vk_enter) || keyboard_check(ord("Z"));
    if (!_hold && instance_exists(obj_gamecontroller))
    {
        var _gph = obj_gamecontroller.gamepad_id;
        if (gamepad_button_check(_gph, gp_face1))   // A / cross held
            _hold = true;
    }

    if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = "profile";
    }
    else if (_hold)
    {
        mm_holdt += delta_time / 1000;   // delta_time is microseconds -> ms
        if (mm_holdt >= 3000)            // hold ~3 seconds to actually delete
        {
            // Heavy "hit": the Hub's slash sound, pitched down for impact.
            // (extended audio_play_sound: index, priority, loop, gain, offset, pitch)
            audio_play_sound(snd_swing, 100, false, 1.2, 0, 0.55);
            mm_wait = "delete";
            var _df = file_text_open_write("profile_cmd.txt");
            file_text_write_string(_df, "delete|" + string(mm_chapter) + "|" + mm_target);
            file_text_close(_df);
            mm_state = "profilewait";
        }
    }
    else
    {
        mm_holdt = 0;   // let go -> reset the bar
    }
}
else if (mm_state == "profilewait")
{
    // Loader is creating/deleting a profile folder. When it signals ready, rebuild
    // the profile list from the refreshed profiles.txt and return to "profile".
    if (file_exists("profile_ready.txt"))
    {
        if (file_exists("profile_cmd.txt"))
            file_delete("profile_cmd.txt");
        file_delete("profile_ready.txt");

        mm_profiles = ["Standard-Saves"];
        if (file_exists("profiles.txt"))
        {
            var _rf = file_text_open_read("profiles.txt");
            while (!file_text_eof(_rf))
            {
                var _rl = file_text_read_string(_rf);
                file_text_readln(_rf);
                var _rs = string_pos("|", _rl);
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
        array_push(mm_profiles, "+ New Profile");

        // Re-select sensibly: created -> highlight the new profile; deleted ->
        // keep the cursor in range.
        mm_pidx = 0;
        if (mm_wait == "create")
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

        mm_state = "profile";
    }
}
else if (mm_state == "waiting")
{
    if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        // Abort the wait (e.g. loader not responding) -> same clean reset.
        if (file_exists("mod_request.txt"))
            file_delete("mod_request.txt");
        global.modmenu_open = false;
        instance_destroy();
        room_restart();
    }
    else if (file_exists("mod_ready.txt"))
    {
        // Loader has swapped the files. Clean up the handshake...
        if (file_exists("mod_request.txt"))
            file_delete("mod_request.txt");
        file_delete("mod_ready.txt");

        // ...mark the parent so the re-entrant show_transition() runs the real
        // launch (see HOOK guard), then hand control back to the vanilla path.
        mm_parent.modmenu_done = true;
        global.modmenu_open = false;

        // `with` makes `self` = parent, so _target_chapter / id / modmenu_done
        // all resolve correctly inside show_transition().
        with (mm_parent)
            show_transition();

        instance_destroy();
    }
}


// ============================================================================
//  SECTION 3 -- obj_modmenu : DRAW GUI event
//  Code entry: gml_Object_obj_modmenu_Draw_64   (Draw GUI subtype)
//  Draws an OPAQUE backdrop over the whole GUI so the chapter-select screen
//  behind is hidden, then the menu. Uses display_get_gui_* so it fills
//  whatever the GUI layer size is.
// ============================================================================
var _gw = display_get_gui_width();
var _gh = display_get_gui_height();
var _cx = _gw * 0.5;

// Opaque backdrop -> hides the chapter-select screen behind the menu.
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
draw_set_font((global.lang == "en") ? 2 : 1);
draw_set_halign(fa_center);

// === Header: CHAPTER N with flanking rules + yellow diamond accents ===
var _hy = _gh * 0.12;
var _htxt = "CHAPTER " + string(mm_chapter);
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

if (mm_state == "waiting")
{
    draw_text(_cx, _gh * 0.45, "Loading mod...");
}
else if (mm_state == "naming")
{
    draw_text(_cx, _gh * 0.21, "NAME NEW PROFILE");
    draw_set_color(c_silver);
    draw_text(_cx, _gh * 0.27, "Mod: " + mm_moddisp);
    draw_set_color(c_white);

    // Text field with a blinking caret.
    var _disp = mm_newname;
    if ((current_time div 500) mod 2 == 0)
        _disp += "|";
    var _fw = 230;
    draw_set_color(c_yellow);
    draw_rectangle(_cx - _fw, _gh * 0.45 - 4, _cx + _fw, _gh * 0.45 + 26, true);
    draw_set_color(c_white);
    draw_text(_cx, _gh * 0.45, _disp);

    draw_text(_cx, _gh * 0.855, "Type a name    [Enter] Create    [X]/[Esc] Back");
}
else if (mm_state == "confirmdelete")
{
    var _prog = mm_holdt / 3000;
    if (_prog > 1) _prog = 1;

    // Screen-shake that intensifies as the hold fills.
    var _mag = 10 * _prog;
    var _shx = random_range(-_mag, _mag);
    var _shy = random_range(-_mag, _mag);

    draw_set_color(c_red);
    draw_text(_cx + _shx, _gh * 0.21 + _shy, "DELETE PROFILE");
    draw_set_color(c_yellow);
    draw_text(_cx + _shx, _gh * 0.32 + _shy, mm_target);
    draw_set_color(c_white);
    draw_text(_cx + _shx, _gh * 0.40 + _shy, "This erases its saves. This cannot be undone.");

    // Hold-to-confirm bar (also shaken).
    var _bw = 220;
    var _bx = _cx + _shx;
    var _by2 = _gh * 0.52 + _shy;
    draw_set_color(c_white);
    draw_rectangle(_bx - _bw, _by2 - 10, _bx + _bw, _by2 + 10, true);
    draw_set_color(c_red);
    draw_rectangle(_bx - _bw + 2, _by2 - 8, _bx - _bw + 2 + (2 * _bw - 4) * _prog, _by2 + 8, false);
    draw_set_color(c_white);

    draw_text(_cx, _gh * 0.855, "HOLD [Z]/[A] to delete  (3s)     [X]/[Esc] Cancel");
}
else if (mm_state == "profilewait")
{
    draw_text(_cx, _gh * 0.45, "Please wait...");
}
else
{
    // "select" (mod list) and "profile" (save-profile list) share one renderer.
    var _isprofile = (mm_state == "profile");
    var _list = _isprofile ? mm_profiles : mm_names;
    var _sel  = _isprofile ? mm_pidx     : mm_index;

    if (_isprofile)
    {
        draw_text(_cx, _gh * 0.21, "SELECT SAVE PROFILE");
        draw_set_color(c_silver);
        draw_text(_cx, _gh * 0.265, "Mod: " + mm_moddisp);
        draw_set_color(c_white);
    }
    else
    {
        draw_text(_cx, _gh * 0.21, "SELECT MOD");
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
        draw_text(_cx, _gh * 0.855, "[Z] Confirm    [Del]/[Y] Delete    [X] Back");
    else
        draw_text(_cx, _gh * 0.855, "[Z]/[Enter] Confirm      [X]/[Shift] Back");
}

// Credit line -- clear above the bottom frame.
draw_set_color(c_gray);
draw_text(_cx, _gh * 0.90, "Mod-Selector v1.1.0 by Saloran26  -  made with Claude AI");

draw_set_halign(fa_left);   // restore defaults for other draws
draw_set_color(c_white);


// ============================================================================
//  SECTION 4 -- HOOK snippet
//  Injected into gml_Object_obj_CHAPTER_SELECT_Create_0, IMMEDIATELY AFTER the
//  line pair:
//        show_transition = function()
//        {
//  (i.e. as the very first statements inside show_transition's body).
//
//  Behaviour: on first entry, if >=1 mod exists for _target_chapter, stash the
//  chapter + parent in globals, spawn obj_modmenu, and `exit` so the original
//  launch is aborted (the menu takes over). On the SECOND entry (after the menu
//  set modmenu_done = true) the guard is false, so the original launch runs and
//  loads whatever the loader swapped into chapterN_windows\data.win.
//
//  `modmenu_done` is read guarded by variable_instance_exists so referencing it
//  before it ever exists does not throw (short-circuit || ).
// ============================================================================
if (!variable_instance_exists(id, "modmenu_done") || !modmenu_done)
{
    var _mm_count = 0;
    if (file_exists("modlist.txt"))
    {
        var _mm_f = file_text_open_read("modlist.txt");
        while (!file_text_eof(_mm_f))
        {
            var _mm_line = file_text_read_string(_mm_f);
            file_text_readln(_mm_f);
            var _mm_sep = string_pos("|", _mm_line);
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
        exit;   // abort original launch; obj_modmenu will re-call us when ready
    }
}


// ============================================================================
//  SECTION 5 -- GUARD snippet
//  PREPENDED to BOTH gml_Object_obj_ui_choice_Step_0 (confirm / button1_p ->
//  select()) AND gml_Object_obj_ui_chapter_Step_0 (navigation / button2_p).
//  These are the objects that ACTUALLY read input during chapter select.
//  NOTE: obj_CHAPTER_SELECT's own Step event is inert (its whole body is just
//  `exit;`), so a guard there would block nothing -- do not use it.
//  While the mod menu is open, this stops those widgets from re-triggering the
//  launch on the same button press (which would otherwise spawn a duplicate
//  obj_modmenu / bleed input through).
// ============================================================================
if (variable_global_exists("modmenu_open") && global.modmenu_open)
    exit;


// ============================================================================
//  SECTION 6 -- TITLE-SCREEN CREDIT
//  Injected into gml_Object_obj_ui_version_Draw_0, right AFTER the line that
//  draws the version ("DELTARUNE v22"). Draws one full-size credit line directly
//  under the version text (same left edge x+16), matching the footer's
//  color/alpha/font/scale (already set above the anchor line).
//  Anchor line to insert after:
//      draw_text_transformed(x + 16, y + 40, _version_text, _scale, _scale, 0);
// ============================================================================
draw_text_transformed(x + 16, y + 56, "Mod-Selector v1.1.0 by Saloran26", _scale, _scale, 0);
