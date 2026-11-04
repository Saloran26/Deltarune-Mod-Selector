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
using System.IO;
using System.Collections.Generic;
using UndertaleModLib;
using UndertaleModLib.Models;
using UndertaleModLib.Compiler;
using UndertaleModLib.Util;
using ImageMagick;

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
mm_credits_scroll = 0;
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

mm_fps_mode = ""off"";
if (file_exists(""fps_mode.txt""))
{
    var _fmf = file_text_open_read(""fps_mode.txt"");
    var _fmv = string_lower(file_text_read_string(_fmf));
    file_text_close(_fmf);
    if (string_pos(""lossless"", _fmv) > 0)
        mm_fps_mode = ""lossless"";
    else if (string_pos(""engine"", _fmv) > 0)
        mm_fps_mode = ""engine"";
    else
        mm_fps_mode = ""off"";
}
else if (file_exists(""fps60_patch.txt""))
{
    var _fpf_legacy = file_text_open_read(""fps60_patch.txt"");
    var _fpv_legacy = file_text_read_string(_fpf_legacy);
    file_text_close(_fpf_legacy);
    if (string_length(_fpv_legacy) > 0 && string_char_at(_fpv_legacy, 1) == ""1"")
        mm_fps_mode = ""engine"";
    else
        mm_fps_mode = ""off"";
    var _fmf2 = file_text_open_write(""fps_mode.txt"");
    file_text_write_string(_fmf2, mm_fps_mode);
    file_text_close(_fmf2);
}
else
{
    var _fmf3 = file_text_open_write(""fps_mode.txt"");
    file_text_write_string(_fmf3, ""off"");
    file_text_close(_fmf3);
}

mm_fps60_patch = (mm_fps_mode == ""engine"");

mm_lsfg_fps = ""60"";
if (file_exists(""lsfg_fps.txt""))
{
    var _lsf = file_text_open_read(""lsfg_fps.txt"");
    var _lsv = file_text_read_string(_lsf);
    file_text_close(_lsf);
    if (string_pos(""240"", _lsv) > 0)
        mm_lsfg_fps = ""240"";
    else if (string_pos(""144"", _lsv) > 0)
        mm_lsfg_fps = ""144"";
    else if (string_pos(""120"", _lsv) > 0)
        mm_lsfg_fps = ""120"";
    else if (string_pos(""90"", _lsv) > 0)
        mm_lsfg_fps = ""90"";
    else
        mm_lsfg_fps = ""60"";
}
else
{
    var _lsf2 = file_text_open_write(""lsfg_fps.txt"");
    file_text_write_string(_lsf2, ""60"");
    file_text_close(_lsf2);
}

mm_lsfg_fps_list = [""60"", ""90"", ""120"", ""144"", ""240""];
mm_lsfg_fps_idx = 0;
for (var _lsi = 0; _lsi < array_length(mm_lsfg_fps_list); _lsi++)
{
    if (mm_lsfg_fps_list[_lsi] == mm_lsfg_fps)
    {
        mm_lsfg_fps_idx = _lsi;
        break;
    }
}

mm_lsfg_mode = ""balanced"";
if (file_exists(""lsfg_mode.txt""))
{
    var _lsmf = file_text_open_read(""lsfg_mode.txt"");
    var _lsmv = string_lower(file_text_read_string(_lsmf));
    file_text_close(_lsmf);
    if (string_pos(""performance"", _lsmv) > 0)
        mm_lsfg_mode = ""performance"";
    else
        mm_lsfg_mode = ""balanced"";
}
else
{
    var _lsmf2 = file_text_open_write(""lsfg_mode.txt"");
    file_text_write_string(_lsmf2, ""balanced"");
    file_text_close(_lsmf2);
}
mm_lsfg_mode_list = [""balanced"", ""performance""];
mm_lsfg_mode_idx = (mm_lsfg_mode == ""performance"") ? 1 : 0;

mm_lsfg_flow = ""100"";
if (file_exists(""lsfg_flow.txt""))
{
    var _lsff = file_text_open_read(""lsfg_flow.txt"");
    var _lsfv = file_text_read_string(_lsff);
    file_text_close(_lsff);
    if (string_pos(""50"", _lsfv) > 0)
        mm_lsfg_flow = ""50"";
    else if (string_pos(""75"", _lsfv) > 0)
        mm_lsfg_flow = ""75"";
    else
        mm_lsfg_flow = ""100"";
}
else
{
    var _lsff2 = file_text_open_write(""lsfg_flow.txt"");
    file_text_write_string(_lsff2, ""100"");
    file_text_close(_lsff2);
}
mm_lsfg_flow_list = [""100"", ""75"", ""50""];
mm_lsfg_flow_idx = 0;
for (var _lfi = 0; _lfi < array_length(mm_lsfg_flow_list); _lfi++)
{
    if (mm_lsfg_flow_list[_lfi] == mm_lsfg_flow)
    {
        mm_lsfg_flow_idx = _lfi;
        break;
    }
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
mm_sub_idx = 0;
mm_tools_op = """";
mm_tools_status = """";
mm_tools_result1 = """";
mm_tools_result2 = """";
mm_tools_result3 = """";
mm_tools_timer = 0;

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
mm_b_settings = mm_ps ? ""Options""  : ""Menu"";

mm_names = [""Vanilla""];

mm_load_modlist = function()
{
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
};
mm_load_modlist();

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
var _is_de = (mm_menu_lang == ""de"");
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
        if (gamepad_button_check_pressed(_gp_sec, gp_stickr) || gamepad_button_check_pressed(_gp_sec, gp_stickl))
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

    var _open_settings = (keyboard_check_pressed(ord(""S"")) && mm_secret_str != ""the"") || keyboard_check_pressed(ord(""C"")) || keyboard_check_pressed(vk_tab) || button3_p();
    if (!_open_settings)
    {
        for (var _gpi = 0; _gpi < 8; _gpi++)
        {
            if (gamepad_is_connected(_gpi))
            {
                if (gamepad_button_check_pressed(_gpi, gp_start) || gamepad_button_check_pressed(_gpi, gp_face4) || gamepad_button_check_pressed(_gpi, gp_select) || gamepad_button_check_pressed(_gpi, gp_shoulderl))
                {
                    _open_settings = true;
                    break;
                }
            }
        }
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
    var _cat_count = 5;
    if (up_p())
    {
        mm_sidx = ((mm_sidx - 1) + _cat_count) mod _cat_count;
        audio_play_sound(snd_menumove, 50, 0);
    }
    if (down_p())
    {
        mm_sidx = (mm_sidx + 1) mod _cat_count;
        audio_play_sound(snd_menumove, 50, 0);
    }

    var _s_action = button1_p() || keyboard_check_pressed(vk_space) || keyboard_check_pressed(vk_enter);
    if (_s_action || right_p())
    {
        audio_play_sound(snd_select, 50, 0);
        if (mm_sidx == 0)
        {
            mm_state = ""settings_gfx"";
            mm_sub_idx = 0;
        }
        else if (mm_sidx == 1)
        {
            mm_state = ""settings_lang"";
            mm_sub_idx = 0;
        }
        else if (mm_sidx == 2)
        {
            mm_state = ""settings_mods"";
            mm_sub_idx = 0;
        }
        else if (mm_sidx == 3)
        {
            mm_state = ""credits"";
            mm_credits_scroll = 0;
        }
        else if (mm_sidx == 4)
        {
            audio_play_sound(snd_swing, 50, 0);
            mm_state = ""select"";
        }
    }
    else if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = ""select"";
    }
}
else if (mm_state == ""settings_gfx"")
{
    var _has_lsfg = (mm_fps_mode == ""lossless"");
    var _count = mm_thesi_unlocked ? (_has_lsfg ? 7 : 4) : (_has_lsfg ? 5 : 2);
    if (up_p())
    {
        mm_sub_idx = ((mm_sub_idx - 1) + _count) mod _count;
        audio_play_sound(snd_menumove, 50, 0);
    }
    if (down_p())
    {
        mm_sub_idx = (mm_sub_idx + 1) mod _count;
        audio_play_sound(snd_menumove, 50, 0);
    }

    var _act = button1_p() || keyboard_check_pressed(vk_space) || keyboard_check_pressed(vk_enter);
    var _lr  = left_p() || right_p();

    if (_act || _lr)
    {
        if (mm_sub_idx == 0)
        {
            var _dir = left_p() ? -1 : 1;
            audio_play_sound(snd_select, 50, 0);
            if (mm_fps_mode == ""off"")
                mm_fps_mode = (_dir > 0) ? ""engine"" : ""lossless"";
            else if (mm_fps_mode == ""engine"")
                mm_fps_mode = (_dir > 0) ? ""lossless"" : ""off"";
            else
                mm_fps_mode = (_dir > 0) ? ""off"" : ""engine"";

            mm_fps60_patch = (mm_fps_mode == ""engine"");
            var _fmf = file_text_open_write(""fps_mode.txt"");
            file_text_write_string(_fmf, mm_fps_mode);
            file_text_close(_fmf);
            var _fpf = file_text_open_write(""fps60_patch.txt"");
            file_text_write_string(_fpf, mm_fps60_patch ? ""1"" : ""0"");
            file_text_close(_fpf);
        }
        else if (_has_lsfg && mm_sub_idx == 1)
        {
            var _dir2 = left_p() ? -1 : 1;
            audio_play_sound(snd_select, 50, 0);
            var _fps_len = array_length(mm_lsfg_fps_list);
            if (_dir2 > 0)
                mm_lsfg_fps_idx = (mm_lsfg_fps_idx + 1) mod _fps_len;
            else
                mm_lsfg_fps_idx = ((mm_lsfg_fps_idx - 1) + _fps_len) mod _fps_len;
            mm_lsfg_fps = mm_lsfg_fps_list[mm_lsfg_fps_idx];
            var _lsf = file_text_open_write(""lsfg_fps.txt"");
            file_text_write_string(_lsf, mm_lsfg_fps);
            file_text_close(_lsf);
        }
        else if (_has_lsfg && mm_sub_idx == 2)
        {
            var _dir_m = left_p() ? -1 : 1;
            audio_play_sound(snd_select, 50, 0);
            var _mod_len = array_length(mm_lsfg_mode_list);
            if (_dir_m > 0)
                mm_lsfg_mode_idx = (mm_lsfg_mode_idx + 1) mod _mod_len;
            else
                mm_lsfg_mode_idx = ((mm_lsfg_mode_idx - 1) + _mod_len) mod _mod_len;
            mm_lsfg_mode = mm_lsfg_mode_list[mm_lsfg_mode_idx];
            var _lsmf = file_text_open_write(""lsfg_mode.txt"");
            file_text_write_string(_lsmf, mm_lsfg_mode);
            file_text_close(_lsmf);
        }
        else if (_has_lsfg && mm_sub_idx == 3)
        {
            var _dir_f = left_p() ? -1 : 1;
            audio_play_sound(snd_select, 50, 0);
            var _flow_len = array_length(mm_lsfg_flow_list);
            if (_dir_f > 0)
                mm_lsfg_flow_idx = (mm_lsfg_flow_idx + 1) mod _flow_len;
            else
                mm_lsfg_flow_idx = ((mm_lsfg_flow_idx - 1) + _flow_len) mod _flow_len;
            mm_lsfg_flow = mm_lsfg_flow_list[mm_lsfg_flow_idx];
            var _lsff = file_text_open_write(""lsfg_flow.txt"");
            file_text_write_string(_lsff, mm_lsfg_flow);
            file_text_close(_lsff);
        }
        else
        {
            var _eff_idx = _has_lsfg ? (mm_sub_idx - 3) : mm_sub_idx;
            if (mm_thesi_unlocked && _eff_idx == 1)
            {
                mm_thesi_overlay = !mm_thesi_overlay;
                audio_play_sound(snd_select, 50, 0);
                var _tof = file_text_open_write(""thesi_overlay.txt"");
                file_text_write_string(_tof, mm_thesi_overlay ? ""1"" : ""0"");
                file_text_close(_tof);
            }
            else if (mm_thesi_unlocked && _eff_idx == 2)
            {
                if (_act)
                {
                    audio_play_sound(snd_swing, 50, 0);
                    mm_thesi_unlocked = false;
                    mm_thesi_overlay = false;
                    if (file_exists(""thesi_unlocked.txt""))
                        file_delete(""thesi_unlocked.txt"");
                    var _tof_l = file_text_open_write(""thesi_overlay.txt"");
                    file_text_write_string(_tof_l, ""0"");
                    file_text_close(_tof_l);
                    mm_load_modlist();
                    mm_sub_idx = 0;
                    mm_sidx = 0;
                    mm_state = ""settings"";
                }
            }
            else if ((mm_thesi_unlocked && _eff_idx == 3) || (!mm_thesi_unlocked && _eff_idx == 1))
            {
                audio_play_sound(snd_swing, 50, 0);
                mm_state = ""settings"";
            }
        }
    }
    else if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = ""settings"";
    }
}
else if (mm_state == ""settings_lang"")
{
    var _count = 3;
    if (up_p())
    {
        mm_sub_idx = ((mm_sub_idx - 1) + _count) mod _count;
        audio_play_sound(snd_menumove, 50, 0);
    }
    if (down_p())
    {
        mm_sub_idx = (mm_sub_idx + 1) mod _count;
        audio_play_sound(snd_menumove, 50, 0);
    }

    var _act = button1_p() || keyboard_check_pressed(vk_space) || keyboard_check_pressed(vk_enter);
    var _lr  = left_p() || right_p();

    if (_act || _lr)
    {
        if (mm_sub_idx == 0)
        {
            mm_german_patch = !mm_german_patch;
            audio_play_sound(snd_select, 50, 0);
            var _gpf = file_text_open_write(""german_patch.txt"");
            file_text_write_string(_gpf, mm_german_patch ? ""1"" : ""0"");
            file_text_close(_gpf);
        }
        else if (mm_sub_idx == 1)
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
        else if (mm_sub_idx == 2)
        {
            audio_play_sound(snd_swing, 50, 0);
            mm_state = ""settings"";
        }
    }
    else if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = ""settings"";
    }
}
else if (mm_state == ""settings_mods"")
{
    var _count = 5;
    if (up_p())
    {
        mm_sub_idx = ((mm_sub_idx - 1) + _count) mod _count;
        audio_play_sound(snd_menumove, 50, 0);
    }
    if (down_p())
    {
        mm_sub_idx = (mm_sub_idx + 1) mod _count;
        audio_play_sound(snd_menumove, 50, 0);
    }

    var _act = button1_p() || keyboard_check_pressed(vk_space) || keyboard_check_pressed(vk_enter);
    var _lr  = left_p() || right_p();

    if (_act || _lr)
    {
        if (mm_sub_idx == 0)
        {
            if (_act)
            {
                audio_play_sound(snd_select, 50, 0);
                var _ucf = file_text_open_write(""mod_tools_cmd.txt"");
                file_text_write_string(_ucf, ""check_updates"");
                file_text_close(_ucf);
                mm_tools_op = ""check_updates"";
                mm_tools_status = _is_de ? ""Pruefe GameBanana auf Updates..."" : ""Checking GameBanana for updates..."";
                mm_tools_result1 = """";
                mm_tools_result2 = """";
                mm_tools_result3 = """";
                mm_tools_timer = 0;
                mm_state = ""modtools_wait"";
            }
        }
        else if (mm_sub_idx == 1)
        {
            if (_act)
            {
                audio_play_sound(snd_select, 50, 0);
                var _icf = file_text_open_write(""mod_tools_cmd.txt"");
                file_text_write_string(_icf, ""auto_import"");
                file_text_close(_icf);
                mm_tools_op = ""auto_import"";
                mm_tools_status = _is_de ? ""Verarbeite Ordner mods\_import..."" : ""Processing mods\_import folder..."";
                mm_tools_result1 = """";
                mm_tools_result2 = """";
                mm_tools_result3 = """";
                mm_tools_timer = 0;
                mm_state = ""modtools_wait"";
            }
        }
        else if (mm_sub_idx == 2)
        {
            if (_act)
            {
                audio_play_sound(snd_select, 50, 0);
                var _scf = file_text_open_write(""mod_tools_cmd.txt"");
                file_text_write_string(_scf, ""scan_lss"");
                file_text_close(_scf);
                mm_tools_op = ""scan_lss"";
                mm_tools_status = _is_de ? ""Suche Lossless Scaling..."" : ""Scanning for Lossless Scaling..."";
                mm_tools_result1 = """";
                mm_tools_result2 = """";
                mm_tools_result3 = """";
                mm_tools_timer = 0;
                mm_state = ""modtools_wait"";
            }
        }
        else if (mm_sub_idx == 3)
        {
            mm_autoupdate = !mm_autoupdate;
            audio_play_sound(snd_select, 50, 0);
            var _uf = file_text_open_write(""autoupdate.txt"");
            file_text_write_string(_uf, mm_autoupdate ? ""1"" : ""0"");
            file_text_close(_uf);
        }
        else if (mm_sub_idx == 4)
        {
            audio_play_sound(snd_swing, 50, 0);
            mm_state = ""settings"";
        }
    }
    else if (_cancel)
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = ""settings"";
    }
}
else if (mm_state == ""modtools_wait"")
{
    mm_tools_timer += 1;
    if (file_exists(""mod_tools_ready.txt""))
    {
        if (file_exists(""mod_tools_result.txt""))
        {
            var _rf = file_text_open_read(""mod_tools_result.txt"");
            if (!file_text_eof(_rf)) { mm_tools_result1 = file_text_read_string(_rf); file_text_readln(_rf); }
            if (!file_text_eof(_rf)) { mm_tools_result2 = file_text_read_string(_rf); file_text_readln(_rf); }
            if (!file_text_eof(_rf)) { mm_tools_result3 = file_text_read_string(_rf); file_text_readln(_rf); }
            file_text_close(_rf);
            file_delete(""mod_tools_result.txt"");
        }
        file_delete(""mod_tools_ready.txt"");
        if (file_exists(""mod_tools_cmd.txt""))
            file_delete(""mod_tools_cmd.txt"");

        if (file_exists(""fps_mode.txt""))
        {
            var _fmf = file_text_open_read(""fps_mode.txt"");
            var _fmv = string_lower(file_text_read_string(_fmf));
            file_text_close(_fmf);
            if (string_pos(""lossless"", _fmv) > 0)
                mm_fps_mode = ""lossless"";
            else if (string_pos(""engine"", _fmv) > 0)
                mm_fps_mode = ""engine"";
            else
                mm_fps_mode = ""off"";
            mm_fps60_patch = (mm_fps_mode == ""engine"");
        }
        if (file_exists(""lsfg_fps.txt""))
        {
            var _lsf = file_text_open_read(""lsfg_fps.txt"");
            var _lsv = file_text_read_string(_lsf);
            file_text_close(_lsf);
            if (string_pos(""240"", _lsv) > 0) mm_lsfg_fps = ""240"";
            else if (string_pos(""144"", _lsv) > 0) mm_lsfg_fps = ""144"";
            else if (string_pos(""120"", _lsv) > 0) mm_lsfg_fps = ""120"";
            else if (string_pos(""90"", _lsv) > 0) mm_lsfg_fps = ""90"";
            else mm_lsfg_fps = ""60"";
            for (var _lsi = 0; _lsi < array_length(mm_lsfg_fps_list); _lsi++)
            {
                if (mm_lsfg_fps_list[_lsi] == mm_lsfg_fps) { mm_lsfg_fps_idx = _lsi; break; }
            }
        }
        if (file_exists(""lsfg_mode.txt""))
        {
            var _lsmf = file_text_open_read(""lsfg_mode.txt"");
            var _lsmv = string_lower(file_text_read_string(_lsmf));
            file_text_close(_lsmf);
            if (string_pos(""performance"", _lsmv) > 0)
                mm_lsfg_mode = ""performance"";
            else
                mm_lsfg_mode = ""balanced"";
            mm_lsfg_mode_idx = (mm_lsfg_mode == ""performance"") ? 1 : 0;
        }
        if (file_exists(""lsfg_flow.txt""))
        {
            var _lsff = file_text_open_read(""lsfg_flow.txt"");
            var _lsfv = file_text_read_string(_lsff);
            file_text_close(_lsff);
            if (string_pos(""50"", _lsfv) > 0)
                mm_lsfg_flow = ""50"";
            else if (string_pos(""75"", _lsfv) > 0)
                mm_lsfg_flow = ""75"";
            else
                mm_lsfg_flow = ""100"";
            for (var _lfi = 0; _lfi < array_length(mm_lsfg_flow_list); _lfi++)
            {
                if (mm_lsfg_flow_list[_lfi] == mm_lsfg_flow) { mm_lsfg_flow_idx = _lfi; break; }
            }
        }

        mm_load_modlist();
        audio_play_sound(snd_select, 50, 0);
        mm_state = ""modtools_result"";
    }
    else if (mm_tools_timer > 360)
    {
        if (file_exists(""mod_tools_cmd.txt""))
            file_delete(""mod_tools_cmd.txt"");
        mm_tools_result1 = _is_de ? ""Hinweis: Hintergrund-Loader inaktiv."" : ""Notice: Background loader inactive."";
        mm_tools_result2 = _is_de ? ""Starte DELTARUNE ueber ModLoader oder nutze:"" : ""Launch game via ModLoader or use:"";
        mm_tools_result3 = _is_de ? ""Import-Mod.bat im Spielordner fuer Updates!"" : ""Import-Mod.bat in game folder for updates!"";
        audio_play_sound(snd_swing, 50, 0);
        mm_state = ""modtools_result"";
    }
}
else if (mm_state == ""modtools_result"")
{
    if (button1_p() || button2_p() || _cancel || keyboard_check_pressed(vk_space) || keyboard_check_pressed(vk_enter))
    {
        audio_play_sound(snd_swing, 50, 0);
        mm_state = ""settings_mods"";
    }
}
else if (mm_state == ""credits"")
{
    // Auto-scroll gently upwards
    mm_credits_scroll += 0.5;

    // Manual scroll with arrow keys or keyboard
    if (up_p() || keyboard_check(vk_up))
        mm_credits_scroll -= 2.5;
    if (down_p() || keyboard_check(vk_down))
        mm_credits_scroll += 2.5;

    if (instance_exists(obj_gamecontroller))
    {
        var _gpc_c = obj_gamecontroller.gamepad_id;
        if (gamepad_button_check(_gpc_c, gp_padu)) mm_credits_scroll -= 2.5;
        if (gamepad_button_check(_gpc_c, gp_padd)) mm_credits_scroll += 2.5;
    }

    var _max_cred_scroll = mm_thesi_unlocked ? 440 : 330;
    if (mm_credits_scroll < 0)
        mm_credits_scroll = 0;
    if (mm_credits_scroll > _max_cred_scroll)
        mm_credits_scroll = _max_cred_scroll;

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

var _spr_confirm  = asset_get_index(mm_ps ? ""spr_btn_ps4_cross""    : ""spr_btn_xbox_a"");
var _spr_back     = asset_get_index(mm_ps ? ""spr_btn_ps4_circle""   : ""spr_btn_xbox_b"");
var _spr_toggle   = asset_get_index(mm_ps ? ""spr_btn_ps4_square""   : ""spr_btn_xbox_x"");
var _spr_delete   = asset_get_index(mm_ps ? ""spr_btn_ps4_triangle"" : ""spr_btn_xbox_y"");
var _spr_settings = asset_get_index(mm_ps ? ""spr_btn_ps4_options""  : ""spr_btn_xbox_menu"");
if (_spr_settings < 0) _spr_settings = _spr_delete;

var _draw_bar2 = function(_bx, _by, _k1, _s1, _t1, _k2, _s2, _t2)
{
    var _w1 = string_width(_k1) + ((_s1 >= 0) ? (sprite_get_width(_s1) + 4) : 0) + string_width(_t1);
    var _w2 = string_width(_k2) + ((_s2 >= 0) ? (sprite_get_width(_s2) + 4) : 0) + string_width(_t2);
    var _gap = 26;
    var _start = _bx - (_w1 + _gap + _w2) * 0.5;
    
    draw_set_halign(fa_left);
    draw_text(_start, _by, _k1);
    _start += string_width(_k1);
    if (_s1 >= 0) {
        draw_sprite(_s1, 0, _start + 2, _by + 2);
        _start += sprite_get_width(_s1) + 4;
    }
    draw_text(_start, _by, _t1);
    _start += string_width(_t1) + _gap;
    
    draw_text(_start, _by, _k2);
    _start += string_width(_k2);
    if (_s2 >= 0) {
        draw_sprite(_s2, 0, _start + 2, _by + 2);
        _start += sprite_get_width(_s2) + 4;
    }
    draw_text(_start, _by, _t2);
    draw_set_halign(fa_center);
};

var _draw_bar3 = function(_bx, _by, _k1, _s1, _t1, _k2, _s2, _t2, _k3, _s3, _t3)
{
    var _w1 = string_width(_k1) + ((_s1 >= 0) ? (sprite_get_width(_s1) + 4) : 0) + string_width(_t1);
    var _w2 = string_width(_k2) + ((_s2 >= 0) ? (sprite_get_width(_s2) + 4) : 0) + string_width(_t2);
    var _w3 = string_width(_k3) + ((_s3 >= 0) ? (sprite_get_width(_s3) + 4) : 0) + string_width(_t3);
    var _gap = 18;
    var _start = _bx - (_w1 + _gap + _w2 + _gap + _w3) * 0.5;
    
    draw_set_halign(fa_left);
    draw_text(_start, _by, _k1);
    _start += string_width(_k1);
    if (_s1 >= 0) {
        draw_sprite(_s1, 0, _start + 2, _by + 2);
        _start += sprite_get_width(_s1) + 4;
    }
    draw_text(_start, _by, _t1);
    _start += string_width(_t1) + _gap;
    
    draw_text(_start, _by, _k2);
    _start += string_width(_k2);
    if (_s2 >= 0) {
        draw_sprite(_s2, 0, _start + 2, _by + 2);
        _start += sprite_get_width(_s2) + 4;
    }
    draw_text(_start, _by, _t2);
    _start += string_width(_t2) + _gap;
    
    draw_text(_start, _by, _k3);
    _start += string_width(_k3);
    if (_s3 >= 0) {
        draw_sprite(_s3, 0, _start + 2, _by + 2);
        _start += sprite_get_width(_s3) + 4;
    }
    draw_text(_start, _by, _t3);
    draw_set_halign(fa_center);
};

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
    var _s_y = _gh * 0.23;
    var _s_step = 42;

    // Category 0: Grafik & Performance (when Thesi unlocked) OR Grafik & Framerate
    var _y0 = _s_y;
    if (mm_sidx == 0)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y0 - 4, _cx + _barw, _y0 + 36, true);
        mm_draw_soul(_cx - _barw + 12, _y0 + 12);
    }
    else draw_set_color(c_white);

    var _cat0_title = mm_thesi_unlocked ? (_is_de ? ""Grafik & Performance  >"" : ""Graphics & Performance  >"") : (_is_de ? ""Grafik & Framerate  >"" : ""Graphics & Framerate  >"");
    draw_text(_cx, _y0, _cat0_title);
    draw_set_color(c_silver);
    var _cat0_sub = mm_thesi_unlocked ? (_is_de ? ""Lossless Scaling, Thesi-Kris & Grafik-Optionen"" : ""Lossless Scaling, Thesi Kris & graphics options"") : (_is_de ? ""Lossless Scaling (60-240 FPS), Framerate & Grafik"" : ""Lossless Scaling (60-240 FPS), framerate & graphics"");
    draw_text_transformed(_cx, _y0 + 18, _cat0_sub, 0.7, 0.7, 0);

    var _y_curr = _s_y + _s_step;

    // Category 1: Sprache & Text
    if (mm_sidx == 1)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y_curr - 4, _cx + _barw, _y_curr + 36, true);
        mm_draw_soul(_cx - _barw + 12, _y_curr + 12);
    }
    else draw_set_color(c_white);
    draw_text(_cx, _y_curr, _is_de ? ""Sprache & Text  >"" : ""Language & Text  >"");
    draw_set_color(c_silver);
    draw_text_transformed(_cx, _y_curr + 18, _is_de ? ""Deutsch-Patch & Menue-Sprache"" : ""German patch & menu language"", 0.7, 0.7, 0);
    _y_curr += _s_step;

    // Category 2: Mod-Verwaltung & Updates
    if (mm_sidx == 2)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y_curr - 4, _cx + _barw, _y_curr + 36, true);
        mm_draw_soul(_cx - _barw + 12, _y_curr + 12);
    }
    else draw_set_color(c_white);
    draw_text(_cx, _y_curr, _is_de ? ""Mod-Verwaltung & Updates  >"" : ""Mods & Updates  >"");
    draw_set_color(c_silver);
    draw_text_transformed(_cx, _y_curr + 18, _is_de ? ""Lossless Scaling suchen, Updates & Auto-Import"" : ""Scan Lossless Scaling, updates & auto-import"", 0.7, 0.7, 0);
    _y_curr += _s_step;

    // Category 3: Credits & Danksagung
    if (mm_sidx == 3)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y_curr - 4, _cx + _barw, _y_curr + 36, true);
        mm_draw_soul(_cx - _barw + 12, _y_curr + 12);
    }
    else draw_set_color(c_white);
    draw_text(_cx, _y_curr, _is_de ? ""Credits & Danksagung  >"" : ""Credits & Contributors  >"");
    draw_set_color(c_silver);
    draw_text_transformed(_cx, _y_curr + 18, _is_de ? ""Mitwirkende, Team & Danksagung ansehen"" : ""View creators, team and contributors"", 0.7, 0.7, 0);
    _y_curr += _s_step + 4;

    // Category 4: Zurueck zum Menue
    if (mm_sidx == 4)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y_curr - 4, _cx + _barw, _y_curr + 26, true);
        mm_draw_soul(_cx - _barw + 12, _y_curr + 7);
    }
    else draw_set_color(c_white);
    draw_text(_cx, _y_curr + 2, _is_de ? ""< Zurueck zum Hauptmenue"" : ""< Back to Main Menu"");

    draw_set_color(c_white);
    _draw_bar2(_cx, _gh * 0.855, ""[Z] "", _spr_confirm, _is_de ? ""Kategorie oeffnen"" : ""Open Category"", ""[X] "", _spr_back, _is_de ? ""Zurueck"" : ""Back"");
}
else if (mm_state == ""settings_gfx"")
{
    draw_text(_cx, _gh * 0.165, _is_de ? ""GRAFIK & PERFORMANCE"" : ""GRAPHICS & PERFORMANCE"");

    var _has_lsfg = (mm_fps_mode == ""lossless"");
    var _barw = 236;
    var _s_y = _has_lsfg ? (_gh * 0.21) : (_gh * 0.24);
    var _s_step = _has_lsfg ? 37 : 44;

    // Item 0: Framerate-Modus
    var _y0 = _s_y;
    if (mm_sub_idx == 0)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y0 - 4, _cx + _barw, _y0 + 36, true);
        mm_draw_soul(_cx - _barw + 12, _y0 + 12);
    }
    else draw_set_color(c_white);
    var _mode_txt = (mm_fps_mode == ""lossless"") ? ""[Lossless Scaling]"" : ((mm_fps_mode == ""engine"") ? ""[Engine-Mod (60)]"" : (_is_de ? ""[Aus (30)]"" : ""[Off (30)]""));
    draw_text(_cx, _y0, (_is_de ? ""Framerate-Modus:  "" : ""Framerate Mode:  "") + _mode_txt);
    draw_set_color((mm_fps_mode == ""lossless"") ? c_aqua : ((mm_fps_mode == ""engine"") ? c_lime : c_dkgray));
    var _sub_txt = (mm_fps_mode == ""lossless"") ? (_is_de ? ""GPU-Frame-Gen (LSFG 3) - Funktioniert mit ALLEN Mods!"" : ""GPU frame-gen (LSFG 3) - Works on ALL mods!"") : ((mm_fps_mode == ""engine"") ? (_is_de ? ""Nativer 60 FPS Bytecode-Patch (nur Vanilla)"" : ""Native 60 FPS bytecode patch (vanilla only)"") : (_is_de ? ""Originale 30 FPS Spielgeschwindigkeit"" : ""Original 30 FPS game speed""));
    draw_text_transformed(_cx, _y0 + 18, _sub_txt, 0.7, 0.7, 0);

    var _y_curr = _s_y + _s_step;

    // Items 1, 2, 3 (if _has_lsfg): LSFG Ziel-FPS, LSFG Modus, LSFG Flow-Wert
    if (_has_lsfg)
    {
        // Item 1: Ziel-FPS
        if (mm_sub_idx == 1)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y_curr - 4, _cx + _barw, _y_curr + 36, true);
            mm_draw_soul(_cx - _barw + 12, _y_curr + 12);
        }
        else draw_set_color(c_white);
        draw_text(_cx, _y_curr, (_is_de ? ""LSFG Ziel-FPS:  ["" : ""LSFG Target FPS:  ["") + mm_lsfg_fps + "" FPS]"");
        draw_set_color(c_aqua);
        draw_text_transformed(_cx, _y_curr + 18, _is_de ? ""Ziel-Framerate fuer Zwischenbildberechnung"" : ""Target framerate for frame generation"", 0.7, 0.7, 0);
        _y_curr += _s_step;

        // Item 2: Modus (Qualitaet / Performance)
        if (mm_sub_idx == 2)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y_curr - 4, _cx + _barw, _y_curr + 36, true);
            mm_draw_soul(_cx - _barw + 12, _y_curr + 12);
        }
        else draw_set_color(c_white);
        var _mode_label = (mm_lsfg_mode == ""performance"") ? (_is_de ? ""[Performance]"" : ""[Performance]"") : (_is_de ? ""[Qualitaet (Balanced)]"" : ""[Quality (Balanced)]"");
        draw_text(_cx, _y_curr, (_is_de ? ""LSFG Modus:  "" : ""LSFG Mode:  "") + _mode_label);
        draw_set_color((mm_lsfg_mode == ""performance"") ? c_orange : c_aqua);
        var _sub_m = (mm_lsfg_mode == ""performance"") ? (_is_de ? ""Schneller / spart GPU-Leistung (halbe Flow-Aufloesung)"" : ""Faster / saves GPU power (half flow resolution)"") : (_is_de ? ""Maximale Bildschaerfe & Qualitaet (volle Aufloesung)"" : ""Maximum image sharpness & quality (full resolution)"");
        draw_text_transformed(_cx, _y_curr + 18, _sub_m, 0.7, 0.7, 0);
        _y_curr += _s_step;

        // Item 3: Flow-Wert
        if (mm_sub_idx == 3)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y_curr - 4, _cx + _barw, _y_curr + 36, true);
            mm_draw_soul(_cx - _barw + 12, _y_curr + 12);
        }
        else draw_set_color(c_white);
        var _flow_txt = (mm_lsfg_flow == ""50"") ? ""[50% (4K Glaette)]"" : ((mm_lsfg_flow == ""75"") ? ""[75% (1440p)]"" : (_is_de ? ""[100% (Standard)]"" : ""[100% (Default)]""));
        draw_text(_cx, _y_curr, (_is_de ? ""LSFG Flow-Wert:  "" : ""LSFG Flow Scale:  "") + _flow_txt);
        draw_set_color(c_aqua);
        var _sub_f = (mm_lsfg_flow == ""50"") ? (_is_de ? ""Empfohlen fuer 4K (maximale Bewegungsglaette)"" : ""Recommended for 4K (maximum smoothness on fast motion)"") : ((mm_lsfg_flow == ""75"") ? (_is_de ? ""Empfohlen fuer 1440p (erhoehte Bewegungstoleranz)"" : ""Recommended for 1440p (higher motion tolerance)"") : (_is_de ? ""100% Nativ - Ideal fuer 1080p und Pixel-Details"" : ""100% Native - Ideal for 1080p & pixel details""));
        draw_text_transformed(_cx, _y_curr + 18, _sub_f, 0.7, 0.7, 0);
        _y_curr += _s_step;
    }

    if (mm_thesi_unlocked)
    {
        // Item: Thesi-Kris Texturen
        var _idx_thesi = _has_lsfg ? 4 : 1;
        if (mm_sub_idx == _idx_thesi)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y_curr - 4, _cx + _barw, _y_curr + 36, true);
            mm_draw_soul(_cx - _barw + 12, _y_curr + 12);
        }
        else draw_set_color(c_white);
        draw_text(_cx, _y_curr, _is_de ? (""Thesi-Kris Texturen:  "" + (mm_thesi_overlay ? ""[AKTIV]"" : ""[AUS]"")) : (""Thesi Kris Textures:  "" + (mm_thesi_overlay ? ""[ON]"" : ""[OFF]"")));
        draw_set_color(mm_thesi_overlay ? c_lime : c_dkgray);
        draw_text_transformed(_cx, _y_curr + 18, _is_de ? ""Kris mit Thesi-Haaren auf Mods anwenden"" : ""Apply Kris with Thesi hair overlay to mods"", 0.7, 0.7, 0);
        _y_curr += _s_step;

        // Item: Thesi-Modus sperren
        var _idx_relock = _has_lsfg ? 5 : 2;
        if (mm_sub_idx == _idx_relock)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y_curr - 4, _cx + _barw, _y_curr + 36, true);
            mm_draw_soul(_cx - _barw + 12, _y_curr + 12);
        }
        else draw_set_color(c_white);
        draw_text(_cx, _y_curr, _is_de ? ""Thesi-Modus sperren / zuruecksetzen"" : ""Lock & Reset Thesi Mode"");
        draw_set_color(c_orange);
        draw_text_transformed(_cx, _y_curr + 18, _is_de ? ""Modus wieder sperren und als Secret verbergen"" : ""Relock Thesi mode and hide it as a secret again"", 0.7, 0.7, 0);
        _y_curr += _s_step + 4;

        // Item: Zurueck
        var _idx_back = _has_lsfg ? 6 : 3;
        if (mm_sub_idx == _idx_back)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y_curr - 4, _cx + _barw, _y_curr + 26, true);
            mm_draw_soul(_cx - _barw + 12, _y_curr + 7);
        }
        else draw_set_color(c_white);
        draw_text(_cx, _y_curr + 2, _is_de ? ""< Zurueck zu Einstellungen"" : ""< Back to Settings"");
    }
    else
    {
        // Item: Zurueck
        var _idx_back = _has_lsfg ? 4 : 1;
        if (mm_sub_idx == _idx_back)
        {
            draw_set_color(c_yellow);
            draw_rectangle(_cx - _barw, _y_curr - 4, _cx + _barw, _y_curr + 26, true);
            mm_draw_soul(_cx - _barw + 12, _y_curr + 7);
        }
        else draw_set_color(c_white);
        draw_text(_cx, _y_curr + 2, _is_de ? ""< Zurueck zu Einstellungen"" : ""< Back to Settings"");
    }

    draw_set_color(c_white);
    var _is_toggle_item = (mm_sub_idx == 0) || (_has_lsfg && mm_sub_idx >= 1 && mm_sub_idx <= 3) || (mm_thesi_unlocked && (_has_lsfg ? (mm_sub_idx == 4) : (mm_sub_idx == 1)));
    if (_is_toggle_item)
        _draw_bar2(_cx, _gh * 0.855, ""[Z] "", _spr_confirm, _is_de ? ""oder [<]/[>] Umschalten"" : ""or [<]/[>] Toggle"", ""[X] "", _spr_back, _is_de ? ""Zurueck"" : ""Back"");
    else
        _draw_bar2(_cx, _gh * 0.855, ""[Z] "", _spr_confirm, _is_de ? ""Auswaehlen"" : ""Select"", ""[X] "", _spr_back, _is_de ? ""Zurueck"" : ""Back"");
}
else if (mm_state == ""settings_lang"")
{
    draw_text(_cx, _gh * 0.165, _is_de ? ""SPRACHE & TEXT"" : ""LANGUAGE & TEXT"");

    var _barw = 236;
    var _s_y = _gh * 0.25;
    var _s_step = 48;

    // Item 0: Deutsch-Patch
    var _y0 = _s_y;
    if (mm_sub_idx == 0)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y0 - 4, _cx + _barw, _y0 + 38, true);
        mm_draw_soul(_cx - _barw + 12, _y0 + 13);
    }
    else draw_set_color(c_white);
    draw_text(_cx, _y0, _is_de ? (""Deutsch-Patch (Spiel):  "" + (mm_german_patch ? ""[AKTIV]"" : ""[AUS]"")) : (""German Patch (Game):  "" + (mm_german_patch ? ""[ON]"" : ""[OFF]"")));
    draw_set_color(mm_german_patch ? c_lime : c_dkgray);
    draw_text_transformed(_cx, _y0 + 18, _is_de ? ""Deutsche Texte & Dialoge im Spiel aktivieren"" : ""Enable German translation and dialogue in-game"", 0.7, 0.7, 0);

    // Item 1: Menue-Sprache
    var _y1 = _s_y + _s_step;
    if (mm_sub_idx == 1)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y1 - 4, _cx + _barw, _y1 + 38, true);
        mm_draw_soul(_cx - _barw + 12, _y1 + 13);
    }
    else draw_set_color(c_white);
    draw_text(_cx, _y1, _is_de ? ""Menue-Sprache:  [Deutsch]"" : ""Menu Language:  [English]"");
    draw_set_color(c_aqua);
    draw_text_transformed(_cx, _y1 + 18, _is_de ? ""Sprache des Mod-Menues umschalten"" : ""Switch Mod Selector language (English / Deutsch)"", 0.7, 0.7, 0);

    // Item 2: Zurueck
    var _y2 = _s_y + _s_step * 2 + 8;
    if (mm_sub_idx == 2)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y2 - 4, _cx + _barw, _y2 + 26, true);
        mm_draw_soul(_cx - _barw + 12, _y2 + 7);
    }
    else draw_set_color(c_white);
    draw_text(_cx, _y2 + 2, _is_de ? ""< Zurueck zu Einstellungen"" : ""< Back to Settings"");

    draw_set_color(c_white);
    _draw_bar2(_cx, _gh * 0.855, ""[Z] "", _spr_confirm, _is_de ? ""oder [<]/[>] Umschalten"" : ""or [<]/[>] Toggle"", ""[X] "", _spr_back, _is_de ? ""Zurueck"" : ""Back"");
}
else if (mm_state == ""settings_mods"")
{
    draw_text(_cx, _gh * 0.165, _is_de ? ""MOD-VERWALTUNG & UPDATES"" : ""MODS & UPDATES"");

    var _barw = 236;
    var _s_y = _gh * 0.22;
    var _s_step = 42;

    // Item 0: Mod-Updates pruefen
    var _y0 = _s_y;
    if (mm_sub_idx == 0)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y0 - 4, _cx + _barw, _y0 + 36, true);
        mm_draw_soul(_cx - _barw + 12, _y0 + 12);
    }
    else draw_set_color(c_white);
    draw_text(_cx, _y0, _is_de ? ""Auf Mod-Updates pruefen  >"" : ""Check for Mod Updates  >"");
    draw_set_color(c_aqua);
    draw_text_transformed(_cx, _y0 + 18, _is_de ? ""Installierte Mods ueber GameBanana abfragen"" : ""Check installed mods via GameBanana"", 0.7, 0.7, 0);

    // Item 1: Auto-Import Ordner verarbeiten
    var _y1 = _s_y + _s_step;
    if (mm_sub_idx == 1)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y1 - 4, _cx + _barw, _y1 + 36, true);
        mm_draw_soul(_cx - _barw + 12, _y1 + 12);
    }
    else draw_set_color(c_white);
    draw_text(_cx, _y1, _is_de ? ""Auto-Import Ordner verarbeiten  >"" : ""Process Auto-Import Folder  >"");
    draw_set_color(c_aqua);
    draw_text_transformed(_cx, _y1 + 18, _is_de ? ""Archive aus 'mods\_import' jetzt installieren"" : ""Install archives from 'mods\_import' now"", 0.7, 0.7, 0);

    // Item 2: Nach Lossless Scaling suchen
    var _y2 = _s_y + _s_step * 2;
    if (mm_sub_idx == 2)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y2 - 4, _cx + _barw, _y2 + 36, true);
        mm_draw_soul(_cx - _barw + 12, _y2 + 12);
    }
    else draw_set_color(c_white);
    draw_text(_cx, _y2, _is_de ? ""Nach Lossless Scaling suchen  >"" : ""Scan for Lossless Scaling  >"");
    draw_set_color(c_aqua);
    draw_text_transformed(_cx, _y2 + 18, _is_de ? ""Steam-Installation pruefen & DELTARUNE-Profil einrichten"" : ""Verify Steam install & configure DELTARUNE profile"", 0.7, 0.7, 0);

    // Item 3: Selector Auto-Update
    var _y3 = _s_y + _s_step * 3;
    if (mm_sub_idx == 3)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y3 - 4, _cx + _barw, _y3 + 36, true);
        mm_draw_soul(_cx - _barw + 12, _y3 + 12);
    }
    else draw_set_color(c_white);
    draw_text(_cx, _y3, _is_de ? (""Selector Auto-Update:  "" + (mm_autoupdate ? ""[AKTIV]"" : ""[AUS]"")) : (""Selector Auto-Update:  "" + (mm_autoupdate ? ""[ON]"" : ""[OFF]"")));
    draw_set_color(mm_autoupdate ? c_lime : c_dkgray);
    draw_text_transformed(_cx, _y3 + 18, _is_de ? ""Automatisch nach neuen Versionen suchen"" : ""Automatically check for new versions on GitHub"", 0.7, 0.7, 0);

    // Item 4: Zurueck
    var _y4 = _s_y + _s_step * 4 + 6;
    if (mm_sub_idx == 4)
    {
        draw_set_color(c_yellow);
        draw_rectangle(_cx - _barw, _y4 - 4, _cx + _barw, _y4 + 26, true);
        mm_draw_soul(_cx - _barw + 12, _y4 + 7);
    }
    else draw_set_color(c_white);
    draw_text(_cx, _y4 + 2, _is_de ? ""< Zurueck zu Einstellungen"" : ""< Back to Settings"");

    draw_set_color(c_white);
    _draw_bar2(_cx, _gh * 0.855, ""[Z] "", _spr_confirm, _is_de ? ""Auswaehlen / Umschalten"" : ""Select / Toggle"", ""[X] "", _spr_back, _is_de ? ""Zurueck"" : ""Back"");
}
else if (mm_state == ""modtools_wait"")
{
    draw_text(_cx, _gh * 0.165, _is_de ? ""MOD-VERWALTUNG"" : ""MOD MANAGER"");

    var _dots = """";
    var _dcount = (mm_tools_timer div 15) mod 4;
    for (var _di = 0; _di < _dcount; _di++) _dots += ""."";

    draw_set_color(c_yellow);
    draw_text(_cx, _gh * 0.42, mm_tools_status + _dots);
    draw_set_color(c_silver);
    draw_text_transformed(_cx, _gh * 0.50, _is_de ? ""(Bitte einen Moment Geduld...)"" : ""(Please wait a moment...)"", 0.8, 0.8, 0);
}
else if (mm_state == ""modtools_result"")
{
    draw_text(_cx, _gh * 0.165, _is_de ? ""MOD-ERGEBNIS"" : ""MOD RESULT"");

    draw_set_color(c_yellow);
    draw_rectangle(_cx - 240, _gh * 0.32, _cx + 240, _gh * 0.64, true);

    draw_set_color(c_yellow);
    draw_text(_cx, _gh * 0.38, mm_tools_result1);

    draw_set_color(c_white);
    draw_text_transformed(_cx, _gh * 0.46, mm_tools_result2, 0.85, 0.85, 0);

    draw_set_color(c_silver);
    draw_text_transformed(_cx, _gh * 0.54, mm_tools_result3, 0.75, 0.75, 0);

    draw_set_color(c_white);
    _draw_bar2(_cx, _gh * 0.855, ""[Z] "", _spr_confirm, _is_de ? ""oder"" : ""or"", ""[X] "", _spr_back, _is_de ? ""Zurueck"" : ""Back"");
}
else if (mm_state == ""credits"")
{
    var _top_cut = _gh * 0.25;
    var _bot_cut = _gh * 0.835;
    var _fade_h  = 24;
    var _base_y  = _gh * 0.29 - mm_credits_scroll;

    // --- Scrolling Credit Items ---
    if (mm_thesi_unlocked)
    {
        // 1. Original Thesi-Sprites
        var _y = _base_y + 0;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_aqua);
            draw_text(_cx, _y, _is_de ? ""Original Thesi-Sprites (Kapitel 1 & 2):"" : ""Original Thesi Sprites (Chapters 1 & 2):"");
            draw_set_color(c_white);
            draw_text(_cx, _y + 20, ""KajaKay_"");
            draw_set_color(c_silver);
            draw_text_transformed(_cx, _y + 40, _is_de ? ""Urheber der urspruenglichen Thesi Kris Sprites."" : ""Creator of the original Thesi Kris sprites."", 0.75, 0.75, 0);
        }

        // 2. Chapters 3, 4 & 5 Sprites
        _y = _base_y + 85;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_aqua);
            draw_text(_cx, _y, _is_de ? ""Kapitel 3, 4 & 5 Sprites:"" : ""Chapters 3, 4 & 5 Sprites:"");
            draw_set_color(c_silver);
            draw_text_transformed(_cx, _y + 20, _is_de ? ""Die restlichen Sprites wurden basierend auf den Originalen"" : ""Remaining sprites were reused from originals or"", 0.75, 0.75, 0);
            draw_text_transformed(_cx, _y + 36, _is_de ? ""erneut benutzt oder mit KI-Unterstuetzung erstellt."" : ""generated with AI assistance."", 0.75, 0.75, 0);
        }

        // 3. Mod-Selector & Menu
        _y = _base_y + 175;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_aqua);
            draw_text(_cx, _y, _is_de ? ""Mod-Selector & Menue:"" : ""Mod Selector & Menu:"");
            draw_set_color(c_white);
            draw_text(_cx, _y + 20, ""Saloran26"");
            draw_set_color(c_silver);
            draw_text_transformed(_cx, _y + 40, _is_de ? ""Mod-Selector Menue & Speicherprofil-Verwaltung."" : ""Mod Selector menu & save profile manager."", 0.75, 0.75, 0);
        }

        // 4. German Translations
        _y = _base_y + 265;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_aqua);
            draw_text(_cx, _y, _is_de ? ""Deltarune Deutsch-Patches:"" : ""Deltarune German Translations:"");
            draw_set_color(c_white);
            draw_text(_cx, _y + 20, ""GermanRune Team"");
            draw_set_color(c_silver);
            draw_text_transformed(_cx, _y + 40, _is_de ? ""Deutsche Uebersetzung fuer Deltarune Kapitel 1 - 5."" : ""German translation for Deltarune Chapters 1 - 5."", 0.75, 0.75, 0);
        }

        // 5. 60 FPS Overhaul Mod
        _y = _base_y + 355;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_aqua);
            draw_text(_cx, _y, _is_de ? ""60 FPS Overhaul Mod:"" : ""60 FPS Overhaul Mod:"");
            draw_set_color(c_white);
            draw_text(_cx, _y + 20, ""BadArtAdventure (GameBanana)"");
            draw_set_color(c_silver);
            draw_text_transformed(_cx, _y + 40, _is_de ? ""60 FPS Physik- und Frame-Pacing-Anpassungen."" : ""60 FPS physics and frame-pacing adjustments."", 0.75, 0.75, 0);
        }

        // 6. Patch Tools
        _y = _base_y + 445;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_aqua);
            draw_text(_cx, _y, _is_de ? ""Patch-Tools & Frameworks:"" : ""Patch Tools & Frameworks:"");
            draw_set_color(c_white);
            draw_text(_cx, _y + 20, ""DeltaPatcher (xdelta)  &  UndertaleModTool"");
            draw_set_color(c_silver);
            draw_text_transformed(_cx, _y + 40, _is_de ? ""Community-Tools fuer Modding und Patcherstellung."" : ""Community tools for modding and patching."", 0.75, 0.75, 0);
        }

        // 7. Special Thanks
        _y = _base_y + 535;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_yellow);
            draw_text_transformed(_cx, _y, _is_de ? ""* (Vielen Dank an KajaKay_, alle Modder & Toby Fox!)"" : ""* (Special thanks to KajaKay_, all modders & Toby Fox!)"", 0.8, 0.8, 0);
            draw_set_color(c_gray);
            draw_text_transformed(_cx, _y + 22, _is_de ? ""* (Danke fuers Spielen der Thesi Edition!)"" : ""* (Thank you for playing Thesi Edition!)"", 0.75, 0.75, 0);
        }
    }
    else
    {
        // 1. Mod-Selector & Menu
        var _y = _base_y + 0;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_aqua);
            draw_text(_cx, _y, _is_de ? ""Mod-Selector & Menue:"" : ""Mod Selector & Menu:"");
            draw_set_color(c_white);
            draw_text(_cx, _y + 20, ""Saloran26"");
            draw_set_color(c_silver);
            draw_text_transformed(_cx, _y + 40, _is_de ? ""In-Game Mod-Selector Menue & Speicherprofil-Verwaltung."" : ""In-game Mod Selector menu & save profile manager."", 0.75, 0.75, 0);
        }

        // 2. German Translations
        _y = _base_y + 85;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_aqua);
            draw_text(_cx, _y, _is_de ? ""Deltarune Deutsch-Patches:"" : ""Deltarune German Translations:"");
            draw_set_color(c_white);
            draw_text(_cx, _y + 20, ""GermanRune Team"");
            draw_set_color(c_silver);
            draw_text_transformed(_cx, _y + 40, _is_de ? ""Deutsche Uebersetzung fuer Deltarune Kapitel 1 - 5."" : ""German translation for Deltarune Chapters 1 - 5."", 0.75, 0.75, 0);
        }

        // 3. 60 FPS Overhaul Mod
        _y = _base_y + 175;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_aqua);
            draw_text(_cx, _y, _is_de ? ""60 FPS Overhaul Mod:"" : ""60 FPS Overhaul Mod:"");
            draw_set_color(c_white);
            draw_text(_cx, _y + 20, ""BadArtAdventure (GameBanana)"");
            draw_set_color(c_silver);
            draw_text_transformed(_cx, _y + 40, _is_de ? ""60 FPS Physik- und Frame-Pacing-Anpassungen."" : ""60 FPS physics and frame-pacing adjustments."", 0.75, 0.75, 0);
        }

        // 4. Patch Tools
        _y = _base_y + 265;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_aqua);
            draw_text(_cx, _y, _is_de ? ""Patch-Tools & Frameworks:"" : ""Patch Tools & Frameworks:"");
            draw_set_color(c_white);
            draw_text(_cx, _y + 20, ""DeltaPatcher (xdelta)  &  UndertaleModTool"");
            draw_set_color(c_silver);
            draw_text_transformed(_cx, _y + 40, _is_de ? ""Community-Tools fuer Modding und Patcherstellung."" : ""Community tools for modding and patching."", 0.75, 0.75, 0);
        }

        // 5. Secret hint
        _y = _base_y + 355;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_gray);
            draw_text_transformed(_cx, _y, _is_de ? ""* (Ein Name mit 5 Buchstaben schlaeft im Menue...)"" : ""* (A 5-letter name sleeps in the menu...)"", 0.75, 0.75, 0);
        }

        // 6. Special Thanks
        _y = _base_y + 430;
        if (_y > _top_cut - 50 && _y < _bot_cut + 50)
        {
            var _a = 1.0;
            if (_y < _top_cut + _fade_h) _a = clamp((_y - _top_cut) / _fade_h, 0, 1);
            else if (_y > _bot_cut - _fade_h) _a = clamp((_bot_cut - _y) / _fade_h, 0, 1);
            draw_set_alpha(_a);
            draw_set_color(c_yellow);
            draw_text_transformed(_cx, _y, _is_de ? ""* (Danke an Toby Fox & alle Deltarune-Fans!)"" : ""* (Thank you to Toby Fox & all Deltarune fans!)"", 0.8, 0.8, 0);
        }
    }

    // --- Fixed Top Header (with solid black backdrop) ---
    draw_set_alpha(1);
    draw_set_color(c_black);
    draw_rectangle(_m + 8, _m + 8, _gw - _m - 8, _top_cut - 1, false);

    draw_set_color(c_white);
    draw_text(_cx, _gh * 0.175, _is_de ? ""CREDITS & DANKSAGUNG"" : ""CREDITS & CONTRIBUTORS"");
    draw_set_color(c_yellow);
    if (mm_thesi_unlocked)
        draw_text(_cx, _gh * 0.215, ""DELTARUNE - Thesi Edition"");
    else
        draw_text(_cx, _gh * 0.215, ""DELTARUNE Mod-Selector"");

    draw_set_color(c_dkgray);
    draw_line_width(_cx - 160, _top_cut, _cx + 160, _top_cut, 2);

    // --- Fixed Bottom Navigation (with solid black backdrop) ---
    draw_set_color(c_black);
    draw_rectangle(_m + 8, _bot_cut + 1, _gw - _m - 8, _gh - _m - 8, false);

    draw_set_color(c_dkgray);
    draw_line_width(_cx - 160, _bot_cut, _cx + 160, _bot_cut, 2);

    draw_set_color(c_white);
    draw_text_transformed(_cx, _gh * 0.855, _is_de ? ""[Oben/Unten] Scrollen      [Z]/[X] Zurueck"" : ""[Up/Down] Scroll      [Z]/[X] Back"", 0.85, 0.85, 0);

    draw_set_alpha(1);
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
    _draw_bar2(_cx, _gh * 0.855, _is_de ? ""HALTEN [Z] "" : ""HOLD [Z] "", _spr_confirm, _is_de ? ""zum Loeschen (3s)"" : ""to delete (3s)"", ""[X] "", _spr_back, _is_de ? ""Abbrechen"" : ""Cancel"");
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
        _draw_bar3(_cx, _gh * 0.855, ""[Z] "", _spr_confirm, _is_de ? ""Bestaetigen"" : ""Confirm"", ""[Entf] "", _spr_delete, _is_de ? ""Loeschen"" : ""Delete"", ""[X] "", _spr_back, _is_de ? ""Zurueck"" : ""Back"");
    else
        _draw_bar2(_cx, _gh * 0.855, ""[Z] "", _spr_confirm, _is_de ? ""Bestaetigen"" : ""Confirm"", ""[X] "", _spr_back, _is_de ? ""Zurueck"" : ""Back"");

    if (!_isprofile)
    {
        var _fps_label = (mm_fps_mode == ""lossless"") ? (""LSFG "" + mm_lsfg_fps + ((mm_lsfg_mode == ""performance"") ? "" Perf"" : """")) : ((mm_fps_mode == ""engine"") ? ""60 FPS"" : ""30 FPS"");
        draw_set_color(c_yellow);
        var _foot_tail = _is_de ? ("" Einstellungen   |   FPS: ["" + _fps_label + ""]"" + (mm_thesi_unlocked ? (""   Thesi: "" + (mm_thesi_overlay ? ""[AN]"" : ""[AUS]"")) : """") + ""   Deutsch: "" + (mm_german_patch ? ""[AN]"" : ""[AUS]"")) : ("" Settings   |   FPS: ["" + _fps_label + ""]"" + (mm_thesi_unlocked ? (""   Thesi: "" + (mm_thesi_overlay ? ""[ON]"" : ""[OFF]"")) : """") + ""   German: "" + (mm_german_patch ? ""[ON]"" : ""[OFF]""));
        var _foot_w = string_width(""[C] "") + ((_spr_settings >= 0) ? (sprite_get_width(_spr_settings) + 4) : 0) + string_width(_foot_tail);
        var _foot_x = _cx - _foot_w * 0.5;
        draw_set_halign(fa_left);
        draw_text(_foot_x, _gh * 0.815, ""[C] "");
        _foot_x += string_width(""[C] "");
        if (_spr_settings >= 0) {
            draw_sprite(_spr_settings, 0, _foot_x + 1, _gh * 0.815 + 2);
            _foot_x += sprite_get_width(_spr_settings) + 4;
        }
        draw_text(_foot_x, _gh * 0.815, _foot_tail);
        draw_set_halign(fa_center);
        draw_set_color(c_white);
    }
}

if (mm_state != ""thesi_anim"")
{
    // Credit lines -- clear above the bottom frame.
    draw_set_color(c_gray);
    if (mm_thesi_unlocked)
        draw_text(_cx, _gh * 0.905, ""Mod-Selector v1.5.0 by Saloran26 & KajaKay_  -  Thesi Edition"");
    else
        draw_text(_cx, _gh * 0.905, ""Mod-Selector v1.5.0 by Saloran26"");
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
    + "draw_text_transformed(x + 16, y + 56, \"Mod-Selector v1.4.0\", _scale, _scale, 0);";

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
//  1.5) Inject controller button sprites into Data if not present
// ----------------------------------------------------------------------------
try
{
    string[] candidateDirs = new string[] {
        Path.Combine(Path.GetDirectoryName(ScriptPath), "Buttons"),
        Path.Combine(Path.GetDirectoryName(ScriptPath), "..", "Patch", "Buttons"),
        Path.Combine(Path.GetDirectoryName(FilePath), "_tools", "XboxButtons"),
        Path.Combine(Path.GetDirectoryName(FilePath), "ModLoader", "Buttons"),
        @"D:\SteamLibrary\steamapps\common\DELTARUNE\_tools\XboxButtons",
        @"D:\SteamLibrary\steamapps\common\DELTARUNE\Patch\Buttons"
    };

    string foundDir = null;
    foreach (var cd in candidateDirs)
    {
        if (Directory.Exists(cd) && File.Exists(Path.Combine(cd, "button_xbox_a_0.png")))
        {
            foundDir = cd;
            break;
        }
    }

    if (foundDir != null && Data.Sprites.ByName("spr_btn_xbox_a") == null)
    {
        var buttonEntries = new (string file, string sprName)[] {
            ("button_xbox_a_0.png", "spr_btn_xbox_a"),
            ("button_xbox_b_0.png", "spr_btn_xbox_b"),
            ("button_xbox_x_0.png", "spr_btn_xbox_x"),
            ("button_xbox_y_0.png", "spr_btn_xbox_y"),
            ("button_xbox_left_0.png", "spr_btn_xbox_left"),
            ("button_xbox_right_0.png", "spr_btn_xbox_right"),
            ("button_xbox_up_0.png", "spr_btn_xbox_up"),
            ("button_xbox_down_0.png", "spr_btn_xbox_down"),
            ("button_xbox_menu_0.png", "spr_btn_xbox_menu"),
            ("button_ps4_cross_0_0.png", "spr_btn_ps4_cross"),
            ("button_ps4_circle_0_0.png", "spr_btn_ps4_circle"),
            ("button_ps4_square_0_0.png", "spr_btn_ps4_square"),
            ("button_ps4_triangle_0_0.png", "spr_btn_ps4_triangle"),
            ("button_ps4_options_0.png", "spr_btn_ps4_options")
        };

        using (var atlas = new MagickImage(MagickColors.Transparent, 128, 64))
        {
            int curX = 2;
            int curY = 2;
            var items = new List<(string name, int x, int y, int w, int h)>();

            foreach (var b in buttonEntries)
            {
                string p = Path.Combine(foundDir, b.file);
                if (!File.Exists(p)) continue;
                using (var btnImg = TextureWorker.ReadBGRAImageFromFile(p))
                {
                    if (curX + btnImg.Width + 2 > 128)
                    {
                        curX = 2;
                        curY += 18;
                    }
                    atlas.Composite(btnImg, curX, curY, CompositeOperator.Over);
                    items.Add((b.sprName, curX, curY, (int)btnImg.Width, (int)btnImg.Height));
                    curX += (int)btnImg.Width + 2;
                }
            }

            var tex = new UndertaleEmbeddedTexture();
            tex.Name = Data.Strings.MakeString($"Texture {Data.EmbeddedTextures.Count}");
            tex.TextureData.Image = GMImage.FromMagickImage(atlas).ConvertToPng();
            Data.EmbeddedTextures.Add(tex);

            foreach (var item in items)
            {
                if (Data.Sprites.ByName(item.name) != null) continue;

                var texItem = new UndertaleTexturePageItem();
                texItem.Name = Data.Strings.MakeString($"PageItem {Data.TexturePageItems.Count}");
                texItem.SourceX = (ushort)item.x;
                texItem.SourceY = (ushort)item.y;
                texItem.SourceWidth = (ushort)item.w;
                texItem.SourceHeight = (ushort)item.h;
                texItem.TargetX = 0;
                texItem.TargetY = 0;
                texItem.TargetWidth = (ushort)item.w;
                texItem.TargetHeight = (ushort)item.h;
                texItem.BoundingWidth = (ushort)item.w;
                texItem.BoundingHeight = (ushort)item.h;
                texItem.TexturePage = tex;
                Data.TexturePageItems.Add(texItem);

                var spr = new UndertaleSprite();
                spr.Name = Data.Strings.MakeString(item.name);
                spr.Width = (uint)item.w;
                spr.Height = (uint)item.h;
                spr.MarginLeft = 0;
                spr.MarginRight = item.w - 1;
                spr.MarginTop = 0;
                spr.MarginBottom = item.h - 1;
                spr.OriginX = 0;
                spr.OriginY = 0;
                spr.BBoxMode = 0;
                spr.SepMasks = UndertaleSprite.SepMaskType.AxisAlignedRect;
                spr.IsSpecialType = true;
                spr.SVersion = 3;
                spr.GMS2PlaybackSpeed = 1;
                spr.GMS2PlaybackSpeedType = AnimSpeedType.FramesPerGameFrame;
                var entry = new UndertaleSprite.TextureEntry();
                entry.Texture = texItem;
                spr.Textures.Add(entry);
                Data.Sprites.Add(spr);
            }
            Console.WriteLine($"[INFO] Injected {items.Count} controller button sprites into data.win.");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[WARN] Could not inject button sprites: {ex.Message}");
}

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
Console.WriteLine(
    "Mod menu injected successfully.\n\n" +
    "- obj_modmenu created (Create / Step / Draw GUI)\n" +
    "- show_transition hooked\n" +
    "- input guards added (obj_ui_choice + obj_ui_chapter)\n" +
    "- title-screen credit added (obj_ui_version)\n" +
    "- controller button sprites injected (Xbox / PlayStation)\n\n" +
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
