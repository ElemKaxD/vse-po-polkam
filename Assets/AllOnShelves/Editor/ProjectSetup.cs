using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace AllOnShelves.EditorTools
{
    /// <summary>
    /// Меню «Всё по полкам → 1. Подготовить проект»: 9-slice для UI, шрифт с кириллицей,
    /// библиотека арта, звуки, настройки YG2 и WebGL. Безопасно запускать повторно.
    /// </summary>
    public static class ProjectSetup
    {
        public const string Root = "Assets/AllOnShelves";
        public const string FontPath = Root + "/Fonts/Nunito SDF.asset";
        public const string OutlineMatPath = Root + "/Fonts/Nunito SDF Outline.mat";
        public const string ArtLibraryPath = Root + "/Resources/ArtLibrary.asset";

        [MenuItem("Всё по полкам/1. Подготовить проект", priority = 1)]
        public static void All()
        {
            SliceBorders();
            CreateFont();
            FixAlpha();
            BuildArtLibrary();
            ConfigureYG();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[AllOnShelves] Проект подготовлен");
        }

        // ------------------------------------------------------------------ 9-slice

        // режутся только простые панели и рамки (UiBuild.Sliceable); у остальных границы снимаются,
        // чтобы кнопки, плашки и ценники нельзя было случайно растянуть
        static readonly string[] Unsliced =
        {
            "ui_btn_primary", "ui_btn_rewarded", "ui_btn_secondary", "ui_btn_green", "ui_btn_round",
            "ui_pill_counter", "ui_speech_bubble", "brd_tag_base",
            // с рисунком по краям — только целиком, в родных пропорциях
            "ui_panel_card", "ui_panel_popup", "brd_shelf_frame", "ui_panel_clipboard_flat2d_generated",
            "ui_panel_shop_product_flat2d_generated", "brd_section", "brd_section 1", "brd_section_big",
        };

        /// <summary>
        /// Unity иногда импортирует новый PNG с alphaSource = None (прозрачный фон становится чёрным).
        /// Поправляем всё разом: если у файла есть альфа — включаем её (23.09.2026, album_book_flat).
        /// </summary>
        public static void FixAlpha()
        {
            int fixedCount = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Art" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                if (ti.alphaSource == TextureImporterAlphaSource.FromInput && ti.alphaIsTransparency) continue;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
                fixedCount++;
            }
            if (fixedCount > 0) Debug.Log($"[AllOnShelves] Прозрачность включена у {fixedCount} картинок");
        }

        public static void SliceBorders()
        {
            foreach (var name in UiBuild.Sliceable.Concat(Unsliced))
            {
                var path = FindArt(name);
                if (path == null) continue;
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.GetSourceTextureWidthAndHeight(out int w, out int h);
                Vector4 border;
                if (Unsliced.Contains(name)) border = Vector4.zero;
                else if (name == "brd_belt_tile") border = new Vector4(62, 0, 62, 0);
                // полосы-«таблетки»: закругление по краям равно половине высоты, иначе торцы поедут
                else if (name.StartsWith("ui_bar_")) { float b = Mathf.Floor(h * 0.56f); border = new Vector4(b, 0, b, 0); }
                // стоячая «таблетка» (ползунок рейтинга): закругления сверху и снизу
                else if (name.StartsWith("ui_scroll_")) { float b = Mathf.Floor(w * 0.56f); border = new Vector4(0, b, 0, b); }
                else
                {
                    float b = Mathf.Floor(Mathf.Min(w, h) * 0.34f);
                    border = new Vector4(b, b, b, b);
                }
                if (ti.spriteBorder == border) continue;
                ti.spriteBorder = border;
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                st.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(st);
                ti.SaveAndReimport();
            }
            UiBuild.ResetCache();
        }

        public static string FindArt(string name)
        {
            foreach (var g in AssetDatabase.FindAssets(name + " t:Texture2D", new[] { Root + "/Art" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == name) return p;
            }
            return null;
        }

        // ------------------------------------------------------------------ шрифт

        const string Chars =
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            " .,:;!?…-–—+×=/%«»\"'()[]№*#@&₽∞<>|_~^$€";

        public static TMP_FontAsset CreateFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (existing != null)
            {
                EnsureOutline(existing);
                SetDefaultFont(existing);
                return existing;
            }
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/Nunito-ExtraBold.ttf");
            var fa = TMP_FontAsset.CreateFontAsset(ttf, 72, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            fa.name = "Nunito SDF";
            AssetDatabase.CreateAsset(fa, FontPath);
            fa.material.name = "Nunito SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.atlasTexture.name = "Nunito SDF Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
            fa.TryAddCharacters(Chars, out _);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            EnsureOutline(fa);
            SetDefaultFont(fa);
            return fa;
        }

        static void EnsureOutline(TMP_FontAsset fa)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);
            if (mat == null)
            {
                mat = new Material(fa.material) { name = "Nunito SDF Outline" };
                AssetDatabase.CreateAsset(mat, OutlineMatPath);
            }
            mat.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(0x4A, 0x2F, 0x24, 255));
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
            EditorUtility.SetDirty(mat);
            UiBuild.Font = fa;
            UiBuild.OutlineMat = mat;
        }

        static void SetDefaultFont(TMP_FontAsset fa)
        {
            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var p = so.FindProperty("m_defaultFontAsset");
            if (p != null) { p.objectReferenceValue = fa; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        // ------------------------------------------------------------------ библиотека арта и звуки

        [MenuItem("Всё по полкам/Обновить библиотеку арта", priority = 20)]
        public static void BuildArtLibrary()
        {
            Directory.CreateDirectory(Root + "/Resources");
            var lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>(ArtLibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(lib, ArtLibraryPath);
            }
            lib.sprites = AssetDatabase.FindAssets("t:Sprite", new[] { Root + "/Art" })
                .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null)
                // карта, фоны меты и магазины грузятся из сцен, но библиотека даёт доступ по имени для кода
                .OrderBy(s => s.name).ToArray();
            lib.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AllOnShelves] ArtLibrary: {lib.sprites.Length} спрайтов");
        }

        [MenuItem("Всё по полкам/Обновить звуки", priority = 21)]
        public static void RefreshAudio()
        {
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { Root })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null).ToArray();
            foreach (var path in new[] { SceneBuilder.AppPrefabPath }.Where(File.Exists))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                var audio = root.GetComponent<AudioService>();
                if (audio != null)
                {
                    audio.clips = clips;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            // музыка — потоковая загрузка, эффекты — в памяти (ГДД 14.1)
            foreach (var c in clips)
            {
                var path = AssetDatabase.GetAssetPath(c);
                var ai = (AudioImporter)AssetImporter.GetAtPath(path);
                bool music = path.Contains("/Music/") && !Path.GetFileName(path).StartsWith("jingle");
                var s = ai.defaultSampleSettings;
                s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = music ? 0.45f : 0.6f;
                ai.defaultSampleSettings = s;
                // музыка тоже моно: два трека занимали 4,9 МБ билда, а стерео у фоновой музыки
                // в динамиках ноутбука и телефона не слышно (27.09.2026)
                ai.forceToMono = true;
                ai.loadInBackground = music;
                ai.SaveAndReimport();
            }
            Debug.Log($"[AllOnShelves] Звуков найдено: {clips.Length}");
        }

        // ------------------------------------------------------------------ YG2

        public static void ConfigureYG()
        {
            var info = Resources.Load("SettingsYG2");
            if (info == null) { Debug.LogWarning("SettingsYG2 не найден"); return; }
            var so = new SerializedObject(info);
            var gra = so.FindProperty("Basic.autoGRA");
            if (gra != null) gra.boolValue = false; // GameReady вызываем сами, когда показан первый экран
            // фон заставки загрузки — наш WebGLTemplates/YandexGames/Images/background.jpg (30.09.2026: стоял
            // «Градиент», а index.html всё равно просил background.png — 404 и пустая заставка)
            var bgFormat = so.FindProperty("Templates.backgroundImgFormat");
            if (bgFormat != null) bgFormat.intValue = 4;   // InfoYG.TemplatesSettings.BackgroundImageFormat.JPG
            var logoFormat = so.FindProperty("Templates.logoImageFormat");
            if (logoFormat != null) logoFormat.intValue = 1;  // LogoImgFormat.PNG: логотип енота на заставке (Images/logo.png)

            var pur = so.FindProperty("Payments.purshases");
            if (pur != null)
            {
                var products = MetaCatalog.Products;
                pur.arraySize = products.Length;
                for (int i = 0; i < products.Length; i++)
                {
                    var e = pur.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("id").stringValue = products[i].Id;
                    e.FindPropertyRelative("title").stringValue = products[i].Title;
                    e.FindPropertyRelative("description").stringValue = products[i].Description;
                    e.FindPropertyRelative("price").stringValue = products[i].FallbackPrice;
                    e.FindPropertyRelative("priceValue").stringValue = new string(products[i].FallbackPrice.Where(char.IsDigit).ToArray());
                    e.FindPropertyRelative("priceCurrencyCode").stringValue = "RUB";
                    e.FindPropertyRelative("consumed").boolValue = true;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(info);
        }

        // ------------------------------------------------------------------ WebGL

        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "AllOnShelves";
            PlayerSettings.productName = "Всё по полкам!";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.stripEngineCode = true;
            // в Unity 6 заставку можно выключить на любой лицензии: минус 2,7 МБ картинки и ~2 с
            // на старте; у шаблона Яндекса своя полоса загрузки (27.09.2026)
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
        }
    }
}
