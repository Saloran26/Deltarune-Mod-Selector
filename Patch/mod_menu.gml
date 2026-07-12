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
if (mm_state == "select")
{
    if (up_p())
        mm_index = ((mm_index - 1) + array_length(mm_names)) mod array_length(mm_names);
    if (down_p())
        mm_index = (mm_index + 1) mod array_length(mm_names);

    if (button1_p())
    {
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
    else if (button2_p())
    {
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
    if (button2_p())
    {
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

draw_set_halign(fa_center);
draw_set_color(c_white);
draw_text(_cx, _gh * 0.16, "CHAPTER " + string(mm_chapter));

if (mm_state == "waiting")
{
    draw_text(_cx, _gh * 0.45, "Loading mod...");
}
else
{
    draw_text(_cx, _gh * 0.27, "SELECT MOD");
    for (var _i = 0; _i < array_length(mm_names); _i++)
    {
        var _pre = "    ";
        if (_i == mm_index)
        {
            draw_set_color(c_yellow);
            _pre = "> ";
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, (_gh * 0.40) + (_i * 26), _pre + mm_names[_i]);
    }
    draw_set_color(c_white);
    draw_text(_cx, _gh * 0.88, "[Z]/[Enter] Confirm      [X]/[Shift] Back");
}

// Credit line -- always shown, subtle at the very bottom.
draw_set_color(c_gray);
draw_text(_cx, _gh * 0.965, "Mod-Selector by Saloran26  -  made with Claude AI");

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
