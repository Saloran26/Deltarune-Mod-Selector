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
//    * mod_request.txt   -> WE write it; one line "N|Name" or "N|vanilla".
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
mm_state   = "select";                // "select" -> "waiting"

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
        // Confirm -> write mod_request.txt, then wait for the loader.
        var _req = string(mm_chapter) + "|";
        if (mm_index == 0)
            _req += "vanilla";
        else
            _req += mm_names[mm_index];

        var _wf = file_text_open_write("mod_request.txt");
        file_text_write_string(_wf, _req);
        file_text_close(_wf);

        mm_state = "waiting";
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
else
{
    draw_text(_cx, _gh * 0.21, "SELECT MOD");

    // Scrolling list: only _maxvis rows visible, selection kept centered.
    var _total = array_length(mm_names);
    var _rowtop = _gh * 0.31;
    var _rowbot = _gh * 0.80;
    var _rowh = 32;
    var _maxvis = floor((_rowbot - _rowtop) / _rowh);
    if (_maxvis < 1)
        _maxvis = 1;

    var _first = 0;
    if (_total > _maxvis)
    {
        _first = mm_index - floor(_maxvis / 2);
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
        if (_i == mm_index)
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
        draw_text(_cx, _yy, mm_names[_i]);
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

    draw_text(_cx, _gh * 0.855, "[Z]/[Enter] Confirm      [X]/[Shift] Back");
}

// Credit line -- clear above the bottom frame.
draw_set_color(c_gray);
draw_text(_cx, _gh * 0.90, "Mod-Selector by Saloran26  -  made with Claude AI");

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
draw_text_transformed(x + 16, y + 56, "Mod-Selector by Saloran26", _scale, _scale, 0);
