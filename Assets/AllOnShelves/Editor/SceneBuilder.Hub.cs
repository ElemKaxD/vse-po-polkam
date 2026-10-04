using System.Linq;
using AllOnShelves.Core;
using AllOnShelves.Hub;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static AllOnShelves.EditorTools.UiBuild;

namespace AllOnShelves.EditorTools
{
    public static partial class SceneBuilder
    {
        const float SegW = 1920f;

        static void BuildHub()
        {
            var scene = NewScene("Hub");
            var canvas = NewCanvas("HubCanvas");
            var root = (RectTransform)canvas.transform;
            var hubGo = new GameObject("HubController");
            hubGo.transform.SetParent(root, false);
            var hub = hubGo.AddComponent<HubController>();

            var screens = Stretch(Node("Screens", root));
            hub.startScreen = BuildStart(screens);
            hub.mapScreen = BuildMap(screens);
            hub.renovationScreen = BuildRenovation(screens);
            hub.albumScreen = BuildAlbum(screens);
            hub.shopScreen = BuildShop(screens);
            hub.dailyScreen = BuildDaily(screens);
            hub.leaderboardScreen = BuildLeaders(screens);
            hub.houseScreen = BuildHouse(screens);
            hub.roomScreen = BuildRoom(screens);
            hub.questsScreen = BuildQuests(screens);

            BuildTopBar(root, hub);
            BuildNav(root, hub);

            hub.settingsPopup = BuildSettings(root);
            hub.packPopup = BuildPack(root);
            hub.offerPopup = BuildOffer(root);
            hub.starTrackPopup = BuildStarTrack(root);
            hub.wardrobePopup = BuildWardrobe(root);
            hub.profilePopup = BuildProfile(root);
            hub.floorPopup = BuildFloorPopup(root);
            hub.piggyPopup = BuildPiggyPopup(root);
            hub.gemShop = BuildGemShop(root);
            hub.streakPopup = BuildStreakPopup(root);
            hub.diffPopup = BuildDiffPopup(root);
            hub.confirmPopup = BuildConfirm(root);
            hub.booster = BuildBooster(root);   // «Не хватает монет» в ремонте (экономика v2, 30.09.2026)
            foreach (var p in new Popup[] { hub.settingsPopup, hub.packPopup, hub.offerPopup, hub.starTrackPopup, hub.wardrobePopup, hub.profilePopup, hub.confirmPopup, hub.booster, hub.floorPopup, hub.piggyPopup, hub.gemShop, hub.streakPopup, hub.diffPopup })
                p.gameObject.SetActive(false);
            BuildToast(root);
            var tut = BuildTutorial(root);
            var hubTut = tut.gameObject.AddComponent<HubTutorial>();
            hubTut.overlay = tut;
            hubTut.hub = hub;
            hub.tutorial = hubTut;
            Save(scene, "Hub");
        }

        /// <summary>Заголовок экрана слева сверху — на деревянной табличке (лента только у карты).</summary>
        static TextMeshProUGUI ScreenTitle(RectTransform rt, string text)
        {
            // длинный заголовок («Ежедневная доставка») не влезал в табличку: она растёт целиком,
            // в родных пропорциях; левый край — правее круглой кнопки «назад» (обход 27.09.2026)
            var size = ByWidth(TitlePlate, text.Length > 12 ? 580f : 460f);
            var plate = Img("TitleRibbon", rt, TitlePlate, Vector2.zero, size);
            Anchor(plate.rectTransform, new Vector2(0f, 1f), new Vector2(236f + size.x / 2f, -78f), size);
            return PlateTitle(plate.rectTransform, text, 42);
        }

        static T Screen<T>(RectTransform parent, string name, string bg) where T : HubScreen
        {
            var rt = Stretch(Node(name, parent));
            if (!string.IsNullOrEmpty(bg)) Background("Background", rt, bg);
            var s = rt.gameObject.AddComponent<T>();
            s.group = Group(rt.gameObject);
            return s;
        }

        // ================================================================ стартовый экран

        static StartScreen BuildStart(RectTransform parent)
        {
            // главное меню: фон с логотипом, енотом и ларьком + «ИГРАТЬ» (ведёт на карту) и «НАСТРОЙКИ»
            var s = Screen<StartScreen>(parent, "StartScreen", "bg_hub_start");
            var rt = (RectTransform)s.transform;

            // две кнопки — ровно там, куда их поставил пользователь мышкой 20.09.2026
            var playSize = ByWidth("btn_play 1", 709f);
            s.playButton = Button("Play", rt, "btn_play 1", new Vector2(414f, -30f), playSize);

            var setSize = ByWidth("ui_btn_secondary", 470f);
            // плашка с надписью вместо ленты ui_btn_settings (просьба 21.09.2026): светлую плашку
            // подписываем тёмным, как табы рейтинга; слева иконка-шестерёнка
            s.settingsButton = Button("Settings", rt, "ui_btn_secondary", new Vector2(414f, -243f), setSize, "Настройки", 46, "icon_settings", Dark, false);
            return s;
        }

        // ================================================================ карта

        static MapScreen BuildMap(RectTransform parent)
        {
            var s = Screen<MapScreen>(parent, "MapScreen", null);
            var rt = (RectTransform)s.transform;
            var pagesRoot = Stretch(Node("Districts", rt));
            int districts = LevelPlanner.DistrictCount;
            s.pages = new RectTransform[districts];
            s.nodes = new MapNode[LevelPlanner.LevelCount];
            s.districtLocks = new RectTransform[districts - 1];
            for (int d = 1; d <= districts; d++)
            {
                // страница района: сегмент на весь экран (без стыков), ценники — в координатах картинки 1920×1080
                var page = Stretch(Node($"District{d}", pagesRoot));
                Group(page.gameObject);
                var seg = Background("Map", page, $"map_segment_{d:00}").rectTransform;
                var nodesRoot = Rect(Node("Levels", seg), Vector2.zero, new Vector2(SegW, 1080));
                nodesRoot.anchorMin = nodesRoot.anchorMax = new Vector2(0.5f, 0.5f);
                // масштаб слоя ценников = масштаб картинки района
                nodesRoot.gameObject.AddComponent<ScaleToParent>().reference = new Vector2(SegW, 1080f);
                int first = LevelPlanner.DistrictStart[d - 1];
                int last = LevelPlanner.DistrictEnd(d);
                for (int id = first; id <= last; id++)
                {
                    // места ценников найдены по дороге на картинке района (Tools/ArtPipeline/map_nodes.py)
                    s.nodes[id - 1] = BuildMapNode(nodesRoot, id, MapRoadData.Node(d, id - first));
                }
                if (d >= 2)
                {
                    // закрытый район целиком гасим: замок по центру, ценники под затемнением не нажимаются
                    var lockRt = Stretch(Node("Lock", nodesRoot));
                    var dim = lockRt.gameObject.AddComponent<Image>();
                    dim.color = new Color(0.10f, 0.07f, 0.05f, 0.42f);
                    dim.raycastTarget = true;
                    Img("Icon", lockRt, "icon_lock", new Vector2(0, 70), ByHeight("icon_lock", 180f));
                    var plate = Img("Plate", lockRt, "ui_tile_button", new Vector2(0, -90), new Vector2(820, 84),
                                    sliced: true, preserve: false, color: new Color(0.25f, 0.17f, 0.13f, 0.92f));
                    Text("Text", plate.transform, "Нужен ремонт предыдущего района", 36, Vector2.zero, new Vector2(760, 60),
                         Color.white, TextAlignmentOptions.Center, true);
                    s.districtLocks[d - 2] = lockRt;
                    lockRt.gameObject.SetActive(false);
                }
                s.pages[d - 1] = page;
                page.gameObject.SetActive(d == 1);
            }

            // заголовок района на ленте — по центру сверху (просьба 21.09.2026);
            // уже 640, чтобы компактные счётчики справа не доходили до неё
            var ribbonSize = ByWidth("ui_ribbon_banner", 560f);
            var ribbon = Img("DistrictRibbon", rt, "ui_ribbon_banner", Vector2.zero, ribbonSize);
            Anchor(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -78), ribbonSize);
            // середина ленты кремовая — надпись тёмная, иначе её не видно
            s.districtTitle = Text("Title", ribbon.transform, "Район 1", 42, new Vector2(0, ribbonSize.y * 0.02f), new Vector2(ribbonSize.x * 0.60f, 64),
                                   Dark, TextAlignmentOptions.Center, false, autoSize: true);
            s.districtTitle.enableWordWrapping = false;
            Arc(s.districtTitle, ribbonSize.y);
            // режим сложности (v4): перчик слева от ленты района, появляется после 150 уровней «Лёгкого»
            var modeSz = ByHeight("ui_btn_round_cream", 112f);
            s.modeButton = Button("DiffMode", rt, "ui_btn_round_cream", Vector2.zero, modeSz);
            Anchor((RectTransform)s.modeButton.transform, new Vector2(0.5f, 1f), new Vector2(-ribbonSize.x / 2f - 30f, -86), modeSz);
            s.modeIcon = Img("Icon", s.modeButton.transform, "icon_diff_easy", new Vector2(0, 4), ByHeight("icon_diff_easy", 78f));
            s.modeButton.gameObject.SetActive(false);
            // стрелки районов — в нижних углах: слева по середине высоты столбец кнопок «Час пик» … «Звёздный путь»
            // (его туда поставил пользователь) закрывал стрелку назад (29.09.2026); ценники карты до углов не доходят
            s.prevButton = Button("PrevDistrict", rt, "ui_btn_round", Vector2.zero, new Vector2(120, 120), icon: "icon_back");
            Anchor((RectTransform)s.prevButton.transform, new Vector2(0f, 0f), new Vector2(90, 170), new Vector2(120, 120));
            s.nextButton = Button("NextDistrict", rt, "ui_btn_round", Vector2.zero, new Vector2(120, 120), icon: "icon_next");
            Anchor((RectTransform)s.nextButton.transform, new Vector2(1f, 0f), new Vector2(-90, 170), new Vector2(120, 120));
            var content = pagesRoot;
            var avatarImg = Img("Avatar", content, "map_avatar", Vector2.zero, new Vector2(120, 120));
            s.avatar = avatarImg.rectTransform;
            s.avatarImage = avatarImg;
            // ореол за енотом — награда «Звёздного пути»; пока ничего не надето, выключен
            s.avatarHalo = Img("Halo", s.avatar, "icon_star", Vector2.zero, new Vector2(190, 190));
            s.avatarHalo.transform.SetAsFirstSibling();
            s.avatarHalo.gameObject.SetActive(false);
            s.tagNormal = S("map_node_normal");
            s.tagHard = S("map_node_hard");
            s.tagSuper = S("map_node_superhard");
            s.tagRevision = S("map_node_revision");
            s.tagLocked = S("map_node_locked");
            s.starOn = S("icon_star");
            s.starOff = S("ui_star_big_empty");

            // кнопки режимов — справа внизу, меню хаба — слева внизу: ценники карты не перекрываются
            var mode = ByHeight("ui_btn_primary", 96f);
            s.rushButton = Button("Rush", rt, "ui_btn_primary", Vector2.zero, mode, "Час пик", 38, "icon_clock");
            Anchor((RectTransform)s.rushButton.transform, new Vector2(1f, 0f), new Vector2(-40 - mode.x / 2f, 100), mode);
            s.dailyButton = Button("Daily", rt, "ui_btn_green", Vector2.zero, ByHeight("ui_btn_green", 96f), "Завоз дня", 38, "icon_calendar");
            Anchor((RectTransform)s.dailyButton.transform, new Vector2(1f, 0f), new Vector2(-60 - mode.x * 1.5f, 100), ByHeight("ui_btn_green", 96f));
            // сумма — в самой подписи: отдельная надпись наезжала на слово «Чаевые»
            s.tipsButton = Button("Tips", rt, "ui_btn_rewarded", Vector2.zero, ByHeight("ui_btn_rewarded", 96f), "Чаевые +60", 36, "icon_video");
            Anchor((RectTransform)s.tipsButton.transform, new Vector2(1f, 0f), new Vector2(-40 - mode.x / 2f, 216), ByHeight("ui_btn_rewarded", 96f));
            s.tipsText = Label(s.tipsButton);
            // «Звёздный путь» — на месте прежнего звёздного сундука (его место в столбце кнопок выбрал пользователь):
            // звезда, под ней «набрано/нужно до награды», красная точка — есть что забрать
            s.starChestButton = Button("StarChest", rt, "ui_btn_round", Vector2.zero, new Vector2(130, 130), icon: "icon_star_big");
            Anchor((RectTransform)s.starChestButton.transform, new Vector2(1f, 0f), new Vector2(-100, 350), new Vector2(130, 130));
            var chip = Img("Progress", s.starChestButton.transform, "ui_tab_wood_flat2d_generated", new Vector2(0, -70), ByHeight("ui_tab_wood_flat2d_generated", 46f));
            s.starTrackText = Text("Text", chip.transform, "0/3", 28, new Vector2(0, 1), new Vector2(chip.rectTransform.sizeDelta.x * 0.8f, 40), Dark, autoSize: true);
            s.starTrackText.enableWordWrapping = false;
            s.starTrackDot = Img("Dot", s.starChestButton.transform, "icon_new", new Vector2(46, 46), new Vector2(44, 44)).gameObject;
            return s;
        }

        static MapNode BuildMapNode(RectTransform parent, int id, Vector2 pos)
        {
            var size = ByWidth("map_node_normal", 128f);
            // у дощечки ценника нет прозрачного поля в холсте — тень кладём отдельным
            // овальным спрайтом чуть ниже дощечки (просьба 22.09.2026)
            var shadow = Img($"Shadow{id:000}", parent, "fx_shadow_oval", pos + new Vector2(0, -size.y * 0.42f),
                             new Vector2(size.x * 1.15f, size.y * 0.62f), color: new Color(0.12f, 0.08f, 0.05f, 0.55f));
            shadow.transform.SetAsFirstSibling();
            var img = Img($"Level{id:000}", parent, "map_node_normal", pos, size, raycast: true);
            var n = img.gameObject.AddComponent<MapNode>();
            n.levelId = id;
            n.tag = img;
            n.button = img.gameObject.AddComponent<Button>();
            img.gameObject.AddComponent<UiButton>();
            // номер — в верхней половине ценника, звёзды на ценнике нарисованы в нижней
            n.number = Text("Number", img.transform, id.ToString(), 40, new Vector2(size.x * 0.07f, size.y * 0.17f), new Vector2(size.x * 0.6f, size.y * 0.5f),
                            Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            n.number.enableWordWrapping = false;
            // заработанные звёзды — точно поверх звёзд, нарисованных на ценнике (36%, 57%, 78% ширины; 70% высоты)
            n.stars = new Image[3];
            float[] sx = { 0.365f, 0.573f, 0.78f };
            for (int i = 0; i < 3; i++)
                n.stars[i] = Img($"Star{i + 1}", img.transform, "icon_star", new Vector2((sx[i] - 0.5f) * size.x, -0.2f * size.y), new Vector2(size.x * 0.17f, size.x * 0.17f));
            n.newBadge = Img("New", img.transform, "icon_new", new Vector2(size.x / 2f - 8, size.y / 2f - 4), new Vector2(40, 40)).gameObject;
            n.newBadge.SetActive(false);
            return n;
        }

        // ================================================================ ремонт

        static RenovationScreen BuildRenovation(RectTransform parent)
        {
            var s = Screen<RenovationScreen>(parent, "RenovationScreen", null);
            var rt = (RectTransform)s.transform;
            // под кадром ремонта — небесная заливка на весь экран: фиксированные 1920×1080 на экранах
            // не 16:9 оставляли по краям почти белую заливку камеры (обход 29.09.2026)
            Stretch(Img("Sky", rt, null, Vector2.zero, new Vector2(1920, 1080), color: new Color32(0x9F, 0xD8, 0xF2, 255)).rectTransform);
            // прежний ремонт «картинками» — только у магазинов из reno_layout.json; рынок и ТЦ (районы 12–15) — только «лего»
            var oldStores = MetaCatalog.Stores.Where(x => RenoLayoutData.Stores.ContainsKey(x.Id)).ToArray();
            s.stores = new RenoStore[oldStores.Length];
            for (int i = 0; i < oldStores.Length; i++)
            {
                var info = oldStores[i];
                var lay = RenoLayoutData.Stores[info.Id];
                var storeRt = Stretch(Node("Store_" + info.Id, rt));
                var st = storeRt.gameObject.AddComponent<RenoStore>();
                st.storeId = info.Id;
                st.firstDistrict = info.Districts[0];
                st.background = Background("Background", storeRt, "bg_meta_" + info.Id);
                // сцена в координатах 1920×1080 от центра экрана: здание стоит низом на тротуаре
                var scene = Rect(Node("Scene", storeRt), Vector2.zero, new Vector2(1920, 1080));
                var items = info.Districts.SelectMany(MetaCatalog.ItemsOf).Where(it => RenoLayoutData.Items.ContainsKey(it.Id)).ToList();
                // порядок отрисовки: дальние наземные → здание → навесные → ближние наземные
                var behind = items.Where(it => !RenoLayoutData.Items[it.Id].Attached && RenoLayoutData.Items[it.Id].Y > lay.Base).ToList();
                var front = items.Where(it => !RenoLayoutData.Items[it.Id].Attached && RenoLayoutData.Items[it.Id].Y <= lay.Base).ToList();
                var attached = items.Where(it => RenoLayoutData.Items[it.Id].Attached).ToList();
                var list = new System.Collections.Generic.List<(Image img, int d, bool att)>();
                foreach (var it in behind.OrderByDescending(it => RenoLayoutData.Items[it.Id].Y)) list.Add((RenoItem(scene, it.Id), it.District, false));
                var bSize = ByWidth($"meta_{info.Id}_new", lay.Width);
                // тень здания нарисована в самом спрайте (овальная)
                st.buildingWorn = BottomImg(scene, "BuildingWorn", $"meta_{info.Id}_worn", new Vector2(lay.X, lay.Base), ByWidth($"meta_{info.Id}_worn", lay.Width));
                st.buildingNew = BottomImg(scene, "BuildingNew", $"meta_{info.Id}_new", new Vector2(lay.X, lay.Base), bSize);
                foreach (var it in attached) list.Add((RenoItem(scene, it.Id), it.District, true));
                foreach (var it in front.OrderByDescending(it => RenoLayoutData.Items[it.Id].Y)) list.Add((RenoItem(scene, it.Id), it.District, false));
                st.items = list.Select(x => x.img).ToArray();
                st.itemDistrict = list.Select(x => x.d).ToArray();
                st.itemAttached = list.Select(x => x.att).ToArray();
                var spark = Img("Sparkle", scene, "fx_light_rays", new Vector2(lay.X, lay.Base + bSize.y * 0.5f), new Vector2(bSize.y * 1.1f, bSize.y * 1.1f),
                                color: new Color(1f, 0.95f, 0.7f, 1f));
                st.sparkle = spark.rectTransform;
                spark.gameObject.SetActive(false);
                s.stores[i] = st;
                storeRt.gameObject.SetActive(i == 0);
            }
            // фон улицы отдельным файлом (если он нарисован) — магазин ложится на него слоем.
            // Фон накрывает весь экран в родных пропорциях (лишнее по краям обрезается), поэтому он
            // не внутри кадра «лего», а прямо на экране (29.09.2026)
            s.legoBg = Img("LegoBg", rt, null, Vector2.zero, new Vector2(1920, 1080));
            s.legoBg.gameObject.AddComponent<ScaleToParent>().cover = true;
            s.legoBg.gameObject.SetActive(false);
            // ремонт-«лего»: голый магазин и слои поверх него, все из одного кадра. Кадр во весь экран
            // (магазин нарисован вместе с улицей) RenovationScreen накрывает им экран, вырезанный
            // магазин на своём фоне — вписывает (ScaleToParent.cover)
            s.legoRoot = Rect(Node("Lego", rt), Vector2.zero, new Vector2(1920, 1080));
            s.legoRoot.gameObject.AddComponent<ScaleToParent>();
            // магазин и его улучшения — в одном контейнере: экран ремонта уменьшает его целиком,
            // чтобы он стоял в свободной зоне, а не под табличкой и полкой (27.09.2026)
            s.legoStore = Rect(Node("Store", s.legoRoot), Vector2.zero, new Vector2(1920, 1080));
            s.legoBase = Img("Base", s.legoStore, RenoLegoData.Base(1), Vector2.zero, new Vector2(1920, 1080));
            s.legoPieces = new Image[10];
            for (int i = 0; i < s.legoPieces.Length; i++)
            {
                var img = Img("Piece" + (i + 1), s.legoStore, null, Vector2.zero, new Vector2(100, 100));
                img.preserveAspect = false;   // слой рисуется ровно в своём размере из кадра
                img.gameObject.SetActive(false);
                s.legoPieces[i] = img;
            }
            s.legoRoot.gameObject.SetActive(false);

            // енот с валиком — слева, над полосой карточек (в ремонте он же и говорит)
            s.raccoon = Skin(Img("Raccoon", rt, "chr_raccoon_paint", new Vector2(-820, -120), new Vector2(240, 240)), "paint").rectTransform;

            // шапка по центру: название магазина и этап — на одной табличке, отдельной полосы нет:
            // прогресс показывают кружки внизу (референс игрока 26.09.2026)
            var headSz = ByWidth("ui_header_wood", 660f);
            var head = Img("Head", rt, "ui_header_wood", new Vector2(0, 424), headSz).rectTransform;
            // название — крупно белым с контуром (как заголовки окон), этап — отдельной строкой ниже, светлым и
            // крупнее прежнего: коричневое «Этап 1/2 · 2/5» сливалось с деревом (просьба 03.10.2026)
            s.storeTitle = Text("StoreTitle", head, "Ларёк у остановки", 44, new Vector2(0, 14), new Vector2(480, 58), Color.white,
                                TextAlignmentOptions.Center, true, autoSize: true);
            s.stageText = Text("StageText", head, "Этап 1 из 2 · куплено 0 из 5", 30, new Vector2(0, -40), new Vector2(470, 40),
                               new Color32(0xFF, 0xF3, 0xC8, 255), TextAlignmentOptions.Center, true, autoSize: true);
            s.stageText.enableWordWrapping = false;
            // стрелки этапов — по бокам таблички, ниже её верха: на y 424 правая задевала счётчик монет (01.10.2026)
            s.prevStage = Button("Prev", rt, "ui_btn_round", new Vector2(-372, 380), new Vector2(72, 72), icon: "icon_back");
            s.nextStage = Button("Next", rt, "ui_btn_round", new Vector2(372, 380), new Vector2(72, 72), icon: "icon_next");

            // подсказка — тёмным по светлой плашке под шапкой: раньше она терялась на картинке
            var hintPlate = Img("HintPlate", rt, "ui_tile_button", new Vector2(0, 270), new Vector2(900, 64),
                                sliced: true, preserve: false);
            s.hintText = Text("Hint", hintPlate.transform, "", 32, new Vector2(0, 1), new Vector2(850, 48), Dark,
                              TextAlignmentOptions.Center, true, autoSize: true);

            // облачки улучшений стоят прямо у своих мест на магазине; места считает RenovationScreen
            var bubbles = Stretch(Node("Upgrades", rt));
            s.cardsPanel = bubbles;
            s.cards = new RenoCard[10];
            for (int i = 0; i < 10; i++) s.cards[i] = BuildRenoBubble(bubbles);

            // полоса улучшений кружками: купленные зажжены, в конце — замок нового района
            var rail = Rect(Node("Rail", rt), new Vector2(0, -220), new Vector2(1500, 120));
            s.railRoot = rail;
            s.railTrack = Img("Track", rail, "ui_bar_track", Vector2.zero, new Vector2(600, 26), sliced: true).rectTransform;
            s.railFill = Img("Fill", s.railTrack, "ui_bar_fill", Vector2.zero, new Vector2(582, 16), sliced: true,
                             color: new Color32(0x58, 0xA6, 0x5C, 255));
            s.railFill.type = Image.Type.Filled; s.railFill.fillMethod = Image.FillMethod.Horizontal;
            SlicedFill.Attach(s.railFill);
            s.railNodes = new RectTransform[11];
            s.railNodeIcons = new Image[11];
            s.railNodeChecks = new GameObject[11];
            var nodeSz = ByHeight("ui_btn_round_cream", 78f);
            for (int i = 0; i < 11; i++)
            {
                var node = Img("Node" + (i + 1), rail, "ui_btn_round_cream", Vector2.zero, nodeSz);
                s.railNodes[i] = node.rectTransform;
                s.railNodeIcons[i] = Img("Icon", node.transform, "icon_hammer", Vector2.zero, new Vector2(54, 54));
                s.railNodeChecks[i] = Img("Check", node.transform, "icon_check", new Vector2(24, -24), new Vector2(34, 34)).gameObject;
                node.gameObject.SetActive(false);
            }

            // панель улучшений: доска-полка, на ней крупные иконки с ценником, без подписей
            // (референс игрока 26.09.2026)
            s.tilesPlank = Img("Plank", rt, "ui_plate_wide", new Vector2(0, -412), new Vector2(1600, 286),
                               sliced: true, preserve: false).rectTransform;
            s.tilesRoot = Rect(Node("Tiles", rt), new Vector2(0, -404), new Vector2(1560, 250));
            s.tiles = new RenoCard[10];
            for (int i = 0; i < 10; i++) s.tiles[i] = BuildRenoTile(s.tilesRoot);

            // подсказка «что откроет» — по наведению на «?» у плитки
            var tip = Img("Tip", rt, "ui_tooltip", new Vector2(0, -252), new Vector2(560, 96), sliced: true);
            s.tipPlate = tip.rectTransform;
            s.tipText = Text("Text", tip.transform, "", 28, new Vector2(0, 1), new Vector2(510, 52), Dark,
                             TextAlignmentOptions.Center, autoSize: true);
            tip.gameObject.SetActive(false);
            // вместо «Уровень N» — значок карты: возврат к уровням одним нажатием (просьба 23.09.2026)
            s.playButton = Button("Play", rt, "btn_map", new Vector2(806, 300), ByHeight("btn_map", 118f));
            s.playText = Text("PlayCaption", s.playButton.transform, "", 24, new Vector2(0, -74), new Vector2(220, 36),
                              Color.white, TextAlignmentOptions.Center, true);
            return s;
        }

        /// <summary>Картинка с опорой в низ-центр (стоит на земле).</summary>
        static Image BottomImg(RectTransform parent, string name, string sprite, Vector2 bottom, Vector2 size)
        {
            var img = Img(name, parent, sprite, Vector2.zero, size);
            var r = img.rectTransform;
            r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = bottom;
            return img;
        }

        /// <summary>Предмет ремонта в сцене; тень нарисована в самом спрайте (овальная).</summary>
        static Image RenoItem(RectTransform scene, string id)
        {
            var d = RenoLayoutData.Items[id];
            var img = BottomImg(scene, id, id, new Vector2(d.X, d.Y), ByHeight(id, d.Height));
            return img;
        }

        /// <summary>
        /// Метка на магазине: маленький кружок с предметом на светящейся ниточке. Он только
        /// показывает, где встанет улучшение; покупают с плитки внизу (просьба 26.09.2026).
        /// </summary>
        static RenoCard BuildRenoBubble(RectTransform parent)
        {
            var root = Rect(Node("Upgrade", parent), Vector2.zero, new Vector2(112, 112));
            var c = root.gameObject.AddComponent<RenoCard>();
            // ниточка-светяшка от метки к месту улучшения (пивот слева — растёт вправо и поворачивается)
            c.thread = Img("Thread", root, "fx_thread", Vector2.zero, new Vector2(120, 16), sliced: false,
                           preserve: false, color: new Color(1f, 0.92f, 0.52f, 0.95f)).rectTransform;
            c.thread.pivot = new Vector2(0f, 0.5f);
            c.thread.anchoredPosition = Vector2.zero;
            var bub = ByHeight("ui_badge_round", 104f);
            c.bubble = Img("Bubble", root, "ui_badge_round", Vector2.zero, bub);
            c.icon = Img("Icon", c.bubble.transform, "icon_hammer", Vector2.zero, new Vector2(66, 66));
            c.boughtMark = Img("Bought", root, "icon_check", Vector2.zero, new Vector2(52, 52)).gameObject;
            // плюс в углу: «здесь появится» (03.10.2026, картинка ui_badge_plus)
            if (S("ui_badge_plus") != null) Img("Plus", root, "ui_badge_plus", new Vector2(38, -38), new Vector2(46, 46));
            else
            {
                var pb = Img("Plus", root, "ui_badge_round", new Vector2(38, -38), ByHeight("ui_badge_round", 46f));
                Img("Icon", pb.transform, "icon_plus", Vector2.zero, new Vector2(28, 28));
            }
            return c;
        }

        /// <summary>
        /// Плитка нижней панели по референсу игрока (26.09.2026): крупная иконка без подписи
        /// и ценник. Что улучшение откроет — по наведению на «?» в углу. Куплено — золотая рамка.
        /// </summary>
        static RenoCard BuildRenoTile(RectTransform parent)
        {
            var size = new Vector2(236, 250);
            var root = Rect(Node("Tile", parent), Vector2.zero, size);
            var c = root.gameObject.AddComponent<RenoCard>();
            c.bgNormal = S("ui_tile_slot");
            c.bgBought = S("ui_tile_slot_gold") ?? c.bgNormal;
            c.cardBg = Img("Bg", root, "ui_tile_slot", Vector2.zero, size, raycast: true, sliced: true, preserve: false);
            c.icon = Img("Icon", root, "icon_hammer", new Vector2(0, 26), new Vector2(150, 150));
            var buy = ByHeight("ui_btn_green", 56f);
            c.buyButton = Button("Buy", root, "ui_btn_green", new Vector2(0, -86), new Vector2(Mathf.Min(buy.x, 168f), buy.y),
                                 null, 30, null);
            Img("Coin", c.buyButton.transform, "icon_coin", new Vector2(-58, 0), new Vector2(42, 42));
            c.costText = Text("Cost", c.buyButton.transform, "0", 30, new Vector2(14, 2), new Vector2(96, 42),
                              Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            c.boughtMark = Img("Bought", root, "icon_check", new Vector2(0, -86), new Vector2(58, 58)).gameObject;
            // «?» в углу: что откроет улучшение — по наведению, подписи на плитке нет
            var badge = Img("Hint", root, "ui_badge_round", new Vector2(size.x / 2f - 26f, size.y / 2f - 26f),
                            ByHeight("ui_badge_round", 46f), raycast: true);
            Text("Q", badge.transform, "?", 30, new Vector2(0, 1), new Vector2(40, 40), Dark, TextAlignmentOptions.Center);
            c.hintBadge = badge.gameObject.AddComponent<HoverHint>();
            return c;
        }

        // ================================================================ альбом

        // Альбом собран из трёх слоёв (23.09.2026): стол album_desk, ровная книга album_book_flat,
        // белые ленты ui_album_bookmark. Книга не наклонена, поэтому закладки ложатся ровно.
        const float BookW = 1420f, BookX = 60f, BookY = -10f;
        static readonly Vector2 PageL = new Vector2(-268f, 9f);    // центр левой страницы
        static readonly Vector2 PageR = new Vector2(388f, 9f);     // центр правой страницы

        static AlbumScreen BuildAlbum(RectTransform parent)
        {
            var s = Screen<AlbumScreen>(parent, "AlbumScreen", "album_desk");
            var rt = (RectTransform)s.transform;
            var bookSz = ByWidth("album_book_flat", BookW);
            float edge = BookX - BookW / 2f;                        // левый край обложки

            // закладки — под книгу слева, торчат «ласточкиным хвостом»; цвет отдела задаёт код
            s.tabs = new Button[10];
            s.tabIcons = new Image[10];
            // ленты v4 (album_tab): короче и шире прежних, чуть заходят одна на другую, как настоящие закладки
            var tab = ByHeight("album_tab", 84f);
            for (int i = 0; i < 10; i++)
            {
                float y = 304f - i * 72f;
                float x = edge + 130f - tab.x / 2f;   // наружу торчит ~2/3 ленты
                s.tabs[i] = Button($"Tab{i + 1}", rt, "album_tab", new Vector2(x, y), tab);
                s.tabIcons[i] = Img("Icon", s.tabs[i].transform, MetaCatalog.StickersOf(i)[0], new Vector2(-tab.x / 2f + 92f, 2f), new Vector2(58, 58));
                var nav = s.tabs[i].navigation; nav.mode = Navigation.Mode.None; s.tabs[i].navigation = nav;
            }
            Img("Book", rt, "album_book_flat", new Vector2(BookX, BookY), bookSz);

            // левая страница — девять наклеек отдела
            s.deptTitle = Text("DeptTitle", rt, "Фрукты", 42, PageL + new Vector2(0, 291), new Vector2(500, 64), Dark);
            s.cells = new StickerCell[9];
            for (int i = 0; i < 9; i++)
            {
                int col = i % 3, row = i / 3;
                s.cells[i] = BuildStickerCell(rt, $"Cell{i + 1}", PageL + new Vector2((col - 1) * 172, 181 - row * 180), new Vector2(164, 168));
            }

            // правая страница — золотая наклейка, полоса отдела и пачка
            s.goldCell = BuildStickerCell(rt, "GoldCell", PageR + new Vector2(0, 185), new Vector2(250, 236));
            s.goldCell.label.gameObject.SetActive(false);   // подпись у золотой — одна, ниже
            Text("GoldCaption", rt, "Золотая наклейка отдела", 28, PageR + new Vector2(0, 46), new Vector2(440, 44), Dark);
            var frame = Img("ProgressFrame", rt, "ui_bar_track", PageR + new Vector2(-40, -20), new Vector2(380, 58), sliced: true, preserve: false);
            s.progressFill = Img("ProgressFill", frame.transform, "ui_bar_fill", Vector2.zero, new Vector2(346, 38), sliced: true,
                                 preserve: false, color: new Color32(0x58, 0xA6, 0x5C, 255));
            s.progressFill.type = Image.Type.Filled; s.progressFill.fillMethod = Image.FillMethod.Horizontal;
            SlicedFill.Attach(s.progressFill);
            s.progressText = Text("ProgressText", frame.transform, "0/9", 28, Vector2.zero, new Vector2(200, 44), Dark, TextAlignmentOptions.Center, true);
            // награда отдела — рамка аватарки (картинку ставит AlbumScreen по отделу)
            s.deptRewardIcon = Img("Reward", rt, "frame_dept_fruits", PageR + new Vector2(206, -20), ByHeight("frame_dept_fruits", 92f));
            s.deptRewardDone = Img("RewardDone", rt, "icon_check", PageR + new Vector2(226, -44), ByHeight("icon_check", 50f)).gameObject;
            // бонус коллекции (v4, 03.10.2026): зачем собирать наклейки
            Img("BonusIcon", rt, "icon_dept_bonus", PageR + new Vector2(-212, -84), ByHeight("icon_dept_bonus", 48f));
            s.bonusText = Text("Bonus", rt, "", 22, PageR + new Vector2(28, -84), new Vector2(420, 50), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            s.bonusText.fontSizeMin = 14;
            Img("Pack", rt, "album_pack_closed", PageR + new Vector2(-150, -176), ByHeight("album_pack_closed", 176f));
            s.openPackButton = Button("OpenPack", rt, "ui_btn_rewarded", PageR + new Vector2(80, -176), ByHeight("ui_btn_rewarded", 112f), "Открыть пачку", 30, "icon_video");
            s.openPackText = Label(s.openPackButton);
            s.openPackVideo = Icon(s.openPackButton).gameObject;
            return s;
        }

        static StickerCell BuildStickerCell(RectTransform parent, string name, Vector2 pos, Vector2 size)
        {
            // рамки нет: наклейка лежит прямо на странице, как настоящая
            var root = Rect(Node(name, parent), pos, size);
            var hold = root.gameObject.AddComponent<Image>();
            hold.sprite = null; hold.enabled = false;
            var c = root.gameObject.AddComponent<StickerCell>();
            c.frame = hold;
            float side = Mathf.Min(size.x, size.y - 34f);
            c.image = Img("Image", root, "item_apple", new Vector2(0, 14), new Vector2(side, side));
            c.shine = Img("Shine", root, "fx_sparkle", new Vector2(size.x * 0.32f, size.y * 0.3f), new Vector2(58, 58));
            c.label = Text("Label", root, "Яблоко", 22, new Vector2(0, -size.y / 2f + 14f), new Vector2(size.x * 0.92f, 32), Dark, autoSize: true);
            // длинное имя («Золотая наклейка») уменьшается в одну строку — раньше наезжало на соседнюю наклейку
            c.label.enableWordWrapping = false;
            c.label.fontSizeMin = 12;
            c.question = Text("Question", root, "?", 76, new Vector2(0, 14), new Vector2(110, 112), new Color(1, 1, 1, 0.92f), TextAlignmentOptions.Center, true).gameObject;
            return c;
        }

        // ================================================================ магазин покупок

        // Магазин по концепту (01.10.2026): страница-картинка на вкладку. Места вкладок, подиума и карточек
        // посчитаны по самим картинкам (зелёные кнопки и кремовые поля), в пикселях страницы 1920×1080.
        static readonly float[] ShopTabX = { 637, 794, 951, 1108, 1265 };
        const float ShopTabY = 172f;
        static readonly string[] ShopPages = { "shop_page_1_coins", "shop_page_2_helpers", "shop_page_3_style", "shop_page_4_packs", "shop_page_5_noads" };

        /// <summary>Точка страницы (x, y от левого верхнего угла картинки 1920×1080) → координаты в «книге» страниц.</summary>
        static Vector2 P(float x, float y) => new Vector2(x - 960f, 540f - y);

        /// <summary>
        /// «Книга» страниц: 1920×1080, масштабируется под экран вместе с содержимым (ScaleToParent),
        /// под ней — приглушённая копия первой страницы, она видна только полосами на широком экране.
        /// </summary>
        static RectTransform PageBook(RectTransform screen, string firstPage, Color flat)
        {
            var back = Background("Backdrop", screen, firstPage);
            back.color = new Color(0.55f, 0.5f, 0.47f);
            var sp = back.sprite;   // AddComponent сразу вызывает OnEnable — картинку запоминаем до него
            var pb = back.gameObject.AddComponent<PageBackdrop>();   // на экране уже 16:9 — ровный цвет вместо копии
            pb.page = sp;
            pb.flat = flat;
            back.sprite = sp;
            back.color = pb.dim;
            var book = Rect(Node("Pages", screen), Vector2.zero, new Vector2(1920f, 1080f));
            var fit = book.gameObject.AddComponent<ScaleToParent>();
            fit.cover = true;
            fit.maxOver = 1.12f;
            return book;
        }

        static RectTransform PageImage(RectTransform book, string name, string sprite)
        {
            var rt = Stretch(Node(name, book));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = S(sprite);
            img.raycastTarget = true;   // клики мимо кнопок не уходят на экран под страницей
            return rt;
        }

        /// <summary>Прозрачная кнопка поверх нарисованной (вкладки, зелёные кнопки страниц).</summary>
        static Button HitButton(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var img = Rect(Node(name, parent), pos, size).gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            img.gameObject.AddComponent<UiButton>();
            return b;
        }

        static ShopScreen BuildShop(RectTransform parent)
        {
            var s = Screen<ShopScreen>(parent, "ShopScreen", null);
            var rt = (RectTransform)s.transform;
            var book = PageBook(rt, ShopPages[0], new Color(0.36f, 0.24f, 0.16f));
            var cards = new System.Collections.Generic.List<ShopCard>();
            s.pages = new GameObject[ShopScreen.TabCount];
            // что лежит на каждой вкладке: подиум и карточки слева направо
            string[] heroes = { "coins_xl", "helpers_big", "set_gold", "starter_pack", "no_ads" };
            string[] heroIcons = { "shop_hero_coins_xl", "shop_hero_helpers", "shop_hero_style", "shop_hero_starter", "shop_hero_noads" };
            string[] heroBadges = { "Хит", "Новое", "Выгодно", "Выгодно", "Навсегда" };
            string[][] items =
            {
                new[] { "ad_coins", "coins_s", "coins_m", "coins_l", "coins_xl" },
                new[] { "buy_undo3", "buy_hint3", "helpers_pack", "helpers_big", "undo_24h" },
                new[] { "theme_farm", "theme_winter", "theme_night", "set_gold", "sets_all" },
                new[] { "builder_pack", "stickers_pack", "set_gold" },
                new[] { "ad_coins", "ad_hint", "ad_undo", "ad_pack" },
            };
            string[][] icons =
            {
                new[] { "shop_coin_video", null, null, null, null },
                new string[] { null, null, null, null, null },
                new string[] { null, null, null, null, null },
                new[] { "shop_pack_builder", "shop_pack_collector", "shop_pack_gold" },
                new[] { "shop_free_coins", null, null, null },
            };
            for (int t = 0; t < ShopScreen.TabCount; t++)
            {
                var page = PageImage(book, $"Page{t + 1}", ShopPages[t]);
                s.pages[t] = page.gameObject;
                // подиум: у «Наборов» и «Без рекламы» на картинке нет кнопки — ставим свою
                var hero = BuildShopHero(page, heroes[t], heroIcons[t], heroBadges[t], t >= 3);
                cards.Add(hero);
                if (t == 3) s.starterHero = hero;
                // карточки: 5 обычных, 3 широкие («Наборы») или 4 с нарисованным значком видео («Без рекламы»)
                float[] bx; float bw, bh, by = 923.5f;
                if (t == 3) { bx = new[] { 450.5f, 960.5f, 1468f }; bw = 413; bh = 54; }
                else if (t == 4) { bx = new[] { 386f, 769f, 1152f, 1534.5f }; bw = 286; bh = 64; by = 926; }
                else { bx = new[] { 349.5f, 654.5f, 960f, 1265f, 1570.5f }; bw = 223; bh = 49; }
                for (int k = 0; k < bx.Length; k++)
                {
                    string id = items[t][k];
                    var c = BuildShopCard(page, $"Card{k + 1}", id, icons[t][k], bx[k], by, bw, bh, t == 4);
                    c.badge = id == "coins_l" ? "Выгодно"
                            : id == "helpers_big" || id == "sets_all" || id == "builder_pack" || id == "stickers_pack" ? "Новое" : "";
                    cards.Add(c);
                    if (t == 3 && k == 0) s.starterSwap = c;
                }
            }
            // вкладки одни на все страницы: они нарисованы на каждой картинке в одном месте
            s.tabs = new Button[ShopScreen.TabCount];
            for (int t = 0; t < ShopScreen.TabCount; t++)
                s.tabs[t] = HitButton($"Tab{t + 1}", book, P(ShopTabX[t], ShopTabY), new Vector2(140, 132));
            s.cards = cards.ToArray();
            for (int t = 1; t < ShopScreen.TabCount; t++) s.pages[t].SetActive(false);   // в сцене видна первая вкладка
            s.unavailable = Text("Unavailable", book, "Покупки сейчас недоступны", 34, P(960, 292), new Vector2(760, 50),
                                 Color.white, TextAlignmentOptions.Center, true).gameObject;
            s.themeApplyButtons = new Button[0];
            return s;
        }

        /// <summary>Подиум вкладки: крупная картинка на столике, ленточка, табличка с названием и цена.</summary>
        static ShopCard BuildShopHero(RectTransform page, string id, string icon, string badge, bool ownButton)
        {
            var root = Rect(Node("Hero", page), P(960, 430), new Vector2(720, 420));
            var c = root.gameObject.AddComponent<ShopCard>();
            c.productId = id;
            c.iconName = icon;
            c.badge = badge;
            Vector2 L(float x, float y) => P(x, y) - P(960, 430);
            // картинка стоит на скатерти: низ — на уровне столешницы (y ≈ 505)
            c.icon = Img("Icon", root, icon, L(960, 380), new Vector2(300, 250));
            // табличка с названием и составом — слева от столика, на фоне окна
            // у таблички по бокам листья и толстая рамка: кремовое поле — примерно 70 % её ширины
            var plate = Img("Plate", root, "ui_plate_wide", L(430, 420), new Vector2(480, 220), sliced: true);
            c.title = Text("Title", plate.transform, "", 32, new Vector2(0, 40), new Vector2(330, 44), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            c.title.enableWordWrapping = false;
            c.description = Text("Desc", plate.transform, "", 24, new Vector2(0, -24), new Vector2(330, 80), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            c.description.fontSizeMin = 15;
            var btnPos = ownButton ? L(960, 652) : L(956, 616);
            if (ownButton)
            {
                c.buyButton = Button("Buy", root, "ui_btn_green", btnPos, ByHeight("ui_btn_green", 90f), "199 ₽", 40);
                c.priceIcon = null;
            }
            else c.buyButton = PriceButton(root, btnPos, new Vector2(356, 66), 36, false, out c.priceIcon);
            c.priceText = Label(c.buyButton);
            c.ownedMark = Img("Owned", root, "icon_check", btnPos + new Vector2(150, 30), ByHeight("icon_check", 56f)).gameObject;
            // ленточка с кремовой серединой: на розетке ui_ribbon_best слово «Выгодно» не помещалось
            var rib = ByHeight("ui_ribbon_sale", 62f);
            c.bestBadge = Img("Best", root, "ui_ribbon_sale", L(1110, 292), rib).gameObject;
            c.bestText = Text("BestText", c.bestBadge.transform, badge, 26, new Vector2(0, rib.y * 0.04f), new Vector2(rib.x * 0.6f, rib.y * 0.5f), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            c.bestText.enableWordWrapping = false;
            return c;
        }

        /// <summary>
        /// Содержимое нарисованной карточки: название на табличке сверху, картинка в середине, цена на кнопке.
        /// bx, by — центр нарисованной зелёной кнопки; drawnVideo — на кнопке уже нарисован значок видео.
        /// </summary>
        static ShopCard BuildShopCard(RectTransform page, string name, string id, string icon, float bx, float by, float bw, float bh, bool drawnVideo)
        {
            // карточка: табличка y≈740, поле картинки 765–885, кнопка ≈ by
            float cardW = bw + 40f;
            var root = Rect(Node(name, page), P(bx, 830), new Vector2(cardW, 260));
            Vector2 L(float x, float y) => P(x, y) - P(bx, 830);
            var c = root.gameObject.AddComponent<ShopCard>();
            c.productId = id;
            c.iconName = icon;
            var info = MetaCatalog.Product(id);
            c.title = Text("Title", root, "", 26, L(bx, 740), new Vector2(cardW - 56f, 36), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            c.title.enableWordWrapping = false;
            c.title.fontSizeMin = 14;
            c.icon = Img("Icon", root, icon ?? (info != null ? info.Icon : id == "buy_hint3" ? "shop_hint3" : id == "buy_undo3" ? "shop_undo3" : "shop_free_coins"),
                         L(bx, 825), new Vector2(cardW - 40f, 116));
            c.description = Text("Desc", root, "", 18, Vector2.zero, new Vector2(cardW, 20f), Dark);
            c.description.gameObject.SetActive(false);   // на карточке описания нет: название и цена говорят сами
            c.buyButton = PriceButton(root, L(bx, by), new Vector2(bw, bh), bh >= 60 ? 32 : 28, drawnVideo, out c.priceIcon);
            c.priceText = Label(c.buyButton);
            c.ownedMark = Img("Owned", root, "icon_check", L(bx + bw / 2f - 6f, by - 30f), ByHeight("icon_check", 44f)).gameObject;
            var rib = ByHeight("ui_ribbon_sale", 44f);
            c.bestBadge = Img("Best", root, "ui_ribbon_sale", L(bx + cardW / 2f - rib.x * 0.4f, 706), rib).gameObject;
            c.bestText = Text("BestText", c.bestBadge.transform, "Выгодно", 20, new Vector2(0, rib.y * 0.04f), new Vector2(rib.x * 0.6f, rib.y * 0.5f), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            c.bestText.enableWordWrapping = false;
            return c;
        }

        /// <summary>
        /// Невидимая кнопка поверх нарисованной зелёной: подпись-цена и значок (монета или видео) слева от неё.
        /// Если значок видео уже нарисован на кнопке (вкладка «Без рекламы»), своего значка нет, подпись — правее.
        /// </summary>
        static Button PriceButton(RectTransform parent, Vector2 pos, Vector2 size, float font, bool drawnVideo, out Image priceIcon)
        {
            var b = HitButton("Buy", parent, pos, size);
            // кнопка нарисована на странице скруглённой, а эта — прямоугольник: подсветку не показываем,
            // нажатие видно по «пружинке» UiButton, погашенная — по подписи «Завтра»
            float left = drawnVideo ? -size.x / 2f + size.y * 1.45f : -size.x * 0.40f, right = size.x * 0.42f;
            float textL = drawnVideo ? left : left + size.y * 0.75f;
            var t = Text("Label", b.transform, "", font, new Vector2((textL + right) / 2f, 2f),
                         new Vector2(right - textL, size.y * 0.8f), Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            t.enableWordWrapping = false;
            t.fontSizeMin = 14;
            priceIcon = null;
            if (!drawnVideo)
            {
                float isz = size.y * 0.72f;
                priceIcon = Img("PriceIcon", b.transform, "icon_coin", new Vector2(left + isz * 0.5f, 2f), new Vector2(isz, isz));
                priceIcon.gameObject.SetActive(false);
            }
            return b;
        }

        // ================================================================ ежедневки

        /// <summary>
        /// «Завоз дня» v4 по картинке daily_empty (1672×944, в книге 1920×1080): доска-календарь 4+3 слота,
        /// «Забрать» под доской, карточка «Испытание дня» справа. Места — по пикселям картинки.
        /// </summary>
        static DailyScreen BuildDaily(RectTransform parent)
        {
            var s = Screen<DailyScreen>(parent, "DailyScreen", null);
            var rt = (RectTransform)s.transform;
            var book = PageBook(rt, "daily_empty", new Color(0.55f, 0.7f, 0.4f));
            PageImage(book, "Page", "daily_empty");
            const float kx = 1920f / 1672f, ky = 1080f / 944f;
            Vector2 Q(float x, float y) => P(x * kx, y * ky);
            Vector2 Sz(float w, float h) => new Vector2(w * kx, h * ky);
            var title = Text("Title", book, "Завоз дня", 56, Q(778, 108), Sz(420, 84), Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            title.enableWordWrapping = false;
            s.crateClosed = S("daily_crate_closed"); s.crateOpen = S("daily_crate_open"); s.crateDone = S("daily_crate_done");
            s.chestBig = S("daily_chest_big");
            // слоты: верхний ряд 4, нижний 2 и широкий под сундук 7-го дня
            var slots = new[] { new Vector2(525, 320), new Vector2(703, 320), new Vector2(879, 320), new Vector2(1055, 320),
                                new Vector2(550, 545), new Vector2(759, 545), new Vector2(997, 545) };
            for (int i = 0; i < 7; i++)
            {
                var c = slots[i];
                bool chest = i == 6;
                float top = c.y - (i < 4 ? 92 : 96), bottom = c.y + (i < 4 ? 92 : 96);
                s.glows[i] = Img("Glow" + (i + 1), book, "brd_slot_glow", Q(c.x, c.y), Sz(chest ? 250 : (i < 4 ? 166 : 192), 196), sliced: true,
                                 color: new Color(1f, 0.88f, 0.35f, 0.9f), preserve: false).gameObject;
                s.dayTexts[i] = Text("Day" + (i + 1), book, $"День {i + 1}", 24, Q(c.x, top + 18), Sz(150, 30), Dark, TextAlignmentOptions.Center, false, autoSize: true);
                var spr = chest ? "daily_chest_big" : "daily_crate_closed";
                s.crates[i] = Img("Crate" + (i + 1), book, spr, Q(c.x, c.y - 4), ByHeight(spr, (chest ? 130f : 112f) * ky));
                s.rewardTexts[i] = Text("Reward" + (i + 1), book, "40 монет", 20, Q(c.x, bottom - 24), Sz(chest ? 230 : 150, 40), Dark,
                                        TextAlignmentOptions.Center, false, autoSize: true);
                s.rewardTexts[i].fontSizeMin = 12;
            }
            s.claimButton = Button("Claim", book, "ui_btn_green", Q(785, 792), ByHeight("ui_btn_green", 100f * ky), "Забрать", 46, "icon_gift");
            s.claimText = Label(s.claimButton);
            s.chest2Button = Button("Chest2", book, "ui_btn_rewarded", Q(250, 840), ByHeight("ui_btn_rewarded", 84f * ky), "Ещё ящик", 34, "icon_video");

            // карточка «Испытание дня»
            s.challengeCard = Rect(Node("Challenge", book), Q(1450, 510), Sz(384, 570));
            var badge = Img("Badge", book, "ui_btn_round_cream", Q(1450, 236), Sz(124, 124));
            s.ruleIcon = Img("Rule", badge.transform, "icon_rule_no_undo", Vector2.zero, Sz(104, 104));
            s.ruleNum = Text("Num", s.ruleIcon.transform, "3", 40, new Vector2(0, -9), Sz(50, 46), new Color32(0x8A, 0x4B, 0x2A, 255),
                             TextAlignmentOptions.Center, false, autoSize: true);
            s.challengeTitle = Text("ChallengeTitle", book, "Без отмен", 32, Q(1450, 322), Sz(330, 40), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            Text("ChallengeCaption", book, "Испытание дня", 22, Q(1450, 352), Sz(330, 28), new Color32(0x8A, 0x5A, 0x2E, 255), TextAlignmentOptions.Center, false, autoSize: true);
            s.challengeChest = Img("Chest", book, "daily_chest_closed", Q(1450, 450), ByHeight("daily_chest_closed", 140f * ky));
            s.ruleText = Text("RuleText", book, "", 22, Q(1450, 556), Sz(330, 56), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            s.ruleText.fontSizeMin = 14;
            s.dotOn = S("daily_dot_on"); s.dotOff = S("daily_dot_off");
            for (int i = 0; i < 7; i++)
                s.dots[i] = Img("Dot" + (i + 1), book, "daily_dot_off", Q(1320 + 43 * i, 612), Sz(32, 32));
            s.playButton = Button("Play", book, "ui_btn_primary", Q(1450, 706), ByHeight("ui_btn_primary", 80f * ky), "Играть", 40, "icon_challenge");
            s.playText = Label(s.playButton);
            return s;
        }

        // ================================================================ лидерборды

        // Рейтинг по концепту (01.10.2026): страница-картинка на вкладку (ларёк, пьедестал, пустое поле таблицы).
        // Места колец, табличек и полей посчитаны по самим картинкам, в пикселях страницы 1920×1080.
        static readonly float[] LbTabX = { 556, 760, 959, 1158.5f, 1359 };
        const float LbTabY = 91f;
        static readonly string[] LbPages = { "lb_page_1_today", "lb_page_2_week", "lb_page_3_near", "lb_page_4_world", "lb_page_5_rewards" };

        // пьедестал: центр кольца и таблички под ник — по местам 1, 2, 3 (1-е в середине)
        static readonly Vector2[] PodiumRing = { new Vector2(959.5f, 297), new Vector2(704.5f, 318), new Vector2(1227, 327) };
        static readonly Vector2[] PodiumPlate = { new Vector2(957, 418), new Vector2(696, 428), new Vector2(1232, 430) };
        static readonly Vector2[] PodiumPlateSize = { new Vector2(170, 48), new Vector2(150, 38), new Vector2(150, 36) };
        // «Весь мир»: пьедестал ниже — над ним гирлянда флажков
        static readonly Vector2[] WorldRing = { new Vector2(960, 390), new Vector2(705, 380), new Vector2(1228, 385) };
        static readonly Vector2[] WorldPlate = { new Vector2(960, 495), new Vector2(695, 498), new Vector2(1228, 498) };

        static LeaderboardScreen BuildLeaders(RectTransform parent)
        {
            var s = Screen<LeaderboardScreen>(parent, "LeaderboardScreen", null);
            var rt = (RectTransform)s.transform;
            var book = PageBook(rt, LbPages[0], new Color(0.38f, 0.56f, 0.33f));
            s.pages = new GameObject[LeaderboardScreen.TabCount];
            s.lists = new LeaderList[4];
            for (int t = 0; t < LeaderboardScreen.TabCount; t++)
            {
                var page = PageImage(book, $"Page{t + 1}", LbPages[t]);
                s.pages[t] = page.gameObject;
                switch (t)
                {
                    case 0:
                    case 1:
                        // поле таблицы 467–1451 × 476–903: пять строк; под разделителем — строка игрока
                        s.lists[t] = BuildLbList(page, PodiumRing, PodiumPlate, 476, 903, 5, 960, 967, 86, scrollRows: 17);
                        if (t == 1)
                            // табличка с часами на крыше ларька — сколько осталось до итогов недели
                            s.lists[t].timer = Text("Timer", page, "3 д 5 ч", 28, P(375, 303), new Vector2(150, 54), Dark, TextAlignmentOptions.Center, false, autoSize: true);
                        break;
                    case 2:
                        // «Рядом»: без пьедестала, поле 463–1453 × 240–1009 — семь строк
                        s.lists[t] = BuildLbList(page, null, null, 240, 1009, 7, 972, 0, 0);
                        break;
                    case 3:
                        s.lists[t] = BuildLbList(page, WorldRing, WorldPlate, 550, 918, 4, 960, 965, 86, scrollRows: 17);
                        break;
                    default:
                        BuildLbRewards(page, s);
                        break;
                }
            }
            s.tabs = new Button[LeaderboardScreen.TabCount];
            for (int t = 0; t < LeaderboardScreen.TabCount; t++)
                s.tabs[t] = HitButton($"Tab{t + 1}", book, P(LbTabX[t], LbTabY), new Vector2(190, 100));
            // подпись-ленточка под активной вкладкой (v4): где ты сейчас — ставит LeaderboardScreen
            var tcs = ByWidth("ui_tab_caption", 190f);
            var tcap = Img("ActiveTab", book, "ui_tab_caption", P(LbTabX[0], LbTabY + 66f), tcs);
            s.activeTabCaption = tcap.rectTransform;
            s.activeTabText = Text("Text", tcap.transform, "Сегодня", 26, new Vector2(0, 1), new Vector2(tcs.x * 0.66f, tcs.y * 0.5f), Dark,
                                   TextAlignmentOptions.Center, false, autoSize: true);
            s.activeTabText.enableWordWrapping = false;
            // табличка справа над енотом: какая вкладка открыта и что она считает (просьба 03.10.2026:
            // «переключаюсь и не понимаю, в какой я вкладке и что она оценивает»)
            var capSz = ByWidth("ui_panel_info", 380f);
            var cap = Img("TabCaption", book, "ui_panel_info", P(1712, 318), capSz).rectTransform;
            s.tabTitle = Text("Title", cap, "Сегодня", 34, new Vector2(0, capSz.y * 0.40f), new Vector2(capSz.x * 0.7f, capSz.y * 0.14f),
                              Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            s.tabTitle.enableWordWrapping = false;
            s.tabInfo = Text("Info", cap, "", 26, new Vector2(0, -capSz.y * 0.05f), new Vector2(capSz.x * 0.76f, capSz.y * 0.56f),
                             Dark, TextAlignmentOptions.Center, false, autoSize: true);
            s.tabInfo.fontSizeMin = 16;
            for (int t = 1; t < LeaderboardScreen.TabCount; t++) s.pages[t].SetActive(false);
            return s;
        }

        /// <summary>Вкладка с таблицей: пьедестал (если есть), rows строк между top и bottom, строка игрока на myY.</summary>
        static LeaderList BuildLbList(RectTransform page, Vector2[] rings, Vector2[] plates, float top, float bottom, int rows,
                                      float rowW, float myY, float myH, int scrollRows = 0)
        {
            var list = page.gameObject.AddComponent<LeaderList>();
            var pod = new System.Collections.Generic.List<PodiumSlot>();
            if (rings != null)
                for (int i = 0; i < 3; i++)
                    pod.Add(BuildPodiumSlot(page, i + 1, rings[i], plates[i], PodiumPlateSize[i]));
            list.podium = pod.ToArray();
            float pitch = (bottom - top) / rows;
            float rowH = Mathf.Min(98f, pitch - 8f);
            if (scrollRows > 0)
            {
                // прокрутка (03.10.2026): в поле те же rows строк, а листается до scrollRows; справа — ползунок
                const float bar = 34f, gap = 12f;
                float w = rowW - bar - gap;
                var view = Img("View", page, null, P(959, (top + bottom) / 2f), new Vector2(rowW, bottom - top), color: new Color(0, 0, 0, 0)).rectTransform;
                view.gameObject.GetComponent<Image>().raycastTarget = true;
                view.gameObject.AddComponent<RectMask2D>();
                var content = Rect(Node("Content", view), Vector2.zero, new Vector2(rowW, pitch * scrollRows));
                content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.anchoredPosition = Vector2.zero;
                list.rows = new LeaderRow[scrollRows];
                for (int i = 0; i < scrollRows; i++)
                {
                    list.rows[i] = BuildLbRow(content, $"Row{i + 1}", Vector2.zero, w, rowH, true);
                    // строки считаются от верха списка (по умолчанию Img ставит их от середины)
                    var rr = (RectTransform)list.rows[i].transform;
                    rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 1f);
                    rr.anchoredPosition = new Vector2(-(bar + gap) / 2f, -pitch * (i + 0.5f));
                }
                var sr = view.gameObject.AddComponent<ScrollRect>();
                sr.content = content;
                sr.viewport = view;
                sr.horizontal = false;
                sr.vertical = true;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.scrollSensitivity = 40f;
                // ползунок (v4): деревянный жёлоб и зелёная ручка-«таблетка», режутся по высоте (9-slice)
                var track = Img("Scrollbar", view, "ui_scroll_track_v", new Vector2(rowW / 2f - bar / 2f, 0), new Vector2(bar, bottom - top - 8f),
                                sliced: true, preserve: false).rectTransform;
                var sb = track.gameObject.AddComponent<Scrollbar>();
                var area = Rect(Node("Area", track), Vector2.zero, Vector2.zero);
                area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
                area.offsetMin = new Vector2(1, 6); area.offsetMax = new Vector2(-1, -6);
                var handle = Img("Handle", area, "ui_scroll_knob_v", Vector2.zero, new Vector2(bar - 2f, 120f), sliced: true, preserve: false);
                handle.rectTransform.anchorMin = Vector2.zero; handle.rectTransform.anchorMax = Vector2.one;
                handle.rectTransform.offsetMin = handle.rectTransform.offsetMax = Vector2.zero;
                sb.handleRect = handle.rectTransform;
                sb.targetGraphic = handle;
                sb.direction = Scrollbar.Direction.BottomToTop;
                sr.verticalScrollbar = sb;
                sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                list.scroll = sr;
            }
            else
            {
                list.rows = new LeaderRow[rows];
                for (int i = 0; i < rows; i++)
                    list.rows[i] = BuildLbRow(page, $"Row{i + 1}", P(959, top + pitch * (i + 0.5f)), rowW, rowH, true);
            }
            if (myY > 0f) list.myRow = BuildLbRow(page, "MyRow", P(959, myY), rowW, myH, true);
            // статус — на месте строки игрока («сыграй уровень…»), а на «Рядом» — посреди пустого поля
            var stPos = myY > 0f ? P(959, myY) : P(959, (top + bottom) / 2f);
            list.status = Text("Status", page, "Загрузка...", 32, stPos, new Vector2(rowW - 60f, 60), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            return list;
        }

        /// <summary>Место на пьедестале: зверёк внутри нарисованного кольца, медаль слева снизу, очки справа снизу.</summary>
        static PodiumSlot BuildPodiumSlot(RectTransform page, int place, Vector2 ring, Vector2 plate, Vector2 plateSize)
        {
            var root = Rect(Node($"Podium{place}", page), P(ring.x, ring.y), new Vector2(150, 150));
            var p = root.gameObject.AddComponent<PodiumSlot>();
            p.avatar = Img("Avatar", root, "av_cat", Vector2.zero, new Vector2(132, 132));
            p.medal = Img("Medal", root, "lb_medal_" + place, new Vector2(-64, -46), ByHeight("lb_medal_" + place, 62f));
            var pillSz = ByHeight("ui_pill_counter", 38f);
            var pill = Img("Score", root, "ui_pill_counter", new Vector2(68, -52), pillSz);
            p.scorePill = pill.gameObject;
            p.scoreIcon = Img("Icon", pill.transform, "lb_points", new Vector2(-pillSz.x / 2f + 16f, 1), new Vector2(34, 34));
            p.score = Text("Value", pill.transform, "0", 24, new Vector2(12, 1), new Vector2(pillSz.x - 44f, 30), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            p.score.enableWordWrapping = false;
            // ник — на нарисованной табличке под кольцом: до ~12 знаков, длиннее — мельче, потом «…»
            p.playerName = Text("Name", root, "—", 24, P(plate.x, plate.y) - P(ring.x, ring.y), plateSize - new Vector2(18, 6), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            p.playerName.enableWordWrapping = false;
            p.playerName.fontSizeMin = 14;
            p.playerName.overflowMode = TextOverflowModes.Ellipsis;
            return p;
        }

        /// <summary>
        /// Строка таблицы — из ровной плашки ui_tile_button (9-slice), а не из присланных ui_lb_row*:
        /// те пришли сплющенными (полоса 40 из 100 px, круги стали овалами). Слева направо:
        /// медаль/номер, аватарка в рамке, ник, очки со значком, подарок за место.
        /// </summary>
        static LeaderRow BuildLbRow(RectTransform parent, string name, Vector2 pos, float w, float h, bool gift)
        {
            var bg = Img(name, parent, "ui_tile_button", pos, new Vector2(w, h), sliced: true, preserve: false);
            var r = bg.gameObject.AddComponent<LeaderRow>();
            r.background = bg;
            float x0 = -w / 2f, x1 = w / 2f, icon = Mathf.Min(h - 14f, 70f);
            r.medal = Img("Medal", bg.transform, "lb_place", new Vector2(x0 + 50, 0), new Vector2(icon, icon));
            r.rank = Text("Rank", r.medal.transform, "4", 30, new Vector2(0, 1), new Vector2(icon * 0.8f, icon * 0.6f), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            r.rank.enableWordWrapping = false;
            r.avatar = Img("Avatar", bg.transform, "av_cat", new Vector2(x0 + 132, 0), new Vector2(icon - 6f, icon - 6f));
            r.frame = Img("Frame", r.avatar.transform, "frame_basic", Vector2.zero, new Vector2(icon + 10f, icon + 10f));
            r.playerName = Text("Name", bg.transform, "Игрок", 34, new Vector2((x0 + 184 + x1 - 290) / 2f, 2), new Vector2(x1 - 290 - (x0 + 184), h - 16f),
                                Dark, TextAlignmentOptions.Left, false, autoSize: true);
            r.playerName.enableWordWrapping = false;
            r.playerName.fontSizeMin = 22;
            r.playerName.overflowMode = TextOverflowModes.Ellipsis;
            r.scoreIcon = Img("ScoreIcon", bg.transform, "lb_points", new Vector2(x1 - 262, 0), new Vector2(46, 46));
            r.score = Text("Score", bg.transform, "0", 32, new Vector2(x1 - 168, 2), new Vector2(140, h - 20f), Dark, TextAlignmentOptions.Left, false, autoSize: true);
            r.score.enableWordWrapping = false;
            r.gift = gift ? Img("Gift", bg.transform, "lb_gift_4", new Vector2(x1 - 50, 0), new Vector2(icon - 6f, icon - 6f)) : null;
            bg.gameObject.SetActive(false);
            return r;
        }

        /// <summary>«Награды»: две доски (день и неделя) — что дают за места; снизу кнопка «Забрать» у сундука.</summary>
        static void BuildLbRewards(RectTransform page, LeaderboardScreen s)
        {
            BuildRewardBoard(page, "Day", new Vector2(714.5f, 542), "Каждый день", false);
            BuildRewardBoard(page, "Week", new Vector2(1206f, 542), "Каждую неделю", true);
            // нарисованная зелёная кнопка 536–1070 × 818–952, справа от неё нарисован сундук
            var b = HitButton("Claim", page, P(803, 885), new Vector2(520, 120));
            s.claimButton = b;
            s.claimText = Text("Label", b.transform, "Забрать награду", 40, new Vector2(0, 4), new Vector2(460, 90), Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            s.claimText.enableWordWrapping = true;
            s.claimText.fontSizeMin = 22;
            s.chest = Rect(Node("Chest", page), P(1262, 900), new Vector2(300, 220));
        }

        /// <summary>Доска наград (443×438): подпись сверху и по строке на ступень мест. Данные — League.</summary>
        static void BuildRewardBoard(RectTransform page, string name, Vector2 center, string caption, bool week)
        {
            var board = Rect(Node("Rewards" + name, page), P(center.x, center.y), new Vector2(443, 438));
            Text("Caption", board, caption, 30, new Vector2(0, 184), new Vector2(400, 44), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            var ranks = new System.Collections.Generic.List<int>(League.TierRanks);
            if (week) ranks.Add(101);   // «за участие»
            // снизу на правую доску заходит нарисованный сундук — строки держим выше него
            float pitch = 300f / ranks.Count, y = 150f;
            for (int i = 0; i < ranks.Count; i++, y -= pitch)
            {
                int rank = ranks[i];
                var rw = week ? League.WeekReward(rank, League.ParticipationPoints) : League.DayReward(rank);
                var row = Rect(Node($"Tier{i + 1}", board), new Vector2(0, y - pitch / 2f), new Vector2(420, pitch));
                if (rank <= 3) Img("Medal", row, "lb_medal_" + rank, new Vector2(-170, 0), ByHeight("lb_medal_" + rank, pitch - 4f));
                else
                {
                    var t = Text("Places", row, rank > 100 ? "за 100 очков" : rw.Places, 22, new Vector2(-150, 1), new Vector2(116, pitch - 6f), Dark, TextAlignmentOptions.Center, false, autoSize: true);
                    t.enableWordWrapping = false;
                }
                float x = -78;
                Img("Coin", row, "icon_coin", new Vector2(x, 0), new Vector2(30, 30));
                var c = Text("Coins", row, rw.Coins.ToString(), 26, new Vector2(x + 52, 1), new Vector2(70, pitch - 6f), Dark, TextAlignmentOptions.Left, false, autoSize: true);
                c.enableWordWrapping = false;
                x += 128;
                if (rw.Packs > 0)
                {
                    Img("Pack", row, "icon_sticker_pack", new Vector2(x, 0), new Vector2(32, 32));
                    Text("Packs", row, "×" + rw.Packs, 24, new Vector2(x + 36, 1), new Vector2(42, pitch - 6f), Dark, TextAlignmentOptions.Left, false, autoSize: true).enableWordWrapping = false;
                    x += 82;
                }
                if (!string.IsNullOrEmpty(rw.Frame))
                {
                    // рамка поверх зверька — как она будет выглядеть в таблице
                    Img("Avatar", row, "av_bunny", new Vector2(x + 6, 0), new Vector2(pitch - 18f, pitch - 18f));
                    Img("Frame", row, rw.Frame, new Vector2(x + 6, 0), new Vector2(pitch - 6f, pitch - 6f));
                }
                Img("Gift", row, "lb_gift_" + rw.Gift, new Vector2(184, 0), new Vector2(pitch - 8f, pitch - 8f));
            }
        }

        // ================================================================ верхняя панель и меню

        static void BuildTopBar(RectTransform root, HubController hub)
        {
            // референс: монеты, звёзды и шестерёнка — справа сверху, «назад» — слева
            // счётчики компактные (просьба 21.09.2026): иконка заходит на плашку чуть больше половины,
            // цифра — по центру между иконкой и краем плашки; слева ничего не доходит до ленты района
            var pill = ByHeight("ui_tab_wood_flat2d_generated", 62f);
            var gear = ByHeight("btn_settings", 92f);
            hub.settingsButton = Button("Settings", root, "btn_settings", Vector2.zero, gear);
            Anchor((RectTransform)hub.settingsButton.transform, new Vector2(1f, 1f), new Vector2(-78, -70), gear);

            // раскладываем от правого края: шестерёнка → плашка звёзд → плашка монет (шире: цифры и «+»)
            float right = -78f - gear.x / 2f - 10f;
            var stars = Img("StarsPill", root, "ui_tab_wood_flat2d_generated", Vector2.zero, pill, raycast: true);
            hub.starsButton = stars.gameObject.AddComponent<Button>();   // звёзды → «Звёздный путь»
            stars.gameObject.AddComponent<UiButton>();
            Anchor(stars.rectTransform, new Vector2(1f, 1f), new Vector2(right - pill.x / 2f, -70), pill);
            var starIcon = ByHeight("ui_star_big", 70f);
            // на плашке чуть больше половины иконки: у доски скруглён левый край (~10% холста),
            // поэтому сдвигаем центр иконки за нарисованный край доски (просьба 21.09.2026)
            var starCx = -pill.x / 2f + starIcon.x * 0.30f;
            Img("StarIcon", stars.transform, "ui_star_big", new Vector2(starCx, 2), starIcon);
            // цифра — ровно в центре плашки и по горизонтали, и по вертикали (просьба 21.09.2026)
            hub.starsText = Text("Stars", stars.transform, "0", 34, Vector2.zero, new Vector2(110, pill.y), Dark, autoSize: true);
            right -= pill.x + starIcon.x * 0.34f + 10f;

            float cw = pill.x * 1.21f;   // плашка монет шире: цифры и кнопка «+»
            var coins = Img("CoinsPill", root, "ui_tab_wood_flat2d_generated", Vector2.zero, new Vector2(cw, pill.y));
            Anchor(coins.rectTransform, new Vector2(1f, 1f), new Vector2(right - cw / 2f, -70), new Vector2(cw, pill.y));
            var coinIcon = ByHeight("icon_coin", 64f);
            var coinCx = -cw / 2f + coinIcon.x * 0.42f;
            hub.coinsIcon = Img("CoinIcon", coins.transform, "icon_coin", new Vector2(coinCx, 2), coinIcon).rectTransform;
            var plus = ByHeight("btn_coins_plus", 58f);
            hub.coinsPlus = Button("Plus", coins.transform, "btn_coins_plus", new Vector2(cw / 2f - 26, 0), plus);
            hub.coinsText = Text("Coins", coins.transform, "0", 34, Vector2.zero, new Vector2(130, pill.y), Dark, autoSize: true);
            // алмазы (экономика v4, 03.10.2026) — под монетами: в ряду сверху они наезжали на ленту названия района
            float gw = cw;
            var gems = Img("GemsPill", root, "ui_tab_wood_flat2d_generated", Vector2.zero, new Vector2(gw, pill.y), raycast: true);
            Anchor(gems.rectTransform, new Vector2(1f, 1f), new Vector2(right - cw / 2f, -70 - pill.y - 12f), new Vector2(gw, pill.y));
            hub.gemsButton = gems.gameObject.AddComponent<Button>();
            gems.gameObject.AddComponent<UiButton>();
            var gemIcon = ByHeight("icon_gem_small", 60f);
            Img("GemIcon", gems.transform, "icon_gem_small", new Vector2(-gw / 2f + gemIcon.x * 0.42f, 2), gemIcon);
            Img("Plus", gems.transform, "btn_coins_plus", new Vector2(gw / 2f - 26, 0), plus);
            hub.gemsText = Text("Gems", gems.transform, "0", 34, new Vector2(-4, 0), new Vector2(gw * 0.46f, pill.y), Dark, autoSize: true);

            // серия побед «Лучший продавец» — плашка с огоньком под алмазами (только на карте)
            var ssz = ByHeight("ui_streak_plate", pill.y * 1.15f);
            var streak = Img("Streak", root, "ui_streak_plate", Vector2.zero, ssz, raycast: true);
            Anchor(streak.rectTransform, new Vector2(1f, 1f), new Vector2(right - cw / 2f, -70 - 2f * (pill.y + 12f) - 4f), ssz);
            hub.streakButton = streak.gameObject.AddComponent<Button>();
            streak.gameObject.AddComponent<UiButton>();
            // полоса под число — правые 2/3 плашки
            hub.streakText = Text("Wins", streak.transform, "×0", 32, new Vector2(ssz.x * 0.14f, 0), new Vector2(ssz.x * 0.52f, ssz.y * 0.6f), Dark,
                                  TextAlignmentOptions.Center, false, autoSize: true);

            // профиль игрока — в самом левом верхнем углу (просьба 23.09.2026)
            var frame = ByHeight("ui_btn_round_cream", 132f);
            hub.profileButton = Button("Profile", root, "ui_btn_round_cream", Vector2.zero, frame);
            Anchor((RectTransform)hub.profileButton.transform, new Vector2(0f, 1f), new Vector2(96, -84), frame);
            hub.profileHalo = Img("Halo", hub.profileButton.transform, "halo_leaf", Vector2.zero, new Vector2(frame.x * 1.24f, frame.x * 1.24f));
            hub.profileHalo.gameObject.SetActive(false);
            hub.profileAvatar = Img("Avatar", hub.profileButton.transform, "map_avatar", new Vector2(0, 4), new Vector2(84, 84));
            var lvlPill = ByHeight("ui_pill_counter", 42f);
            var lvl = Img("LevelPill", hub.profileButton.transform, "ui_pill_counter", new Vector2(0, -frame.y * 0.44f), lvlPill);
            hub.profileLevel = Text("Level", lvl.transform, "1", 26, new Vector2(0, 1), new Vector2(lvlPill.x - 24, 34), Dark,
                                    TextAlignmentOptions.Center, false, autoSize: true);

            var back = ByHeight("btn_back", 104f);
            hub.backButton = Button("Back", root, "btn_back", Vector2.zero, back);
            Anchor((RectTransform)hub.backButton.transform, new Vector2(0f, 1f), new Vector2(96 + frame.x / 2f + 20f + back.x / 2f, -78), back);
        }

        /// <summary>Экран «Задания» по картинке quests_empty (1672×944, в книге 1920×1080).</summary>
        static QuestsScreen BuildQuests(RectTransform parent)
        {
            var s = Screen<QuestsScreen>(parent, "QuestsScreen", null);
            var rt = (RectTransform)s.transform;
            var book = PageBook(rt, "quests_empty", new Color(0.55f, 0.36f, 0.22f));
            PageImage(book, "Page", "quests_empty");
            const float kx = 1920f / 1672f, ky = 1080f / 944f;
            Vector2 Q(float x, float y) => P(x * kx, y * ky);
            Vector2 Sz(float w, float h) => new Vector2(w * kx, h * ky);
            var title = Text("Title", book, "Задания", 56, Q(835, 98), Sz(420, 80), Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            title.enableWordWrapping = false;
            // три листочка: значок в кружке, текст, полоса, награда, «Забрать»
            float[] ys = { 288, 480, 675 };
            for (int i = 0; i < 3; i++)
            {
                float y = ys[i];
                var ring = Img("Ring" + (i + 1), book, "ui_btn_round_cream", Q(250, y + 4), Sz(104, 104));
                s.noteIcons[i] = Img("Icon", ring.transform, "icon_q_levels", Vector2.zero, Sz(80, 80));
                s.noteTexts[i] = Text("Text" + (i + 1), book, "Пройди 3 уровня", 30, Q(415, y - 28), Sz(200, 62), Dark,
                                      TextAlignmentOptions.Left, false, autoSize: true);
                s.noteTexts[i].fontSizeMin = 18;
                s.noteFills[i] = Bar("Bar" + (i + 1), book, Q(412, y + 32), 200f * kx, 30f * ky, new Color32(0x58, 0xA6, 0x5C, 255), out var track);
                s.noteProgress[i] = Text("Progress", track.transform, "0 / 3", 22, Vector2.zero, Sz(200, 28), Color.white,
                                         TextAlignmentOptions.Center, true, autoSize: true);
                Img("Coin" + (i + 1), book, "icon_coin", Q(560, y - 26), Sz(46, 46));
                Text("Coins" + (i + 1), book, Quests.CoinsEach.ToString(), 28, Q(600, y - 26), Sz(50, 40), Dark, TextAlignmentOptions.Left, false, autoSize: true);
                Img("Token" + (i + 1), book, "quest_token", Q(560, y + 26), Sz(42, 42));
                Text("Points" + (i + 1), book, Quests.PointsEach.ToString(), 28, Q(600, y + 26), Sz(50, 40), Dark, TextAlignmentOptions.Left, false, autoSize: true);
                s.noteClaim[i] = Button("Claim" + (i + 1), book, "ui_btn_green", Q(678, y), ByHeight("ui_btn_green", 58f * ky), "Забрать", 26);
                s.noteDone[i] = Img("Done" + (i + 1), book, "icon_check", Q(678, y), ByHeight("icon_check", 60f)).gameObject;
            }
            s.dayText = Text("DayLeft", book, "Новые задания через 5 ч", 26, Q(458, 785), Sz(520, 36), Color.white,
                             TextAlignmentOptions.Center, true, autoSize: true);
            // неделя: 5 сундуков на доске, полоса с листиками под ними
            float[] wx = { 848, 965, 1093, 1230, 1395 };
            float[] wsz = { 92, 98, 108, 118, 140 };
            s.weekText = Text("WeekText", book, "Неделя: 0 / 175", 28, Q(1140, 232), Sz(660, 40), Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            s.weekX0 = 815f; s.weekX1 = 1440f;
            s.weekFill = Bar("WeekBar", book, Q((s.weekX0 + s.weekX1) / 2f, 425), (s.weekX1 - s.weekX0) * kx, 26f * ky, new Color32(0x6C, 0xC2, 0x5A, 255), out _);
            for (int i = 0; i < 5; i++)
            {
                s.weekX[i] = wx[i];
                var chest = Button("Chest" + (i + 1), book, Quests.WeekChests[i], Q(wx[i], 352 - wsz[i] * 0.1f), ByHeight(Quests.WeekChests[i], wsz[i] * ky));
                s.weekChests[i] = chest;
                s.weekChecks[i] = Img("Check", chest.transform, "icon_check", new Vector2(0, -6), ByHeight("icon_check", 48f)).gameObject;
                s.weekDots[i] = Img("Dot", chest.transform, "icon_new", new Vector2(wsz[i] * 0.42f, wsz[i] * 0.36f), new Vector2(36, 36)).gameObject;
                Img("Token" + (i + 1), book, "quest_token", Q(wx[i], 425), Sz(40, 40));
                Text("Pts" + (i + 1), book, Quests.WeekPoints[i].ToString(), 22, Q(wx[i], 457), Sz(60, 28), Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            }
            // месяц: лента с 4 наградами и большим призом
            float[] mx = { 865, 1003, 1140, 1277, 1420 };
            s.monthText = Text("MonthText", book, "Месяц: 0 / 700", 28, Q(1150, 540), Sz(560, 40), Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            s.monthX0 = 830f; s.monthX1 = 1420f;
            s.monthFill = Bar("MonthBar", book, Q((s.monthX0 + s.monthX1) / 2f, 696), (s.monthX1 - s.monthX0) * kx, 20f * ky, new Color32(0xF2, 0xB1, 0x2E, 255), out _);
            for (int i = 0; i < 5; i++)
            {
                s.monthX[i] = mx[i];
                bool prize = i == 4;
                var r = Quests.MonthRewards[i];
                Button b;
                if (prize)
                {
                    b = Button("Prize", book, "chest_month", Q(mx[i], 618), ByHeight("chest_month", 150f * ky));
                    s.monthIcons[i] = b.image;
                }
                else
                {
                    b = Button("Reward" + (i + 1), book, "ui_btn_round_cream", Q(mx[i], 632), Sz(96, 96));
                    s.monthIcons[i] = Img("Icon", b.transform, r.Icon, new Vector2(0, 6), Sz(64, 64));
                    Text("Caption", b.transform, r.Caption, 22, new Vector2(0, -32 * ky), Sz(90, 26), Color.white, TextAlignmentOptions.Center, true, autoSize: true);
                }
                s.monthButtons[i] = b;
                s.monthChecks[i] = Img("Check", b.transform, "icon_check", new Vector2(0, -4), ByHeight("icon_check", 44f)).gameObject;
                s.monthDots[i] = Img("Dot", b.transform, "icon_new", new Vector2(36 * kx, 36 * ky), new Vector2(34, 34)).gameObject;
            }
            return s;
        }

        /// <summary>Окно серии побед по картинке streak_empty (1540×956): пять ступеней, значки бонусов над ними.</summary>
        static StreakPopup BuildStreakPopup(RectTransform root)
        {
            const float W = 1300f;
            float k = W / 1540f;
            Vector2 Px(float x, float y) => new Vector2((x - 770f) * k, (478f - y) * k);
            var r = Stretch(Node("Streak", root));
            Group(r.gameObject);
            Dim(r);
            var panel = Img("Panel", r, "streak_empty", new Vector2(0, -10), ByWidth("streak_empty", W), raycast: true).rectTransform;
            var p = AddPopup<StreakPopup>(r, panel);
            // заголовок — на зелёной ленте
            p.title = Text("Title", panel, "Лучший продавец", 50, Px(745, 138), new Vector2(720 * k, 84 * k), Color.white,
                           TextAlignmentOptions.Center, true, autoSize: true);
            p.title.enableWordWrapping = false;
            float[] xs = { 290, 535, 770, 1018, 1265 };
            for (int i = 0; i < 5; i++)
            {
                p.stepsOn[i] = Img("StepOn" + (i + 1), panel, "ui_streak_step_on", Px(xs[i], 512), ByHeight("ui_streak_step_on", 170 * k));
                p.stepNums[i] = Text("Num" + (i + 1), panel, (i + 1).ToString(), 44, Px(xs[i], 522), new Vector2(90 * k, 70 * k), Color.white,
                                     TextAlignmentOptions.Center, true, autoSize: true);
                p.icons[i] = Img("Icon" + (i + 1), panel, "icon_hint", Px(xs[i], 372), new Vector2(150 * k, 150 * k));
            }
            p.info = Text("Info", panel, "", 32, Px(777, 720), new Vector2(1100 * k, 120 * k), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            p.info.fontSizeMin = 18;
            p.okButton = Button("Ok", panel, "ui_btn_green", Px(770, 892), ByHeight("ui_btn_green", 100 * k), "Понятно", 42);
            return p;
        }

        /// <summary>Выбор сложности (v4, 03.10.2026): три карточки ui_diff_card на ui_panel_info; баннер «Новая сложность».</summary>
        static DiffPopup BuildDiffPopup(RectTransform root)
        {
            var (r, panel) = PopupShell("Diff", root, new Vector2(1240, 0), "Сложность", out var title, "ui_panel_info");
            var p = AddPopup<DiffPopup>(r, panel);
            p.title = title;
            var sz = panel.sizeDelta;
            // поле бумаги при ширине 1240: y от +245 до −332 — карточки стоят на нём в ряд
            var card = ByHeight("ui_diff_card", 470f);
            for (int m = 0; m < 3; m++)
            {
                var c = Button("Card" + (m + 1), panel, "ui_diff_card", new Vector2((m - 1) * 335f, -48f), card);
                p.cards[m] = c;
                p.cardImages[m] = c.GetComponent<Image>();
                float w = card.x, h = card.y;
                // по картинке: круглое гнездо сверху (13 % высоты), кремовое поле посередине, лента снизу (85 %)
                p.icons[m] = Img("Icon", c.transform, GameApp.ModeIcons[m], new Vector2(0, h * 0.365f), ByHeight(GameApp.ModeIcons[m], h * 0.14f));
                p.locks[m] = Img("Lock", c.transform, "icon_lock", new Vector2(0, h * 0.365f), ByHeight("icon_lock", h * 0.12f)).gameObject;
                p.names[m] = Text("Name", c.transform, GameApp.ModeNames[m], 40, new Vector2(0, h * 0.17f), new Vector2(w * 0.66f, h * 0.09f), Dark,
                                  TextAlignmentOptions.Center, true, autoSize: true);
                p.infos[m] = Text("Info", c.transform, "", 27, new Vector2(0, -h * 0.04f), new Vector2(w * 0.68f, h * 0.30f), Dark,
                                  TextAlignmentOptions.Center, false, autoSize: true);
                p.infos[m].fontSizeMin = 16;
                p.ribbons[m] = Text("Ribbon", c.transform, "Играть", 32, new Vector2(0, -h * 0.338f), new Vector2(w * 0.56f, h * 0.075f), Dark,
                                    TextAlignmentOptions.Center, true, autoSize: true);
                p.checks[m] = Img("Check", c.transform, "icon_check", new Vector2(w * 0.36f, h * 0.43f), new Vector2(64, 64)).gameObject;
            }
            // «Новая сложность открыта!» — праздничный баннер поверх доски с заголовком (вне панели: лежит на её кромке)
            var bsz = ByWidth("diff_unlock_banner", 760f);
            var banner = Img("Banner", r, "diff_unlock_banner", new Vector2(0, sz.y / 2f - 16f), bsz);
            p.banner = banner.gameObject;
            p.bannerText = Text("Text", banner.transform, "Открыт новый режим!", 40, new Vector2(0, -bsz.y * 0.02f), new Vector2(bsz.x * 0.62f, bsz.y * 0.34f), Dark,
                                TextAlignmentOptions.Center, false, autoSize: true);
            p.bannerText.enableWordWrapping = false;
            p.closeButton = Button("Close", panel, "ui_btn_round", new Vector2(sz.x * 0.43f, sz.y * 0.34f), ByHeight("ui_btn_round", 84f), icon: "icon_close");
            return p;
        }

        /// <summary>
        /// Окно «Алмазы» (v4, 03.10.2026) — «книгой» на странице shop_page_6_gems: сетка 3×2 карточек с кружком под бирку
        /// и нарисованной зелёной кнопкой. Обе вкладки («Алмазы» и «Для дома») — на этой странице: shop_page_7_house
        /// пришла со старой доской вкладок магазина («Монеты» горит) — не берём. Вкладки — круглые значки слева.
        /// Места посчитаны по картинке 1672×944 (Q — пиксели картинки).
        /// </summary>
        static GemShopPopup BuildGemShop(RectTransform root)
        {
            var r = Stretch(Node("GemShop", root));
            Group(r.gameObject);
            Dim(r);
            var pages = PageBook(r, "shop_page_6_gems", new Color(0.45f, 0.32f, 0.2f));
            // окно «выпрыгивает» панелью, а у книги свой масштаб под экран — панель внутри книги
            var book = Stretch(Node("Panel", pages));
            PageImage(book, "Page", "shop_page_6_gems");
            const float kx = 1920f / 1672f, ky = 1080f / 944f;
            Vector2 Q(float x, float y) => P(x * kx, y * ky);
            var p = AddPopup<GemShopPopup>(r, book);

            // вкладки — слева, на витрине: значок и подпись на ленточке
            string[] tabNames = { "Алмазы", "Для дома" };
            string[] tabIcons = { "shop_tab_gems", "shop_tab_house" };
            for (int i = 0; i < 2; i++)
            {
                var pos = Q(215, 300 + i * 270);
                var t = Button("Tab" + (i + 1), book, tabIcons[i], pos, ByHeight(tabIcons[i], 150f));
                p.tabs[i] = t;
                p.tabBgs[i] = t.GetComponent<Image>();
                var cap = Img("Caption", t.transform, "ui_tab_caption", new Vector2(0, -92), ByWidth("ui_tab_caption", 210f));
                Text("Text", cap.transform, tabNames[i], 30, new Vector2(0, 2), new Vector2(150, 46), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            }
            // баланс алмазов — справа над доской
            var pill = ByHeight("ui_pill_counter", 76f);
            var bal = Img("Balance", book, "ui_pill_counter", Q(1450, 95), pill);
            Img("Icon", bal.transform, "icon_gem_small", new Vector2(-pill.x / 2f + 30, 2), ByHeight("icon_gem_small", 62f));
            p.gemsText = Text("Gems", bal.transform, "0", 36, new Vector2(18, 2), new Vector2(pill.x - 90, 56), Dark, TextAlignmentOptions.Center, false, autoSize: true);

            float[] cx = { 548, 840, 1128 };
            float[] cy = { 265, 625 }, by = { 406, 764 };
            for (int i = 0; i < 6; i++)
                p.cards[i] = BuildGemCard(book, i, Q(cx[i % 3], cy[i / 3]), Q(cx[i % 3], by[i / 3]), Q(cx[i % 3] + 76, cy[i / 3] - 82), kx);
            p.closeButton = Button("Close", book, "ui_btn_round", Q(1290, 70), ByHeight("ui_btn_round", 96f), icon: "icon_close");
            return p;
        }

        /// <summary>Карточка «Алмазов» на странице: картинка и количество — на кремовом поле, цена — на нарисованной
        /// зелёной кнопке, бирка выгоды или «×N у тебя» — в кружке в правом верхнем углу.</summary>
        static GemShopCard BuildGemCard(RectTransform parent, int i, Vector2 cream, Vector2 button, Vector2 circle, float k)
        {
            var root = Rect(Node("Card" + (i + 1), parent), cream, new Vector2(234 * k, 187 * k));
            var c = root.gameObject.AddComponent<GemShopCard>();
            c.icon = Img("Icon", root, "gem_pack_1", new Vector2(-10, 18), new Vector2(150, 132));
            c.titleIcon = Img("Gem", root, "icon_gem_small", new Vector2(-52, -82), ByHeight("icon_gem_small", 40f));
            c.title = Text("Title", root, "30", 36, new Vector2(10, -82), new Vector2(150, 44), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            c.title.enableWordWrapping = false;
            // кружок в углу карточки: «+29 %» (алмазы) или «×2» (сколько ускорителей уже есть)
            var tag = Rect(Node("Badge", parent), circle, new Vector2(70, 70));
            c.badgeText = Text("Text", tag, "+10 %", 22, Vector2.zero, new Vector2(64, 40), new Color32(0x2E, 0x7D, 0x32, 255),
                               TextAlignmentOptions.Center, false, autoSize: true);
            c.badge = tag.gameObject;
            c.owned = Text("Owned", parent, "×1", 30, circle, new Vector2(64, 44), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            c.owned.name = "Owned" + (i + 1);
            tag.name = "Badge" + (i + 1);
            c.buy = HitButton("Buy" + (i + 1), parent, button, new Vector2(217 * k, 52 * k));
            c.price = Text("Label", c.buy.transform, "29 ₽", 36, new Vector2(0, 3), new Vector2(200 * k, 44), Color.white,
                           TextAlignmentOptions.Center, true, autoSize: true);
            c.priceIcon = Img("Gem", c.buy.transform, "icon_gem_small", new Vector2(-62, 3), ByHeight("icon_gem_small", 40f));
            return c;
        }

        /// <summary>Окно профиля: аватар со скином, имя, счётчики и обычные настройки звука.</summary>
        static ProfilePopup BuildProfile(RectTransform root)
        {
            var (r, panel) = PopupShell("Profile", root, new Vector2(1000, 0), "Профиль", out _, "ui_panel_paper");
            var p = AddPopup<ProfilePopup>(r, panel);
            var ps = panel.sizeDelta;
            p.closeButton = Button("Close", panel, "ui_btn_round", new Vector2(ps.x * 0.455f, ps.y * 0.43f), ByHeight("ui_btn_round", 88f), icon: "icon_close");

            // слева — сам игрок: аватар, свой ник и гардероб
            var card = ByHeight("ui_btn_round_cream", 220f);
            var av = Img("AvatarFrame", panel, "ui_btn_round_cream", new Vector2(-262, 104), card);
            p.halo = Img("Halo", av.transform, "halo_leaf", Vector2.zero, new Vector2(card.x * 1.22f, card.x * 1.22f));
            p.halo.gameObject.SetActive(false);
            p.avatar = Img("Avatar", av.transform, "map_avatar", new Vector2(0, 6), new Vector2(144, 144));
            p.levelText = Text("Level", panel, "Уровень 1", 26, new Vector2(-258, -22), new Vector2(316, 38),
                               new Color32(0x8A, 0x6B, 0x4F, 255));
            // ник игрок задаёт сам (просьба 26.09.2026)
            p.nameInput = BuildInput("Name", panel, new Vector2(-252, -76), new Vector2(330, 76), "Твой ник");
            p.wardrobeButton = Button("Wardrobe", panel, "ui_btn_secondary", new Vector2(-252, -158),
                                      ByHeight("ui_btn_secondary", 76f), "Гардероб", 30, null, Dark, false);

            // справа — счётчики плитками: число крупно, подпись мелко, ничего не вылезает
            string[] icons = { "icon_trophy", "icon_star", "icon_sticker_pack", "icon_coin" };
            string[] names = { "Пройдено", "Звёзд", "Наклеек", "Монет" };
            p.statValues = new TextMeshProUGUI[4];
            var tile = new Vector2(220, 132);
            for (int i = 0; i < 4; i++)
            {
                var pos = new Vector2(70 + (i % 2) * 240, 150 - (i / 2) * 152);
                var bg = Img("Stat" + i, panel, "ui_tile_button", pos, tile, sliced: true, preserve: false);
                Img("Icon", bg.transform, icons[i], new Vector2(-tile.x / 2f + 42, 22), ByHeight(icons[i], 48f));
                p.statValues[i] = Text("Value", bg.transform, "0", 40, new Vector2(24, 22), new Vector2(tile.x - 92, 52),
                                       Dark, TextAlignmentOptions.Center, autoSize: true);
                p.statValues[i].fontSizeMin = 20;
                Text("Name", bg.transform, names[i], 24, new Vector2(0, -36), new Vector2(tile.x - 24, 32),
                     new Color32(0x8A, 0x6B, 0x4F, 255), TextAlignmentOptions.Center, autoSize: true);
            }
            p.versionText = Text("Version", panel, "", 22, new Vector2(190, -158), new Vector2(320, 32),
                                 new Color32(0x9A, 0x86, 0x76, 255));
            return p;
        }

        static void BuildNav(RectTransform root, HubController hub)
        {
            // меню — по центру внизу: у левого края его закрывал енот обучения (просьба 21.09.2026)
            var bar = Anchor(Node("NavBar", root), new Vector2(0.5f, 0f), new Vector2(0, 105), new Vector2(1220, 150));
            hub.navBar = bar.gameObject;
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 26;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            // готовые кнопки: значок нарисован внутри кружка, подписи не нужны
            // кнопки «Карта» нет: карта и есть главный экран игры, меню живёт прямо на ней
            // «Дом» (Торговый дом, 03.10.2026) — сразу за ремонтом: оба про стройку
            // «Задания» (v4, 03.10.2026): готовой круглой кнопки нет — кремовый кружок со значком доски
            var icons = new[] { "btn_reno", "btn_house", "btn_album", "btn_daily", "ui_btn_round_cream", "btn_leaders", "btn_shop" };
            var names = new[] { "Ремонт", "Дом", "Альбом", "Доставка", "Задания", "Рейтинг", "Магазин" };
            var buttons = new Button[icons.Length];
            for (int i = 0; i < icons.Length; i++)
            {
                var size = ByHeight(icons[i], 124f);
                buttons[i] = Button("Nav_" + names[i], bar, icons[i], new Vector2((i - 3f) * 160, 10), size,
                                    icon: icons[i] == "ui_btn_round_cream" ? "icon_tasks" : null);
            }
            hub.navRenovation = buttons[0]; hub.navHouse = buttons[1]; hub.navAlbum = buttons[2];
            hub.navDaily = buttons[3]; hub.navQuests = buttons[4]; hub.navLeaders = buttons[5]; hub.navShop = buttons[6];
            hub.questsDot = Img("Dot", buttons[4].transform, "icon_new", new Vector2(46, 46), new Vector2(40, 40)).gameObject;
            hub.renovationDot = Img("Dot", buttons[0].transform, "icon_new", new Vector2(46, 46), new Vector2(40, 40)).gameObject;
            hub.houseDot = Img("Dot", buttons[1].transform, "icon_new", new Vector2(46, 46), new Vector2(40, 40)).gameObject;
            hub.albumDot = Img("Dot", buttons[2].transform, "icon_new", new Vector2(46, 46), new Vector2(40, 40)).gameObject;
            hub.dailyDot = Img("Dot", buttons[3].transform, "icon_new", new Vector2(46, 46), new Vector2(40, 40)).gameObject;
        }

        // ================================================================ окна хаба

        static SettingsPopup BuildSettings(RectTransform root)
        {
            // кремовое поле панели — от +0.244 до -0.338 её высоты, всё держим внутри
            var (r, panel) = PopupShell("Settings", root, new Vector2(760, 0), "Настройки", out _);
            var p = AddPopup<SettingsPopup>(r, panel);
            // строка «звук»: кнопка-значок (вкл/выкл) + ползунок громкости; под ней то же для музыки
            p.soundButton = Button("Sound", panel, "btn_sound_on", new Vector2(-218, 96), ByHeight("btn_sound_on", 104f));
            p.soundIcon = p.soundButton.image;
            p.soundSlider = BuildSlider("SoundVolume", panel, new Vector2(58, 96), new Vector2(330, 76));
            p.musicButton = Button("Music", panel, "btn_music_on", new Vector2(-218, -24), ByHeight("btn_music_on", 104f));
            p.musicIcon = p.musicButton.image;
            p.musicSlider = BuildSlider("MusicVolume", panel, new Vector2(58, -24), new Vector2(330, 76));
            // кнопки «восстановить покупки» нет: на Яндексе покупки возвращаются сами при входе
            p.closeButton = Button("Close", panel, "ui_btn_primary", new Vector2(0, -140), ByHeight("ui_btn_primary", 104f), "Готово", 42);
            p.versionText = Text("Version", panel, "", 18, new Vector2(150, -206), new Vector2(170, 30), Dark,
                                 TextAlignmentOptions.Right);
            return p;
        }

        /// <summary>Ползунок из готовых картинок: гладкий трек ui_tile_button, зелёная заливка
        /// ui_progress_fill (растянута якорями — размером управляет сам Slider), ручка — круглая кнопка.</summary>
        /// <summary>Поле ввода на светлой плитке (ник игрока).</summary>
        static TMP_InputField BuildInput(string name, RectTransform parent, Vector2 pos, Vector2 size, string hint)
        {
            var bg = Img(name, parent, "ui_tile_button", pos, size, raycast: true, sliced: true, preserve: false);
            var field = bg.gameObject.AddComponent<TMP_InputField>();
            var inner = new Vector2(size.x - 40f, size.y - 18f);
            var area = Rect(Node("Area", bg.transform), Vector2.zero, inner);
            area.gameObject.AddComponent<RectMask2D>();
            var text = Text("Text", area, "", 32, Vector2.zero, inner, Dark);
            var hintText = Text("Hint", area, hint, 32, Vector2.zero, inner, new Color32(0xB0, 0x9C, 0x8A, 255));
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = hintText;
            field.targetGraphic = bg;
            field.characterLimit = 16;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.restoreOriginalTextOnEscape = true;
            return field;
        }

        static Slider BuildSlider(string name, RectTransform parent, Vector2 pos, Vector2 size)
        {
            var rt = Rect(Node(name, parent), pos, size);
            var slider = rt.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.transition = Selectable.Transition.ColorTint;

            var track = Img("Track", rt, "ui_bar_track", Vector2.zero, size, sliced: true, preserve: false);
            track.raycastTarget = false;

            // заливка обязана быть растянутой (sizeDelta = 0): Slider двигает её правый якорь,
            // фиксированный размер вылезал бы за трек (баг 21.09.2026)
            var fillArea = Stretch(Node("FillArea", rt));
            fillArea.offsetMin = new Vector2(11, 11);
            fillArea.offsetMax = new Vector2(-11, -11);
            var fill = Img("Fill", fillArea, "ui_bar_fill", Vector2.zero, fillArea.rect.size, sliced: true, preserve: false);
            fill.raycastTarget = false;
            fill.color = new Color32(0x58, 0xA6, 0x5C, 255);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            slider.fillRect = frt;

            var handleArea = Stretch(Node("HandleArea", rt));
            handleArea.offsetMin = new Vector2(0, -2);
            handleArea.offsetMax = new Vector2(0, 2);
            var handle = Img("Handle", handleArea, "ui_bar_knob", Vector2.zero, new Vector2(62, 62), raycast: true);
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        static PackPopup BuildPack(RectTransform root)
        {
            // широкое окно наград (03.10.2026): было тесно — картинки мелкие, подписи наезжали друг на друга
            var (r, panel) = PopupShell("Pack", root, new Vector2(1240, 0), "Новые наклейки!", out _, "ui_panel_reward");
            var p = AddPopup<PackPopup>(r, panel);
            p.cards = new StickerCell[3];
            p.newBadges = new GameObject[3];
            p.slots = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector2(-320 + i * 320, 40);
                p.slots[i] = Img("Slot" + (i + 1), panel, "ui_reward_slot", pos + new Vector2(0, 16), ByWidth("ui_reward_slot", 270f)).gameObject;
                p.cards[i] = BuildStickerCell(panel, $"Sticker{i + 1}", pos, new Vector2(290, 300));
                p.cards[i].label.fontSize = 30;
                p.newBadges[i] = Img("New", p.cards[i].transform, "icon_new", new Vector2(112, 118), new Vector2(60, 60)).gameObject;
            }
            p.packImage = Img("PackImage", panel, "album_pack_closed", new Vector2(0, 30), ByHeight("album_pack_closed", 300f));
            p.info = Text("Info", panel, "", 32, new Vector2(0, -160), new Vector2(860, 50), Dark, autoSize: true);
            p.okButton = Button("Ok", panel, "ui_btn_primary", new Vector2(0, -232), ByHeight("ui_btn_primary", 92f), "Отлично!", 42);
            p.okText = Label(p.okButton);
            return p;
        }

        static OfferPopup BuildOffer(RectTransform root)
        {
            var (r, panel) = PopupShell("Offer", root, new Vector2(1100, 0), "Специальное предложение", out var title, "ui_panel_shop");
            var p = AddPopup<OfferPopup>(r, panel);
            title.fontSize = 40;
            p.title = Text("Name", panel, "Стартовый набор", 42, new Vector2(0, 56), new Vector2(680, 64), Dark, autoSize: true);
            p.icon = Img("Icon", panel, "shop_starter", new Vector2(0, -40), ByHeight("shop_starter", 150f));
            // описание в две строки уменьшается, чтобы не наезжать на таймер (01.10.2026)
            p.description = Text("Desc", panel, "", 30, new Vector2(0, -146), new Vector2(680, 66), Dark, autoSize: true);
            p.timer = Text("Timer", panel, "", 30, new Vector2(0, -200), new Vector2(600, 38), new Color32(0xD9, 0x3B, 0x3B, 255));
            p.buyButton = Button("Buy", panel, "ui_btn_green", new Vector2(0, -262), ByHeight("ui_btn_green", 90f), "149 ₽", 42);
            p.priceText = Label(p.buyButton);
            p.closeButton = Button("Close", panel, "ui_btn_round", new Vector2(500, 356), ByHeight("ui_btn_round", 88f), icon: "icon_close");
            return p;
        }

        /// <summary>«Звёздный путь»: лента наград за звёзды листается вбок внутри кремового поля.</summary>
        static StarTrackPopup BuildStarTrack(RectTransform root)
        {
            // v4 (03.10.2026): вторая строка — Золотой путь; окно шире и выше, слева — значки строк
            var (r, panel) = PopupShell("StarTrack", root, new Vector2(1100, 0), "Звёздный путь", out _);
            var p = AddPopup<StarTrackPopup>(r, panel);
            var ps = panel.sizeDelta;   // поле: y от +0.244 до −0.338 высоты (≈ +234 … −325), x ±0.39 ширины (±429)
            p.closeButton = Button("Close", panel, "ui_btn_round", new Vector2(ps.x * 0.455f, ps.y * 0.43f), ByHeight("ui_btn_round", 88f), icon: "icon_close");
            p.info = Text("Info", panel, "", 30, new Vector2(-96, 196), new Vector2(640, 72), Dark, autoSize: true);
            // «Гардероб» — рядом с описанием: надеть полученные ореолы и скины
            p.wardrobeButton = Button("Wardrobe", panel, "ui_btn_secondary", new Vector2(330, 196), ByHeight("ui_btn_secondary", 58f), "Гардероб", 24, null, Dark, false);

            // слева: значки строк — звезда (бесплатно) и карточка Золотого пути (нажал — купить)
            var rows = Rect(Node("Rows", panel), new Vector2(-366, -88), new Vector2(124, 470));
            Img("FreeIcon", rows, "ui_star_big", new Vector2(0, 72), ByHeight("ui_star_big", 84f));
            Text("FreeText", rows, "Бесплатно", 24, new Vector2(0, 16), new Vector2(124, 30), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            p.goldButton = Button("Gold", rows, "gold_path_card", new Vector2(0, -112), ByWidth("gold_path_card", 122f));
            p.goldOwned = Img("Owned", p.goldButton.transform, "icon_check", new Vector2(40, -36), ByHeight("icon_check", 40f)).gameObject;
            var tag = Img("Price", rows, "ui_pill_counter", new Vector2(0, -186), ByHeight("ui_pill_counter", 40f));
            p.goldPrice = Text("Text", tag.transform, "299 ₽", 26, new Vector2(0, 1), new Vector2(tag.rectTransform.sizeDelta.x - 20, 34), Dark,
                               TextAlignmentOptions.Center, false, autoSize: true);

            // окно ленты (маска)
            var view = Rect(Node("Viewport", panel), new Vector2(66, -88), new Vector2(720, 470));
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);          // ловит перетаскивание ленты
            var track = MetaCatalog.StarTrack;
            const float step = 180f, first = 100f;
            var content = Node("Content", view);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = new Vector2(first * 2f + step * (track.Length - 1), 0f);
            content.anchoredPosition = Vector2.zero;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view;
            sr.content = content;
            sr.horizontal = true;
            sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 40f;
            p.scroll = sr;
            p.content = content;

            // полоса прогресса через всю ленту (рамка и заливка — гладкие, их можно тянуть)
            float lineY = 166f;
            var frame = Img("TrackFrame", content, "ui_bar_track", Vector2.zero, new Vector2(content.sizeDelta.x - 40f, 64f), sliced: true);
            Anchor(frame.rectTransform, new Vector2(0f, 0.5f), new Vector2(content.sizeDelta.x / 2f, lineY), new Vector2(content.sizeDelta.x - 40f, 64f));
            var fill = Img("TrackFill", content, "ui_bar_fill", Vector2.zero, new Vector2(10f, 44f), sliced: true,
                           color: new Color32(0xF2, 0xB1, 0x2E, 255));
            var frt = fill.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0f, 0.5f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.anchoredPosition = new Vector2(26f, lineY);
            frt.sizeDelta = new Vector2(10f, 44f);
            p.trackFill = frt;

            // полки под рядами наград (картинки v4): деревянная — бесплатный, золотая — Золотой путь. Полка целиком,
            // в родных пропорциях, встык одна за другой — кружки наград стоят на доске
            foreach (var (sprite, cardBottom, nm) in new[] { ("ui_track_row_free", 66f - 61f, "ShelfFree"), ("ui_track_row_gold", -116f - 56f, "ShelfGold") })
            {
                var shelf = ByHeight(sprite, 84f);
                var shelves = Node(nm, content);
                shelves.anchorMin = new Vector2(0f, 0.5f); shelves.anchorMax = new Vector2(0f, 0.5f);
                shelves.pivot = new Vector2(0f, 0.5f);
                // верх доски — на 9 % высоты картинки: на него ставим низ кружков
                shelves.anchoredPosition = new Vector2(0f, cardBottom + shelf.y * 0.09f - shelf.y / 2f + 6f);
                shelves.sizeDelta = new Vector2(content.sizeDelta.x, shelf.y);
                for (float x = 0f; x < content.sizeDelta.x; x += shelf.x - 2f)
                {
                    var im = Img("Plank", shelves, sprite, Vector2.zero, shelf);
                    var irt = im.rectTransform;
                    irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                    irt.anchoredPosition = new Vector2(x + shelf.x / 2f, 0f);
                    im.raycastTarget = false;
                }
            }

            p.nodes = new StarTrackNode[track.Length];
            for (int i = 0; i < track.Length; i++)
            {
                var node = Node($"Reward{i + 1:00}", content);
                node.anchorMin = node.anchorMax = new Vector2(0f, 0.5f);
                node.pivot = new Vector2(0.5f, 0.5f);
                node.anchoredPosition = new Vector2(first + i * step, 0f);
                node.sizeDelta = new Vector2(170f, 470f);
                var n = node.gameObject.AddComponent<StarTrackNode>();
                p.nodes[i] = n;
                // сколько звёзд нужно — звезда на полосе и число над ней
                Img("Star", node, "ui_star_big", new Vector2(0, lineY + 2), ByHeight("ui_star_big", 72f));
                n.starsText = Text("Stars", node, track[i].Stars.ToString(), 32, new Vector2(0, lineY + 50), new Vector2(150, 40), Dark, autoSize: true);
                // бесплатная награда: кружок, подпись внутри снизу, «Забрать» под ним
                n.card = Img("Card", node, "ui_btn_round_cream", new Vector2(0, 66), ByHeight("ui_btn_round_cream", 122f));
                n.icon = Img("Icon", n.card.transform, track[i].Icon, new Vector2(0, 10), new Vector2(80, 80));
                n.lockIcon = Img("Lock", n.card.transform, "icon_lock", new Vector2(40, -38), ByHeight("icon_lock", 40f)).gameObject;
                n.caption = Text("Caption", n.card.transform, track[i].Caption, 24, new Vector2(0, -38), new Vector2(112, 30), Color.white,
                                 TextAlignmentOptions.Center, true, autoSize: true);
                n.caption.fontSizeMin = 14f;
                n.caption.enableWordWrapping = false;
                n.claimButton = Button("Claim", node, "ui_btn_green", new Vector2(0, -24), ByHeight("ui_btn_green", 52f), "Забрать", 26);
                n.done = Img("Done", node, "icon_check", new Vector2(0, -24), ByHeight("icon_check", 44f)).gameObject;
                // Золотой путь: тот же кружок в золотой рамке
                var gold = MetaCatalog.GoldTrack[i];
                n.goldCard = Img("GoldCard", node, "ui_btn_round_cream", new Vector2(0, -116), ByHeight("ui_btn_round_cream", 112f));
                Img("Frame", n.goldCard.transform, "frame_goldcard", Vector2.zero, ByHeight("frame_goldcard", 132f));
                n.goldIcon = Img("Icon", n.goldCard.transform, gold.Icon, new Vector2(0, 8), new Vector2(72, 72));
                n.goldLock = Img("Lock", n.goldCard.transform, "ui_track_lock_gold", new Vector2(40, -34), ByHeight("ui_track_lock_gold", 46f)).gameObject;
                n.goldCaption = Text("Caption", n.goldCard.transform, gold.Caption, 22, new Vector2(0, -34), new Vector2(104, 28), Color.white,
                                     TextAlignmentOptions.Center, true, autoSize: true);
                n.goldCaption.fontSizeMin = 14f;
                n.goldCaption.enableWordWrapping = false;
                n.goldClaim = Button("GoldClaim", node, "ui_btn_primary", new Vector2(0, -204), ByHeight("ui_btn_primary", 50f), "Забрать", 26);
                n.goldDone = Img("GoldDone", node, "icon_check", new Vector2(0, -204), ByHeight("icon_check", 42f)).gameObject;
            }
            return p;
        }

        /// <summary>«Гардероб»: вкладки «Енот» и «Магазин», до четырёх рядов вещей; нажатие надевает, снимает или покупает.</summary>
        static WardrobePopup BuildWardrobe(RectTransform root)
        {
            var (r, panel) = PopupShell("Wardrobe", root, new Vector2(960, 0), "Гардероб", out _);
            var p = AddPopup<WardrobePopup>(r, panel);
            var ps = panel.sizeDelta;
            p.closeButton = Button("Close", panel, "ui_btn_round", new Vector2(ps.x * 0.455f, ps.y * 0.43f), ByHeight("ui_btn_round", 88f), icon: "icon_close");
            // вкладки (30.09.2026): рядов стало семь — енот отдельно, вид магазина в уровне отдельно
            // третья вкладка (01.10.2026) — аватарка и рамка в рейтинге; окно пользователь расширил до 1173
            string[] tabNames = { "Енот", "Магазин", "Аватар" };
            p.tabs = new Button[3];
            p.tabOn = new GameObject[3];
            for (int t = 0; t < 3; t++)
            {
                var tabSz = ByHeight("ui_btn_secondary", 50f);
                var tb = Button($"Tab{t + 1}", panel, "ui_btn_secondary", new Vector2(80 + t * 145, 233), tabSz, tabNames[t], 26, null, Dark, false);
                p.tabs[t] = tb;
                // выбранная вкладка — зелёная кнопка поверх (та же форма, другой цвет картинки)
                var on = Img("On", tb.transform, "ui_btn_green", Vector2.zero, ByHeight("ui_btn_green", 50f));
                on.transform.SetAsFirstSibling();
                p.tabOn[t] = on.gameObject;
            }
            // раскладку окна пользователь сделал сам (29.09.2026): вкладки — в строке первого заголовка справа, подсказка — под рядами
            p.info = Text("Info", panel, "", 20, new Vector2(0, -335), new Vector2(760, 26), Dark, autoSize: true);
            const int Rows = WardrobePopup.Rows;
            p.rowTitles = new TextMeshProUGUI[Rows];
            p.slots = new Button[Rows * WardrobePopup.PerRow];
            p.slotIcons = new Image[p.slots.Length];
            p.slotWorn = new GameObject[p.slots.Length];
            p.slotPrice = new GameObject[p.slots.Length];
            p.slotPriceText = new TextMeshProUGUI[p.slots.Length];
            for (int row = 0; row < Rows; row++)
            {
                float y = 96f - row * 104f;
                p.rowTitles[row] = Text($"Row{row + 1}", panel, "", 24, new Vector2(-250, y + 46f), new Vector2(230, 32f), Dark, TextAlignmentOptions.Left, false, autoSize: true);
                for (int k = 0; k < WardrobePopup.PerRow; k++)
                {
                    int i = row * WardrobePopup.PerRow + k;
                    var b = Button($"Slot{row + 1}_{k + 1}", panel, "ui_btn_round_cream", new Vector2(-330 + k * 132f, y), ByHeight("ui_btn_round_cream", 98f));
                    p.slots[i] = b;
                    // под ореолом показываем самого енота: иначе кольцо ни о чём не говорит (включает код — ряд «Ореол»)
                    Img("Base", b.transform, "map_avatar", Vector2.zero, new Vector2(50, 50));
                    p.slotIcons[i] = Img("Icon", b.transform, "icon_star", Vector2.zero, new Vector2(76, 76));
                    p.slotWorn[i] = Img("Worn", b.transform, "icon_check", new Vector2(30, -30), ByHeight("icon_check", 38f)).gameObject;
                    // цена непокупленной вещи — ценник снизу кружка с монеткой
                    var tagSz = ByHeight("ui_pill_counter", 30f);
                    var tag = Img("Price", b.transform, "ui_pill_counter", new Vector2(0, -33), tagSz);
                    Img("Coin", tag.transform, "icon_coin", new Vector2(-tagSz.x / 2f + 14f, 0), ByHeight("icon_coin", 24f));
                    p.slotPriceText[i] = Text("Text", tag.transform, "1000", 19, new Vector2(10, 1), new Vector2(tagSz.x - 36f, 26f), Dark, autoSize: true);
                    p.slotPrice[i] = tag.gameObject;
                }
            }
            return p;
        }

        static ConfirmPopup BuildConfirm(RectTransform root)
        {
            // кремовая плашка с зелёной лентой (03.10.2026): бордово-сизая ui_panel_dark убрана из игры по просьбе игрока
            var (r, panel) = PopupShell("Confirm", root, new Vector2(900, 0), "", out var title, "ui_panel_info");
            var p = AddPopup<ConfirmPopup>(r, panel);
            p.title = title;
            p.icon = Img("Icon", panel, "icon_info", new Vector2(0, 80), ByHeight("icon_info", 120f));
            p.text = Text("Text", panel, "", 34, new Vector2(0, -40), new Vector2(680, 110), Dark, autoSize: true);
            p.text.fontSizeMin = 20;
            p.okButton = Button("Ok", panel, "ui_btn_primary", new Vector2(p.icon ? 140 : 0, -170), ByHeight("ui_btn_primary", 90f), "Да", 42);
            p.okText = Label(p.okButton);
            p.cancelButton = Button("Cancel", panel, "ui_btn_secondary", new Vector2(-160, -170), ByHeight("ui_btn_secondary", 90f), "Нет", 40, null, Dark, false);
            return p;
        }
    }
}
