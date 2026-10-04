using AllOnShelves.Game;
using AllOnShelves.Hub;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static AllOnShelves.EditorTools.UiBuild;

namespace AllOnShelves.EditorTools
{
    /// <summary>
    /// Торговый дом (v3.1/v3.2, 03.10.2026): дом снаружи, окно этажа с ячейками комнат, комната во весь экран.
    /// Картинки дома и комнат — Art/Team (дом не обрезан: два кадра одного рисунка совпадают пиксель в пиксель),
    /// декор комнат — куски из Tools/ArtPipeline/room_build.py (RoomLegoData).
    /// </summary>
    public static partial class SceneBuilder
    {
        static Vector2 RoomFrame => new Vector2(RoomLegoData.FrameW, RoomLegoData.FrameH);
        // дом стоит на песчаной площадке фона team_bg (её середина — x≈+46, передний край — y≈−292),
        // крыша не уходит за верх экрана (просьба 03.10.2026; раньше масштаб 0.64 и дом висел над площадкой)
        const float HouseScale = 0.48f;
        static readonly Vector2 HousePos = new Vector2(55, 119);

        // ================================================================ дом снаружи

        static HouseScreen BuildHouse(RectTransform parent)
        {
            var s = Screen<HouseScreen>(parent, "HouseScreen", null);
            var rt = (RectTransform)s.transform;
            // площадь накрывает экран; дом и всё, что к нему привязано, — в макете 1920×1080
            var bg = Img("Square", rt, "team_bg", Vector2.zero, new Vector2(1920, 1080));
            bg.gameObject.AddComponent<ScaleToParent>().cover = true;
            var layer = Rect(Node("Layer", rt), Vector2.zero, new Vector2(1920, 1080));
            layer.gameObject.AddComponent<ScaleToParent>();

            var house = Rect(Node("House", layer), HousePos, new Vector2(1024, 1536));
            house.localScale = Vector3.one * HouseScale;
            s.house = house;
            var open = Img("Open", house, "house_open", Vector2.zero, new Vector2(1024, 1536));

            // полосы этажей: house_scaffold под маской своей полосы — закрытый этаж в лесах
            for (int f = 0; f < 4; f++)
            {
                var b = HouseScreen.FloorBand[f];
                var mask = Rect(Node("Scaffold" + (f + 1), house), new Vector2(0, 768f - (b.x + b.y) / 2f), new Vector2(1024, b.y - b.x));
                mask.gameObject.AddComponent<RectMask2D>();
                var sc = Img("Img", mask, "house_scaffold", new Vector2(0, (b.x + b.y) / 2f - 768f), new Vector2(1024, 1536));
                s.scaffold[f] = mask;
            }

            // окна комнат: замок / доски / стройка / «заселить»; под окном — уровень или таймер
            for (int i = 0; i < 12; i++)
            {
                var w = HouseScreen.Windows[i];
                var c = HouseScreen.HousePx((w.x + w.z) / 2f, (w.y + w.w) / 2f);
                var size = new Vector2(w.z - w.x, w.w - w.y);
                var img = Img("Window" + (i + 1), house, "house_window_boards", c, size);
                img.gameObject.SetActive(false);
                s.windows[i] = img;
                var t = Text("WindowText" + (i + 1), house, "", 46, c + new Vector2(0, -size.y / 2f - 6f), new Vector2(260, 60),
                             Color.white, TextAlignmentOptions.Center, true, autoSize: true);
                t.gameObject.SetActive(false);
                s.windowText[i] = t;
            }

            // закрытый этаж: цепь поперёк, замок и табличка «Уровень N»
            for (int f = 0; f < 4; f++)
            {
                var b = HouseScreen.FloorBand[f];
                float y0 = f == 3 ? 260f : b.x;      // у верхнего этажа полоса начинается с крыши — цепь ниже
                var c = HouseScreen.HousePx(512, (y0 + b.y) / 2f);
                var root = Rect(Node("Chain" + (f + 1), house), c, new Vector2(900, 260));
                Img("Chain", root, "house_chain", Vector2.zero, ByWidth("house_chain", 880));
                Img("Lock", root, "house_padlock", new Vector2(0, 30), ByHeight("house_padlock", 190f));
                var plate = Img("Plate", root, "ui_tile_button", new Vector2(0, -112), new Vector2(330, 84), sliced: true, preserve: false);
                s.chainText[f] = Text("Text", plate.transform, "Уровень 24", 44, new Vector2(0, 2), new Vector2(300, 70), Dark,
                                      TextAlignmentOptions.Center, false, autoSize: true);
                s.chains[f] = root;
            }

            // вывеска на крыше
            s.signText = Text("Sign", house, "Торговый дом", 60, HouseScreen.HousePx(512, 150), new Vector2(340, 90),
                              new Color32(0x6B, 0x3F, 0x22, 255), TextAlignmentOptions.Center, false, autoSize: true);

            // нажатие на этаж (поверх всего дома)
            for (int f = 0; f < 4; f++)
            {
                var b = HouseScreen.FloorBand[f];
                float y0 = f == 3 ? 245f : b.x, y1 = f == 0 ? 1430f : b.y;
                s.floorButtons[f] = HitButton("Floor" + (f + 1), house, HouseScreen.HousePx(512, (y0 + y1) / 2f), new Vector2(800, y1 - y0));
            }

            // енот площади — он же ведёт обучение дома (второй енот обучения здесь не нужен)
            s.raccoon = Skin(Img("Raccoon", layer, "chr_raccoon_point", new Vector2(-760, -250), new Vector2(380, 380)), "point").rectTransform;
            // крана нет (просьба 03.10.2026): стройку показывает пыль у окна комнаты (vfx_build_dust)
            // внизу по центру — копилка и выручка кассы, каждая на своей подставке, число — с монетой
            BuildStand(layer, "Piggy", new Vector2(-170, -385), "ui_piggy_plate", 300f, "piggy_empty", 150f,
                       new Vector2(7f, -2f), 216f, 44f, out s.piggyButton, out s.piggyImage, out s.piggyText);
            s.piggyButton.gameObject.SetActive(false);
            BuildStand(layer, "Cash", new Vector2(170, -385), "ui_cash_stand", 300f, "icon_cash_register", 130f,
                       new Vector2(0f, -21f), 207f, 34f, out s.cashButton, out s.cashIcon, out s.cashText);
            s.cashButton.gameObject.SetActive(false);
            ScreenTitle(rt, "Торговый дом");
            return s;
        }

        /// <summary>
        /// Подставка с предметом и числом (копилка, касса): подставка — кнопка, предмет стоит сверху,
        /// в кремовой полосе подставки — значок монеты и число. band — центр и размер полосы на подставке.
        /// </summary>
        static void BuildStand(RectTransform parent, string name, Vector2 pos, string stand, float standW, string item, float itemH,
                               Vector2 band, float bandW, float bandH, out Button button, out Image itemImage, out TextMeshProUGUI amount)
        {
            var ssz = ByWidth(stand, standW);
            button = Button(name, parent, stand, pos, ssz);
            var isz = ByHeight(item, itemH);
            // предмет стоит на верхнем краю подставки (чуть заходит на неё — как стоит, а не висит)
            itemImage = Img("Item", button.transform, item, new Vector2(0, ssz.y / 2f + isz.y / 2f - ssz.y * 0.18f), isz);
            float coin = bandH * 0.95f;
            Img("Coin", button.transform, "icon_coin", band + new Vector2(-bandW / 2f + coin * 0.6f, 0), new Vector2(coin, coin));
            amount = Text("Amount", button.transform, "0", 34, band + new Vector2(coin * 0.55f, 0), new Vector2(bandW - coin * 1.4f, bandH),
                          Dark, TextAlignmentOptions.Center, false, autoSize: true);
            amount.enableWordWrapping = false;
        }

        // ================================================================ окно этажа

        static FloorPopup BuildFloorPopup(RectTransform root)
        {
            var r = Stretch(Node("FloorPopup", root));
            Group(r.gameObject);
            Dim(r, 0.62f);
            // рамка-домик ui_floor_panel (v3.2): черепичная крыша с табличкой, кремовое поле для ячеек
            var psz = ByWidth("ui_floor_panel", 1800f);
            var panel = Img("Panel", r, "ui_floor_panel", new Vector2(0, -10), psz, raycast: true).rectTransform;
            var p = AddPopup<FloorPopup>(r, panel);
            float k = psz.x / 1020f;   // пиксели картинки 1020×496 → макет
            // табличка на крыше — центр (511, 95) картинки
            p.title = Text("Title", panel, "Этаж 1 · Лавка", 50, new Vector2(0, (248 - 95) * k), new Vector2(300 * k, 70 * k), Dark,
                           TextAlignmentOptions.Center, false, autoSize: true);
            p.title.enableWordWrapping = false;
            p.closeButton = Button("Close", panel, "ui_btn_round", new Vector2(psz.x / 2f - 70f, (248 - 150) * k), ByHeight("ui_btn_round", 92f),
                                   icon: "icon_close");
            // кремовое поле: x 117..905, y 185..405 картинки → три ячейки по 430
            for (int i = 0; i < 3; i++) p.cells[i] = BuildRoomCell(panel, i, new Vector2((i - 1) * 455f, -75f));
            Text("Hint", panel, "Нажми на комнату — зайдёшь внутрь", 28, new Vector2(0, -245), new Vector2(900, 40),
                 new Color32(0x8A, 0x63, 0x36, 255), TextAlignmentOptions.Center, false, autoSize: true);
            return p;
        }

        /// <summary>
        /// Ячейка комнаты (v3.2): деревянная рамка ui_room_cell 512×296 с окном x 41..470, y 41..223 и медной
        /// табличкой внизу (на ней звёзды жильца); полностью обставленная — золотая рамка. Название — над ячейкой.
        /// </summary>
        static RoomCell BuildRoomCell(RectTransform panel, int i, Vector2 pos)
        {
            var size = ByWidth("ui_room_cell", 430f);
            float k = size.x / 512f;
            var root = Rect(Node("Cell" + (i + 1), panel), pos, size);
            var c = root.gameObject.AddComponent<RoomCell>();
            var hit = Img("Hit", root, null, Vector2.zero, size, raycast: true, color: new Color(0, 0, 0, 0));
            c.button = hit.gameObject.AddComponent<Button>();
            hit.gameObject.AddComponent<UiButton>();
            // превью комнаты под маской окна рамки (обрезается сверху и снизу, не сплющивается)
            var thumb = new Vector2(429 * k, 182 * k);
            var mask = Rect(Node("Thumb", root), new Vector2(0, (148 - 132) * k), thumb);
            mask.gameObject.AddComponent<RectMask2D>();
            float cover = Mathf.Max(thumb.x / RoomLegoData.FrameW, thumb.y / RoomLegoData.FrameH);
            c.view = BuildRoomView(mask, cover);
            c.shade = Img("Shade", mask, null, Vector2.zero, thumb, color: new Color(0.08f, 0.06f, 0.05f, 0.5f));
            // замок поверх комнаты: цепи крест-накрест, на бирке — уровень (бирка: центр (3, −297), 372×124 в кадре)
            var lockImg = Img("Lock", mask, "room_lock_overlay", Vector2.zero, RoomFrame * cover);
            c.lockOverlay = lockImg.gameObject;
            c.lockText = Text("Text", lockImg.transform, "Ур. 18", 22, new Vector2(3, -297) * cover, new Vector2(340, 110) * cover, Dark,
                              TextAlignmentOptions.Center, false, autoSize: true);
            c.stateIcon = Img("StateIcon", mask, "icon_build", new Vector2(0, 18), new Vector2(84, 84));
            c.stateText = Text("State", mask, "", 34, new Vector2(0, -50), new Vector2(340, 46), Color.white,
                               TextAlignmentOptions.Center, true, autoSize: true);
            c.frame = Img("Frame", root, "ui_room_cell", Vector2.zero, size);
            c.frameNormal = S("ui_room_cell");
            c.frameGold = S("ui_room_cell_gold") ?? c.frameNormal;
            // звёзды жильца — на медной табличке (y 255..282, x 179..332 картинки)
            for (int s = 0; s < 4; s++)
                c.stars[s] = Img("Star" + (s + 1), root, "ui_star_big", new Vector2((s - 1.5f) * 26f, (148 - 268.5f) * k), new Vector2(24, 24));
            // жилец — бейдж на левом верхнем углу рамки
            c.face = Img("Face", root, "icon_staff_badge", new Vector2(-size.x / 2f + 30f, size.y / 2f - 22f), new Vector2(86, 86));
            c.nameText = Text("Name", root, "Касса", 36, new Vector2(0, size.y / 2f + 30f), new Vector2(400, 46), Dark,
                              TextAlignmentOptions.Center, false, autoSize: true);
            return c;
        }

        /// <summary>Кадр комнаты FrameW×FrameH: голая комната и куски декора (их создаёт RoomView).</summary>
        static RoomView BuildRoomView(RectTransform parent, float scale)
        {
            var frame = Rect(Node("Room", parent), Vector2.zero, RoomFrame);
            frame.localScale = Vector3.one * scale;
            var v = frame.gameObject.AddComponent<RoomView>();
            v.baseImage = Img("Base", frame, "room_01_base", Vector2.zero, RoomFrame);
            v.piecesRoot = Rect(Node("Pieces", frame), Vector2.zero, RoomFrame);
            return v;
        }

        // ================================================================ комната

        static RoomScreen BuildRoom(RectTransform parent)
        {
            var s = Screen<RoomScreen>(parent, "RoomScreen", null);
            var rt = (RectTransform)s.transform;
            Stretch(Img("Back", rt, null, Vector2.zero, new Vector2(1920, 1080), color: new Color32(0x3A, 0x2A, 0x20, 255)).rectTransform);
            // кадр комнаты накрывает экран целиком, в родных пропорциях
            var holder = Rect(Node("RoomFrame", rt), Vector2.zero, RoomFrame);
            var fit = holder.gameObject.AddComponent<ScaleToParent>();
            fit.reference = RoomFrame;
            fit.cover = true;
            s.frame = holder;
            s.view = BuildRoomView(holder, 1f);
            // жилец стоит на полу перед мебелью; низ доски с декором его не закрывает
            // место (x) RoomScreen выбирает сам — где жилец меньше всего закрывает декор (RoomView.StaffX)
            var staff = Img("Staff", holder, "staff_squirrel", Vector2.zero, ByHeight("staff_squirrel", RoomScreen.StaffHeight));
            staff.rectTransform.pivot = new Vector2(0.5f, 0f);
            staff.rectTransform.anchoredPosition = new Vector2(0, RoomScreen.StaffFeet);
            staff.gameObject.SetActive(false);
            s.staffFigure = staff;
            s.darken = Img("Darken", holder, null, Vector2.zero, RoomFrame, color: new Color(0.1f, 0.07f, 0.05f, 0.45f));
            s.buildOverlay = Img("BuildOverlay", holder, "room_build_overlay", Vector2.zero, RoomFrame);
            s.buildOverlay.rectTransform.sizeDelta = RoomFrame;
            // комната ещё не построена — фон стройки во весь экран (team_build_screen_bg: енот с валиком, часы),
            // раньше под панелью стройки был размытый увеличенный кадр комнаты (просьба 03.10.2026)
            var bbg = Img("BuildBg", rt, "team_build_screen_bg", Vector2.zero, ByHeight("team_build_screen_bg", 1080f));
            bbg.gameObject.AddComponent<ScaleToParent>().cover = true;
            bbg.gameObject.SetActive(false);
            s.buildBg = bbg;
            // закрытая комната: цепи крест-накрест с замком, на бирке — уровень
            var lk = Img("LockOverlay", holder, "room_lock_overlay", Vector2.zero, RoomFrame);
            s.lockOverlay = lk.gameObject;
            s.lockLevel = Text("Level", lk.transform, "Уровень 18", 60, new Vector2(3, -297), new Vector2(330, 100), Dark,
                               TextAlignmentOptions.Center, false, autoSize: true);
            lk.gameObject.SetActive(false);
            // метки: где встанет ещё не купленный предмет
            // метка — значок стройки, под ним круглая кнопка «+»: нажал — предмет строится (просьба 03.10.2026)
            var markSz = ByHeight("ui_badge_round", 70f);
            for (int i = 0; i < 6; i++)
            {
                var m = Rect(Node("Marker" + (i + 1), holder), Vector2.zero, new Vector2(110, 150));
                Img("Spot", m, "icon_build_spot", new Vector2(0, 34), ByHeight("icon_build_spot", 76f));
                var plus = Button("Plus", m, "ui_badge_round", new Vector2(0, -38), markSz, icon: "icon_plus");
                m.gameObject.SetActive(false);
                s.markers[i] = m;
                s.markerButtons[i] = plus;
            }

            // название комнаты — на табличке-двери сверху по центру
            var head = Img("DoorPlate", rt, "ui_door_plate", new Vector2(0, 428), ByWidth("ui_door_plate", 600));
            s.roomTitle = Text("Title", head.transform, "Касса", 46, new Vector2(0, 8), new Vector2(470, 64), Dark,
                               TextAlignmentOptions.Center, false, autoSize: true);
            s.floorText = Text("Floor", head.transform, "Этаж 1 · Лавка", 26, new Vector2(0, -46), new Vector2(420, 36),
                               new Color32(0x8A, 0x63, 0x36, 255), TextAlignmentOptions.Center, false, autoSize: true);
            s.prevRoom = Button("Prev", rt, "ui_btn_round", new Vector2(-880, -170), ByHeight("ui_btn_round", 96f), icon: "icon_back");
            s.nextRoom = Button("Next", rt, "ui_btn_round", new Vector2(880, -170), ByHeight("ui_btn_round", 96f), icon: "icon_next");

            // карточка сотрудника — бейдж на ленте слева
            var cardSz = ByHeight("ui_staff_card", 500f);
            var card = Img("StaffCard", rt, "ui_staff_card", new Vector2(-712, 120), cardSz);
            s.staffCard = card.rectTransform;
            float top = cardSz.y / 2f;
            s.staffFace = Img("Face", card.transform, "icon_staff_badge", new Vector2(0, top - cardSz.y * 0.19f), new Vector2(150, 150));
            s.staffName = Text("Name", card.transform, "Белка-кассир", 32, new Vector2(0, 34), new Vector2(cardSz.x - 44, 46), Dark,
                               TextAlignmentOptions.Center, false, autoSize: true);
            for (int k = 0; k < 4; k++)
                s.stars[k] = Img("Star" + (k + 1), card.transform, "ui_star_big", new Vector2(-72 + k * 48, -14), new Vector2(46, 46));
            s.skillText = Text("Skill", card.transform, "", 26, new Vector2(0, -92), new Vector2(cardSz.x - 48, 104), Dark,
                               TextAlignmentOptions.Center, false, autoSize: true);
            s.nextText = Text("Next", card.transform, "", 23, new Vector2(0, -180), new Vector2(cardSz.x - 48, 56),
                              new Color32(0x3E, 0x7D, 0x3A, 255), TextAlignmentOptions.Center, false, autoSize: true);

            // доска с декором: шесть плиток по порядку покупки
            // доска — внутри tilesRoot: в не построенной комнате её не видно вместе с плитками
            s.tilesRoot = Rect(Node("Tiles", rt), new Vector2(0, -404), new Vector2(1480, 250));
            Img("Plank", s.tilesRoot, "ui_plate_wide", new Vector2(0, -8), new Vector2(1500, 286), sliced: true, preserve: false);
            for (int i = 0; i < 6; i++) s.tiles[i] = BuildDecorTile(s.tilesRoot, new Vector2((i - 2.5f) * 236f, 0));

            // стройка: «Построить» → таймер и ускорение → «Заселить!»
            var bp = Img("BuildPanel", rt, "ui_panel_paper", new Vector2(0, 10), ByWidth("ui_panel_paper", 760));
            s.buildPanel = bp.rectTransform;
            s.buildTitle = Text("Title", bp.transform, "Комната не построена", 44, new Vector2(0, 150), new Vector2(600, 60), Dark,
                                TextAlignmentOptions.Center, false, autoSize: true);
            s.buildText = Text("Text", bp.transform, "", 30, new Vector2(0, 66), new Vector2(580, 100), Dark,
                               TextAlignmentOptions.Center, false, autoSize: true);
            // полоса стройки — общий комплект ui_bar_*: заливка не растягивается (SlicedFill)
            s.buildFill = Bar("Track", bp.transform, new Vector2(0, -12), 520f, 44f, new Color32(0x58, 0xA6, 0x5C, 255), out _);
            var bsz = ByHeight("ui_btn_green", 100f);
            s.buildButton = Button("Build", bp.transform, "ui_btn_green", new Vector2(0, -122), bsz);
            Img("Coin", s.buildButton.transform, "icon_coin", new Vector2(-bsz.x * 0.26f, 2), new Vector2(64, 64));
            s.buildCost = Text("Cost", s.buildButton.transform, "600", 44, new Vector2(bsz.x * 0.1f, 3), new Vector2(bsz.x * 0.44f, 60),
                               Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            s.speedAdButton = Button("SpeedAd", bp.transform, "ui_btn_rewarded", new Vector2(-150, -122), ByHeight("ui_btn_rewarded", 96f),
                                     "−30 мин", 36);
            // «Готово сейчас» за алмазы (экономика v4): цена — за оставшееся время
            var gsz = ByHeight("ui_btn_green", 96f);
            s.gemFinishButton = Button("GemFinish", bp.transform, "ui_btn_green", new Vector2(160, -122), gsz);
            Img("Gem", s.gemFinishButton.transform, "icon_gem_small", new Vector2(-gsz.x * 0.25f, 2), ByHeight("icon_gem_small", 56f));
            s.gemFinishText = Text("Cost", s.gemFinishButton.transform, "15", 42, new Vector2(gsz.x * 0.1f, 3), new Vector2(gsz.x * 0.44f, 60),
                                   Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            s.speedAdText = Label(s.speedAdButton);
            // значок видео нарисован в левой части кнопки — надпись правее него
            var sat = s.speedAdText.rectTransform;
            sat.anchoredPosition = new Vector2(sat.sizeDelta.x * 0.16f, sat.anchoredPosition.y);
            sat.sizeDelta = new Vector2(sat.sizeDelta.x * 0.7f, sat.sizeDelta.y);
            // ускорители-предметы и молоток — полоса кружков под панелью стройки: есть — тратится, нет — окно «Для дома»
            var bpSz = bp.rectTransform.sizeDelta;
            var boosts = Rect(Node("Boosts", rt), new Vector2(0, 10 - bpSz.y / 2f - 64f), new Vector2(6 * 118f, 110));
            s.boostsRoot = boosts.gameObject;
            boosts.gameObject.SetActive(false);
            var rsz2 = ByHeight("ui_badge_round", 100f);
            for (int k = 0; k < 6; k++)
            {
                bool ham = k == 5;
                var b = Button(ham ? "Hammer" : "Boost" + (k + 1), boosts, "ui_badge_round", new Vector2((k - 2.5f) * 118f, 0), rsz2);
                Img("Icon", b.transform, ham ? "icon_hammer_gold" : Core.Economy.BoostIcons[k], new Vector2(0, 4), new Vector2(66, 66));
                var cnt = Text("Count", b.transform, "×0", 34, new Vector2(28, -34), new Vector2(70, 40), Color.white,
                               TextAlignmentOptions.Center, true, autoSize: true);
                if (ham) { s.hammerButton = b; s.hammerText = cnt; }
                else { s.boostButtons[k] = b; s.boostTexts[k] = cnt; }
            }
            s.moveInButton = Button("MoveIn", bp.transform, "ui_btn_primary", new Vector2(0, -122), ByHeight("ui_btn_primary", 104f),
                                    "Заселить!", 42, "icon_move_in");

            // касса: монеты копятся, пока игрока нет
            // выручка кассы (03.10.2026): касса на подставке, в полосе — монета и «накоплено / вместимость»,
            // под ней «Забрать» и «×2». Прежняя табличка была тесной: всё вылезало, монеты сплющены. Копилка — на экране дома
            var cp = Rect(Node("CashPanel", rt), new Vector2(740, 60), new Vector2(320, 470));
            s.cashPanel = cp;
            var ssz = ByWidth("ui_cash_stand", 300f);
            Img("Stand", cp, "ui_cash_stand", new Vector2(0, 40), ssz);
            var rsz = ByHeight("icon_cash_register", 130f);
            Img("Register", cp, "icon_cash_register", new Vector2(0, 40 + ssz.y / 2f + rsz.y / 2f - ssz.y * 0.18f), rsz);
            Img("Coin", cp, "icon_coin", new Vector2(-72, 40 - 21), new Vector2(32, 32));
            s.cashText = Text("Amount", cp, "0 / 160", 30, new Vector2(18, 40 - 21), new Vector2(150, 34), Dark,
                              TextAlignmentOptions.Center, false, autoSize: true);
            s.cashText.enableWordWrapping = false;
            s.cashTake = Button("Take", cp, "ui_btn_green", new Vector2(0, -90), ByHeight("ui_btn_green", 76f), "Забрать", 34);
            s.cashDouble = Button("Double", cp, "ui_btn_rewarded", new Vector2(0, -180), ByHeight("ui_btn_rewarded", 76f), "×2", 36);
            var dl = Label(s.cashDouble).rectTransform;
            dl.anchoredPosition = new Vector2(dl.sizeDelta.x * 0.16f, dl.anchoredPosition.y);
            return s;
        }

        // ================================================================ копилка

        static PiggyPopup BuildPiggyPopup(RectTransform root)
        {
            // поле рамки ui_panel_festive невысокое: свинка — слева, полоса, текст и кнопка — справа
            var (r, panel) = PopupShell("Piggy", root, new Vector2(1000, 0), "Копилка енота", out var title, "ui_panel_festive");
            var p = AddPopup<PiggyPopup>(r, panel);
            p.title = title;
            p.picture = Img("Picture", panel, "piggy_full", new Vector2(-250, -60), ByHeight("piggy_full", 220f));
            var track = Img("Track", panel, "ui_bar_track", new Vector2(130, 70), new Vector2(420, 44), sliced: true);
            p.fill = Img("Fill", track.transform, "ui_bar_fill", Vector2.zero, new Vector2(400, 30), sliced: true,
                         color: new Color32(0xF2, 0xB8, 0x3D, 255));
            p.fill.type = Image.Type.Filled;
            p.fill.fillMethod = Image.FillMethod.Horizontal;
            Img("Coin", track.transform, "icon_coin", new Vector2(-218, 0), new Vector2(56, 56));
            p.amountText = Text("Amount", track.transform, "0 / 4000", 30, new Vector2(0, 2), new Vector2(360, 40), Color.white,
                                TextAlignmentOptions.Center, true, autoSize: true);
            p.infoText = Text("Info", panel, "", 28, new Vector2(130, -40), new Vector2(440, 120), Dark,
                              TextAlignmentOptions.Center, false, autoSize: true);
            p.breakButton = Button("Break", panel, "ui_btn_primary", new Vector2(130, -180), ByHeight("ui_btn_primary", 100f), "Разбить · 99 ₽", 42);
            p.breakText = Label(p.breakButton);
            p.closeButton = Button("Close", panel, "ui_btn_round", new Vector2(panel.sizeDelta.x / 2f - 40f, panel.sizeDelta.y / 2f - 40f),
                                   ByHeight("ui_btn_round", 84f), icon: "icon_close");
            return p;
        }

        // ================================================================ смена перед уровнем (сцена Game)

        /// <summary>
        /// Окно смены v2 (03.10.2026, по картинке shift_empty): сверху пять гнёзд «что ждёт в уровне» (нажал — описание
        /// в строке внизу), по центру три больших места на смене, внизу строка-описание, на нижнем канте —
        /// «Рекомендация» и «Начать!». Команда — в отдельном окне (BuildStaffPicker), его открывает «+» в месте.
        /// Места посчитаны по пикселям картинки 1252×940 (ShiftPx).
        /// </summary>
        const float ShiftW = 1200f;
        static Vector2 ShiftPx(float x, float y) => new Vector2((x - 626f) * ShiftW / 1252f, (470f - y) * ShiftW / 1252f);

        static ShiftPopup BuildShift(RectTransform root)
        {
            float k = ShiftW / 1252f;
            var r = Stretch(Node("Shift", root));
            Group(r.gameObject);
            Dim(r);
            var panel = Img("Panel", r, "shift_empty", new Vector2(0, -10), ByWidth("shift_empty", ShiftW), raycast: true).rectTransform;
            var p = AddPopup<ShiftPopup>(r, panel);
            p.cardNormal = S("ui_tile_slot");
            p.cardChosen = S("ui_tile_slot_gold") ?? p.cardNormal;
            // заголовок — на доске картинки (x 335…915, y 45…165)
            p.title = Text("Title", panel, "Уровень 25 · смена", 46, ShiftPx(626, 104), new Vector2(500 * k, 74 * k), Color.white,
                           TextAlignmentOptions.Center, true, autoSize: true);
            p.title.enableWordWrapping = false;

            // что ждёт в уровне — значки в пяти гнёздах (x 225…1025, y 288), каждый можно нажать
            p.featuresRoot = Rect(Node("Features", panel), ShiftPx(626, 288), new Vector2(1000 * k, 190 * k));
            p.featureSockets = new GameObject[5];
            for (int i = 0; i < 5; i++)
            {
                // гнездо ui_mech_socket (252×256, кольцо Ø236) ложится на нарисованное кольцо Ø176 — только у занятых
                p.featureSockets[i] = Img("Socket" + (i + 1), p.featuresRoot, "ui_mech_socket", new Vector2((225 + i * 200 - 626) * k, -2 * k),
                                          ByWidth("ui_mech_socket", 188 * k)).gameObject;
                var ic = Img("Icon" + (i + 1), p.featuresRoot, "obj_lock_closed", new Vector2((225 + i * 200 - 626) * k, 0), new Vector2(124 * k, 124 * k),
                             raycast: true);
                var b = ic.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                ic.gameObject.AddComponent<UiButton>();
                p.featureIcons[i] = ic;
                p.featureButtons[i] = b;
            }

            // три места на смене (x 340/626/910, y 525, кольцо ~275 px)
            for (int i = 0; i < 3; i++)
            {
                var slot = HitButton("Slot" + (i + 1), panel, ShiftPx(340 + i * 285, 525), new Vector2(250 * k, 250 * k));
                var st = (RectTransform)slot.transform;
                p.slots[i] = slot;
                // пустое место — кольцо с зелёным «+» ui_shift_slot_add (512×484, кольцо Ø448) поверх нарисованного Ø268
                p.slotPlus[i] = Img("Plus", st, "ui_shift_slot_add", new Vector2(0, -2 * k), ByWidth("ui_shift_slot_add", 306 * k)).gameObject;
                p.slotFaces[i] = Img("Face", st, "icon_staff_badge", new Vector2(0, 8 * k), new Vector2(196 * k, 196 * k));
                for (int s = 0; s < 4; s++)
                    p.slotStars[i * 4 + s] = Img("Star" + (s + 1), st, "ui_star_big", new Vector2((s - 1.5f) * 40 * k, -112 * k), new Vector2(40 * k, 40 * k));
                // закрытое место — серое кольцо с цепью поверх нарисованного, под ним — когда откроется
                p.slotLocks[i] = Img("Lock", st, "ui_shift_slot_locked", Vector2.zero, ByWidth("ui_shift_slot_locked", 282 * k)).gameObject;
                p.slotTexts[i] = Text("Text", st, "Буфет", 26, new Vector2(0, -166 * k), new Vector2(270 * k, 40 * k), Dark,
                                      TextAlignmentOptions.Center, false, autoSize: true);
                p.slotTexts[i].enableWordWrapping = false;
            }

            // строка-описание (x 235…1015, y 740…850)
            p.infoText = Text("Info", panel, "Поставь сотрудников на смену.", 30, ShiftPx(626, 795), new Vector2(740 * k, 96 * k), Dark,
                              TextAlignmentOptions.Center, false, autoSize: true);
            p.infoText.fontSizeMin = 18;
            // кнопки — на нижнем канте рамки, между цветами
            // «Рекомендация» — оранжевая, без значка: на бледной secondary мелкая надпись не читалась (проверка 03.10.2026)
            p.recommendButton = Button("Recommend", panel, "ui_btn_primary", ShiftPx(440, 900), ByHeight("ui_btn_primary", 112 * k),
                                       "Рекомендация", 36);
            // волшебная палочка icon_recommend — наклейкой на левом краю, чтобы не отнимать место у надписи
            var rb = (RectTransform)p.recommendButton.transform;
            Img("Wand", rb, "icon_recommend", new Vector2(-rb.sizeDelta.x / 2f - 10f, 10f), ByHeight("icon_recommend", 92 * k));
            p.startButton = Button("Start", panel, "ui_btn_green", ShiftPx(818, 900), ByHeight("ui_btn_green", 108 * k), "Начать!", 44);
            return p;
        }

        /// <summary>Окно «Команда»: все 10 сотрудников 5×2, нажал — встал на смену (03.10.2026).</summary>
        // окно «Команда» (v4, картинка staff_picker_empty 1596×1016): 10 карточек нарисованы на местах —
        // портрет кладём в зелёное окошко, имя — на полоску, звёзды уровня — в гнёзда. Места посчитаны по картинке
        const float PickW = 1300f;
        static Vector2 PickPx(float x, float y) { float k = PickW / 1596f; return new Vector2((x - 798f) * k, (508f - y) * k); }

        static StaffPickerPopup BuildStaffPicker(RectTransform root, ShiftPopup sp)
        {
            var r = Stretch(Node("StaffPicker", root));
            Group(r.gameObject);
            Dim(r);
            var panel = Img("Panel", r, "staff_picker_empty", new Vector2(0, -10), ByWidth("staff_picker_empty", PickW), raycast: true).rectTransform;
            var pk = AddPopup<StaffPickerPopup>(r, panel);
            float k = PickW / 1596f;
            // заголовок — на зелёной табличке сверху
            var title = Text("Title", panel, "Команда", 46, PickPx(805, 104), new Vector2(380 * k, 70 * k), Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            title.enableWordWrapping = false;
            float[] cx = { 282, 541, 798, 1055, 1312 };
            for (int i = 0; i < 10; i++)
                sp.cards[i] = BuildStaffCard(panel, i, cx[i % 5], i < 5 ? 0f : 341f, k);
            // подсказка — под окном, по затемнению (в раме места нет)
            pk.hintText = Text("Hint", r, "Выбери, кого поставить на смену.", 30, PickPx(798, 1010) + new Vector2(0, -10), new Vector2(1000, 46), Color.white,
                               TextAlignmentOptions.Center, true, autoSize: true);
            pk.hintText.fontSizeMin = 14;
            pk.closeButton = Button("Close", panel, "ui_btn_round", PickPx(1500, 92), ByHeight("ui_btn_round", 84f), icon: "icon_close");
            sp.picker = pk;
            return pk;
        }

        /// <summary>Карточка поверх нарисованной: dy — сдвиг второго ряда в пикселях картинки, k — масштаб окна.</summary>
        static StaffCardView BuildStaffCard(RectTransform parent, int i, float cx, float dy, float k)
        {
            // нарисованная карточка: x ±117 от центра, y 188…512 (окошко 223…407, полоска имени 417…451, звёзды ≈ 480)
            var root = Rect(Node("Card" + (i + 1), parent), PickPx(cx, 350 + dy), new Vector2(234 * k, 324 * k));
            var c = root.gameObject.AddComponent<StaffCardView>();
            // фон карточки нарисован на окне: здесь — прозрачная зона нажатия
            c.bg = Img("Bg", root, null, Vector2.zero, root.sizeDelta, raycast: true, color: new Color(1f, 1f, 1f, 0f));
            c.button = c.bg.gameObject.AddComponent<Button>();
            c.bg.gameObject.AddComponent<UiButton>();
            Vector2 L(float x, float y) => PickPx(cx + x, y + dy) - PickPx(cx, 350 + dy);
            c.face = Img("Face", root, "icon_staff_badge", L(0, 315), new Vector2(170 * k, 170 * k));
            c.nameText = Text("Name", root, "Белка", 26, L(0, 434), new Vector2(178 * k, 32 * k), Dark, TextAlignmentOptions.Center, false, autoSize: true);
            c.nameText.enableWordWrapping = false;
            for (int s = 0; s < 4; s++)
                c.stars[s] = Img("Star" + (s + 1), root, "ui_star_big", L(-70 + s * 47, 480), new Vector2(34 * k, 34 * k));
            // закрыт: окошко темнее, замок, когда придёт — на месте звёзд
            var lockRoot = Rect(Node("Lock", root), Vector2.zero, root.sizeDelta);
            Img("Shade", lockRoot, null, L(0, 315), new Vector2(186 * k, 184 * k), color: new Color(0.12f, 0.16f, 0.12f, 0.45f));
            Img("Icon", lockRoot, "icon_lock", L(0, 296), ByHeight("icon_lock", 76 * k));
            c.lockText = Text("Text", lockRoot, "Ур. 24", 24, L(0, 372), new Vector2(176 * k, 34 * k),
                              Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            c.lockText.enableWordWrapping = false;   // «Золотой путь» — уменьшается в одну строку, а не режется
            c.lockText.fontSizeMin = 12;
            c.lockMark = lockRoot.gameObject;
            // «Советую» — зелёная бирка у верхнего края карточки
            var tsz = ByWidth("ui_tag_recommend", 190 * k);
            var tag = Img("Recommend", root, "ui_tag_recommend", L(0, 212), tsz);
            Text("Text", tag.transform, "Советую", 22, new Vector2(tsz.x * 0.13f, 1), new Vector2(tsz.x * 0.6f, tsz.y * 0.62f), Color.white,
                 TextAlignmentOptions.Center, true, autoSize: true);
            c.recommend = tag.gameObject;
            c.onShift = Img("OnShift", root, "icon_check", L(84, 236), new Vector2(56, 48)).gameObject;
            return c;
        }

        static DecorTile BuildDecorTile(RectTransform parent, Vector2 pos)
        {
            var size = new Vector2(220, 236);
            var root = Rect(Node("Tile", parent), pos, size);
            var t = root.gameObject.AddComponent<DecorTile>();
            t.bgNormal = S("ui_tile_slot");
            t.bgBought = S("ui_tile_slot_gold") ?? t.bgNormal;
            t.bg = Img("Bg", root, "ui_tile_slot", Vector2.zero, size, raycast: true, sliced: true, preserve: false);
            t.icon = Img("Icon", root, null, new Vector2(0, 24), new Vector2(150, 140));
            t.icon.rectTransform.sizeDelta = new Vector2(150, 140);
            var buy = ByHeight("ui_btn_green", 56f);
            t.buyButton = Button("Buy", root, "ui_btn_green", new Vector2(0, -84), new Vector2(Mathf.Min(buy.x, 168f), buy.y));
            Img("Coin", t.buyButton.transform, "icon_coin", new Vector2(-56, 0), new Vector2(40, 40));
            t.costText = Text("Cost", t.buyButton.transform, "0", 30, new Vector2(14, 2), new Vector2(96, 42),
                              Color.white, TextAlignmentOptions.Center, true, autoSize: true);
            t.boughtMark = Img("Bought", root, "icon_check", new Vector2(0, -84), new Vector2(58, 58)).gameObject;
            t.lockMark = Img("Lock", root, "icon_lock", new Vector2(70, 80), new Vector2(46, 46));
            return t;
        }
    }
}
