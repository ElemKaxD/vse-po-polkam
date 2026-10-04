using System.Linq;
using AllOnShelves.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Ремонт магазина (ГДД 8.2): этап района, карточки предметов, прогресс, открытие района.</summary>
    public class RenovationScreen : HubScreen
    {
        public RenoStore[] stores = new RenoStore[5];
        public RenoCard[] cards = new RenoCard[10];     // кружки-метки на магазине
        public TextMeshProUGUI storeTitle;
        public TextMeshProUGUI stageText;
        public TextMeshProUGUI hintText;

        [Header("Полоса улучшений с кружками и панель плиток (референс игрока 26.09.2026)")]
        public RectTransform railRoot;
        public RectTransform railTrack;
        public Image railFill;
        public RectTransform[] railNodes = new RectTransform[11];
        public Image[] railNodeIcons = new Image[11];
        public GameObject[] railNodeChecks = new GameObject[11];
        public RectTransform tilesRoot;
        [OptionalRef] public RectTransform tilesPlank;   // доска-полка под плитками
        public RenoCard[] tiles = new RenoCard[10];
        public RectTransform tipPlate;
        public TextMeshProUGUI tipText;
        public Button playButton;
        public TextMeshProUGUI playText;
        public Button prevStage, nextStage;
        public RectTransform cardsPanel;
        public RectTransform raccoon;

        [Header("Ремонт-«лего» (районы 1–4)")]
        public RectTransform legoRoot;
        // задний фон отдельной картинкой: если у серии он есть, магазин рисуется поверх него
        [OptionalRef] public Image legoBg;
        // магазин со слоями: уменьшается целиком и встаёт в свободную зону экрана
        [OptionalRef] public RectTransform legoStore;
        // свободная зона под магазин: между табличкой сверху и полосой с кружками снизу,
        // правее енота (референс игрока 26.09.2026)
        static readonly Rect StoreZone = new Rect(-640f, -190f, 1400f, 435f);   // верх 245 — под плашкой-подсказкой
        // приглушение фона: магазин должен быть в фокусе, а не сливаться с улицей (просьба 26.09.2026)
        static readonly Color BgFar = new Color(0.76f, 0.80f, 0.86f, 1f);
        public Image legoBase;
        public Image[] legoPieces = new Image[10];

        int _district;

        void Awake()
        {
            foreach (var c in cards.Concat(tiles))
            {
                var card = c;
                if (card == null || card.buyButton == null) continue;
                card.buyButton.onClick.AddListener(() => Buy(card));
            }
            playButton.onClick.AddListener(OnPlay);
            prevStage.onClick.AddListener(() => { _district = Mathf.Max(1, _district - 1); Refresh(); });
            nextStage.onClick.AddListener(() => { _district = Mathf.Min(LevelPlanner.DistrictCount, _district + 1); Refresh(); });
        }

        public override void Open()
        {
            _district = Mathf.Min(GameApp.I.StageDistrict, LevelPlanner.DistrictCount);
            base.Open();
            TutorialFirstBuy();
            // енот объясняет ремонт по шагам (HubTutorial.RunOnReno)
            if (hub != null && hub.tutorial != null) Tween.Delay(this, 0.4f, () => hub.tutorial.RunOnReno(this));
        }

        public override void Refresh()
        {
            var app = GameApp.I;
            var store = MetaCatalog.StoreOf(_district);
            bool lego = RefreshLego(app);
            foreach (var s in stores)
            {
                bool on = !lego && s.storeId == store.Id;
                s.gameObject.SetActive(on);
                if (!on) continue;
                s.Refresh(app, _district);
            }
            int done = app.StageProgress(_district, out int total);
            storeTitle.text = store.Name;
            stageText.text = $"Этап {System.Array.IndexOf(store.Districts, _district) + 1} из {store.Districts.Length} · куплено {done} из {total}";

            bool open = app.StageOpen(_district);
            var items = MetaCatalog.ItemsOf(_district).ToList();
            var pieces = RenoLegoData.Pieces(_district);
            for (int i = 0; i < cards.Length; i++)
            {
                if (i >= items.Count) { cards[i].gameObject.SetActive(false); continue; }
                var it = items[i];
                string icon = pieces != null && i < pieces.Length ? pieces[i].Sprite : null;
                bool bought = app.Save.boughtItems.Contains(it.Id);
                cards[i].Set(it, bought, app.Coins >= it.Cost, open, icon);
                if (i < tiles.Length)
                {
                    tiles[i].gameObject.SetActive(true);
                    tiles[i].Set(it, bought, app.Coins >= it.Cost, open, icon);
                    // «?» у плитки: навёл мышкой — сказали, что улучшение откроет в игре
                    var tile = tiles[i];
                    if (tile.hintBadge != null)
                        tile.hintBadge.onHover = on => ShowTip(on ? tile : null,
                            tile.item != null ? "Откроет новинку: " + tile.item.Unlocks : "");
                }
            }
            for (int i = items.Count; i < tiles.Length; i++) tiles[i].gameObject.SetActive(false);
            LayoutBubbles(items.Count, pieces);
            PulseMarkers();
            LayoutTiles(items.Count);
            RefreshRail(app, items);
            ShowTip(null, "");

            bool complete = app.StageComplete(_district);
            if (!open) hintText.text = $"Откроется после уровня {LevelPlanner.DistrictEnd(_district - 1)}";
            else if (complete) hintText.text = "Этап завершён! Новый район открыт.";
            else if (app.BlockedByRenovation && _district == app.StageDistrict) hintText.text = "Закончи ремонт, чтобы открыть следующий район";
            else hintText.text = "";
            // плашка с подсказкой — только когда есть что сказать (закрыто, завершено, блок)
            hintText.transform.parent.gameObject.SetActive(hintText.text.Length > 0);

            playText.text = "На карту";
            prevStage.interactable = _district > 1;
            nextStage.interactable = _district < LevelPlanner.DistrictCount && _district < app.CurrentDistrict + 1;
        }

        /// <summary>
        /// Магазин района собирается из голого кадра и слоёв, вырезанных из этого же кадра
        /// (Tools/ArtPipeline/lego_build.py): перспектива и масштаб совпадают всегда.
        /// </summary>
        /// <summary>Чей кадр «лего» показывать. Второй этап магазина без своей серии (так было у района 15, пока
        /// серия не пришла) показывает готовый магазин первого этапа — тот же магазин со всеми улучшениями.</summary>
        int LegoDistrict =>
            RenoLegoData.Has(_district) ? _district
            : _district > 1 && RenoLegoData.Has(_district - 1) && MetaCatalog.StoreOf(_district - 1) == MetaCatalog.StoreOf(_district) ? _district - 1
            : _district;

        bool RefreshLego(GameApp app)
        {
            if (legoRoot == null) return false;
            int ld = LegoDistrict;
            bool borrowed = ld != _district;
            var pieces = RenoLegoData.Pieces(ld);
            legoRoot.gameObject.SetActive(pieces != null);
            if (pieces == null)
            {
                // фон улицы лежит не внутри кадра «лего», а на экране — прячем его отдельно
                if (legoBg != null) legoBg.gameObject.SetActive(false);
                return false;
            }
            // фон улицы отдельным файлом (meta_lego_dNN_bg) — магазин тогда лежит на нём слоем.
            // Нет такого файла — кадр серии сам себе фон, как было раньше.
            if (legoBg != null)
            {
                // свой фон серии, а если его не прислали — общий фон этого магазина
                var bg = ArtLibrary.S($"meta_lego_d{ld:00}_bg")
                         ?? ArtLibrary.S("bg_meta_" + MetaCatalog.StoreOf(_district).Id);
                legoBg.gameObject.SetActive(bg != null);
                if (bg != null) legoBg.sprite = bg;
                // улицу уводим в дымку, магазин остаётся в полном цвете и читается как объект игры
                legoBg.color = BgFar;
            }
            bool separate = legoBg != null && legoBg.gameObject.activeSelf && ArtLibrary.S($"meta_lego_d{ld:00}_bg") != null;
            // кадр, где магазин нарисован вместе с улицей, накрывает весь экран (на экранах не 16:9
            // иначе по краям полосы); вырезанный магазин стоит на своём фоне и вписан в макет 1920×1080
            var fit = legoRoot.GetComponent<ScaleToParent>();
            if (fit != null) { fit.cover = !separate; fit.Apply(); }
            FitStore(separate, ld);
            legoBase.sprite = ArtLibrary.S(RenoLegoData.Base(ld));
            var items = MetaCatalog.ItemsOf(_district).ToList();
            for (int i = 0; i < legoPieces.Length; i++)
            {
                var img = legoPieces[i];
                if (img == null) continue;
                // чужой кадр — магазин прошлого этапа целиком, со всеми его улучшениями
                bool on = i < pieces.Length && (borrowed || (i < items.Count && app.Save.boughtItems.Contains(items[i].Id)));
                img.gameObject.SetActive(on);
                if (!on) continue;
                var p = pieces[i];
                img.sprite = ArtLibrary.S(p.Sprite);
                img.rectTransform.anchoredPosition = new Vector2(p.X, p.Y);
                img.rectTransform.sizeDelta = new Vector2(p.W, p.H);
                img.transform.localScale = Vector3.one;
                img.color = Color.white;
            }
            return true;
        }

        /// <summary>
        /// Вырезанный магазин на отдельном фоне уменьшаем и ставим в свободную зону, чтобы его не
        /// закрывали табличка, полоса и полка. Магазин, нарисованный вместе с улицей, — как есть.
        /// </summary>
        void FitStore(bool separate, int ld)
        {
            if (legoStore == null) return;
            var b = separate ? RenoLegoData.Bounds(ld) : null;
            if (b == null)
            {
                legoStore.localScale = Vector3.one;
                legoStore.anchoredPosition = Vector2.zero;
                return;
            }
            float w = b[2] - b[0], h = b[3] - b[1];
            float k = Mathf.Min(1f, Mathf.Min(StoreZone.width / w, StoreZone.height / h));
            // низ магазина — на низ зоны (стоит на тротуаре), по ширине — по центру зоны
            float cx = (b[0] + b[2]) * 0.5f;
            legoStore.localScale = new Vector3(k, k, 1f);
            legoStore.anchoredPosition = new Vector2(StoreZone.center.x - cx * k, StoreZone.yMin - b[1] * k);
        }

        /// <summary>Где на экране встало купленное улучшение (null — у района нет «лего»).</summary>
        Vector3? PieceWorld(RenovationItem it)
        {
            var pieces = RenoLegoData.Pieces(_district);
            if (pieces == null || legoRoot == null || !legoRoot.gameObject.activeSelf) return null;
            int i = MetaCatalog.ItemsOf(_district).ToList().FindIndex(x => x.Id == it.Id);
            if (i < 0 || i >= legoPieces.Length || legoPieces[i] == null) return null;
            return legoPieces[i].rectTransform.position;
        }

        /// <summary>Точка кадра «лего» → координаты экрана ремонта (с учётом уменьшения магазина
        /// и масштаба самого кадра — он накрывает экран, если тот не 16:9).</summary>
        Vector2 StorePoint(Vector2 p)
        {
            var q = legoStore == null ? p : legoStore.anchoredPosition + p * legoStore.localScale.x;
            return legoRoot == null ? q : legoRoot.anchoredPosition + q * legoRoot.localScale.x;
        }

        /// <summary>Купленное улучшение прилетает и встаёт ровно на своё место.</summary>
        void FlyLegoPiece(RenovationItem it, Vector3 from)
        {
            var pieces = RenoLegoData.Pieces(_district);
            if (pieces == null) return;
            int i = MetaCatalog.ItemsOf(_district).ToList().FindIndex(x => x.Id == it.Id);
            if (i < 0 || i >= pieces.Length || i >= legoPieces.Length) return;
            var img = legoPieces[i];
            if (img == null || !img.gameObject.activeSelf) return;
            var rt = img.rectTransform;
            if (!pieces[i].Fly)
            {
                // слой во весь кадр (инпейнт задел фон) — просто проявляем
                img.color = new Color(1f, 1f, 1f, 0f);
                Tween.Fade(img, 1f, 0.5f);
                return;
            }
            var home = rt.anchoredPosition;
            rt.position = from;
            rt.localScale = Vector3.one * 0.35f;
            Tween.MoveAnchored(rt, home, 0.5f, Ease.OutQuad);
            Tween.Scale(rt, Vector3.one, 0.5f, Ease.OutBack);
        }

        /// <summary>Подсказка «что откроет» над плиткой; null — спрятать.</summary>
        void ShowTip(RenoCard tile, string text)
        {
            if (tipPlate == null) return;
            bool on = tile != null && !string.IsNullOrEmpty(text);
            tipPlate.gameObject.SetActive(on);
            if (!on) return;
            tipText.text = text;
            float x = tilesRoot.anchoredPosition.x + ((RectTransform)tile.transform).anchoredPosition.x;
            tipPlate.anchoredPosition = new Vector2(Mathf.Clamp(x, -640f, 640f), -252f);
        }

        /// <summary>
        /// Полоса улучшений кружками (референс игрока 26.09.2026): кружок на каждое улучшение района,
        /// купленные зажжены, следующий чуть крупнее, последний кружок — замок нового района.
        /// </summary>
        void RefreshRail(GameApp app, System.Collections.Generic.List<RenovationItem> items)
        {
            // полосы кружками больше нет (просьба 03.10.2026): прогресс — в строке этапа на табличке
            if (railRoot != null) { railRoot.gameObject.SetActive(false); return; }
            if (railNodes == null || railNodes.Length == 0) return;
            int n = Mathf.Min(items.Count + 1, railNodes.Length);
            const float Step = 132f;
            float x0 = -(n - 1) * Step * 0.5f;
            int done = items.Count(it => app.Save.boughtItems.Contains(it.Id));
            for (int i = 0; i < railNodes.Length; i++)
            {
                bool on = i < n;
                railNodes[i].gameObject.SetActive(on);
                if (!on) continue;
                railNodes[i].anchoredPosition = new Vector2(x0 + i * Step, 0f);
                bool last = i >= items.Count;
                bool bought = !last && app.Save.boughtItems.Contains(items[i].Id);
                var ic = railNodeIcons[i];
                ic.sprite = last ? ArtLibrary.S("icon_lock")
                    : ArtLibrary.S(items[i].Id) ?? ArtLibrary.S(MetaCatalog.IconFor(items[i].Id)) ?? ArtLibrary.S("icon_hammer");
                ic.preserveAspect = true;
                // некупленное — приглушённым, чтобы зажжённые кружки читались сразу
                ic.color = bought ? Color.white : new Color(0.66f, 0.62f, 0.56f, 1f);
                railNodes[i].localScale = Vector3.one * (!last && i == done ? 1.14f : 1f);
                if (railNodeChecks[i] != null) railNodeChecks[i].SetActive(bought);
            }
            float w = Mathf.Max(1f, (n - 1) * Step);
            if (railTrack != null) railTrack.sizeDelta = new Vector2(w, railTrack.sizeDelta.y);
            if (railFill != null)
            {
                railFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, w - 18f), railFill.rectTransform.sizeDelta.y);
                railFill.fillAmount = n <= 1 ? 0f : done / (float)(n - 1);
            }
        }

        /// <summary>
        /// Нижняя панель по референсу игрока (26.09.2026): крупная иконка без подписи и ценник.
        /// Плитки сужаются, если улучшений много, но не уже 150 — иначе иконку уже не разглядеть.
        /// </summary>
        void LayoutTiles(int count)
        {
            if (tilesRoot == null || tiles.Length == 0 || count <= 0) return;
            float step = Mathf.Clamp(1560f / count, 152f, 250f);
            float x0 = -(count - 1) * step * 0.5f;
            float icon = Mathf.Clamp(step * 0.66f, 104f, 158f);
            for (int i = 0; i < count && i < tiles.Length; i++)
            {
                var t = tiles[i];
                var rt = (RectTransform)t.transform;
                rt.anchoredPosition = new Vector2(x0 + i * step, 0f);
                var size = new Vector2(step - 14f, 250f);
                rt.sizeDelta = size;
                if (t.cardBg != null) t.cardBg.rectTransform.sizeDelta = size;
                if (t.icon != null)
                {
                    t.icon.rectTransform.sizeDelta = new Vector2(icon, icon);
                    t.icon.rectTransform.anchoredPosition = new Vector2(0f, 26f);
                }
                if (t.buyButton != null)
                {
                    // ширину меняем только вместе с высотой: картинку кнопки растягивать нельзя
                    var b = (RectTransform)t.buyButton.transform;
                    var bi = t.buyButton.GetComponent<Image>();
                    float a = bi != null && bi.sprite != null ? bi.sprite.rect.width / bi.sprite.rect.height : 3f;
                    float bw = Mathf.Min(size.x - 18f, 168f);
                    b.sizeDelta = new Vector2(bw, bw / a);
                }
                if (t.hintBadge != null)
                    ((RectTransform)t.hintBadge.transform).anchoredPosition =
                        new Vector2(size.x / 2f - 26f, size.y / 2f - 26f);
            }
            // доска-полка растягивается ровно под ряд плиток
            if (tilesPlank != null)
                tilesPlank.sizeDelta = new Vector2(count * step + 56f, tilesPlank.sizeDelta.y);
        }

        // безопасное поле для меток: слева енот, сверху плашка-подсказка, снизу полоса с кружками.
        // Метки не должны перекрывать ничего из этого (просьба 26.09.2026)
        static readonly Rect Safe = new Rect(-640f, -60f, 1340f, 240f);
        const float BubbleGap = 128f;      // метки мелкие — и расходятся они теснее прежних облачков

        /// <summary>
        /// Метка каждого улучшения стоит рядом со своим местом на магазине, от неё к месту идёт
        /// светящаяся ниточка (по референсу игрока). Метки разводятся, чтобы не слипались.
        /// </summary>
        // метки будущих улучшений мягко пульсируют (v4): глаз сразу находит, куда что встанет.
        // Метки двигаются при смене района — пульс пересоздаём на новых местах
        VfxPlayer[] _pulses;

        void PulseMarkers()
        {
            if (_pulses == null) _pulses = new VfxPlayer[cards.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                Vfx.Keep(ref _pulses[i], false, null);
                var c = cards[i];
                if (c != null && c.gameObject.activeInHierarchy)
                    _pulses[i] = Vfx.Behind("vfx_reno_marker_pulse", c.transform, ((RectTransform)c.transform).rect.height * 1.7f);
            }
        }

        public override void Close()
        {
            if (_pulses != null) for (int i = 0; i < _pulses.Length; i++) Vfx.Keep(ref _pulses[i], false, null);
            base.Close();
        }

        void LayoutBubbles(int count, RenoLegoData.Piece[] pieces)
        {
            if (cards.Length == 0) return;
            var anchors = new Vector2[count];
            var pos = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                // место улучшения: из «лего»-данных, иначе — веером над магазином
                anchors[i] = pieces != null && i < pieces.Length
                    ? StorePoint(new Vector2(pieces[i].X, pieces[i].Y))
                    : new Vector2(-560f + i * (count > 1 ? 1120f / (count - 1) : 0f), -40f);
                // метка стоит прямо там, где появится предмет (просьба 03.10.2026), а не в стороне на ниточке
                pos[i] = anchors[i];
            }
            // разводим облачки: два рядом читаются хуже, чем два чуть раздвинутых.
            // прижатие к безопасному полю — внутри цикла, иначе у края они снова слипаются
            for (int pass = 0; pass < 40; pass++)
            {
                for (int i = 0; i < count; i++)
                    for (int k = i + 1; k < count; k++)
                    {
                        var d = pos[k] - pos[i];
                        float dist = d.magnitude;
                        if (dist > BubbleGap * 0.85f) continue;
                        var push = (dist < 1f ? new Vector2(1f, 0.3f) : d / dist) * (BubbleGap * 0.85f - dist) * 0.5f;
                        pos[i] -= push; pos[k] += push;
                    }
                for (int i = 0; i < count; i++)
                    pos[i] = new Vector2(Mathf.Clamp(pos[i].x, Safe.xMin, Safe.xMax), Mathf.Clamp(pos[i].y, Safe.yMin, Safe.yMax));
            }
            for (int i = 0; i < count && i < cards.Length; i++)
            {
                ((RectTransform)cards[i].transform).anchoredPosition = pos[i];
                cards[i].PointTo(anchors[i]);
            }
        }

        void Buy(RenoCard card)
        {
            var app = GameApp.I;
            var it = card.item;
            if (app.Coins < it.Cost)
            {
                AudioService.Play("sfx_nope");
                Vfx.At("vfx_locked_shake", card.transform, 200f);
                // экономика v2: не просто сообщение, а выход — видео за монеты или пакет монет
                hub.CoinShortage(it.Cost - app.Coins, Refresh);
                return;
            }
            bool stageWasComplete = app.StageComplete(it.District);
            if (!app.BuyItem(it)) return;
            AudioService.Play("sfx_renovate_build");
            hub.RefreshTop();
            Refresh();
            // монеты ушли с плитки, плитка стала золотой, на магазине — вспышка стройки
            Vfx.At("vfx_coins_spend", card.transform, 260f);
            Vfx.At("vfx_tile_bought", card.transform, 300f, delay: 0.15f);
            var placed = PieceWorld(it);
            if (placed.HasValue) Vfx.Play("vfx_build_in", placed.Value, 420f, delay: 0.25f);
            // анимация появления предмета в сцене магазина (звук траты — в GameApp.TrySpend)
            if (legoRoot != null && legoRoot.gameObject.activeSelf) FlyLegoPiece(it, card.transform.position);
            else
            {
                var store = stores.FirstOrDefault(s => s.gameObject.activeSelf);
                var img = store != null ? store.Find(it.Id) : null;
                if (img != null && img.gameObject.activeSelf)
                {
                    img.transform.localScale = Vector3.zero;
                    Tween.Scale(img.transform, Vector3.one, 0.45f, Ease.OutBack);
                }
                else if (store != null) store.Celebrate();
            }
            Tween.Punch(raccoon, 0.12f, 0.5f);
            if (hub.tutorial != null) Tween.Delay(this, 0.8f, () => hub.tutorial.RunOnReno(this));
            if (!stageWasComplete && app.StageComplete(it.District))
            {
                AudioService.Play("jingle_stage_complete");
                CelebrateStage();
                var skin = app.StageReward(it.District);
                bool showSkin = skin != null && skin.ArtReady;
                // окно — после праздника: раньше оно сразу закрывало и магазин, и эффект
                Tween.Delay(this, 1.6f, () => hub.confirmPopup.Info("Этап завершён!",
                    showSkin ? $"+100 монет, пачка наклеек и «{skin.Title}». Новый район открыт!" : "+100 монет и пачка наклеек. Новый район открыт!",
                    showSkin ? skin.IconSprite : "icon_trophy",
                    () => hub.packPopup.OpenPending(hub)));
            }
        }

        /// <summary>
        /// Магазин отремонтирован целиком (02.10.2026, вместо большого vfx_stage_complete, который не понравился):
        /// тёплая вспышка, конфетти как в окне победы двумя волнами, фонтан монет из магазина, звёзды по краям,
        /// магазин подпрыгивает, енот радуется.
        /// </summary>
        void CelebrateStage()
        {
            Transform store = legoRoot != null && legoRoot.gameObject.activeSelf ? legoRoot : stores.FirstOrDefault(s => s.gameObject.activeSelf)?.transform;
            var center = store != null ? store.position : transform.position;
            float k = ((RectTransform)transform).lossyScale.x;
            Celebration.Flash();
            Celebration.Confetti(55);
            Celebration.Confetti(40, 0.7f);
            Vfx.Play("vfx_coin_fountain", center, 560f, delay: 0.1f);
            var sides = new[] { new Vector3(-260f, 120f), new Vector3(260f, 160f), new Vector3(0f, 260f) };
            for (int i = 0; i < sides.Length; i++)
                Vfx.Play("vfx_star_award", center + sides[i] * k, 300f, delay: 0.25f + i * 0.22f);
            // «книгу» магазина масштабирует ScaleToParent каждый кадр — её не качаем, иначе рывок
            if (store != null && store.GetComponent<ScaleToParent>() == null) Tween.Punch(store, 0.08f, 0.6f, store.localScale.x);
            Tween.Punch(raccoon, 0.18f, 0.7f);
            AudioService.Play("sfx_confetti");
        }

        /// <summary>Значок карты всегда ведёт на карту, а не внутрь уровня (просьба 26.09.2026).</summary>
        void OnPlay() => hub.Show(hub.mapScreen);

        /// <summary>Обучение меты после уровня 3: рука на первом предмете (ГДД 7.2).</summary>
        void TutorialFirstBuy()
        {
            var app = GameApp.I;
            if (app.Save.Tutorial(8) || app.Save.boughtItems.Count > 0) return;
            app.Save.SetTutorial(8);
            app.MarkDirty();
            var first = tiles.Length > 0 ? tiles[0] : cards[0];
            Tween.Run(first.buyButton.transform, Tween.ChScale, 6f, Ease.Linear, k =>
            {
                if (first != null) first.buyButton.transform.localScale = Vector3.one * (1f + Mathf.Abs(Mathf.Sin(k * 6f * 4f)) * 0.12f);
            }, () => { if (first != null) first.buyButton.transform.localScale = Vector3.one; });
        }
    }
}
