namespace AllOnShelves
{
    /// <summary>
    /// Эффекты из Arcaidia Effector: листы кадров в Resources/VFX (27.09.2026).
    /// Файл создаёт Tools/ArtPipeline/vfx_import.py — руками не править.
    /// </summary>
    public static class VfxCatalog
    {
        public struct Info
        {
            public int Frames, Cols, Rows, CellW, CellH;
            public float Fps;
            public bool Loop;
        }

        public static bool TryGet(string name, out Info info)
        {
            info = default;
            switch (name)
            {
                case "vfx_box_open": info = new Info { Frames = 66, Cols = 9, Rows = 8, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_build_dust": info = new Info { Frames = 72, Cols = 9, Rows = 8, CellW = 192, CellH = 192, Fps = 60f, Loop = true }; return true;
                case "vfx_build_in": info = new Info { Frames = 60, Cols = 8, Rows = 8, CellW = 224, CellH = 224, Fps = 60f, Loop = false }; return true;
                case "vfx_button_press": info = new Info { Frames = 24, Cols = 5, Rows = 5, CellW = 128, CellH = 128, Fps = 60f, Loop = false }; return true;
                case "vfx_cart_overflow": info = new Info { Frames = 60, Cols = 8, Rows = 8, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_chains_fall": info = new Info { Frames = 60, Cols = 8, Rows = 8, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_chest_open": info = new Info { Frames = 96, Cols = 10, Rows = 10, CellW = 204, CellH = 204, Fps = 60f, Loop = false }; return true;
                case "vfx_coin_fountain": info = new Info { Frames = 72, Cols = 9, Rows = 8, CellW = 224, CellH = 224, Fps = 60f, Loop = false }; return true;
                case "vfx_coins_spend": info = new Info { Frames = 60, Cols = 8, Rows = 8, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_combo_ring": info = new Info { Frames = 42, Cols = 7, Rows = 6, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_customer_call": info = new Info { Frames = 48, Cols = 7, Rows = 7, CellW = 160, CellH = 160, Fps = 60f, Loop = false }; return true;
                case "vfx_freezer_open": info = new Info { Frames = 54, Cols = 8, Rows = 7, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_gift_pop": info = new Info { Frames = 60, Cols = 8, Rows = 8, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_hint_glow": info = new Info { Frames = 60, Cols = 8, Rows = 8, CellW = 160, CellH = 160, Fps = 60f, Loop = true }; return true;
                case "vfx_hint_target": info = new Info { Frames = 36, Cols = 6, Rows = 6, CellW = 128, CellH = 128, Fps = 60f, Loop = false }; return true;
                case "vfx_item_land": info = new Info { Frames = 27, Cols = 6, Rows = 5, CellW = 128, CellH = 128, Fps = 60f, Loop = false }; return true;
                case "vfx_item_wrong": info = new Info { Frames = 30, Cols = 6, Rows = 5, CellW = 128, CellH = 128, Fps = 60f, Loop = false }; return true;
                case "vfx_lock_break": info = new Info { Frames = 42, Cols = 7, Rows = 6, CellW = 160, CellH = 160, Fps = 60f, Loop = false }; return true;
                case "vfx_locked_shake": info = new Info { Frames = 27, Cols = 6, Rows = 5, CellW = 128, CellH = 128, Fps = 60f, Loop = false }; return true;
                case "vfx_node_pulse": info = new Info { Frames = 72, Cols = 9, Rows = 8, CellW = 160, CellH = 160, Fps = 60f, Loop = true }; return true;
                case "vfx_pack_tear": info = new Info { Frames = 60, Cols = 8, Rows = 8, CellW = 224, CellH = 224, Fps = 60f, Loop = false }; return true;
                case "vfx_pallet_drop": info = new Info { Frames = 48, Cols = 7, Rows = 7, CellW = 224, CellH = 224, Fps = 60f, Loop = false }; return true;
                case "vfx_piggy_break": info = new Info { Frames = 66, Cols = 9, Rows = 8, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_piggy_coin_drop": info = new Info { Frames = 42, Cols = 7, Rows = 6, CellW = 128, CellH = 128, Fps = 60f, Loop = false }; return true;
                case "vfx_piggy_glow": info = new Info { Frames = 90, Cols = 10, Rows = 9, CellW = 160, CellH = 160, Fps = 60f, Loop = true }; return true;
                case "vfx_rewind_wave": info = new Info { Frames = 54, Cols = 6, Rows = 9, CellW = 256, CellH = 128, Fps = 60f, Loop = false }; return true;
                case "vfx_section_done": info = new Info { Frames = 54, Cols = 8, Rows = 7, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_spoil_stink": info = new Info { Frames = 72, Cols = 9, Rows = 8, CellW = 128, CellH = 128, Fps = 60f, Loop = true }; return true;
                case "vfx_staff_arrive": info = new Info { Frames = 72, Cols = 9, Rows = 8, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_staff_badge_up": info = new Info { Frames = 48, Cols = 7, Rows = 7, CellW = 160, CellH = 160, Fps = 60f, Loop = false }; return true;
                case "vfx_staff_level_up": info = new Info { Frames = 66, Cols = 9, Rows = 8, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_staff_pick": info = new Info { Frames = 30, Cols = 6, Rows = 5, CellW = 128, CellH = 128, Fps = 60f, Loop = false }; return true;
                case "vfx_staff_skill": info = new Info { Frames = 42, Cols = 7, Rows = 6, CellW = 128, CellH = 128, Fps = 60f, Loop = false }; return true;
                case "vfx_star_award": info = new Info { Frames = 54, Cols = 8, Rows = 7, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_tile_bought": info = new Info { Frames = 42, Cols = 7, Rows = 6, CellW = 160, CellH = 160, Fps = 60f, Loop = false }; return true;
                case "vfx_undo_swirl": info = new Info { Frames = 42, Cols = 7, Rows = 6, CellW = 160, CellH = 160, Fps = 60f, Loop = false }; return true;
                case "vfx_crate_open": info = new Info { Frames = 72, Cols = 9, Rows = 8, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_reward_rays": info = new Info { Frames = 60, Cols = 8, Rows = 8, CellW = 184, CellH = 184, Fps = 60f, Loop = true }; return true;
                case "vfx_challenge_win": info = new Info { Frames = 84, Cols = 10, Rows = 9, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_daily_dot_fill": info = new Info { Frames = 30, Cols = 6, Rows = 5, CellW = 128, CellH = 128, Fps = 60f, Loop = false }; return true;
                case "vfx_quest_done": info = new Info { Frames = 42, Cols = 7, Rows = 6, CellW = 160, CellH = 160, Fps = 60f, Loop = false }; return true;
                case "vfx_quest_token_fly": info = new Info { Frames = 36, Cols = 5, Rows = 8, CellW = 192, CellH = 96, Fps = 60f, Loop = false }; return true;
                case "vfx_chest_ready": info = new Info { Frames = 72, Cols = 9, Rows = 8, CellW = 160, CellH = 160, Fps = 60f, Loop = true }; return true;
                case "vfx_month_prize": info = new Info { Frames = 96, Cols = 10, Rows = 10, CellW = 204, CellH = 204, Fps = 60f, Loop = false }; return true;
                case "vfx_streak_up": info = new Info { Frames = 48, Cols = 7, Rows = 7, CellW = 128, CellH = 128, Fps = 60f, Loop = false }; return true;
                case "vfx_streak_lost": info = new Info { Frames = 54, Cols = 8, Rows = 7, CellW = 128, CellH = 128, Fps = 60f, Loop = false }; return true;
                case "vfx_streak_flame": info = new Info { Frames = 60, Cols = 8, Rows = 8, CellW = 128, CellH = 128, Fps = 60f, Loop = true }; return true;
                case "vfx_gem_burst": info = new Info { Frames = 54, Cols = 8, Rows = 7, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_gem_shine": info = new Info { Frames = 120, Cols = 11, Rows = 11, CellW = 128, CellH = 128, Fps = 60f, Loop = true }; return true;
                case "vfx_boost_time": info = new Info { Frames = 48, Cols = 7, Rows = 7, CellW = 160, CellH = 160, Fps = 60f, Loop = false }; return true;
                case "vfx_hammer_instant": info = new Info { Frames = 60, Cols = 8, Rows = 8, CellW = 192, CellH = 192, Fps = 60f, Loop = false }; return true;
                case "vfx_gold_path_unlock": info = new Info { Frames = 84, Cols = 5, Rows = 17, CellW = 256, CellH = 64, Fps = 60f, Loop = false }; return true;
                case "vfx_difficulty_unlock": info = new Info { Frames = 96, Cols = 10, Rows = 10, CellW = 204, CellH = 204, Fps = 60f, Loop = false }; return true;
                case "vfx_cash_collect": info = new Info { Frames = 48, Cols = 7, Rows = 7, CellW = 160, CellH = 160, Fps = 60f, Loop = false }; return true;
                case "vfx_reno_marker_pulse": info = new Info { Frames = 72, Cols = 9, Rows = 8, CellW = 128, CellH = 128, Fps = 60f, Loop = true }; return true;
                case "vfx_dept_complete": info = new Info { Frames = 84, Cols = 10, Rows = 9, CellW = 204, CellH = 204, Fps = 60f, Loop = false }; return true;
                case "vfx_rank_up": info = new Info { Frames = 48, Cols = 4, Rows = 12, CellW = 224, CellH = 56, Fps = 60f, Loop = false }; return true;
                case "vfx_skin_equip": info = new Info { Frames = 42, Cols = 7, Rows = 6, CellW = 160, CellH = 160, Fps = 60f, Loop = false }; return true;
                default: return false;
            }
        }
    }
}
