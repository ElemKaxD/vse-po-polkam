using System.IO;
using AllOnShelves.Game;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static AllOnShelves.EditorTools.UiBuild;

namespace AllOnShelves.EditorTools
{
    /// <summary>
    /// «Всё по полкам → 2. Собрать сцены». Создаёт сцены Boot, Hub, Game со всеми объектами
    /// и сохраняет их. После сборки сцены — обычные ассеты: объекты можно двигать и настраивать руками.
    /// ВНИМАНИЕ: повторный запуск перезапишет сцены.
    /// </summary>
    public static partial class SceneBuilder
    {
        public const string ScenesDir = ProjectSetup.Root + "/Scenes";
        public const string PrefabsDir = ProjectSetup.Root + "/Prefabs";
        public const string AppPrefabPath = ProjectSetup.Root + "/Resources/App.prefab";
        public const string ItemPrefabPath = PrefabsDir + "/ItemView.prefab";

        static readonly Color Dark = new Color32(0x4A, 0x2F, 0x24, 255);

        [MenuItem("Всё по полкам/2. Собрать сцены (перезапишет Boot, Hub, Game)", priority = 2)]
        public static void BuildAll()
        {
            if (!EditorUtility.DisplayDialog("Собрать сцены", "Сцены Boot, Hub и Game будут созданы заново.\n\n" +
                    "Ваши перемещения, размеры, повороты и размеры шрифта сохранятся (Всё по полкам → Раскладка).\n" +
                    "Пропадут только объекты, добавленные вручную, и другие изменения компонентов. Продолжить?", "Собрать", "Отмена"))
                return;
            BuildAllSilent();
        }

        /// <summary>Для автоматизации (Tools/rebuild.py): то же без окна подтверждения.</summary>
        [MenuItem("Всё по полкам/Служебное/Собрать сцены без подтверждения", priority = 100)]
        static void BuildAllNoPrompt() => BuildAllSilent();

        public static void BuildAllSilent()
        {
            // сначала запомнить, что пользователь подвинул мышкой, — после сборки это применится к новым объектам
            UserLayout.CaptureAll();
            ProjectSetup.CreateFont();
            UiBuild.ResetCache();
            Directory.CreateDirectory(ScenesDir);
            Directory.CreateDirectory(PrefabsDir);
            BuildAppPrefab();
            BuildItemPrefab();
            BuildBoot();
            BuildGame();
            BuildHub();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenesDir + "/Boot.unity", true),
                new EditorBuildSettingsScene(ScenesDir + "/Hub.unity", true),
                new EditorBuildSettingsScene(ScenesDir + "/Game.unity", true),
            };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenesDir + "/Boot.unity");
            Debug.Log("[AllOnShelves] Сцены собраны: Boot, Hub, Game");
        }

        // ================================================================ общие части

        /// <summary>Надпись на ленте гнём дугой: лента изогнута, прямая строка смотрится наклейкой.</summary>
        static void Arc(TMP_Text text, float ribbonHeight)
        {
            var a = text.gameObject.AddComponent<AllOnShelves.UI.TextArc>();
            a.bend = ribbonHeight * 0.077f;   // середина кремовой полосы выше краёв на 7,7 % высоты ленты (снято с ui_ribbon_banner)
            a.tilt = 6f;
        }

        static Scene NewScene(string name)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera");
            var c = cam.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color32(0xFF, 0xF4, 0xE3, 255);
            c.orthographic = true;
            c.cullingMask = 0;
            cam.tag = "MainCamera";
            cam.AddComponent<AudioListener>();
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
            return scene;
        }

        static Canvas NewCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<AdaptiveScaler>();
            return canvas;
        }

        public const string BoardLayoutPath = ProjectSetup.Root + "/Resources/BoardLayout.asset";

        /// <summary>Раскладка поля — отдельный ассет: сборка сцен его не перезаписывает.</summary>
        public static BoardLayout BoardLayoutAsset()
        {
            var a = AssetDatabase.LoadAssetAtPath<BoardLayout>(BoardLayoutPath);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<BoardLayout>();
            AssetDatabase.CreateAsset(a, BoardLayoutPath);
            AssetDatabase.SaveAssets();
            return a;
        }

        static void Save(Scene scene, string name)
        {
            UserLayout.ApplyAndSnapshot(scene, name);
            EditorSceneManager.SaveScene(scene, ScenesDir + "/" + name + ".unity");
        }

        /// <summary>Окно: затемнение + панель. Возвращает корень (с CanvasGroup) и панель.</summary>
        /// <summary>Высота окна по ширине: панель берём целиком, в родных пропорциях (по краям рисунок).</summary>
        public static Vector2 PanelSize(float width) => ByWidth("ui_panel_popup", width);

        /// <summary>Енот на экране надевает скин из «Гардероба» (картинка позы в костюме), 03.10.2026.</summary>
        static Image Skin(Image img, string pose)
        {
            img.gameObject.AddComponent<RaccoonSkin>().pose = pose;
            return img;
        }

        static (RectTransform root, RectTransform panel) PopupShell(string name, Transform parent, Vector2 panelSize, string title, out TextMeshProUGUI titleText,
                                                                   string sprite = "ui_panel_popup")
        {
            var root = Stretch(Node(name, parent));
            Group(root.gameObject);
            Dim(root);
            // панель не режем на 9 частей: у неё рисунок по краям — он «поедет».
            // Рамку выбираем под смысл окна (просьба 26.09.2026): праздничная, тёмная, бумага, магазин
            panelSize = ByWidth(sprite, panelSize.x);
            var panel = Img("Panel", root, sprite, new Vector2(0, -10), panelSize, raycast: true).rectTransform;
            // заголовок — на деревянной табличке: лента осталась только у карты (просьба 22.09.2026)
            var plateSz = ByWidth(TitlePlate, Mathf.Clamp(panelSize.x * 0.62f, 400f, 580f));
            var plate = Img("Ribbon", panel, TitlePlate, new Vector2(0, panelSize.y / 2f - plateSz.y * 0.12f), plateSz);
            titleText = PlateTitle(plate.rectTransform, title, 50);
            return (root, panel);
        }

        /// <summary>Табличка для заголовков окон и экранов: доска с листьями по углам (не тянется — ставится целиком).</summary>
        public const string TitlePlate = "ui_header_wood";

        /// <summary>Надпись на доске: белая с тёмным контуром — по дереву тёмный текст теряется.
        /// Доска прямая, поэтому строка тоже прямая.</summary>
        public static TextMeshProUGUI PlateTitle(RectTransform plate, string title, float size)
        {
            var sz = plate.sizeDelta;
            // доски занимают нижние 61 % картинки (сверху — листья с цветами): их середина на 10 % высоты
            // ниже центра картинки. По центру картинки надпись садилась на верхнюю доску (обход 29.09.2026)
            // шурупы досок — на 73 % ширины картинки: надпись уже, иначе длинная («Уровень пройден!») на них наезжала
            var t = Text("Title", plate, title, size, new Vector2(0, -sz.y * 0.10f),
                         new Vector2(sz.x * 0.60f, sz.y * 0.50f), Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            t.enableWordWrapping = false; // длинный заголовок уменьшается, а не переносится за края доски
            return t;
        }

        static T AddPopup<T>(RectTransform root, RectTransform panel) where T : Popup
        {
            var p = root.gameObject.AddComponent<T>();
            p.group = root.GetComponent<CanvasGroup>();
            p.panel = panel;
            return p;
        }

        static Toast BuildToast(Transform parent)
        {
            // «таблетка» из комплекта полос вместо тёмной кляксы из плитки (просьба 26.09.2026)
            var size = new Vector2(880, 94);
            var rt = Anchor(Node("Toast", parent), new Vector2(0.5f, 0f), new Vector2(0, 180), size);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = S("ui_toast") ?? S("ui_bar_track");
            bg.type = Image.Type.Sliced;
            if (bg.sprite != null) bg.pixelsPerUnitMultiplier = Mathf.Max(1f, bg.sprite.rect.height / size.y);
            bg.raycastTarget = false;
            var t = rt.gameObject.AddComponent<Toast>();
            t.group = Group(rt.gameObject);
            t.group.blocksRaycasts = false;
            t.group.alpha = 0f; // в редакторе не закрывает поле; появляется только с сообщением
            t.icon = Img("Icon", rt, "icon_coin", new Vector2(-size.x / 2f + 56, 0), new Vector2(56, 56));
            t.icon.gameObject.SetActive(false);
            // рамка по высоте в одну строку: так автоподбор реально ужимает длинную надпись
            t.label = Text("Text", rt, "", 34, Vector2.zero, new Vector2(size.x - 130, 46), Dark,
                           TextAlignmentOptions.Center, autoSize: true);
            t.label.fontSizeMin = 20;
            return t;
        }

        // ================================================================ префабы

        static void BuildAppPrefab()
        {
            var go = new GameObject("App");
            go.AddComponent<GameApp>();
            go.AddComponent<BotBridge>();   // мост BuildBot: SendMessage("BotBridge","Cmd",json)
            var audio = go.AddComponent<AudioService>();
            var music = go.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false;
            var sfx = go.AddComponent<AudioSource>(); sfx.playOnAwake = false;
            audio.musicSource = music;
            audio.sfxSource = sfx;
            PrefabUtility.SaveAsPrefabAsset(go, AppPrefabPath);
            Object.DestroyImmediate(go);
            ProjectSetup.RefreshAudio();
        }

        static void BuildItemPrefab()
        {
            var root = new GameObject("ItemView", typeof(RectTransform));
            root.layer = 5;
            var rt = Rect((RectTransform)root.transform, Vector2.zero, new Vector2(112, 112));
            var hit = root.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);
            hit.raycastTarget = true;
            var v = root.AddComponent<ItemView>();
            v.rect = rt;
            v.group = root.AddComponent<CanvasGroup>();
            var visual = Rect(Node("Visual", rt), Vector2.zero, new Vector2(112, 112));
            v.visual = visual;
            // овальная тень под товаром (за иконкой, на нижнем крае); позицию ставит ItemView.PlaceShadow
            v.shadow = Img("Shadow", visual, "fx_shadow_oval", new Vector2(0, -48), new Vector2(106, 36),
                           color: new Color(0.12f, 0.08f, 0.05f, 0.55f));
            v.shadow.transform.SetAsFirstSibling();
            // зелёный кружок доступности — без подкраски, иначе зелёный уходит в бежевый
            v.glow = Img("Glow", visual, "brd_access_ring", Vector2.zero, new Vector2(150, 150), color: new Color(1f, 1f, 1f, 0.95f));
            v.palletBase = Img("Pallet", visual, "obj_pallet", new Vector2(0, -34), new Vector2(128, 64));
            v.icon = Img("Icon", visual, "item_apple", Vector2.zero, new Vector2(104, 104));
            v.icon2 = Img("Icon2", visual, "item_apple", Vector2.zero, new Vector2(104, 104));
            v.box = Img("Box", visual, "obj_box_closed", Vector2.zero, new Vector2(110, 110));
            v.tape = Img("Tape", visual, "obj_tape", Vector2.zero, new Vector2(110, 110));
            v.frost = Img("Frost", visual, "ovl_frost", Vector2.zero, new Vector2(112, 112), color: new Color(1, 1, 1, 0.55f));
            v.fly = Img("Fly", visual, "ovl_fly", new Vector2(30, 40), new Vector2(48, 48));
            v.timerBadge = Img("Timer", visual, "ovl_timer_badge", new Vector2(38, 38), new Vector2(54, 54));
            v.timerText = Text("TimerText", v.timerBadge.transform, "4", 30, new Vector2(0, -1), new Vector2(54, 54), Color.white, TextAlignmentOptions.Center, true);
            foreach (var g in new Graphic[] { v.icon2, v.box, v.tape, v.frost, v.fly, v.timerBadge, v.palletBase, v.glow }) g.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, ItemPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void InstantiateApp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AppPrefabPath);
            PrefabUtility.InstantiatePrefab(prefab);
        }

        // ================================================================ Boot

        static void BuildBoot()
        {
            var scene = NewScene("Boot");
            InstantiateApp();
            var canvas = NewCanvas("BootCanvas");
            var root = canvas.transform;
            Background("Background", root, "bg_loading");
            Img("Logo", root, "logo_main", new Vector2(0, 250), ByWidth("logo_main", 760f));

            // полоса загрузки: высота = родной высоте картинки, тянется только серединой (9-slice)
            const float barW = 900f, barH = 82f;
            var frame = Img("ProgressFrame", root, "ui_bar_track", new Vector2(0, -300), new Vector2(barW, barH), sliced: true, preserve: false);
            var fill = Img("ProgressFill", frame.transform, "ui_bar_fill", new Vector2(0, 0), new Vector2(barW - 22f, barH - 22f),
                           sliced: true, preserve: false, color: new Color32(0x58, 0xA6, 0x5C, 255));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            var fillRt = fill.rectTransform;
            fillRt.pivot = new Vector2(0f, 0.5f); // заполняется слева направо от края полосы
            fillRt.anchorMin = fillRt.anchorMax = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = new Vector2(13f, 0f);
            SlicedFill.Attach(fill);

            // енот бежит по полосе: ноги стоят на её верхнем крае, едет вместе с заполнением
            var raccoon = Img("Raccoon", frame.transform, "chr_raccoon_run_cart", Vector2.zero, ByHeight("chr_raccoon_run_cart", 190f));
            Skin(raccoon, "run_cart");
            var rr = raccoon.rectTransform;
            rr.anchorMin = rr.anchorMax = new Vector2(0f, 1f);
            rr.pivot = new Vector2(0.5f, 0f);
            rr.anchoredPosition = new Vector2(0f, -14f);

            Text("Loading", root, "Загружаем товар...", 40, new Vector2(0, -390), new Vector2(800, 60), Dark);
            var boot = canvas.gameObject.AddComponent<BootController>();
            boot.progressFill = fill;
            boot.progressFrame = frame.rectTransform;
            boot.raccoon = rr;
            Save(scene, "Boot");
        }

        // ================================================================ Game

        static void BuildGame()
        {
            var scene = NewScene("Game");
            var canvas = NewCanvas("GameCanvas");
            var root = (RectTransform)canvas.transform;
            var gc = canvas.gameObject.AddComponent<GameController>();
            gc.canvas = canvas;
            gc.background = Background("Background", root, "bg_level_kiosk");
            gc.itemPrefab = AssetDatabase.LoadAssetAtPath<ItemView>(ItemPrefabPath);
            gc.boardLayout = BoardLayoutAsset();

            var board = Stretch(Node("Board", root));
            gc.boardRoot = board;

            // ---------- ленты
            gc.belts = new BeltView[2];
            for (int b = 0; b < 2; b++)
            {
                var beltRoot = Rect(Node($"Belt{b + 1}", board), new Vector2(0, b == 0 ? 330 : 255), new Vector2(1400, 170));
                var body = Img("BeltBody", beltRoot, "brd_belt_tile", new Vector2(-10, -30), new Vector2(1360, 120), sliced: true, preserve: false);
                var hatch = Img("Hatch", beltRoot, "brd_hatch", new Vector2(690, 10), new Vector2(210, 180));
                var window = Img("WindowFrame", beltRoot, "brd_slot_glow", new Vector2(-470, 0), new Vector2(460, 160), sliced: true,
                                 color: new Color(1f, 0.93f, 0.55f, 0.55f), preserve: false);
                var itemsArea = Rect(Node("ItemsArea", beltRoot), new Vector2(-30, 10), new Vector2(1300, 120));
                var bv = beltRoot.gameObject.AddComponent<BeltView>();
                bv.rect = itemsArea;
                bv.hatch = hatch.rectTransform;
                bv.windowFrame = window.rectTransform;
                bv.itemSize = 112;
                bv.body = body;
                body.transform.SetAsFirstSibling();
                gc.belts[b] = bv;
                if (b == 1) beltRoot.gameObject.SetActive(false);
            }

            // ---------- стеллаж и секции
            var shelf = Rect(Node("Shelf", board), new Vector2(0, -17), new Vector2(1260, 545));
            gc.shelfRoot = shelf;
            // общей рамки стеллажа больше нет: каждая секция — отдельный шкаф в родных пропорциях,
            // растягивать нечего (прежняя brd_shelf_frame резалась на 9 частей и гнула цветы по краям)
            gc.shelfFrame = Img("ShelfFrame", shelf, null, Vector2.zero, new Vector2(1260, 545));
            gc.shelfFrame.enabled = false;
            var sectionsRoot = Rect(Node("Sections", shelf), Vector2.zero, new Vector2(1240, 545));
            gc.sections = new SectionView[9];
            for (int i = 0; i < 9; i++) gc.sections[i] = BuildSection(sectionsRoot, i);

            // ---------- тележка
            gc.cart = BuildCart(board);

            // ---------- заказ
            // пустой держатель: его двигает пользователь, а высоту карточки пересчитывает код
            var goalsPanel = Anchor(Node("GoalsPanel", root), new Vector2(0f, 0.5f), new Vector2(214, 140), new Vector2(320, 420));
            var gp = goalsPanel.gameObject.AddComponent<GoalPanelView>();
            // планшет с заказом — целиком, в родных пропорциях (его выбрал пользователь)
            var gpSize = ByHeight(GoalPanelView.Sprite, 544f);
            var gpBg = Img("Bg", goalsPanel, GoalPanelView.Sprite, new Vector2(-12, -81), gpSize);
            gp.panel = gpBg.rectTransform;
            var gpTitle = Text("Title", gpBg.transform, "Заказ", 36, Vector2.zero, new Vector2(230, 52), Dark);
            Anchor(gpTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -GoalPanelView.Head - GoalPanelView.TitleH / 2f), new Vector2(230, 52));
            gp.rows = new GoalRow[GoalPanelView.MaxRows];
            for (int i = 0; i < gp.rows.Length; i++)
            {
                // одна колонка: значок — число — галочка
                var r = Anchor(Node($"Goal{i + 1}", gpBg.transform), new Vector2(0.5f, 1f),
                               new Vector2(0, -GoalPanelView.Head - GoalPanelView.TitleH - GoalPanelView.Step / 2f - i * GoalPanelView.Step),
                               new Vector2(250, 60));
                var gr = r.gameObject.AddComponent<GoalRow>();
                gr.icon = Img("Icon", r, "item_apple", new Vector2(-86, 0), ByHeight("item_apple", 52f));
                gr.text = Text("Count", r, "0/3", 32, new Vector2(4, -2), new Vector2(104, 48), Dark, TextAlignmentOptions.Left);
                gr.check = Img("Check", r, "icon_check", new Vector2(96, 0), ByHeight("icon_check", 42f));
                gr.check.gameObject.SetActive(false);
                gp.rows[i] = gr;
            }
            gc.goals = gp;
            gc.goalsPanel = goalsPanel.gameObject;

            // ---------- покупатель
            gc.customer = BuildCustomer(root);

            // ---------- слои товаров и эффектов
            gc.itemsRoot = Stretch(Node("Items", root));
            gc.fxRoot = Stretch(Node("FX", root));

            // ---------- HUD
            gc.hud = BuildHud(root);

            // ---------- час пик
            var rush = Anchor(Node("RushHud", root), new Vector2(0.5f, 1f), new Vector2(0, -74), new Vector2(900, 148));
            // время — в рамке сверху по центру: раньше это была голая жёлтая полоска поперёк поля
            // полоса времени — по центру между счётчиками, ниже верхнего края экрана
            var timerFrame = Img("TimerFrame", rush, "ui_bar_track", new Vector2(0, -4), new Vector2(560, 54), sliced: true, preserve: false);
            var timerBar = Img("TimerFill", timerFrame.transform, "ui_bar_fill", Vector2.zero, new Vector2(540, 34),
                               sliced: true, color: new Color(1f, 0.78f, 0.24f), preserve: false);
            timerBar.type = Image.Type.Filled; timerBar.fillMethod = Image.FillMethod.Horizontal;
            SlicedFill.Attach(timerBar);
            gc.rushTimerFill = timerBar;

            // «пробка» и счёт — слева, на месте планшета заказа: раньше они лежали прямо на ленте
            Text("JamLabel", rush, "Пробка", 30, new Vector2(-700, -330), new Vector2(320, 44), Color.white, TextAlignmentOptions.Center, true);
            var jamFrame = Img("JamFrame", rush, "ui_bar_track", new Vector2(-700, -386), new Vector2(320, 52), sliced: true, preserve: false);
            gc.jamFill = Img("JamFill", jamFrame.transform, "ui_bar_fill", Vector2.zero, new Vector2(302, 34), sliced: true,
                             color: new Color(0.91f, 0.29f, 0.29f), preserve: false);
            gc.jamFill.type = Image.Type.Filled; gc.jamFill.fillMethod = Image.FillMethod.Horizontal; gc.jamFill.fillAmount = 0;
            SlicedFill.Attach(gc.jamFill);

            // счёт — на плашке: белая цифра по светлому залу не читалась
            var scorePill = ByHeight("ui_pill_counter", 80f);
            var scoreBg = Img("ScorePill", rush, "ui_pill_counter", new Vector2(-700, -256), scorePill);
            gc.rushScore = Text("Score", scoreBg.transform, "0", 46, new Vector2(0, 2), new Vector2(scorePill.x - 40, 58), Dark,
                                TextAlignmentOptions.Center, false, autoSize: true);
            gc.rushCombo = Text("Combo", rush, "", 44, new Vector2(-700, -466), new Vector2(320, 72), new Color(1f, 0.78f, 0.24f), TextAlignmentOptions.Center, true);
            gc.rushHud = rush.gameObject;
            rush.gameObject.SetActive(false);

            // ---------- окна и обучение
            gc.tutorial = BuildTutorial(root);
            gc.victory = BuildVictory(root);
            gc.defeat = BuildDefeat(root);
            gc.pausePopup = BuildPause(root);
            gc.mechanicPopup = BuildMechanic(root);
            gc.shiftPopup = BuildShift(root);
            var picker = BuildStaffPicker(root, gc.shiftPopup);
            gc.rushResult = BuildRushResult(root);
            gc.booster = BuildBooster(root);
            BuildToast(root);

            foreach (var p in new Popup[] { gc.victory, gc.defeat, gc.pausePopup, gc.mechanicPopup, gc.shiftPopup, picker, gc.rushResult, gc.booster }) p.gameObject.SetActive(false);
            Save(scene, "Game");
        }

        static SectionView BuildSection(RectTransform parent, int i)
        {
            var size = ByWidth(SectionView.Sprite, SectionView.MaxWidth);
            var rt = Rect(Node($"Section{i + 1}", parent), new Vector2(-560 + i * 188, 0), size);
            var sv = rt.gameObject.AddComponent<SectionView>();
            sv.rect = rt;
            var frameImg = rt.gameObject.AddComponent<Image>();
            frameImg.sprite = S(SectionView.Sprite);
            frameImg.preserveAspect = true;
            frameImg.raycastTarget = true;
            sv.frame = frameImg;
            sv.normalSprite = S(SectionView.Sprite);
            sv.bigSprite = S(SectionView.BigSprite);
            sv.glow = Img("Glow", rt, "brd_slot_glow", Vector2.zero, new Vector2(0, 0), sliced: true, preserve: false);
            Stretch(sv.glow.rectTransform, -14);
            sv.glow.gameObject.SetActive(false);
            sv.slots = new RectTransform[3];
            for (int k = 0; k < 3; k++)
            {
                // места стоят ровно в нишах шкафа — доли снимал с самой картинки
                var slotRt = Rect(Node($"Slot{k + 1}", rt), new Vector2(0, SectionView.SlotY[k] * size.y), Vector2.one * (size.y * SectionView.SlotFrac));
                sv.slots[k] = slotRt;
            }
            // шкаф под крупный товар — отдельная картинка со своими двумя нишами
            var bigSize = ByWidth(SectionView.BigSprite, size.x);
            sv.bigFrame = Img("Big", rt, SectionView.BigSprite, new Vector2(0, (bigSize.y - size.y) / 2f), bigSize);
            sv.bigSlots = new RectTransform[2];
            for (int k = 0; k < 2; k++)
                sv.bigSlots[k] = Rect(Node($"BigSlot{k + 1}", sv.bigFrame.transform),
                                      new Vector2(0, SectionView.BigSlotY[k] * bigSize.y),
                                      Vector2.one * (bigSize.y * SectionView.BigSlotFrac));
            sv.bigFrame.gameObject.SetActive(false);
            sv.tagBase = Img("Tag", rt, "brd_tag_base", new Vector2(0, size.y * 0.398f), ByWidth("brd_tag_base", size.x * 0.62f));
            sv.tagIcon = Img("TagIcon", sv.tagBase.transform, "item_apple", Vector2.zero, ByHeight("item_apple", size.x * 0.28f));
            // морозилка — настоящий холодильник вместо деревянного стеллажа (просьба 26.09.2026):
            // стоит низом на том же полу, размеры считает SectionView.LayoutSlots в родных пропорциях
            var frzSize = ByWidth(SectionView.FreezerSprite, size.x);
            sv.freezerFrame = Img("FreezerBox", rt, SectionView.FreezerSprite,
                                  new Vector2(0, (frzSize.y - size.y) / 2f), frzSize);
            sv.freezerSlots = new RectTransform[3];
            for (int k = 0; k < 3; k++)
                sv.freezerSlots[k] = Rect(Node($"FreezerSlot{k + 1}", sv.freezerFrame.transform),
                                          new Vector2(0, SectionView.FreezerSlotY[k] * frzSize.y),
                                          Vector2.one * (frzSize.y * SectionView.FreezerSlotFrac));
            sv.freezerFrame.gameObject.SetActive(false);
            // холодильник — сразу за широким шкафом, под ярлыком: ярлык висит на его крыше
            sv.freezerFrame.transform.SetSiblingIndex(sv.bigFrame.transform.GetSiblingIndex() + 1);
            // «стекло» дверцы: закрыта — матовое голубое, открыта — почти прозрачное.
            // Лежит ровно по стеклу холодильника, а не по всей секции; снежинка больше не нужна —
            // и так видно, что это холодильник
            sv.freezerGlass = Img("Freezer", sv.freezerFrame.transform, "brd_slot_glow", Vector2.zero, Vector2.zero,
                                  sliced: true, color: new Color(0.78f, 0.92f, 1f, 0.5f));
            var grt = sv.freezerGlass.rectTransform;
            grt.anchorMin = new Vector2(0.10f, 0.25f); grt.anchorMax = new Vector2(0.90f, 0.85f);
            grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
            sv.freezerClosed = S("brd_freezer_closed");
            sv.freezerOpen = S("brd_freezer_open");
            sv.doorLamp = Img("DoorLamp", rt, "ui_btn_round", new Vector2(size.x * 0.32f, size.y * 0.40f), new Vector2(40, 40));
            var lockImg = Img("Lock", rt, "obj_lock_closed", new Vector2(0, 0), new Vector2(130, 130));
            sv.lockRoot = lockImg.gameObject;
            sv.lockText = Text("LockCount", lockImg.transform, "3", 44, new Vector2(0, -18), new Vector2(80, 60), Color.white, TextAlignmentOptions.Center, true);
            sv.saleTag = Img("SaleTag", rt, "obj_sale_tag", new Vector2(size.x * 0.34f, size.y * 0.30f), ByHeight("obj_sale_tag", 78f)).gameObject;
            sv.saleTag.SetActive(false);
            sv.freezerGlass.gameObject.SetActive(false);
            sv.doorLamp.gameObject.SetActive(false);
            sv.lockRoot.SetActive(false);

            // предложение «ещё полка за рекламу»: тёмный шкаф с киноплёнкой поверх (просьба 23.09.2026)
            var ad = Rect(Node("AdOffer", rt), Vector2.zero, size);
            var adHit = ad.gameObject.AddComponent<Image>();
            adHit.color = new Color(1f, 1f, 1f, 0f);   // сам шкаф уже затемнён, прямоугольник поверх не рисуем
            adHit.raycastTarget = true;
            sv.adButton = ad.gameObject.AddComponent<Button>();
            ad.gameObject.AddComponent<UiButton>();
            // только значок рекламы ровно по центру шкафа: подпись «+ полка» не нужна,
            // и так понятно, что за рекламу будет полка (просьба 27.09.2026)
            Img("Video", ad, "icon_video", Vector2.zero, new Vector2(size.x * 0.66f, size.x * 0.66f));
            sv.adRoot = ad.gameObject;
            sv.adRoot.SetActive(false);
            return sv;
        }

        static CartView BuildCart(RectTransform board)
        {
            // ширина и высота — строго по картинке; тележка по центру поля (просьба 21.09.2026)
            float cw = 681f, ch = cw / CartView.Aspect;
            var rt = Rect(Node("Cart", board), new Vector2(0, -396), new Vector2(cw, ch));
            var cv = rt.gameObject.AddComponent<CartView>();
            cv.rect = rt;
            cv.body = Img("Body", rt, "brd_cart_5", Vector2.zero, new Vector2(cw, ch));
            cv.body.raycastTarget = true;
            cv.cart5 = S("brd_cart_5");
            cv.slotsRoot = Rect(Node("Slots", rt), new Vector2(0, ch * CartView.SlotY), new Vector2(cw, ch * 0.3f));
            cv.slots = new RectTransform[9];
            cv.extraCrates = new GameObject[4];
            cv.slotLocks = new GameObject[5];
            for (int i = 0; i < 9; i++)
            {
                // пять ячеек — ровно в нарисованных корзинках, ящики грузчика — справа от тележки
                float side = cw * CartView.SlotFrac;
                var pos = i < 5 ? new Vector2((i - 2) * cw * CartView.SlotStep, 0f)
                                : new Vector2(cw * 0.60f + (i - 5) * side * 1.18f, -ch * 0.2f);
                cv.slots[i] = Rect(Node($"Slot{i + 1}", cv.slotsRoot), pos, new Vector2(side, side));
                if (i < 5)
                {
                    // закрытая ячейка, если вместимость тележки меньше 5
                    var lk = Img($"Lock{i + 1}", cv.slotsRoot, "icon_lock", pos, ByHeight("icon_lock", side * 0.62f), color: new Color(1f, 1f, 1f, 0.85f));
                    cv.slotLocks[i] = lk.gameObject;
                    lk.gameObject.SetActive(false);
                }
                else
                {
                    var crate = Img("Crate", cv.slots[i], "obj_box_open", new Vector2(0, -side * 0.16f), ByWidth("obj_box_open", side * 1.15f));
                    crate.transform.SetAsFirstSibling();
                    cv.extraCrates[i - 5] = cv.slots[i].gameObject;
                }
            }
            return cv;
        }

        static CustomerView BuildCustomer(RectTransform root)
        {
            // покупатель — ниже кнопок «отмена» и «подсказка»: раньше облачко с желанием налезало на них
            var rt = Anchor(Node("Customer", root), new Vector2(1f, 0.5f), new Vector2(-260, -250), new Vector2(330, 360));
            var cv = rt.gameObject.AddComponent<CustomerView>();
            cv.rect = rt;
            cv.group = Group(rt.gameObject);
            cv.group.blocksRaycasts = false;
            cv.body = Img("Body", rt, "chr_customer_bunny_wait", new Vector2(0, -20), new Vector2(320, 320));
            var bubble = Img("Bubble", rt, "ui_thought_bubble", new Vector2(-95, 225), new Vector2(210, 170));
            cv.bubble = bubble.rectTransform;
            cv.wantIcon = Img("Want", bubble.transform, "item_apple", new Vector2(6, 16), new Vector2(92, 92));
            cv.hearts = new Image[6];
            for (int i = 0; i < 6; i++) cv.hearts[i] = Img($"Heart{i + 1}", rt, "icon_heart", new Vector2(-100 + i * 40, -205), new Vector2(38, 38));
            rt.gameObject.SetActive(false);
            return cv;
        }

        static GameHud BuildHud(RectTransform root)
        {
            var hudRt = Stretch(Node("HUD", root));
            var hud = hudRt.gameObject.AddComponent<GameHud>();

            var pill = ByHeight("ui_pill_counter", 96f);
            var level = Img("LevelPill", hudRt, "ui_pill_counter", Vector2.zero, pill);
            Anchor(level.rectTransform, new Vector2(0f, 1f), new Vector2(60 + pill.x / 2f, -64), pill);
            hud.levelBadge = Img("Badge", level.transform, "icon_star", new Vector2(-pill.x / 2f + 6, 2), new Vector2(84, 84));
            hud.levelText = Text("Level", level.transform, "Уровень 1", 40, new Vector2(22, 2), new Vector2(pill.x - 100, 76), Dark, autoSize: true);
            var diffSize = ByHeight("ui_pill_counter", 56f);
            var diff = Img("Difficulty", level.transform, "ui_pill_counter", new Vector2(10, -80), diffSize,
                           color: new Color32(0xD9, 0x3B, 0x3B, 255));
            hud.difficultyBadge = diff.gameObject;
            hud.difficultyText = Text("Text", diff.transform, "Сложно", 30, new Vector2(0, 2), new Vector2(diffSize.x - 30, 48), Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            diff.gameObject.SetActive(false);

            var coins = Img("CoinsPill", hudRt, "ui_pill_counter", Vector2.zero, pill);
            Anchor(coins.rectTransform, new Vector2(1f, 1f), new Vector2(-180 - pill.x / 2f, -64), pill);
            hud.coinsIcon = Img("CoinIcon", coins.transform, "icon_coin", new Vector2(-pill.x / 2f + 6, 2), new Vector2(84, 84)).rectTransform;
            hud.coinsText = Text("Coins", coins.transform, "0", 44, new Vector2(22, 2), new Vector2(pill.x - 100, 76), Dark, autoSize: true);

            // готовые кнопки: значок нарисован внутри, подписи не нужны
            var pauseSz = ByHeight("btn_pause", 112f);
            hud.pauseButton = Button("Pause", hudRt, "btn_pause", Vector2.zero, pauseSz);
            Anchor((RectTransform)hud.pauseButton.transform, new Vector2(1f, 1f), new Vector2(-95, -64), pauseSz);

            // правый столбик: пауза → карта → отмена → подсказка, между кнопками не меньше 40 точек
            // (просьба 26.09.2026: «иконки слишком близко друг к другу» — карта налезала на счётчик отмен)
            var undoSz = ByHeight("btn_undo", 148f);
            hud.undoButton = Button("Undo", hudRt, "btn_undo", Vector2.zero, undoSz);
            Anchor((RectTransform)hud.undoButton.transform, new Vector2(1f, 0.5f), new Vector2(-110, 140), undoSz);
            hud.undoCount = Badge(hud.undoButton.transform, new Vector2(52, 52), "5", new Color32(0x5B, 0xC4, 0x6A, 255));
            hud.undoVideo = Img("Video", hud.undoButton.transform, "icon_video", new Vector2(-52, 52), new Vector2(54, 54)).gameObject;

            var hintSz = ByHeight("btn_hint", 148f);
            hud.hintButton = Button("Hint", hudRt, "btn_hint", Vector2.zero, hintSz);
            Anchor((RectTransform)hud.hintButton.transform, new Vector2(1f, 0.5f), new Vector2(-110, -50), hintSz);
            hud.hintCount = Badge(hud.hintButton.transform, new Vector2(52, 52), "3", new Color32(0x5B, 0xC4, 0x6A, 255));
            hud.hintVideo = Img("Video", hud.hintButton.transform, "icon_video", new Vector2(-52, 52), new Vector2(54, 54)).gameObject;

            // «на карту» — под паузой: выйти из уровня одним нажатием (просьба 23.09.2026)
            var mapSz = ByHeight("btn_map", 112f);
            hud.mapButton = Button("Map", hudRt, "btn_map", Vector2.zero, mapSz);
            Anchor((RectTransform)hud.mapButton.transform, new Vector2(1f, 1f), new Vector2(-95, -200), mapSz);
            return hud;
        }

        static TutorialOverlay BuildTutorial(RectTransform root)
        {
            var rt = Stretch(Node("Tutorial", root));
            var t = rt.gameObject.AddComponent<TutorialOverlay>();
            var dimImg = Dim(rt, 1f);
            dimImg.raycastTarget = false;
            t.dim = Group(dimImg.gameObject);
            t.dim.blocksRaycasts = false;
            var catcher = Stretch(Node("TapCatcher", rt));
            var ci = catcher.gameObject.AddComponent<Image>();
            ci.color = new Color(0, 0, 0, 0);
            t.tapCatcher = catcher.gameObject.AddComponent<Button>();
            var racSz = ByHeight("chr_raccoon_point", 300f);
            var rac = Img("Raccoon", rt, "chr_raccoon_point", Vector2.zero, racSz);
            Skin(rac, "point");
            Anchor(rac.rectTransform, new Vector2(0f, 0f), new Vector2(150, 170), racSz);
            t.raccoon = rac.rectTransform;
            // облачко в родных пропорциях; кончик хвостика (−0.38 ширины, −0.43 высоты от центра)
            // стоит у рта енота (+0.06 ширины, +0.15 высоты от его центра) — слова идут «из уст»
            var bsz = ByWidth("ui_speech_bubble", 460f);
            var mouth = new Vector2(150f + racSz.x * 0.06f, 170f + racSz.y * 0.15f);
            var tip = mouth + new Vector2(racSz.x * 0.24f, racSz.y * 0.09f);   // чуть правее щеки, не на мордочке
            var bubble = Img("Bubble", rt, "ui_speech_bubble", Vector2.zero, bsz);
            Anchor(bubble.rectTransform, new Vector2(0f, 0f), tip, bsz);
            // пивот — кончик хвостика: облачко растёт под длинный текст, а хвостик остаётся у рта
            bubble.rectTransform.pivot = new Vector2(0.118f, 0.07f);
            bubble.rectTransform.anchoredPosition = tip;
            t.bubble = bubble.rectTransform;
            // рамка текста — как было (просьба 22.09.2026 «верни как было»): крупный текст в центре пузыря;
            // от вылезания страхует глубокое автоужатие (min 12) вместо урезания рамки
            t.bubbleText = Text("Text", bubble.transform, "", 36, new Vector2(0, bsz.y * 0.09f), new Vector2(bsz.x * 0.84f, bsz.y * 0.6f), Dark, autoSize: true);
            t.bubbleText.fontSizeMin = 12;
            // «призрак» товара: его рука тащит от ленты к полке
            t.ghost = Img("Ghost", rt, "item_apple", Vector2.zero, new Vector2(110, 110));
            t.ghost.raycastTarget = false;
            t.ghost.gameObject.SetActive(false);
            t.hand = Img("Hand", rt, "ui_hand_pointer", Vector2.zero, ByHeight("ui_hand_pointer", 150f)).rectTransform;
            return t;
        }

        static VictoryPopup BuildVictory(RectTransform root)
        {
            // компактное окно (~3 раза меньше прежнего, просьба 22.09): 640 в ширину,
            // высота по пропорциям спрайта; мелкие звёзды, монеты и кнопки в один ряд
            var (r, panel) = PopupShell("Victory", root, new Vector2(760, 0), "Уровень пройден!", out var title, "ui_panel_festive");
            title.fontSize = 34;
            var v = AddPopup<VictoryPopup>(r, panel);
            v.title = title;
            v.starOn = S("ui_star_big");
            v.starOff = S("ui_star_big_empty");
            v.stars = new Image[3];
            for (int i = 0; i < 3; i++)
                v.stars[i] = Img($"Star{i + 1}", panel, "ui_star_big_empty", new Vector2(-95 + i * 95, i == 1 ? 52 : 41),
                                 ByHeight("ui_star_big_empty", i == 1 ? 86f : 74f));
            v.raccoon = Skin(Img("Raccoon", r, "chr_raccoon_jump", new Vector2(-330, -20), new Vector2(200, 200)), "jump").rectTransform;
            // монеты и «×2 за видео» — одной строкой: награда и способ её удвоить рядом
            Img("CoinPile", panel, "ui_coin_pile", new Vector2(-218, -40), ByHeight("ui_coin_pile", 64f));
            v.coinsText = Text("Coins", panel, "+0", 42, new Vector2(-110, -40), new Vector2(150, 58), Dark);
            // готовая кнопка «▶ x2» целиком; для «Подарка» и ×3 — обычная кнопка рекламы с подписью
            v.doubleArtButton = Button("DoubleArt", panel, "btn_video_x2", new Vector2(150, -40), ByHeight("btn_video_x2", 86f));
            v.doubleButton = Button("Double", panel, "ui_btn_rewarded", new Vector2(150, -40), ByHeight("ui_btn_rewarded", 78f), "×2", 32, "icon_video");
            v.doubleText = Label(v.doubleButton);
            v.doubleVideoIcon = Icon(v.doubleButton).gameObject;
            // нижняя строка: слева — пачка наклеек или «Звёздный путь», справа — копилка
            v.packHint = Text("PackHint", panel, "На карте ждёт пачка наклеек!", 22, new Vector2(-60, -92), new Vector2(420, 34), new Color32(0x3E, 0x9E, 0x5E, 255), autoSize: true).gameObject;
            // на том же месте, когда пачки нет: сколько звёзд до награды «Звёздного пути»
            v.trackText = Text("StarTrack", panel, "", 22, new Vector2(-60, -92), new Vector2(420, 34), new Color32(0xC0, 0x7A, 0x12, 255), autoSize: true);
            v.trackText.enableWordWrapping = false;
            // копилка — крупно в правом верхнем углу окна, как наклейка на раме; под ней «+35» — сколько упало
            // с этой победы, в неё падает монетка (vfx_piggy_coin_drop). Раньше — мелкая у кнопок (просьба 03.10.2026)
            var piggy = Img("Piggy", panel, "piggy_empty", new Vector2(286, 120), ByHeight("piggy_empty", 100f));
            v.piggyRoot = piggy.gameObject;
            v.piggyText = Text("Text", piggy.transform, "+35", 34, new Vector2(0, -62), new Vector2(130, 40), new Color32(0xC2, 0x4E, 0x7A, 255),
                               TextAlignmentOptions.Center, true, autoSize: true);
            piggy.gameObject.SetActive(false);
            // «На карту» (со значком карты) и «Дальше» — обе всегда (просьба 22.09.2026)
            // «Заново» — переиграть уровень на три звезды (просьба 26.09.2026); кружок, как в паузе
            // три кнопки с равными промежутками, ряд — ровно по центру окна (было 39 и 52 точки, обход 29.09.2026)
            var retrySz = ByHeight("ui_btn_round", 80f);
            var mapSz = ByHeight("ui_btn_secondary", 68f);
            var nextSz = ByHeight("ui_btn_primary", 68f);
            const float gap = 40f;
            float x0 = -(retrySz.x + mapSz.x + nextSz.x + gap * 2f) / 2f;
            v.retryButton = Button("Retry", panel, "ui_btn_round", new Vector2(x0 + retrySz.x / 2f, -152), retrySz, icon: "icon_retry");
            v.mapButton = Button("Map", panel, "ui_btn_secondary", new Vector2(x0 + retrySz.x + gap + mapSz.x / 2f, -152), mapSz, "На карту", 30, "icon_map", Dark, false);
            v.nextButton = Button("Next", panel, "ui_btn_primary", new Vector2(x0 + retrySz.x + mapSz.x + gap * 2f + nextSz.x / 2f, -152), nextSz, "Дальше", 34);
            v.nextText = Label(v.nextButton);
            return v;
        }

        static DefeatPopup BuildDefeat(RectTransform root)
        {
            var (r, panel) = PopupShell("Defeat", root, new Vector2(1020, 0), "Тележка переполнена!", out var title);
            var d = AddPopup<DefeatPopup>(r, panel);
            d.title = title;
            Img("Cart", panel, "brd_cart_tipped", new Vector2(-262, 140), ByWidth("brd_cart_tipped", 250f));
            Img("Bear", r, "chr_bear_offer", new Vector2(640, -40), new Vector2(420, 420));
            Skin(Img("Raccoon", r, "chr_raccoon_think", new Vector2(-640, -60), new Vector2(380, 380)), "think");
            d.reason = Text("Reason", panel, "Ходить больше некуда", 38, new Vector2(150, 152), new Vector2(420, 120), Dark, autoSize: true);
            // серия побед сгорит — плашка под енотом слева (в окне кнопки стоят столбиком, места нет)
            var sl = Img("Streak", r, "ui_bar_track", new Vector2(-700, -300), new Vector2(340, 120), sliced: true);
            Img("Icon", sl.transform, "icon_streak_lost", new Vector2(-122, 4), ByHeight("icon_streak_lost", 86f));
            d.streakText = Text("Text", sl.transform, "Серия ×3 сгорит, если сдашься", 30, new Vector2(40, 2), new Vector2(230, 96), Color.white,
                                TextAlignmentOptions.Center, true, autoSize: true);
            d.streakLine = sl.gameObject;
            sl.gameObject.SetActive(false);
            // «Сохранить серию» — под плашкой: за алмазы или видео (раз за серию)
            d.keepStreakButton = Button("KeepStreak", sl.transform, "ui_btn_green", new Vector2(0, -110), ByHeight("ui_btn_green", 88f),
                                        "Сохранить 8", 32, "icon_gem_small");
            d.keepStreakText = Label(d.keepStreakButton);
            d.keepStreakIcon = Icon(d.keepStreakButton);
            d.keepStreakButton.gameObject.SetActive(false);
            // бесплатный выход из тупика: отматываем ходы до положения, откуда уровень ещё выигрывается
            d.freeUndoButton = Button("FreeUndo", panel, "ui_btn_primary", new Vector2(0, 60), ByHeight("ui_btn_primary", 126f), "Отмотать назад", 40, "icon_undo");
            d.loaderAdButton = Button("LoaderAd", panel, "ui_btn_rewarded", new Vector2(0, 10), ByHeight("ui_btn_rewarded", 126f), "Грузчик +2", 40, "icon_video");
            d.loaderCoinsButton = Button("LoaderCoins", panel, "ui_btn_green", new Vector2(0, 10), ByHeight("ui_btn_green", 126f), "Грузчик +2", 40, "icon_loader");
            // цена — прямо в подписи: отдельная надпись с монеткой не помещалась на кнопку
            d.loaderCoinsText = Label(d.loaderCoinsButton);
            d.bonusRetryButton = Button("BonusRetry", panel, "ui_btn_rewarded", new Vector2(0, -120), ByHeight("ui_btn_rewarded", 114f), "Заново: +1 место", 34, "icon_video");
            d.retryButton = Button("Retry", panel, "ui_btn_primary", new Vector2(-170, -250), ByHeight("ui_btn_primary", 104f), "Заново", 44, "icon_retry");
            d.mapButton = Button("Map", panel, "ui_btn_secondary", new Vector2(175, -250), ByHeight("ui_btn_secondary", 104f), "На карту", 40, "icon_map", Dark, false);
            return d;
        }

        static PausePopup BuildPause(RectTransform root)
        {
            var (r, panel) = PopupShell("Pause", root, new Vector2(1080, 0), "Пауза", out _, "ui_panel_wood");
            var p = AddPopup<PausePopup>(r, panel);
            p.resumeButton = Button("Resume", panel, "ui_btn_primary", new Vector2(0, 175), ByHeight("ui_btn_primary", 110f), "Продолжить", 46);
            var sec = ByHeight("ui_btn_secondary", 88f);
            p.restartButton = Button("Restart", panel, "ui_btn_secondary", new Vector2(0, 62), sec, "Заново", 40, "icon_retry", Dark, false);
            p.mapButton = Button("Map", panel, "ui_btn_secondary", new Vector2(0, -38), sec, "На карту", 40, "icon_map", Dark, false);
            p.helpButton = Button("Help", panel, "ui_btn_secondary", new Vector2(0, -138), sec, "Как играть", 40, "icon_info", Dark, false);
            p.soundButton = Button("Sound", panel, "ui_btn_round", new Vector2(-90, -216), ByHeight("ui_btn_round", 76f), icon: "icon_sound_on");
            p.soundIcon = Icon(p.soundButton);
            p.musicButton = Button("Music", panel, "ui_btn_round", new Vector2(90, -216), ByHeight("ui_btn_round", 76f), icon: "icon_music_on");
            p.musicIcon = Icon(p.musicButton);
            return p;
        }

        static MechanicPopup BuildMechanic(RectTransform root)
        {
            var (r, panel) = PopupShell("Mechanic", root, new Vector2(1040, 0), "Новое!", out var title, "ui_panel_info");
            var m = AddPopup<MechanicPopup>(r, panel);
            m.title = title;
            title.fontSizeMax = 42;   // кегль задаёт автоподбор, обычное fontSize он перезапишет
            // про новинку рассказывает енот: он стоит сбоку от окна (просьба 26.09.2026)
            m.raccoon = Skin(Img("Raccoon", r, "chr_raccoon_point", new Vector2(-640, -80), ByHeight("chr_raccoon_point", 400f)), "point").rectTransform;
            m.picture = Img("Picture", panel, "obj_lock_closed", new Vector2(0, 112), ByHeight("obj_lock_closed", 170f));
            // текст длинный: подробный рассказ про механику. Рамка текста начинается НИЖЕ картинки —
            // раньше текст заезжал на покупателя, а кнопка — на медальон рамки (просьба 03.10.2026)
            m.text = Text("Text", panel, "", 34, new Vector2(0, -78), new Vector2(800, 170), Dark, autoSize: true);
            m.text.fontSizeMin = 20;
            m.okButton = Button("Ok", panel, "ui_btn_primary", new Vector2(0, -222), ByHeight("ui_btn_primary", 96f), "Понятно", 44);
            return m;
        }

        /// <summary>Бустер кончился: монеты / реклама / набор за деньги (рекомендация издателя, 29.09.2026).</summary>
        static BoosterPopup BuildBooster(RectTransform root)
        {
            var (r, panel) = PopupShell("Booster", root, new Vector2(860, 0), "Подсказки закончились", out var title);
            var b = AddPopup<BoosterPopup>(r, panel);
            b.title = title;
            var ps = panel.sizeDelta;
            b.picture = Img("Picture", panel, "icon_hint", new Vector2(0, 122), ByHeight("icon_hint", 126f));
            b.text = Text("Text", panel, "", 32, new Vector2(0, 4), new Vector2(620, 80), Dark, autoSize: true);
            b.coinsButton = Button("Coins", panel, "ui_btn_green", new Vector2(-150, -88), ByHeight("ui_btn_green", 104f), "×3 · 120", 36, "icon_coin");
            b.coinsText = Label(b.coinsButton);
            b.adButton = Button("Ad", panel, "ui_btn_rewarded", new Vector2(150, -88), ByHeight("ui_btn_rewarded", 104f), "+1 за видео", 34, "icon_video");
            b.adText = Label(b.adButton);
            b.iapButton = Button("Iap", panel, "ui_btn_secondary", new Vector2(0, -204), ByHeight("ui_btn_secondary", 100f), "Набор помощника", 28, "icon_gift", Dark, false);
            b.iapText = Label(b.iapButton);
            b.closeButton = Button("Close", panel, "ui_btn_round", new Vector2(ps.x * 0.42f, ps.y * 0.40f), ByHeight("ui_btn_round", 80f), icon: "icon_close");
            return b;
        }

        static RushResultPopup BuildRushResult(RectTransform root)
        {
            var (r, panel) = PopupShell("RushResult", root, new Vector2(960, 0), "Час пик окончен", out _);
            var p = AddPopup<RushResultPopup>(r, panel);
            Img("Clock", panel, "icon_clock", new Vector2(0, 140), ByHeight("icon_clock", 120f));
            p.scoreText = Text("Score", panel, "0", 82, new Vector2(0, 32), new Vector2(500, 116), Dark);
            p.bestText = Text("Best", panel, "Рекорд: 0", 34, new Vector2(0, -50), new Vector2(500, 48), Dark);
            p.coinsText = Text("Coins", panel, "+0", 32, new Vector2(0, -100), new Vector2(680, 46), new Color32(0x3E, 0x9E, 0x5E, 255));
            p.continueButton = Button("Continue", panel, "ui_btn_rewarded", new Vector2(0, -160), ByHeight("ui_btn_rewarded", 100f), "Продолжить", 34, "icon_video");
            p.againButton = Button("Again", panel, "ui_btn_primary", new Vector2(-160, -240), ByHeight("ui_btn_primary", 88f), "Ещё раз", 40);
            p.mapButton = Button("Map", panel, "ui_btn_secondary", new Vector2(160, -240), ByHeight("ui_btn_secondary", 88f), "На карту", 38, null, Dark, false);
            return p;
        }
    }
}
