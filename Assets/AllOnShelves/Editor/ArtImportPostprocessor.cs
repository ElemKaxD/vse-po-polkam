using UnityEditor;
using UnityEngine;

namespace AllOnShelves.EditorTools
{
    /// <summary>
    /// Настройки импорта графики «Всё по полкам!» (ГДД 13.3): спрайт, PPU 100, без мипмапов,
    /// пивот по группе (персонажи, здания, тележка и секции — низ-центр), сжатие.
    /// Срабатывает только для Assets/AllOnShelves/Art.
    /// </summary>
    public class ArtImportPostprocessor : AssetPostprocessor
    {
        const string ArtRoot = "Assets/AllOnShelves/Art/";

        const string VfxRoot = "Assets/AllOnShelves/Resources/VFX/";

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(VfxRoot)) { Vfx(); return; }
            if (!assetPath.StartsWith(ArtRoot)) return;
            var ti = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100;
            ti.mipmapEnabled = false;
            // полнокадровые — только непрозрачные фоны. album_book_flat и слои ремонта прозрачные:
            // если списать им альфу, прозрачный фон станет чёрным (23.09.2026)
            // голый магазин района — вырезанный (пак 26.09), альфу у него не снимаем,
            // а отдельный фон улицы meta_lego_dNN_bg, наоборот, плотный.
            // Правило «*_base — полный кадр» убрано 29.09.2026: под него попала табличка полки brd_tag_base,
            // и её прозрачные углы стали чёрными. Проверка «Проверить сцены» теперь ловит такие картинки.
            bool fullFrame = file.StartsWith("bg_") || file.StartsWith("map_segment_") || file == "album_book"
                             || file.StartsWith("album_desk") || file.StartsWith("shop_page_") || file.StartsWith("lb_page_")
                             || (file.StartsWith("meta_lego_") && file.EndsWith("_bg"))
                             // Торговый дом (03.10.2026): площадь и голые комнаты — плотные кадры
                             || file == "team_bg" || (file.StartsWith("room_") && file.EndsWith("_base"));
            // полнокадровые картинки без альфы — DXT1, вдвое легче
            ti.alphaSource = fullFrame ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = !fullFrame;
            ti.wrapMode = file.StartsWith("brd_belt_tile") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.crunchedCompression = true;
            ti.compressionQuality = fullFrame ? 45 : 55;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            // скины ленты нарисованы целой лентой с торцами-роликами: торцы — края спрайта (рисуются один раз
            // по концам), повторяется только середина. Без этого торцы шли через каждую плитку — «рваная» лента
            // (02.10.2026). Ширина торца — по картинке (ролик ≈ высота ленты)
            if (BeltCaps.TryGetValue(file, out int cap)) settings.spriteBorder = new Vector4(cap, 0, cap, 0);
            settings.spriteAlignment = (int)(IsBottomPivot(file) ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            ti.SetTextureSettings(settings);

            var web = ti.GetPlatformTextureSettings("WebGL");
            web.overridden = true;
            web.maxTextureSize = 2048;
            web.format = TextureImporterFormat.Automatic;
            web.textureCompression = TextureImporterCompression.Compressed;
            web.crunchedCompression = true;
            web.compressionQuality = fullFrame ? 45 : 55;
            ti.SetPlatformTextureSettings(web);
        }

        /// <summary>
        /// Листы эффектов (Arcaidia Effector, 27.09.2026): обычные текстуры для RawImage, без мипмапов,
        /// crunch-сжатие. С 29.09.2026 эффекты в 60 кадров — листы до 2048 (предел WebGL); меньше нельзя:
        /// при уменьшении ячейки расплываются, а границы кадров перестают совпадать с сеткой.
        /// </summary>
        void Vfx()
        {
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = false;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.crunchedCompression = true;
            ti.compressionQuality = 50;
            var web = ti.GetPlatformTextureSettings("WebGL");
            web.overridden = true;
            web.maxTextureSize = 2048;
            web.format = TextureImporterFormat.Automatic;
            web.textureCompression = TextureImporterCompression.Compressed;
            web.crunchedCompression = true;
            web.compressionQuality = 50;
            ti.SetPlatformTextureSettings(web);
            ti.maxTextureSize = 2048;
        }

        static readonly System.Collections.Generic.Dictionary<string, int> BeltCaps = new System.Collections.Generic.Dictionary<string, int>
        {
            // ленты v4 (03.10.2026) — одна форма у всех: торец-ролик ≈ 44 точки с каждой стороны
            { "brd_belt_tile_candy", 44 }, { "brd_belt_tile_gold", 44 }, { "brd_belt_tile_winter", 44 },
            { "brd_belt_tile_neon", 44 }, { "brd_belt_tile_wood", 44 },
        };

        static bool IsBottomPivot(string f) =>
            f.StartsWith("chr_") || (f.StartsWith("meta_") && !f.StartsWith("meta_lego_")) || f.StartsWith("brd_cart") || f.StartsWith("brd_section")
            || f.StartsWith("brd_freezer") || f.StartsWith("map_van") || f.StartsWith("ui_coin_pile")
            || f.StartsWith("obj_pallet") || f.StartsWith("ovl_stink");
    }
}
