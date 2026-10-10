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

mm_autoupdate = false;
if (file_exists(""autoupdate.txt""))
{
    var _af = file_text_open_read(""autoupdate.txt"");
    var _av = file_text_read_string(_af);
    file_text_close(_af);
    if (string_length(_av) > 0 && string_char_at(_av, 1) == ""1"")
        mm_autoupdate = true;
}

mm_secret_str = """";
mm_anim_timer = 0;
mm_thesi_unlocked = false;
if (file_exists(""thesi_unlocked.txt""))
{
    var _tuf = file_text_open_read(""thesi_unlocked.txt"");
    var _tuv = file_text_read_string(_tuf);
    file_text_close(_tuf);
    mm_thesi_unlocked = (string_length(_tuv) > 0 && string_char_at(_tuv, 1) == ""1"");
}

mm_thesi_overlay = false;
if (mm_thesi_unlocked)
{
    if (file_exists(""thesi_overlay.txt""))
    {
        var _tof = file_text_open_read(""thesi_overlay.txt"");
        var _tov = file_text_read_string(_tof);
        file_text_close(_tof);
        mm_thesi_overlay = (string_length(_tov) > 0 && string_char_at(_tov, 1) == ""1"");
    }
    else
    {
        var _tof2 = file_text_open_write(""thesi_overlay.txt"");
        file_text_write_string(_tof2, ""1"");
        file_text_close(_tof2);
        mm_thesi_overlay = true;
    }
}
else
{
    var _tof3 = file_text_open_write(""thesi_overlay.txt"");
    file_text_write_string(_tof3, ""0"");
    file_text_close(_tof3);
}

mm_german_patch = true;
if (file_exists(""german_patch.txt""))
{
    var _gpf = file_text_open_read(""german_patch.txt"");
    var _gpv = file_text_read_string(_gpf);
    file_text_close(_gpf);
    mm_german_patch = (string_length(_gpv) > 0 && string_char_at(_gpv, 1) == ""1"");
}
else
{
    var _gpf2 = file_text_open_write(""german_patch.txt"");
    file_text_write_string(_gpf2, ""1"");
    file_text_close(_gpf2);
}

mm_menu_lang = ""en"";
if (file_exists(""menu_lang.txt""))
{
    var _mlf = file_text_open_read(""menu_lang.txt"");
    var _mlv = file_text_read_string(_mlf);
    file_text_close(_mlf);
    if (string_pos(""de"", string_lower(_mlv)) > 0)
        mm_menu_lang = ""de"";
    else
        mm_menu_lang = ""en"";
}
else
{
    var _mlf2 = file_text_open_write(""menu_lang.txt"");
    file_text_write_string(_mlf2, ""en"");
    file_text_close(_mlf2);
}
mm_sidx = 0;

mm_draw_soul = function(_hx, _hy)
{
    if (mm_thesi_overlay)
    {
        var _t_col = make_color_rgb(117, 250, 209);
        var _p_col = make_color_rgb(235, 0, 109);
        var _x0s = [1, 0, 0, 0, 0, 1, 2, 3, 4];
        var _x1s = [2, 3, 4, 4, 4, 4, 4, 4, 4];
        for (var _r = 0; _r < 9; _r++)
        {
            var _rx0 = _x0s[_r];
            var _rx1 = _x1s[_r];
            draw_set_color(_t_col);
            draw_rectangle(_hx + _rx0, _hy + _r, _hx + _rx1, _hy + _r, false);
            draw_set_color(_p_col);
            draw_rectangle(_hx + 9 - _rx1, _hy + _r, _hx + 9 - _rx0, _hy + _r, false);
        }
        draw_set_color(c_yellow);
    }
    else
    {
        draw_sprite_ext(spr_heart, 0, _hx, _hy, 1, 1, 0, c_white, 1);
        draw_set_color(c_yellow);
    }
};

mm_ps = false;
if (instance_exists(obj_gamecontroller))
{
    var _gpd2 = obj_gamecontroller.gamepad_id;
    if (gamepad_is_connected(_gpd2))
    {
        var _desc = string_lower(gamepad_get_description(_gpd2));
        if (string_pos(""playstation"", _desc) > 0 || string_pos(""sony"", _desc) > 0
            || string_pos(""dualshock"", _desc) > 0 || string_pos(""dualsense"", _desc) > 0
            || string_pos(""ps3"", _desc) > 0 || string_pos(""ps4"", _desc) > 0 || string_pos(""ps5"", _desc) > 0
            || string_pos(""wireless controller"", _desc) > 0)
            mm_ps = true;
    }
}
mm_b_confirm  = mm_ps ? ""Cross""    : ""A"";
mm_b_back     = mm_ps ? ""Circle""   : ""B"";
mm_b_delete   = mm_ps ? ""Triangle"" : ""Y"";
mm_b_toggle   = mm_ps ? ""Square""   : ""X"";
mm_b_settings = mm_ps ? ""Select""   : ""Back"";

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
            {
                var _is_thesi = (string_pos(""thesi"", string_lower(_nm)) > 0);
                if (!_is_thesi || mm_thesi_unlocked)
                    array_push(mm_names, _nm);
            }
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

// --- Secret Easter Egg Code: 'thesi' (or Triangle/Y x5 on controller) ---
if (!mm_thesi_unlocked && mm_state != ""naming"" && mm_state != ""thesi_anim"")
{
    var _hit_letter = """";
    if (keyboard_check_pressed(ord(""T""))) _hit_letter = ""t"";
    else if (keyboard_check_pressed(ord(""H""))) _hit_letter = ""h"";
    else if (keyboard_check_pressed(ord(""E""))) _hit_letter = ""e"";
    else if (keyboard_check_pressed(ord(""S""))) _hit_letter = ""s"";
    else if (keyboard_check_pressed(ord(""I""))) _hit_letter = ""i"";

    var _controller_hit = false;
    if (instance_exists(obj_gamecontroller))
    {
        var _gp_sec = obj_gamecontroller.gamepad_id;
        if (gamepad_button_check_pressed(_gp_sec, gp_face4))
            _controller_hit = true;
    }

    if (_hit_letter != """" || _controller_hit)
    {
        var _target_seq = ""thesi"";
        var _matched = false;
        if (_controller_hit)
        {
            mm_secret_str += ""c"";
            _matched = true;
            if (string_length(mm_secret_str) >= 5)
                _hit_letter = ""WIN"";
        }
        else
        {
            var _expected_char = string_char_at(_target_seq, string_length(mm_secret_str) + 1);
            if (_hit_letter == _expected_char)
            {
                mm_secret_str += _hit_letter;
                _matched = true;
                if (mm_secret_str == ""thesi"")
                    _hit_letter = ""WIN"";
            }
            else if (_hit_letter == ""t"")
            {
                mm_secret_str = ""t"";
                _matched = true;
            }
            else
            {
                mm_secret_str = """";
            }
        }

        if (_matched && _hit_letter != ""WIN"")
        {
            var _pitch = 0.9 + (string_length(mm_secret_str) * 0.15);
            audio_play_sound(snd_menumove, 60, false, 0.8, 0, _pitch);
        }

        if (_hit_letter == ""WIN"")
        {
            mm_secret_str = """";
            mm_thesi_unlocked = true;
            mm_thesi_overlay = true;
            var _tuf = file_text_open_write(""thesi_unlocked.txt"");
            file_text_write_string(_tuf, ""1"");
            file_text_close(_tuf);

            var _tof = file_text_open_write(""thesi_overlay.txt"");
            file_text_write_string(_tof, ""1"");
            file_text_close(_tof);

            mm_names = [""Vanilla""];
            if (file_exists(""modlist.txt""))
            {
                var _rf = file_text_open_read(""modlist.txt"");
                while (!file_text_eof(_rf))
                {
                    var _rl = file_text_read_string(_rf);
                    file_text_readln(_rf);
                    var _rsep = string_pos(""|"", _rl);
                    if (_rsep > 0)
                    {
                        var _rn  = real(string_copy(_rl, 1, _rsep - 1));
                        var _rnm = string_copy(_rl, _rsep + 1, string_length(_rl) - _rsep);
                        if (_rn == mm_chapter)
                            array_push(mm_names, _rnm);
                    }
                }
                file_text_close(_rf);
            }

            audio_play_sound(ch5_select, 100, false);
            audio_play_sound(snd_select, 100, false);
            mm_state = ""thesi_anim"";
            mm_anim_timer = 0;
        }
    }
}

if (mm_state == ""thesi_anim"")
{
    mm_anim_timer += 1;
    var _cont = button1_p() || keyboard_check_pressed(vk_space) || keyboard_check_pressed(vk_enter);
    if (!_cont && instance_exists(obj_gamecontroller))
    {
        var _gpc = obj_gamecontroller.gamepad_id;
        if (gamepad_button_check_pressed(_gpc, gp_face1))
            _cont = true;
    }
    if ((_cont && mm_anim_timer > 30) || mm_anim_timer > 360)
    {
        audio_play_sound(snd_select, 50, 0);
        mm_state = ""settings"";
        mm_sidx = 0;
    }
    exit;
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

    var _toggle = keyboard_check_pressed(ord(""U""));
    if (!_toggle && instance_exists(obj_gamecontroller))
    {
        var _gpt = obj_gamecontroller.gamepad_id;
        if (gamepad_button_check_pressed(_gpt, gp_face3))
            _toggle = true;
    }
    if (_toggle)
    {
        mm_autoupdate = !mm_autoupdate;
        audio_play_sound(snd_select, 50, 0);
        var _uf = file_text_open_write(""autoupdate.txt"");
        file_text_write_string(_uf, mm_autoupdate ? ""1"" : ""0"");
        file_text_close(_uf);
    }

    var _open_settings = (keyboard_check_pressed(ord(""S"")) && mm_secret_str != ""the"") || keyboard_check_pressed(vk_tab);
    if (!_open_settings && instance_exists(obj_gamecontroller))
    {
        var _gps = obj_gamecontroller.gamepad_id;
        if (gamepad_button_check_pressed(_gps, gp_select) || gamepad_button_check_pressed(_gps, gp_shoulderl))
            _open_settings = true;
    }
    if (_open_settings)
    {
        audio_play_sound(snd_select, 50, 0);
        mm_sidx = 0;
        mm_state = ""settings"";
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
else if (mm_state == ""settings"")
{
    var _s_count = mm_thesi_unlocked ? 7 : 5;
    if (up_p())
    {
        mm_sidx = ((mm_sidx - 1) + _s_count) mod _s_count;
        audio_play_sound(snd_menumove, 50, 0);
    }
    if (down_p())
    {
        mm_sidx = (mm_sidx + 1) mod _s_count;
        audio_play_sound(snd_menumove, 50, 0);
    }

    var _s_action = button1_p() || keyboard_check_pressed(vk_space) || keyboard_check_pressed(vk_enter);
    var _s_lr     = left_p() || right_p();

    if (_s_action || _s_lr)
    {
        if (mm_thesi_unlocked)
        {
            if (mm_sidx == 0)
            {
                mm_thesi_overlay = !mm_thesi_overlay;
                audio_play_sound(snd_select, 50, 0);
                var _tof = file_text_open_write(""thesi_overlay.txt"");
                file_text_write_string(_tof, mm_thesi_overlay ? ""1"" : ""0"");
                file_text_close(_tof);
            }
            else if (mm_sidx == 1)
            {
                mm_german_patch = !mm_german_patch;
                audio_play_sound(snd_select, 50, 0);
                var _gpf = file_text_open_write(""german_patch.txt"");
                file_text_write_string(_gpf, mm_german_patch ? ""1"" : ""0"");
                file_text_close(_gpf);
            }
            else if (mm_sidx == 2)
            {
                if (mm_menu_lang == ""en"")
                    mm_menu_lang = ""de"";
                else
                    mm_menu_lang = ""en"";
                audio_play_sound(snd_select, 50, 0);
                var _mlf = file_text_open_write(""menu_lang.txt"");
                file_text_write_string(_mlf, mm_menu_lang);
                file_text_close(_mlf);
            }
            else if (mm_sidx == 3)
            {
                mm_autoupdate = !mm_autoupdate;
                audio_play_sound(snd_select, 50, 0);
                var _uf = file_text_open_write(""autoupdate.txt"");
                file_text_write_string(_uf, mm_autoupdate ? ""1"" : ""0"");
                file_text_close(_uf);
            }
            else if (mm_sidx == 4)
            {
                if (_s_action)
                {
                    audio_play_sound(snd_swing, 50, 0);
                    mm_thesi_unlocked = false;
                    mm_thesi_overlay = false;
                    if (file_exists(""thesi_unlocked.txt""))
                        file_delete(""thesi_unlocked.txt"");
                    var _tof_l = file_text_open_write(""thesi_overlay.txt"");
                    file_text_write_string(_tof_l, ""0"");
                    file_text_close(_tof_l);

                    mm_names = [""Vanilla""];
                    if (file_exists(""modlist.txt""))
                    {
                        var _rf_l = file_text_open_read(""modlist.txt"");
                        while (!file_text_eof(_rf_l))
                        {
                            var _rl_l = file_text_read_string(_rf_l);
                            file_text_readln(_rf_l);
                            var _rsep_l = string_pos(""|"", _rl_l);
                            if (_rsep_l > 0)
                            {
                                var _rn_l  = real(string_copy(_rl_l, 1, _rsep_l - 1));
                                var _rnm_l = string_copy(_rl_l, _rsep_l + 1, string_length(_rl_l) - _rsep_l);
                                if (_rn_l == mm_chapter)
                                {
                                    var _is_th = (string_pos(""thesi"", string_lower(_rnm_l)) > 0);
                                    if (!_is_th)
                                        array_push(mm_names, _rnm_l);
                                }
                            }
                        }
                        file_text_close(_rf_l);
                    }
                    mm_sidx = 0;
                }
            }
            else if (mm_sidx == 5)
            {
                if (_s_action)
                {
                    audio_play_sound(snd_select, 50, 0);
                    mm_state = ""credits"";
                }
            }
            else if (mm_sidx == 6)
            {
                audio_play_sound(snd_swing, 50, 0);
                mm_state = ""select"";
            }
        }
        else
        {
            if (mm_sidx == 0)
            {
                mm_german_patch = !mm_german_patch;
                audio_play_sound(snd_select, 50, 0);
                var _gpf = file_text_open_write(""german_patch.txt"");
                file_text_write_string(_gpf, mm_german_patch ? ""1"" : ""0"");
                file_text_close(_gpf);
            }
            else if (mm_sidx == 1)
            {
                if (mm_menu_lang == ""en"")
                    mm_menu_lang = ""de"";
                else
                    mm_menu_lang = ""en"";
                audio_play_sound(snd_select, 50, 0);
                var _mlf = file_text_open_write(""menu_lang.txt"");
                file_text_write_string(_mlf, mm_menu_lang);
                file_text_close(_mlf);
            }
            else if (mm_sidx == 2)
            {
                mm_autoupdate = !mm_autoupdate;
                audio_play_sound(snd_select, 50, 0);
                var _uf = file_text_open_write(""autoupdate.txt"");
                file_text_write_string(_uf, mm_autoupdate ? ""1"" : ""0"");
                file_text_close(_uf);
            }
            else if (mm_sidx == 3)
            {
                if (_s_action)
                {
                    audio_play_sound(snd_select, 50, 0);
                    mm_state = ""credits"";
                }
            }
            else if (mm_sidx == 4)
            {
                audio_play_sound(snd_swing, 50, 0);
                mm_state = ""select"";
            }
        }
    }
    else if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = ""select"";
    }
}
else if (mm_state == ""credits"")
{
    if (button1_p() || button2_p() || _cancel || keyboard_check_pressed(vk_space) || keyboard_check_pressed(vk_enter))
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = ""settings"";
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
var _is_de = (mm_menu_lang == ""de"");
var _hy = _gh * 0.12;
var _htxt = (_is_de ? ""KAPITEL "" : ""CHAPTER "") + string(mm_chapter);
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
    draw_text(_cx, _gh * 0.45, _is_de ? ""Mod wird geladen..."" : ""Loading mod..."");
}
else if (mm_state == ""settings"")
{
    draw_text(_cx, _gh * 0.165, _is_de ? ""EINSTELLUNGEN"" : ""SETTINGS"");

    var _barw = 236;

    if (mm_thesi_unlocked)
    {
        var _s_y = _gh * 0.23;
        var _s_step = 38;

        // Setting 0: Thesi Kris Texturen
        var _y0 = _s_y;
        if (mm_sidx == 0)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y0 - 4, _cx + _barw, _y0 + 34, true);
            mm_draw_soul(_cx - _barw + 12, _y0 + 11);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _y0, _is_de ? (""Thesi-Kris Texturen:  "" + (mm_thesi_overlay ? ""[AKTIV]"" : ""[AUS]"")) : (""Thesi Kris Textures:  "" + (mm_thesi_overlay ? ""[ON]"" : ""[OFF]"")));
        draw_set_color(mm_thesi_overlay ? c_lime : c_dkgray);
        draw_text_transformed(_cx, _y0 + 18, _is_de ? ""Kris mit Thesi-Haaren auf Mods anwenden"" : ""Apply Kris with Thesi hair overlay to mods"", 0.7, 0.7, 0);

        // Setting 1: Deutsch-Patch
        var _y1 = _s_y + _s_step;
        if (mm_sidx == 1)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y1 - 4, _cx + _barw, _y1 + 34, true);
            mm_draw_soul(_cx - _barw + 12, _y1 + 11);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _y1, _is_de ? (""Deutsch-Patch (Spiel):  "" + (mm_german_patch ? ""[AKTIV]"" : ""[AUS]"")) : (""German Patch (Game):  "" + (mm_german_patch ? ""[ON]"" : ""[OFF]"")));
        draw_set_color(mm_german_patch ? c_lime : c_dkgray);
        draw_text_transformed(_cx, _y1 + 18, _is_de ? ""Deutsche Texte & Dialoge im Spiel aktivieren"" : ""Enable German translation and dialogue in-game"", 0.7, 0.7, 0);

        // Setting 2: Menu Language
        var _y2 = _s_y + _s_step * 2;
        if (mm_sidx == 2)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y2 - 4, _cx + _barw, _y2 + 34, true);
            mm_draw_soul(_cx - _barw + 12, _y2 + 11);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _y2, _is_de ? ""Menue-Sprache:  [Deutsch]"" : ""Menu Language:  [English]"");
        draw_set_color(c_aqua);
        draw_text_transformed(_cx, _y2 + 18, _is_de ? ""Sprache des Mod-Menues aendern"" : ""Switch Mod Selector language (English / Deutsch)"", 0.7, 0.7, 0);

        // Setting 3: Auto-Update
        var _y3 = _s_y + _s_step * 3;
        if (mm_sidx == 3)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y3 - 4, _cx + _barw, _y3 + 34, true);
            mm_draw_soul(_cx - _barw + 12, _y3 + 11);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _y3, _is_de ? (""Auto-Update:  "" + (mm_autoupdate ? ""[AKTIV]"" : ""[AUS]"")) : (""Auto-Update:  "" + (mm_autoupdate ? ""[ON]"" : ""[OFF]"")));
        draw_set_color(mm_autoupdate ? c_lime : c_dkgray);
        draw_text_transformed(_cx, _y3 + 18, _is_de ? ""Automatisch nach neuen Versionen suchen"" : ""Automatically check for new versions on GitHub"", 0.7, 0.7, 0);

        // Setting 4: Thesi-Modus sperren & zuruecksetzen
        var _y4 = _s_y + _s_step * 4;
        if (mm_sidx == 4)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y4 - 4, _cx + _barw, _y4 + 34, true);
            mm_draw_soul(_cx - _barw + 12, _y4 + 11);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _y4, _is_de ? ""Thesi-Modus sperren / zuruecksetzen"" : ""Lock & Reset Thesi Mode"");
        draw_set_color(c_orange);
        draw_text_transformed(_cx, _y4 + 18, _is_de ? ""Modus wieder sperren und als Secret verbergen"" : ""Relock Thesi mode and hide it as a secret again"", 0.7, 0.7, 0);

        // Setting 5: Credits & Contributors
        var _y5 = _s_y + _s_step * 5;
        if (mm_sidx == 5)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y5 - 4, _cx + _barw, _y5 + 34, true);
            mm_draw_soul(_cx - _barw + 12, _y5 + 11);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _y5, _is_de ? ""Credits & Danksagung  >"" : ""Credits & Contributors  >"");
        draw_set_color(c_aqua);
        draw_text_transformed(_cx, _y5 + 18, _is_de ? ""Urheber & Entstehung der Thesi-Sprites ansehen"" : ""View sprite origins and contributors"", 0.7, 0.7, 0);

        // Setting 6: Zurueck / Back
        var _y6 = _s_y + _s_step * 6;
        if (mm_sidx == 6)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y6 - 4, _cx + _barw, _y6 + 26, true);
            mm_draw_soul(_cx - _barw + 12, _y6 + 7);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _y6 + 2, _is_de ? ""< Zurueck"" : ""< Back"");
    }
    else
    {
        var _s_y = _gh * 0.26;
        var _s_step = 45;

        // LOCKED: Only 5 settings shown (0: German, 1: Lang, 2: Update, 3: Credits, 4: Back)
        // Setting 0: Deutsch-Patch
        var _y0 = _s_y;
        if (mm_sidx == 0)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y0 - 4, _cx + _barw, _y0 + 38, true);
            mm_draw_soul(_cx - _barw + 12, _y0 + 13);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _y0, _is_de ? (""Deutsch-Patch (Spiel):  "" + (mm_german_patch ? ""[AKTIV]"" : ""[AUS]"")) : (""German Patch (Game):  "" + (mm_german_patch ? ""[ON]"" : ""[OFF]"")));
        draw_set_color(mm_german_patch ? c_lime : c_dkgray);
        draw_text_transformed(_cx, _y0 + 20, _is_de ? ""Deutsche Texte & Dialoge im Spiel aktivieren"" : ""Enable German translation and dialogue in-game"", 0.7, 0.7, 0);

        // Setting 1: Menu Language
        var _y1 = _s_y + _s_step;
        if (mm_sidx == 1)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y1 - 4, _cx + _barw, _y1 + 38, true);
            mm_draw_soul(_cx - _barw + 12, _y1 + 13);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _y1, _is_de ? ""Menue-Sprache:  [Deutsch]"" : ""Menu Language:  [English]"");
        draw_set_color(c_aqua);
        draw_text_transformed(_cx, _y1 + 20, _is_de ? ""Sprache des Mod-Menues aendern"" : ""Switch Mod Selector language (English / Deutsch)"", 0.7, 0.7, 0);

        // Setting 2: Auto-Update
        var _y2 = _s_y + _s_step * 2;
        if (mm_sidx == 2)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y2 - 4, _cx + _barw, _y2 + 38, true);
            mm_draw_soul(_cx - _barw + 12, _y2 + 13);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _y2, _is_de ? (""Auto-Update:  "" + (mm_autoupdate ? ""[AKTIV]"" : ""[AUS]"")) : (""Auto-Update:  "" + (mm_autoupdate ? ""[ON]"" : ""[OFF]"")));
        draw_set_color(mm_autoupdate ? c_lime : c_dkgray);
        draw_text_transformed(_cx, _y2 + 20, _is_de ? ""Automatisch nach neuen Versionen suchen"" : ""Automatically check for new versions on GitHub"", 0.7, 0.7, 0);

        // Setting 3: Credits & Contributors
        var _y3 = _s_y + _s_step * 3;
        if (mm_sidx == 3)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y3 - 4, _cx + _barw, _y3 + 38, true);
            mm_draw_soul(_cx - _barw + 12, _y3 + 13);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _y3, _is_de ? ""Credits & Danksagung  >"" : ""Credits & Contributors  >"");
        draw_set_color(c_aqua);
        draw_text_transformed(_cx, _y3 + 20, _is_de ? ""Urheber & Entstehung ansehen"" : ""View creators and contributors"", 0.7, 0.7, 0);

        // Setting 4: Zurueck / Back
        var _y4 = _s_y + _s_step * 4;
        if (mm_sidx == 4)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y4 - 4, _cx + _barw, _y4 + 26, true);
            mm_draw_soul(_cx - _barw + 12, _y4 + 7);
        }
        else
        {
            draw_set_color(c_white);
        }
        draw_text(_cx, _y4 + 2, _is_de ? ""< Zurueck"" : ""< Back"");
    }

    draw_set_color(c_white);
    draw_text(_cx, _gh * 0.855, _is_de ? (""[Z]/("" + mm_b_confirm + "") Waehlen      [X]/("" + mm_b_back + "") Zurueck"") : (""[Z]/("" + mm_b_confirm + "") Select      [X]/("" + mm_b_back + "") Back""));
}
else if (mm_state == ""credits"")
{
    draw_text(_cx, _gh * 0.19, _is_de ? ""CREDITS & DANKSAGUNG"" : ""CREDITS & CONTRIBUTORS"");
    draw_set_color(c_yellow);
    if (mm_thesi_unlocked)
        draw_text(_cx, _gh * 0.245, ""DELTARUNE - Thesi Edition"");
    else
        draw_text(_cx, _gh * 0.245, ""DELTARUNE Mod-Selector"");

    if (mm_thesi_unlocked)
    {
        draw_set_color(c_aqua);
        draw_text(_cx, _gh * 0.325, _is_de ? ""Original Thesi-Sprites (Kapitel 1 & 2):"" : ""Original Thesi Sprites (Chapters 1 & 2):"");
        draw_set_color(c_white);
        draw_text(_cx, _gh * 0.37, ""KajaKay_"");
        draw_set_color(c_silver);
        draw_text_transformed(_cx, _gh * 0.41, _is_de ? ""Urheber der urspruenglichen Thesi Kris Sprites."" : ""Creator of the original Thesi Kris sprites."", 0.75, 0.75, 0);

        draw_set_color(c_aqua);
        draw_text(_cx, _gh * 0.485, _is_de ? ""Kapitel 3, 4 & 5 Sprites:"" : ""Chapters 3, 4 & 5 Sprites:"");
        draw_set_color(c_silver);
        draw_text_transformed(_cx, _gh * 0.53, _is_de ? ""Die restlichen Sprites wurden basierend auf den Originalen"" : ""Remaining sprites were reused from originals or"", 0.75, 0.75, 0);
        draw_text_transformed(_cx, _gh * 0.565, _is_de ? ""erneut benutzt oder mit KI-Unterstuetzung erstellt."" : ""generated with AI assistance."", 0.75, 0.75, 0);

        draw_set_color(c_aqua);
        draw_text(_cx, _gh * 0.64, _is_de ? ""Mod-Selector & Menue:"" : ""Mod Selector & Menu:"");
        draw_set_color(c_white);
        draw_text(_cx, _gh * 0.685, ""Saloran26"");
        draw_set_color(c_silver);
        draw_text_transformed(_cx, _gh * 0.725, _is_de ? ""Mod-Selector Menue & Speicherprofil-Verwaltung."" : ""Mod Selector menu & save profile manager."", 0.75, 0.75, 0);
    }
    else
    {
        draw_set_color(c_aqua);
        draw_text(_cx, _gh * 0.36, _is_de ? ""Mod-Selector & Menue:"" : ""Mod Selector & Menu:"");
        draw_set_color(c_white);
        draw_text(_cx, _gh * 0.41, ""Saloran26"");
        draw_set_color(c_silver);
        draw_text_transformed(_cx, _gh * 0.46, _is_de ? ""In-Game Mod-Selector Menue & Speicherprofil-Verwaltung."" : ""In-game Mod Selector menu & save profile manager."", 0.75, 0.75, 0);

        draw_set_color(c_aqua);
        draw_text(_cx, _gh * 0.54, _is_de ? ""Deltarune Deutsch-Patches:"" : ""Deltarune German Translations:"");
        draw_set_color(c_white);
        draw_text(_cx, _gh * 0.585, ""GermanRune Team"");
        draw_set_color(c_silver);
        draw_text_transformed(_cx, _gh * 0.63, _is_de ? ""Deutsche Uebersetzung fuer Deltarune Kapitel 1 - 5."" : ""German translation for Deltarune Chapters 1 - 5."", 0.75, 0.75, 0);

        draw_set_color(c_gray);
        draw_text_transformed(_cx, _gh * 0.725, _is_de ? ""* (Ein Name mit 5 Buchstaben schlaeft im Menue...)"" : ""* (A 5-letter name sleeps in the menu...)"", 0.72, 0.72, 0);
    }

    draw_set_color(c_dkgray);
    draw_text_transformed(_cx, _gh * 0.785, _is_de ? ""Patch-Tool: DeltaPatcher (xdelta)"" : ""Patch Tool: DeltaPatcher (xdelta)"", 0.7, 0.7, 0);

    draw_set_color(c_white);
    draw_text(_cx, _gh * 0.855, _is_de ? (""[Z]/("" + mm_b_confirm + "") oder [X]/("" + mm_b_back + "") Zurueck"") : (""[Z]/("" + mm_b_confirm + "") or [X]/("" + mm_b_back + "") Back""));
}
else if (mm_state == ""thesi_anim"")
{
    var _cy = _gh * 0.44;
    var _teal = make_color_rgb(117, 250, 209);
    var _pink = make_color_rgb(235, 0, 109);

    // Orbiting sparkle stars with bright cores
    for (var _p = 0; _p < 8; _p++)
    {
        var _ang = (mm_anim_timer * 0.05) + (_p * 0.785398);
        var _dist = 72 + sin(mm_anim_timer * 0.1 + _p) * 10;
        var _px = _cx + cos(_ang) * _dist;
        var _py = _cy + sin(_ang) * _dist;
        var _pcol = (_p mod 2 == 0) ? _teal : _pink;
        var _sl = 6 + sin(mm_anim_timer * 0.15 + _p) * 2;

        draw_set_color(_pcol);
        draw_line_width(_px - _sl, _py, _px + _sl, _py, 2);
        draw_line_width(_px, _py - _sl, _px, _py + _sl, 2);
        draw_set_color(c_white);
        draw_rectangle(_px - 1, _py - 1, _px + 1, _py + 1, false);
    }

    // Split SOUL in center (exact 16x16 Deltarune battle soul bitmap geometry)
    // Perfectly symmetric: left half TEAL, right half PINK, meets seamlessly at _cx!
    var _ps = 3.6 + sin(mm_anim_timer * 0.15) * 0.4;
    var _x0s = [2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, 4, 4, 6, 6];
    var _x1s = [4, 5, 6, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7];

    for (var _row = 0; _row < 16; _row++)
    {
        var _ry = _cy - (8 * _ps) + (_row * _ps);
        var _x0 = _x0s[_row];
        var _x1 = _x1s[_row];

        // Left half (Teal): from (_cx - (8 - _x0)*_ps) to (_cx - (8 - _x1 - 1)*_ps - 1)
        var _lx0 = _cx - (8 - _x0) * _ps;
        var _lx1 = _cx - (8 - _x1 - 1) * _ps - 1;
        draw_set_color(_teal);
        draw_rectangle(_lx0, _ry, _lx1, _ry + _ps - 1, false);

        // Right half (Pink): from (_cx + (7 - _x1)*_ps) to (_cx + (7 - _x0 + 1)*_ps - 1)
        var _rx0 = _cx + (7 - _x1) * _ps;
        var _rx1 = _cx + (7 - _x0 + 1) * _ps - 1;
        draw_set_color(_pink);
        draw_rectangle(_rx0, _ry, _rx1, _ry + _ps - 1, false);
    }

    // Text Header
    draw_set_font((global.lang == ""en"") ? 2 : 1);
    draw_set_halign(fa_center);

    draw_set_color(c_yellow);
    draw_text(_cx, _gh * 0.17, _is_de ? ""*  GEHEIMNIS ENTDECKT!  *"" : ""*  SECRET DISCOVERED!  *"");

    var _title_col = ((mm_anim_timer mod 20) < 10) ? _pink : _teal;
    draw_set_color(_title_col);
    draw_text_transformed(_cx, _gh * 0.24, _is_de ? ""THESI-MODUS FREIGESCHALTET!"" : ""THESI MODE UNLOCKED!"", 1.2, 1.2, 0);

    draw_set_color(c_white);
    draw_text_transformed(_cx, _gh * 0.65, _is_de ? ""Kris' Thesi-Haare sind nun im Mod-Menue freigeschaltet!"" : ""Kris' Thesi hair is now unlocked in the Mod Menu!"", 0.85, 0.85, 0);

    draw_set_color(c_silver);
    draw_text_transformed(_cx, _gh * 0.71, _is_de ? ""Die Option 'Thesi-Kris Texturen' ist ab jetzt in den Einstellungen sichtbar."" : ""The 'Thesi Kris Textures' option is now visible in Settings."", 0.75, 0.75, 0);

    if ((mm_anim_timer mod 40) < 25)
    {
        draw_set_color(c_yellow);
        draw_text(_cx, _gh * 0.84, _is_de ? ""[Z] / [Enter] / (A) Fortfahren"" : ""[Z] / [Enter] / (A) Continue"");
    }

    if (mm_anim_timer < 12)
    {
        draw_set_alpha((12 - mm_anim_timer) / 12);
        draw_set_color(c_white);
        draw_rectangle(0, 0, _gw, _gh, false);
        draw_set_alpha(1);
    }
}
else if (mm_state == ""naming"")
{
    draw_text(_cx, _gh * 0.21, _is_de ? ""NEUES PROFIL BENENNEN"" : ""NAME NEW PROFILE"");
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

    draw_text(_cx, _gh * 0.855, _is_de ? ""Name eingeben    [Enter] Erstellen    [Esc] Zurueck"" : ""Type a name    [Enter] Create    [Esc] Back"");
}
else if (mm_state == ""confirmdelete"")
{
    var _prog = mm_holdt / 3000;
    if (_prog > 1) _prog = 1;

    var _mag = 10 * _prog;
    var _shx = random_range(-_mag, _mag);
    var _shy = random_range(-_mag, _mag);

    draw_set_color(c_red);
    draw_text(_cx + _shx, _gh * 0.21 + _shy, _is_de ? ""PROFIL LOESCHEN"" : ""DELETE PROFILE"");
    draw_set_color(c_yellow);
    draw_text(_cx + _shx, _gh * 0.32 + _shy, mm_target);
    draw_set_color(c_white);
    draw_text(_cx + _shx, _gh * 0.40 + _shy, _is_de ? ""Loescht die Spielstaende unwiderruflich."" : ""This erases its saves. This cannot be undone."");

    var _bw = 220;
    var _bx = _cx + _shx;
    var _by2 = _gh * 0.52 + _shy;
    draw_set_color(c_white);
    draw_rectangle(_bx - _bw, _by2 - 10, _bx + _bw, _by2 + 10, true);
    draw_set_color(c_red);
    draw_rectangle(_bx - _bw + 2, _by2 - 8, _bx - _bw + 2 + (2 * _bw - 4) * _prog, _by2 + 8, false);
    draw_set_color(c_white);

    draw_text(_cx, _gh * 0.855, _is_de ? (""HALTEN [Z]/("" + mm_b_confirm + "") zum Loeschen (3s)     [X]/("" + mm_b_back + "") Abbrechen"") : (""HOLD [Z]/("" + mm_b_confirm + "") to delete  (3s)     [X]/("" + mm_b_back + "") Cancel""));
}
else if (mm_state == ""profilewait"")
{
    draw_text(_cx, _gh * 0.45, _is_de ? ""Bitte warten..."" : ""Please wait..."");
}
else
{
    // ""select"" (mod list) and ""profile"" (save-profile list) share one renderer.
    var _isprofile = (mm_state == ""profile"");
    var _list = _isprofile ? mm_profiles : mm_names;
    var _sel  = _isprofile ? mm_pidx     : mm_index;

    if (_isprofile)
    {
        draw_text(_cx, _gh * 0.21, _is_de ? ""SPEICHERPROFIL WAEHLEN"" : ""SELECT SAVE PROFILE"");
        draw_set_color(c_silver);
        draw_text(_cx, _gh * 0.265, ""Mod: "" + mm_moddisp);
        draw_set_color(c_white);
    }
    else
    {
        draw_text(_cx, _gh * 0.21, _is_de ? ""MOD WAEHLEN"" : ""SELECT MOD"");
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
            mm_draw_soul(_cx - _barw + 12, _yy + 6);
        }
        else
        {
            draw_set_color(c_white);
        }
        var _entry_text = _list[_i];
        if (_isprofile)
        {
            if (_i == 0)
                _entry_text = _is_de ? ""Standard-Spielstaende"" : ""Standard-Saves"";
            else if (_i == array_length(mm_profiles) - 1)
                _entry_text = _is_de ? ""+ Neues Profil"" : ""+ New Profile"";
        }
        draw_text(_cx, _yy, _entry_text);
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
        draw_text(_cx, _gh * 0.855, _is_de ? (""[Z]/("" + mm_b_confirm + "") Bestaetigen   [Entf]/("" + mm_b_delete + "") Loeschen   [X]/("" + mm_b_back + "") Zurueck"") : (""[Z]/("" + mm_b_confirm + "") Confirm   [Del]/("" + mm_b_delete + "") Delete   [X]/("" + mm_b_back + "") Back""));
    else
        draw_text(_cx, _gh * 0.855, _is_de ? (""[Z]/("" + mm_b_confirm + "") Bestaetigen      [X]/("" + mm_b_back + "") Zurueck"") : (""[Z]/("" + mm_b_confirm + "") Confirm      [X]/("" + mm_b_back + "") Back""));

    if (!_isprofile)
    {
        draw_set_color(c_yellow);
        if (_is_de)
        {
            if (mm_thesi_unlocked)
                draw_text(_cx, _gh * 0.815, ""[S]/("" + mm_b_settings + "") Einstellungen   |   Thesi: "" + (mm_thesi_overlay ? ""[AN]"" : ""[AUS]"") + ""   Deutsch: "" + (mm_german_patch ? ""[AN]"" : ""[AUS]""));
            else
                draw_text(_cx, _gh * 0.815, ""[S]/("" + mm_b_settings + "") Einstellungen   |   Deutsch: "" + (mm_german_patch ? ""[AN]"" : ""[AUS]""));
        }
        else
        {
            if (mm_thesi_unlocked)
                draw_text(_cx, _gh * 0.815, ""[S]/("" + mm_b_settings + "") Settings   |   Thesi: "" + (mm_thesi_overlay ? ""[ON]"" : ""[OFF]"") + ""   German: "" + (mm_german_patch ? ""[ON]"" : ""[OFF]""));
            else
                draw_text(_cx, _gh * 0.815, ""[S]/("" + mm_b_settings + "") Settings   |   German: "" + (mm_german_patch ? ""[ON]"" : ""[OFF]""));
        }
        draw_set_color(c_white);
    }
}

if (mm_state != ""thesi_anim"")
{
    // Credit lines -- clear above the bottom frame.
    draw_set_color(c_gray);
    if (mm_thesi_unlocked)
        draw_text(_cx, _gh * 0.905, ""Mod-Selector v1.3.2 by Saloran26 & KajaKay_  -  Thesi Edition"");
    else
        draw_text(_cx, _gh * 0.905, ""Mod-Selector v1.3.2 by Saloran26"");
}

draw_set_halign(fa_left);
draw_set_color(c_white);
";

// The HOOK is inserted right after `show_transition = function()\n{`.
string HOOK_GML = @"
    if (!variable_instance_exists(id, ""modmenu_done"") || !modmenu_done)
    {
        // Always show the menu for a real chapter launch (1-5), even when no mod
        // is installed for that chapter -- so Vanilla, the save profiles and the
        // settings are always reachable. The menu reads modlist.txt itself and
        // simply shows only ""Vanilla"" when there are no mods for the chapter.
        if (_target_chapter >= 1 && _target_chapter <= 5)
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
    + "draw_text_transformed(x + 16, y + 56, \"Mod-Selector v1.3.2\", _scale, _scale, 0);";

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
