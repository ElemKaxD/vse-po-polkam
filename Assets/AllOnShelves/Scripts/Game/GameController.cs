using System.Collections.Generic;
using System.Linq;
using AllOnShelves.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = System.Random;

namespace AllOnShelves.Game
{
    /// <summary>
    /// Контроллер сцены Game: связывает модель (Core.Rules) с объектами сцены.
    /// Все объекты поля лежат в сцене; динамически создаются только товары (префаб ItemView).
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Поле")]
        public Canvas canvas;
        public Image background;
        public RectTransform boardRoot;
        public BeltView[] belts = new BeltView[2];
        public RectTransform shelfRoot;
        public Image shelfFrame;
        public SectionView[] sections = new SectionView[9];
        public CartView cart;
        public GoalPanelView goals;
        public CustomerView customer;
        public RectTransform itemsRoot;
        public RectTransform fxRoot;
        public ItemView itemPrefab;

        [Header("Интерфейс")]
        public GameHud hud;
        public GameObject goalsPanel;
        public TutorialOverlay tutorial;
        public VictoryPopup victory;
        public DefeatPopup defeat;
        public PausePopup pausePopup;
        public MechanicPopup mechanicPopup;
        [OptionalRef] public ShiftPopup shiftPopup;   // смена перед уровнем (03.10.2026)
        public RushResultPopup rushResult;
        public BoosterPopup booster;

        [Header("Час пик")]
        public GameObject rushHud;
        public Image jamFill;
        public TextMeshProUGUI rushScore;
        public TextMeshProUGUI rushCombo;
        public Image rushTimerFill;

        [Header("Раскладка поля (Resources/BoardLayout)")]
        public BoardLayout boardLayout;

        [Header("Тест в редакторе (если сцена запущена без Boot)")]
        public int editorTestLevel = 1;

        // ---------------------------------------------------------------- состояние
        LevelData _data;
        LevelState _st;
        PlayMode _mode;
        readonly Stack<LevelState> _undo = new Stack<LevelState>();
        readonly Dictionary<int, ItemView> _views = new Dictionary<int, ItemView>();
        readonly HashSet<int> _dying = new HashSet<int>();
        readonly Dictionary<int, Vector3> _spawnAt = new Dictionary<int, Vector3>();
        readonly List<GameEvent> _ev = new List<GameEvent>();
        int _freeUndo;
        // смена Торгового дома (03.10.2026): кто помогает в этом уровне, сколько раз Ёж ещё спасёт тележку
        List<Team.Member> _shift = new List<Team.Member>();
        int _rescues;
        RectTransform _shiftRow;
        bool _cartBonus;
        bool _streakCart;        // серия ×2: +1 место в тележке (03.10.2026)
        int _streakHint;         // серия ×1: бесплатная подсказка в этом уровне
        int _undosUsed;          // для задания «Пройди уровень без отмен»
        // Испытание дня (PlayMode.Daily): правило дня
        bool _noUndo, _noHint;
        int _movesLimit;
        bool _challengeFail;
        float _timeLeft = -1f;        // «На время»: секунд осталось (−1 — правило не действует)
        int _starCap;                 // «Только на 3 звезды»: больше стольких мест в тележке занимать нельзя (0 — нет)
        RectTransform _rulePlate;     // плашка правила Испытания под заказом
        TextMeshProUGUI _ruleText;
        int _ruleShown = -1;
        bool IsChallenge => _mode == PlayMode.Daily;
        // прежняя смена игрока: при повторе того же уровня окно открывается с ней
        static List<string> _lastShift = new List<string>();
        static int _lastShiftLevel = -1;
        bool _inputLocked;
        bool _finished;
        bool _loaderCoinsUsed;
        float _idleTime;
        int _tutorialStep;
        ItemView _drag;
        ItemView _hover;
        Vector3 _dragOffset;
        bool _cartWarned;

        // час пик
        Random _rushRng;
        float _rushTimer;
        int _score;
        int _combo;
        float _lastSoldTime = -99f;
        int _setsWithoutDrop;
        int _extraShelves;
        bool _rushContinueUsed;
        bool _rushRegistered;

        Camera Cam => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        bool IsRush => _mode == PlayMode.Rush;

        // ================================================================ запуск

        void Start()
        {
            if (!EnsureApp()) return;
            var app = GameApp.I;
            _mode = app.PendingMode;
            _data = app.PendingLevelData ?? app.Level(app.PendingLevel) ?? app.Level(Mathf.Max(1, editorTestLevel));

            hud.undoButton.onClick.AddListener(OnUndo);
            hud.hintButton.onClick.AddListener(OnHint);
            hud.pauseButton.onClick.AddListener(OnPause);
            if (hud.mapButton != null) hud.mapButton.onClick.AddListener(OnMap);
            foreach (var s in sections) if (s.adButton != null) s.adButton.onClick.AddListener(OnExtraShelf);
            GameApp.WalletChanged += RefreshHud;

            victory.HideInstant();
            defeat.HideInstant();
            pausePopup.HideInstant();
            mechanicPopup.HideInstant();
            if (shiftPopup != null) shiftPopup.HideInstant();
            rushResult.HideInstant();

            if (IsRush) StartRush();
            else StartLevel(app.PendingCartBonus);

            Platform.Sticky(false);
            Platform.GameReady();
        }

        void OnDestroy()
        {
            GameApp.WalletChanged -= RefreshHud;
        }

        /// <summary>
        /// Play нажали прямо в сцене Game — сервисов ещё нет. Раньше здесь молча стартовал
        /// editorTestLevel, и игра открывалась с уровня вместо карты с обучением (баг 23.09.2026).
        /// Теперь уходим в Boot: игрок (и редактор) всегда начинают игру с начала.
        /// </summary>
        bool EnsureApp()
        {
            if (GameApp.I != null) return true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameApp.SceneBoot);
            return false;
        }

        void StartLevel(bool cartBonus)
        {
            _cartBonus = cartBonus;
            bool career = _mode == PlayMode.Career;
            _streakCart = career && Streak.CartSlot;
            _streakHint = career && Streak.FreeHint ? 1 : 0;
            var rule = Challenge.Today;
            _noUndo = IsChallenge && rule == Challenge.Rule.NoUndo;
            _noHint = IsChallenge && rule == Challenge.Rule.NoHint;
            _movesLimit = IsChallenge && rule == Challenge.Rule.Moves
                ? (_data.solution != null && _data.solution.Length > 0 ? _data.solution.Length : 30) + Challenge.MovesSlack : 0;
            _timeLeft = IsChallenge && rule == Challenge.Rule.Timer
                ? (_data.solution != null && _data.solution.Length > 0 ? _data.solution.Length : 30) * Challenge.SecondsPerMove + Challenge.TimeSlack : -1f;
            _starCap = IsChallenge && rule == Challenge.Rule.ThreeStars ? Challenge.PeakLimit(rule, _data.cart) : 0;
            _challengeFail = false;
            _st = NewState();
            if (career && Streak.Wins > 0) Tween.Delay(this, 1.4f, () => { if (!_finished) Toast.Show(Streak.BonusLine(), "icon_streak_2", 3f); });
            _hintBook.Clear();
            if (_data.solution != null) LearnLine(_st, _data.solution.Select(Move.FromData).ToList());
            _undo.Clear();
            _finished = false;
            _loaderCoinsUsed = false;
            _cartWarned = false;
            // бесплатные отмены есть на каждом уровне: из тупика должен быть выход без рекламы
            // (просьба 26.09.2026). Дальше идут отмены из кошелька, и только потом — видео.
            _freeUndo = _mode == PlayMode.Daily ? (_noUndo ? 0 : 2) : 3;
            // смену выбирает игрок в окне перед уровнем (ShiftPopup); умения применяются после него — ApplyShift
            _shift = new List<Team.Member>();
            _rescues = 0;
            _tutorialStep = 0;
            _idleTime = 0f;
            _extraShelves = 0;

            rushHud.SetActive(false);
            goalsPanel.SetActive(true);
            ApplyTheme();
            Layout();
            goals.Build(_st);
            customer.HideInstant();
            ClearViews();
            Sync(true);
            BuildShiftRow();
            BuildRulePlate();

            string title = _mode == PlayMode.Daily ? "Испытание дня" : $"Уровень {_data.id}";
            // режим сложности (v4): перчик вместо звёздочки у номера (приписка к номеру не влезала в табличку)
            bool modeLevel = (_mode == PlayMode.Career || _mode == PlayMode.Replay) && GameApp.I != null && GameApp.I.Mode > 0;
            hud.SetModeIcon(modeLevel ? GameApp.ModeIcons[GameApp.I.Mode] : null);
            hud.SetLevel(title, _data.revision ? "hard" : _data.difficulty);
            if (IsChallenge)
                Tween.Delay(this, 0.9f, () =>
                {
                    int r = (int)Challenge.Today;
                    Toast.Show(Challenge.Titles[r] + ": " + Challenge.Texts[r] + (_movesLimit > 0 ? $" Ходов: {_movesLimit}." : "")
                               + (_starCap > 0 ? $" Мест в тележке: {_starCap}." : ""), Challenge.Icons[r], 4f);
                });
            RefreshHud();
            AudioService.Music("music_level_loop");
            Platform.GameplayStart();
            Platform.Metrica("level_start", new Dictionary<string, object> { { "level", _data.id }, { "mode", _mode.ToString() } });

            // карточка новой механики, затем обучение
            var app = GameApp.I;
            int mechBit = MechanicBit(_data.mechanic);
            if (!string.IsNullOrEmpty(_data.mechanic) && mechBit >= 0 && !app.Save.MechanicSeen(mechBit))
            {
                _inputLocked = true;
                mechanicPopup.Setup(_data.mechanic, () =>
                {
                    app.Save.SetMechanicSeen(mechBit);
                    app.MarkDirty();
                    _inputLocked = false;
                    AskShift(TutorialOnStart);
                }, MechanicSprite());
            }
            else AskShift(TutorialOnStart);
        }

        // ================================================================ смена перед уровнем

        /// <summary>
        /// Окно смены (просьба 03.10.2026): что ждёт в уровне + кого поставить. Игрок выбирает сам, игра советует.
        /// Нет ещё ни одного жильца в Торговом доме или «Час пик» — окна нет, уровень сразу.
        /// </summary>
        void AskShift(System.Action next)
        {
            if (shiftPopup == null || !Team.Available(_mode)) { next(); return; }
            _inputLocked = true;
            var preset = _lastShiftLevel == _data.id ? _lastShift : null;
            string title = _mode == PlayMode.Daily ? "Испытание дня · смена" : $"Уровень {_data.id} · смена";
            shiftPopup.Open(_st, title, preset, ids =>
            {
                _inputLocked = false;
                _lastShift = ids;
                _lastShiftLevel = _data.id;
                if (ids.Count > 0) ApplyShift(ids);
                next();
            });
            if (_shiftHome == null) _shiftHome = shiftPopup.panel.anchoredPosition;
            bool lesson = !GameApp.I.Save.Tutorial(ShiftTutFeatures) || !GameApp.I.Save.Tutorial(ShiftTutStart);
            // в обучении окно стоит правее: слева енот с облачком, они не должны закрывать места на смене
            shiftPopup.panel.anchoredPosition = _shiftHome.Value + (lesson ? new Vector2(300f, 0f) : Vector2.zero);
            if (lesson) Tween.Delay(shiftPopup.transform, 0.45f, ShiftTutorial);
        }

        /// <summary>Новое состояние уровня: +1 место за повтор с видео и ещё +1 — за серию побед.</summary>
        LevelState NewState()
        {
            var s = LevelState.FromData(_data, _cartBonus);
            if (_streakCart) s.CartCapacity++;
            if (IsChallenge && Challenge.Today == Challenge.Rule.SmallCart) s.CartCapacity = System.Math.Max(3, s.CartCapacity - 1);
            return s;
        }

        /// <summary>Сотрудники вышли на смену: уровень заново с их умениями (тележка, замки, таймеры — до первого хода).</summary>
        void ApplyShift(List<string> ids)
        {
            _st = NewState();
            _freeUndo = _mode == PlayMode.Daily ? 2 : 3;
            _shift = ids.Select(Team.Get).Where(m => m != null && m.Level > 0).ToList();
            var notes = Team.ApplyStart(_st, _shift, ref _freeUndo, out int giftHints);
            if (giftHints > 0 && GameApp.I.Save.hint == 0) GameApp.I.AddHint(giftHints);
            _rescues = Team.Rescues(_shift);
            Layout();
            goals.Build(_st);
            customer.HideInstant();
            ClearViews();
            Sync(true);
            BuildShiftRow();
            RefreshRulePlate();
            RefreshHud();
            if (_shiftRow != null)
                foreach (Transform f in _shiftRow)
                {
                    f.localScale = Vector3.zero;
                    Tween.Scale(f, Vector3.one, 0.35f, Ease.OutBack);
                    if (f.gameObject.activeSelf) Vfx.Play("vfx_staff_skill", f.position, 170f, delay: 0.25f);
                }
            if (notes.Count > 0 && _shift.Count > 0)
                Tween.Delay(goalsPanel.transform, 0.6f, () => Toast.Show(string.Join(" · ", notes), Team.Face(_shift[0].Info.Id), 3f));
        }

        // обучение окну смены: что ждёт в уровне → выбери сотрудника → «Начать»
        const int ShiftTutFeatures = 49, ShiftTutPick = 50, ShiftTutStart = 51;
        Vector2? _shiftHome;

        void ShiftTutorial()
        {
            if (shiftPopup == null || !shiftPopup.IsOpen) return;
            var save = GameApp.I.Save;
            void Mark(int bit) { save.SetTutorial(bit); GameApp.I.MarkDirty(); }
            void Again() => Tween.Delay(shiftPopup.transform, 0.35f, ShiftTutorial);
            if (!save.Tutorial(ShiftTutFeatures))
            {
                Mark(ShiftTutFeatures);
                var f = Team.Features(_st);
                string what = f.Count == 0 ? "Здесь — что ждёт в уровне. В этом всё по-простому, без сюрпризов."
                    : "Здесь — что ждёт в уровне: " + string.Join(", ", f.Select(k => MechanicPopup.Info(k).title.ToLowerInvariant())) + ".";
                tutorial.Focus(what + " Смотри сюда перед уровнем.", shiftPopup.featuresRoot, Again);
                return;
            }
            if (!save.Tutorial(ShiftTutPick))
            {
                // окно смены v2: «Рекомендация» ставит тех, кто поможет; если советовать некого — «+» и окно «Команда»
                if (Team.Recommend(_st).Count > 0)
                {
                    Mark(ShiftTutPick);
                    tutorial.Focus("Жми «Рекомендация» — поставлю тех, кто поможет именно здесь. «+» в кружке — выбрать самому.",
                                   (RectTransform)shiftPopup.recommendButton.transform, () => { shiftPopup.recommendButton.onClick.Invoke(); Again(); },
                                   strict: true);
                    return;
                }
                string id = Team.Ready().Select(m => m.Info.Id).FirstOrDefault();
                var card = id != null ? shiftPopup.Card(id) : null;
                if (card != null && shiftPopup.picker != null)
                {
                    if (!shiftPopup.picker.IsOpen)
                    {
                        tutorial.Focus("Нажми «+» — откроется вся команда.", (RectTransform)shiftPopup.slots[0].transform,
                                       () => { shiftPopup.slots[0].onClick.Invoke(); Again(); }, strict: true);
                        return;
                    }
                    Mark(ShiftTutPick);
                    tutorial.Focus("Нажми на сотрудника — он выйдет на смену. «Советую» — тот, кто поможет именно здесь.",
                                   (RectTransform)card.transform, () => { shiftPopup.DebugPick(id); Again(); }, strict: true);
                    return;
                }
            }
            if (!save.Tutorial(ShiftTutStart))
            {
                Mark(ShiftTutStart);
                tutorial.Focus("Он на смене — умение сработает в уровне. Жми «Начать»!",
                               (RectTransform)shiftPopup.startButton.transform, () => shiftPopup.startButton.onClick.Invoke(), strict: true);
            }
        }

        /// <summary>Картинка окна механики: у «Лишнего товара» — тот лишний товар, что едет в этом уровне.</summary>
        string MechanicSprite()
        {
            if (_data == null || _data.mechanic != "extra" || _data.goals == null || _data.belts == null) return null;
            var goals = new HashSet<string>(_data.goals.Select(g => g.type));
            foreach (var b in _data.belts)
                foreach (var t in b.tokens)
                {
                    string body = t.Contains(":") ? t.Substring(t.IndexOf(':') + 1) : t;
                    foreach (var id in body.Split('+')) if (!goals.Contains(id)) return "item_" + id;
                }
            return null;
        }

        static int MechanicBit(string m)
        {
            int i = 0;
            foreach (var kv in LevelPlanner.MechanicIntro) { if (kv.Key == m) return i; i++; }
            return -1;
        }

        /// <summary>Скины конвейера и стеллажей из «Гардероба».</summary>
        void ApplySkins()
        {
            var app = GameApp.I;
            var belt = app != null ? app.WornSprite("belt") : null;
            var shelf = app != null ? app.WornSprite("shelf") : null;
            foreach (var b in belts) if (b != null) b.SetSkin(belt);
            foreach (var s in sections) if (s != null) s.SetSkin(shelf);
        }

        void ApplyTheme()
        {
            ApplySkins();
            string bg;
            if (IsRush) bg = "bg_rush_evening";
            else
            {
                // надетое оформление магазина (награда пути или набор из магазина покупок) важнее вида района;
                // прежние «темы» (activeTheme) с 30.09.2026 — такие же скины в «Гардеробе»
                var worn = GameApp.I != null ? GameApp.I.WornSprite("scene") : null;
                if (worn != null)
                {
                    background.sprite = worn;
                    background.color = Color.white;
                    return;
                }
                int d = _data != null ? _data.district : 1;
                bg = d <= 2 ? "bg_level_kiosk" : d <= 4 ? "bg_level_corner" : d <= 6 ? "bg_level_minimarket" : d <= 9 ? "bg_level_supermarket"
                   : d <= 11 ? "bg_level_hypermarket" : d == 12 ? "bg_level_market_hall" : d == 13 ? "bg_level_harbor"
                   // ТЦ: ночная смена и последний район — вечерний ТЦ с гирляндами (свой фон, без синего фильтра)
                   : d == 15 || (_st != null && _st.Night) ? "bg_level_mall_night" : "bg_level_mall";
            }
            var sp = ArtLibrary.S(bg);
            if (sp != null) background.sprite = sp;
            // ночная смена — приглушённый синеватый свет (ГДД 4.13)
            background.color = _st != null && _st.Night && bg != "bg_level_mall_night" ? new Color(0.5f, 0.55f, 0.8f) : Color.white;
        }

        Sprite CartSkin()
        {
            if (GameApp.I == null) return null;
            if (_st.CartCapacity < 5 || _st.CartCapacity > 6) return null;
            // золотая тележка за весь альбом убрана (30.09.2026): она теперь в наборе «Золотой магазин»
            return GameApp.I.WornSprite("cart");
        }

        /// <summary>Расстановка лент и секций под уровень (объекты в сцене переиспользуются).</summary>
        void Layout()
        {
            bool two = _st.Belts.Length > 1;
            belts[0].gameObject.SetActive(true);
            belts[1].gameObject.SetActive(two);
            // по высоте всё поле (ленты, стеллаж, тележка с колёсами) помещается в экран 16:9 (1080 единиц);
            // положения — из BoardLayout (их можно подвинуть мышкой, см. окно «Просмотр сцен и уровней»)
            var c = LayoutConfig;
            var b1 = (RectTransform)belts[0].transform;
            b1.anchoredPosition = c.belt1Pos;
            b1.localScale = Vector3.one * c.beltScale;
            if (two)
            {
                var b2 = (RectTransform)belts[1].transform;
                b2.anchoredPosition = c.belt2Pos;
                b2.localScale = Vector3.one * c.beltScale;
            }
            for (int b = 0; b < _st.Belts.Length && b < belts.Length; b++) belts[b].Configure(_st.Visible, _st.Window);
            shelfRoot.anchoredPosition = c.shelfPos;
            shelfRoot.localScale = Vector3.one * c.shelfScale;
            cart.rect.anchoredPosition = c.cartPos;
            cart.rect.localScale = Vector3.one * c.cartScale;

            int n = _st.Sections.Length;
            // лишний тёмный шкаф справа — предложение «+ полка за рекламу» (просьба 23.09.2026)
            bool adSlot = ExtraShelfAvailable && n < sections.Length;
            int shown = n + (adSlot ? 1 : 0);
            float total = 1240f;
            // шкаф — целая картинка: высоту считаем от ширины, иначе он сплющится.
            // боковины соседних шкафов заходят друг на друга — так их и поставил пользователь
            float w = Mathf.Min(SectionView.MaxWidth, total / Mathf.Max(1, shown) * SectionView.Overlap);
            float step = w / SectionView.Overlap;
            float start = -(shown - 1) * step / 2f;
            float h = w / SectionView.Aspect;
            for (int i = 0; i < sections.Length; i++)
            {
                bool on = i < shown;
                sections[i].gameObject.SetActive(on);
                if (!on) continue;
                sections[i].rect.anchoredPosition = new Vector2(start + i * step, 0f);
                sections[i].rect.sizeDelta = new Vector2(w, h);
                sections[i].LayoutSlots();
                if (i < n) { sections[i].ShowAdOffer(false); sections[i].Configure(_st.Sections[i].Kind); }
                else sections[i].ShowAdOffer(true);
            }
            shelfFrame.rectTransform.sizeDelta = new Vector2(shown * step + 40f, h);
            cart.Configure(_st.CartCapacity, CartSkin());
        }

        BoardLayout Board => boardLayout != null ? boardLayout : BoardLayout.Default;
        BoardLayout.Config LayoutConfig => _st != null && _st.Belts.Length > 1 ? Board.twoBelts : Board.oneBelt;

        // ================================================================ синхронизация вида

        ItemView GetView(int uid, Vector3 spawnWorld)
        {
            if (_views.TryGetValue(uid, out var v) && v != null) return v;
            v = Instantiate(itemPrefab, itemsRoot);
            if (!Application.isPlaying) v.gameObject.hideFlags = HideFlags.DontSave; // предпросмотр в редакторе — не сохраняется в сцену
            v.name = "Item_" + uid;
            v.Uid = uid;
            v.Controller = this;
            v.rect.position = spawnWorld;
            v.group.alpha = 1f;
            _views[uid] = v;
            return v;
        }

        void ClearViews()
        {
            foreach (var v in _views.Values) if (v != null) Kill(v.gameObject);
            _views.Clear();
            _dying.Clear();
            _spawnAt.Clear();
        }

        /// <summary>Расставляет все товары по состоянию модели (ГДД 3.3 — плавные перемещения).</summary>
        void Sync(bool instant)
        {
            var alive = new HashSet<int>();

            // ленты
            for (int b = 0; b < _st.Belts.Length && b < belts.Length; b++)
            {
                var belt = _st.Belts[b];
                for (int i = 0; i < belt.Count; i++)
                {
                    var tok = belt[i];
                    alive.Add(tok.Uid);
                    bool visible = i < _st.Visible;
                    var v = GetView(tok.Uid, belts[b].HatchWorld);
                    if (v.Kind != tok.Kind || v.Type != tok.Type || (tok.Kind == TokenKind.Box && !tok.Revealed && v.icon.enabled)) v.SetToken(tok);
                    v.visual.localScale = Vector3.one;
                    bool inWindow = i < _st.Window;
                    v.SetInteractable(inWindow && !_finished);
                    v.SetHighlight(inWindow && !_finished && !IsRush);
                    Place(v, visible ? belts[b].SlotWorld(i) : belts[b].HatchWorld, instant, visible ? 1f : 0f, 0f);
                }
            }

            // секции
            for (int j = 0; j < _st.Sections.Length; j++)
            {
                var s = _st.Sections[j];
                sections[j].Refresh(s, _st.DoorIsOpen, _st.DoorOpen > 0);
                for (int k = 0; k < s.Uids.Count; k++)
                {
                    int uid = s.Uids[k];
                    alive.Add(uid);
                    var v = GetView(uid, SpawnFor(uid, sections[j].SlotWorld(k, s.Type)));
                    if (v.Kind != TokenKind.Item || v.Type != s.Type) v.SetItem(s.Type);
                    v.visual.localScale = Vector3.one *
                        (Core.ItemCatalog.IsBig(s.Type) ? sections[j].BigItemScale : sections[j].ItemScale);   // товар по размеру ниши
                    v.PlaceShadow();
                    v.SetTimer(-1);
                    v.SetSpoiled(false);
                    v.SetInteractable(false);
                    v.SetHighlight(false);
                    Place(v, sections[j].SlotWorld(k, s.Type), instant, 1f, 70f);
                }
            }

            // тележка
            int slot = 0;
            for (int i = 0; i < _st.Cart.Count; i++)
            {
                var c = _st.Cart[i];
                int size = ItemCatalog.SlotSize(c.Type);
                alive.Add(c.Uid);
                var pos = cart.SlotWorld(slot, size);
                var v = GetView(c.Uid, SpawnFor(c.Uid, pos));
                if (v.Kind != TokenKind.Item || v.Type != c.Type) v.SetItem(c.Type);
                v.visual.localScale = Vector3.one * cart.ItemScale;   // товар по размеру корзинки
                v.PlaceShadow();
                v.SetSpoiled(c.Spoiled);
                v.SetTimer(c.Spoiled ? -1 : c.Timer);
                v.SetInteractable(!c.Spoiled && !_finished);
                v.SetHighlight(false);
                Place(v, pos, instant, 1f, 70f);
                slot += size;
            }

            // удалить исчезнувшие (кроме анимируемых)
            var dead = _views.Keys.Where(k => !alive.Contains(k) && !_dying.Contains(k)).ToList();
            foreach (var k in dead)
            {
                if (_views[k] != null) Kill(_views[k].gameObject);
                _views.Remove(k);
            }
            _spawnAt.Clear();

            cart.SetWarning(!IsRush && _st.Result == GameResult.Playing && _st.CartUsed >= _st.CartCapacity - 1);
            goals.Refresh(_st, !instant);
        }

        static void Kill(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        Vector3 SpawnFor(int uid, Vector3 fallback) => _spawnAt.TryGetValue(uid, out var p) ? p : fallback;

        void Place(ItemView v, Vector3 target, bool instant, float alpha, float arc)
        {
            if (instant)
            {
                Tween.Kill(v.rect, Tween.ChPos);
                v.rect.position = target;
                v.group.alpha = alpha;
                return;
            }
            if ((v.rect.position - target).sqrMagnitude > 0.01f)
            {
                float dist = Vector3.Distance(v.rect.position, target) / Mathf.Max(0.0001f, canvas.transform.lossyScale.x);
                float dur = Mathf.Clamp(dist / 2400f, 0.16f, 0.32f);
                Tween.Move(v.rect, target, dur, Ease.OutQuad, dist > 200f ? arc : 0f);
            }
            if (Mathf.Abs(v.group.alpha - alpha) > 0.01f) Tween.Fade(v.group, alpha, 0.15f);
        }

        // ================================================================ ввод

        public bool CanDrag(ItemView v) => !_inputLocked && !_finished && v.Interactable && !v.Spoiled && FindSource(v, out _);

        bool FindSource(ItemView v, out Move m)
        {
            m = default;
            for (int b = 0; b < _st.Belts.Length; b++)
            {
                int n = Rules.SourceCount(_st, SourceKind.Belt, b);
                for (int i = 0; i < n; i++)
                    if (_st.Belts[b][i].Uid == v.Uid) { m = Move.FromBelt(b, i, TargetKind.Auto); return true; }
            }
            for (int i = 0; i < _st.Cart.Count; i++)
                if (_st.Cart[i].Uid == v.Uid && !_st.Cart[i].Spoiled) { m = Move.FromCart(i, TargetKind.Auto); return true; }
            return false;
        }

        public void OnItemClicked(ItemView v)
        {
            if (_inputLocked || _finished || _drag != null) return;
            // два способа хода (просьба 22.09.2026, раунд 5): клик — товар сам едет на свою полку
            // (своя секция → пустая → тележка), или перетаскивание — куда игрок сам решит
            if (v.Interactable && !v.Spoiled && FindSource(v, out var m))
            {
                var auto = Rules.ResolveAuto(_st, m);
                if (auto.Dst != TargetKind.Auto && Rules.IsLegal(_st, auto))
                {
                    ClearTargetHighlights();
                    _hover = null;
                    if (TryApply(auto, v)) return;
                }
            }
            Tween.Shake(v.visual, 8f, 0.25f);
            AudioService.Play("sfx_nope");
            Vfx.Play("vfx_item_wrong", v.rect.position, 130f);
        }

        public void OnItemHover(ItemView v, bool enter)
        {
            if (_drag != null || _inputLocked || _finished) return;
            if (!enter)
            {
                if (_hover == v) { ClearTargetHighlights(); _hover = null; }
                Tween.MoveAnchored(v.visual, Vector2.zero, 0.1f);
                return;
            }
            if (!FindSource(v, out var m)) return;
            _hover = v;
            Tween.MoveAnchored(v.visual, new Vector2(0f, 10f), 0.1f);
            AudioService.Play("sfx_hover", 1f, 0.4f);
            var auto = Rules.ResolveAuto(_st, m);
            ClearTargetHighlights();
            if (auto.Dst == TargetKind.Section) sections[auto.DstIndex].SetHighlight(1);
            else if (auto.Dst == TargetKind.Cart) cart.SetHighlight(1);
        }

        public void OnItemDragBegin(ItemView v, PointerEventData e)
        {
            if (!FindSource(v, out var m)) return;
            CompleteItemTweens();
            _drag = v;
            v.transform.SetAsLastSibling();
            RectTransformUtility.ScreenPointToWorldPointInRectangle(itemsRoot, e.position, Cam, out var world);
            _dragOffset = v.rect.position - world;
            Tween.Scale(v.visual, Vector3.one * 1.15f, 0.1f);
            AudioService.Play("sfx_pick");
            ClearTargetHighlights();
            for (int j = 0; j < _st.Sections.Length; j++)
            {
                var t = m; t.Dst = TargetKind.Section; t.DstIndex = j;
                sections[j].SetHighlight(Rules.IsLegal(_st, t) ? 1 : 0);
            }
            if (m.Src == SourceKind.Belt)
            {
                var t = m; t.Dst = TargetKind.Cart;
                cart.SetHighlight(Rules.IsLegal(_st, t) ? 1 : 2);
            }
        }

        public void OnItemDrag(ItemView v, PointerEventData e)
        {
            if (_drag != v) return;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(itemsRoot, e.position, Cam, out var world))
                v.rect.position = world + _dragOffset;
        }

        public void OnItemDragEnd(ItemView v, PointerEventData e)
        {
            if (_drag != v) return;
            _drag = null;
            Tween.Scale(v.visual, Vector3.one, 0.1f);
            ClearTargetHighlights();
            if (!FindSource(v, out var m)) { Sync(false); return; }
            for (int j = 0; j < _st.Sections.Length; j++)
            {
                if (!sections[j].gameObject.activeSelf || !sections[j].Contains(e.position, Cam)) continue;
                m.Dst = TargetKind.Section; m.DstIndex = j;
                if (!TryApply(m, v)) Sync(false);
                return;
            }
            if (cart.Contains(e.position, Cam) && m.Src == SourceKind.Belt)
            {
                m.Dst = TargetKind.Cart; m.DstIndex = -1;
                if (!TryApply(m, v)) Sync(false);
                return;
            }
            Sync(false); // вернуть на место
        }

        VfxPlayer _hintGlow;
        int _hintSeq;

        /// <summary>Гасит подсказку на товаре (ход, отмена, новая подсказка).</summary>
        void StopHintGlow()
        {
            if (_hintGlow != null) _hintGlow.Stop();
            _hintGlow = null;
        }

        void ClearTargetHighlights()
        {
            foreach (var s in sections) if (s.gameObject.activeSelf) s.SetHighlight(0);
            cart.SetHighlight(0);
        }

        void CompleteItemTweens()
        {
            foreach (var v in _views.Values)
            {
                if (v == null) continue;
                Tween.Complete(v.rect);
            }
        }

        // ================================================================ ход

        bool TryApply(Move m, ItemView source)
        {
            CompleteItemTweens();
            _idleTime = 0f;
            Rules.TryGetSource(_st, m, out var tok);
            var before = _st.Clone();
            _ev.Clear();
            if (!Rules.Apply(_st, m, _ev))
            {
                if (source != null) Tween.Shake(source.visual, 10f, 0.3f);
                AudioService.Play("sfx_nope");
                if (source != null) Vfx.Play("vfx_item_wrong", source.rect.position, 130f);
                if (m.Dst == TargetKind.Section && m.DstIndex >= 0) Tween.Shake(sections[m.DstIndex].rect, 8f, 0.25f);
                return false;
            }
            if (!IsRush)
            {
                _undo.Push(before);
                if (_undo.Count > 50) TrimUndo();
            }
            ClearTargetHighlights();
            StopHintGlow();
            _hover = null;

            // связка: второй товар появляется там же, где была связка
            if (tok.Kind == TokenKind.Bundle && _views.TryGetValue(tok.Uid, out var bv) && bv != null)
            {
                _spawnAt[tok.Uid2] = bv.rect.position;
                bv.SetItem(tok.Type);
                AudioService.Play("sfx_tape_rip");
            }

            HandleEvents(_ev);
            if (IsRush) RushRules.Refill(_st, _rushRng, null);
            Sync(false);
            RefreshHud();
            AfterMove();
            // Испытание «Ходов в обрез»: ходы кончились, а заказ не собран
            if (_movesLimit > 0 && !_finished && _st.Result == GameResult.Playing)
            {
                int left = _movesLimit - _st.Moves;
                if (left <= 0) { _challengeFail = true; OnLose(); }
                else if (left == 5 || left == 2) Toast.Show($"Осталось ходов: {left}", "icon_rule_moves");
            }
            // «Только на 3 звезды»: тележка заполнилась сверх трёх звёзд — испытание провалено
            if (_starCap > 0 && !_finished && _st.Result == GameResult.Playing && _st.PeakCart > _starCap)
            {
                _challengeFail = true;
                OnLose();
            }
            RefreshRulePlate();
            return true;
        }

        void TrimUndo()
        {
            var arr = _undo.ToArray();
            _undo.Clear();
            for (int i = 39; i >= 0; i--) _undo.Push(arr[i]);
        }

        void HandleEvents(List<GameEvent> events)
        {
            ItemView palletView = null;
            bool placedShelf = false, placedCart = false;
            int landSec = -1, landSlot = 0, landType = -1;
            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case EventKind.ItemToSection:
                        placedShelf = true;
                        landSec = e.B; landType = e.C;
                        landSlot = e.B >= 0 && e.B < _st.Sections.Length ? Mathf.Max(0, _st.Sections[e.B].Uids.IndexOf(e.A)) : 0;
                        if (palletView != null) _spawnAt[e.A] = palletView.rect.position;
                        break;
                    case EventKind.ItemToCart:
                        placedCart = true;
                        if (palletView != null) _spawnAt[e.A] = palletView.rect.position;
                        break;
                    case EventKind.PalletUnpacked:
                        if (_views.TryGetValue(e.A, out palletView) && palletView != null)
                        {
                            var pv = palletView;
                            _views.Remove(e.A);
                            Tween.Fade(pv.group, 0f, 0.25f, () => { if (pv != null) Destroy(pv.gameObject); }, 0.1f);
                            AudioService.Play("sfx_pallet_unload");
                            Vfx.Play("vfx_pallet_drop", pv.rect.position, 320f);
                        }
                        break;
                    case EventKind.BeltAdvanced:
                        if (e.A < belts.Length) belts[e.A].Step();
                        // в «Часе пик» лента едет сама по таймеру — щелчок звучал без хода и сбивал (03.10.2026)
                        if (!IsRush) AudioService.Play("sfx_belt_step", 1f, 0.5f);
                        break;
                    case EventKind.BoxRevealed:
                        if (_views.TryGetValue(e.A, out var box) && box != null)
                        {
                            box.RevealBox(e.C); AudioService.Play("sfx_box_open");
                            Vfx.Play("vfx_box_open", box.rect.position, 280f);
                        }
                        break;
                    case EventKind.SetSold:
                        OnSetSold(e);
                        break;
                    case EventKind.SectionUnlocked:
                        sections[e.B].PopLock();
                        AudioService.Play("sfx_lock_open");
                        Vfx.Play("vfx_lock_break", sections[e.B].rect.position, 280f);
                        break;
                    case EventKind.LockProgress:
                        sections[e.B].PopLock();
                        break;
                    case EventKind.Spoiled:
                        if (_views.TryGetValue(e.A, out var sp) && sp != null)
                        {
                            sp.SetSpoiled(true); Tween.Punch(sp.visual, 0.2f);
                            // вонь висит над испорченным товаром, пока его не уберут (эффект — его дочка)
                            Vfx.Play("vfx_spoil_stink", sp.visual.position, 150f, parent: sp.visual);
                        }
                        AudioService.Play("sfx_spoil");
                        if (!IsRush) Toast.Show("Испортилось! Свежий уже едет в конце ленты", "icon_clock");
                        break;
                    case EventKind.SpoiledRemoved:
                        Vanish(e.A, Vector3.zero, 0.3f);
                        break;
                    case EventKind.DoorChanged:
                        AudioService.Play("sfx_freezer_door");
                        if (e.D == 1)
                            for (int f = 0; f < _st.Sections.Length && f < sections.Length; f++)
                                if (_st.Sections[f].Kind == SectionKind.Freezer)
                                    Vfx.Play("vfx_freezer_open", sections[f].rect.position, 300f);
                        break;
                    case EventKind.CustomerArrived:
                        customer.Arrive(e.C, e.D, e.D, e.B);
                        AudioService.Play("sfx_customer_arrive");
                        Vfx.Play("vfx_customer_call", customer.HomePoint, 240f, delay: 0.2f);
                        break;
                    case EventKind.CustomerPatience:
                        customer.SetPatience(e.D, _st.Customer.MaxPatience);
                        break;
                    case EventKind.CustomerLeft:
                        customer.Leave(false);
                        AudioService.Play("sfx_customer_leave");
                        break;
                    case EventKind.CustomerServed:
                        Vanish(e.A, customer.WantPoint, 0.35f);
                        customer.Leave(true);
                        FlyCoins(customer.HomePoint, e.D);
                        AudioService.Play("sfx_customer_happy");
                        break;
                    case EventKind.CartCleared:
                        if (e.Uids != null)
                            foreach (var uid in e.Uids) Vanish(uid, cart.rect.position + new Vector3(900f, 150f, 0f) * canvas.transform.lossyScale.x, 0.5f);
                        break;
                    case EventKind.Win:
                        OnWin();
                        break;
                    case EventKind.Lose:
                        OnLose();
                        break;
                }
            }
            if (placedShelf)
            {
                AudioService.Play("sfx_place_shelf");
                // пыль у основания товара, когда он долетит до полки. Облачко — не уже самого товара
                // (просьба 29.09.2026): на листе оно занимает до 32 % ширины ячейки и сидит на 14,5 %
                // её высоты ниже центра. Ячейку масштабируем целиком (без растяжения вширь)
                // и сдвигаем так, чтобы облачко легло под низ товара
                if (landSec >= 0 && landSec < sections.Length && landType >= 0)
                {
                    var sec = sections[landSec];
                    float itemW = 112f * (ItemCatalog.IsBig(landType) ? sec.BigItemScale : sec.ItemScale);
                    float size = itemW * 1.15f / 0.32f;
                    float k = canvas.transform.lossyScale.y;
                    var pos = sec.SlotWorld(landSlot, landType) + new Vector3(0f, (size * 0.145f - itemW * 0.45f) * k, 0f);
                    Vfx.Play("vfx_item_land", pos, size, delay: 0.16f);
                }
            }
            else if (placedCart) AudioService.Play("sfx_place_cart");
        }

        void OnSetSold(GameEvent e)
        {
            var sec = sections[e.B];
            int n = e.Uids?.Length ?? 0;
            for (int k = 0; k < n; k++)
            {
                int uid = e.Uids[k];
                var target = sec.SlotWorld(k, e.C);
                var v = GetView(uid, SpawnFor(uid, target));
                if (v.Type != e.C) v.SetItem(e.C);
                _dying.Add(uid);
                Tween.Move(v.rect, target, 0.18f, Ease.OutQuad, 60f);
                var vv = v;
                Tween.Run(v.visual, Tween.ChScale, 0.32f, Ease.Linear, t =>
                {
                    if (vv == null) return;
                    float s = t < 0.5f ? 1f + t * 0.5f : 1.25f * (1f - (t - 0.5f) * 2f);
                    vv.visual.localScale = Vector3.one * Mathf.Max(0f, s);
                }, () =>
                {
                    _dying.Remove(uid);
                    if (vv != null) Destroy(vv.gameObject);
                    _views.Remove(uid);
                }, 0.22f);
            }
            Burst(sec.rect.position);
            Vfx.Play("vfx_section_done", sec.rect.position, 380f, delay: 0.12f);
            FlyCoins(sec.rect.position, e.D);

            float now = Time.time;
            _combo = now - _lastSoldTime < 4f ? Mathf.Min(_combo + 1, 5) : 1;
            _lastSoldTime = now;
            AudioService.Play("sfx_set_sold", 1f + 0.08f * (_combo - 1));
            if (_combo > 1) Vfx.Play("vfx_combo_ring", sec.rect.position, 340f, delay: 0.2f);
            if (IsRush)
            {
                _score += 10 * _combo;
                _setsWithoutDrop++;
                if (_setsWithoutDrop >= 3) { _setsWithoutDrop = 0; RushRules.RelieveJam(_st); }
                if (_combo > 1) AudioService.Play("sfx_combo_up");
            }
        }

        void Vanish(int uid, Vector3 toWorld, float duration)
        {
            if (!_views.TryGetValue(uid, out var v) || v == null) return;
            _dying.Add(uid);
            if (toWorld != Vector3.zero) Tween.Move(v.rect, toWorld, duration, Ease.InQuad, 40f);
            Tween.Fade(v.group, 0f, duration, () =>
            {
                _dying.Remove(uid);
                if (v != null) Destroy(v.gameObject);
                _views.Remove(uid);
            });
        }

        // ================================================================ эффекты

        void Burst(Vector3 world)
        {
            var sp = ArtLibrary.S("fx_burst");
            if (sp == null) return;
            var go = new GameObject("fx_burst", typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.sprite = sp; img.raycastTarget = false; img.color = new Color(1f, 0.95f, 0.6f, 0.9f);
            var rt = (RectTransform)go.transform;
            rt.SetParent(fxRoot, false);
            rt.sizeDelta = new Vector2(220f, 220f);
            rt.position = world;
            rt.localScale = Vector3.one * 0.3f;
            Tween.Scale(rt, Vector3.one * 1.3f, 0.35f, Ease.OutQuad);
            Tween.Fade(img, 0f, 0.35f, () => Destroy(go));
        }

        void FlyCoins(Vector3 from, int count)
        {
            var sp = ArtLibrary.S("icon_coin");
            if (sp == null) return;
            count = Mathf.Clamp(count, 1, 5);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("fx_coin", typeof(RectTransform), typeof(Image));
                bool first = i == 0;   // лямбда ниже ловит общий i цикла — копия обязательна
                var img = go.GetComponent<Image>();
                img.sprite = sp; img.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.SetParent(fxRoot, false);
                rt.sizeDelta = new Vector2(56f, 56f);
                rt.position = from + new Vector3(Random01() * 60f - 30f, Random01() * 40f, 0f) * canvas.transform.lossyScale.x;
                Tween.Move(rt, hud.coinsIcon.position, 0.55f, Ease.InQuad, 120f, () =>
                {
                    Destroy(go);
                    if (hud != null) Tween.Punch(hud.coinsIcon, 0.2f, 0.2f);
                    if (first) AudioService.Play("sfx_coins_fly", 1f, 0.7f);
                }, i * 0.06f);
            }
        }

        static readonly System.Random _fxRng = new System.Random();
        static float Random01() => (float)_fxRng.NextDouble();

        void Confetti()
        {
            var shapes = new[] { "fx_confetti_a", "fx_confetti_b", "fx_confetti_c" };
            var colors = new[] { new Color(0.95f, 0.4f, 0.24f), new Color(1f, 0.78f, 0.24f), new Color(0.36f, 0.77f, 0.42f), new Color(0.53f, 0.72f, 0.94f), new Color(0.96f, 0.61f, 0.82f) };
            var root = (RectTransform)fxRoot;
            float scale = canvas.transform.lossyScale.x;
            for (int i = 0; i < 40; i++)
            {
                var sp = ArtLibrary.S(shapes[i % 3]);
                if (sp == null) return;
                var go = new GameObject("fx_confetti", typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>();
                img.sprite = sp; img.raycastTarget = false; img.color = colors[i % colors.Length];
                var rt = (RectTransform)go.transform;
                rt.SetParent(root, false);
                rt.sizeDelta = new Vector2(34f, 34f);
                rt.anchoredPosition = new Vector2(Random01() * 1800f - 900f, 600f);
                rt.localRotation = Quaternion.Euler(0, 0, Random01() * 360f);
                var to = rt.position + new Vector3(Random01() * 300f - 150f, -(900f + Random01() * 400f), 0f) * scale;
                Tween.Move(rt, to, 1.6f + Random01(), Ease.InQuad, 0f, () => Destroy(go), Random01() * 0.4f);
            }
            AudioService.Play("sfx_confetti");
        }

        // ================================================================ после хода: обучение, простой

        void AfterMove()
        {
            if (_finished) return;
            var save = GameApp.I.Save;
            // первое «почти полна»
            if (!IsRush && !_cartWarned && _st.CartUsed >= _st.CartCapacity - 1 && !save.Tutorial(7))
            {
                _cartWarned = true;
                save.SetTutorial(7);
                GameApp.I.MarkDirty();
                tutorial.Show("Тележка почти полна — думай наперёд!", cart.rect);
                Tween.Delay(tutorial, 3f, () => { if (tutorial.Visible) tutorial.Hide(); });
                return;
            }
            TutorialOnMove();
        }

        void Update()
        {
            if (_st == null || _finished) return;
            if (IsRush)
            {
                UpdateRush();
                return;
            }
            if (_inputLocked || Platform.AdShowing) return;
            if (_timeLeft > 0f && _st.Result == GameResult.Playing) TickTimer();
            _idleTime += Time.deltaTime;
            // автоподсказка на первых уровнях после 20 c бездействия (ГДД 2.5)
            if (_data.id <= 8 && _mode == PlayMode.Career && _idleTime > 20f)
            {
                _idleTime = 0f;
                ShowHintMove(free: true);
            }
        }

        // ================================================================ обучение (ГДД 7)

        void TutorialOnStart()
        {
            var save = GameApp.I.Save;
            switch (_data.tutorial)
            {
                case "tut_tap" when !save.Tutorial(0):
                    DragTutorial("Нажми на товар — он сам встанет на полку. Или перетащи его мышкой");
                    break;
                case "tut_goal" when !save.Tutorial(1):
                    _inputLocked = true;
                    tutorial.Show("Собери заказ слева — и уровень пройден!", goals.rows[0].GetComponent<RectTransform>(), () =>
                    {
                        _inputLocked = false;
                        save.SetTutorial(1);
                        GameApp.I.MarkDirty();
                    }, dimScreen: true);
                    break;
                case "tut_window" when !save.Tutorial(3):
                    tutorial.Show("Брать можно любой светящийся товар", belts[0].windowFrame);
                    Tween.Delay(tutorial, 4f, () => { if (tutorial.Visible) tutorial.Hide(); });
                    save.SetTutorial(3);
                    break;
                case "tut_drag" when !save.Tutorial(4):
                    tutorial.Show("Перетащи товар в тележку, чтобы не занимать полку. Ошибся — нажми «Отмена»", cart.rect);
                    Tween.Delay(tutorial, 5f, () => { if (tutorial.Visible) tutorial.Hide(); });
                    save.SetTutorial(4);
                    break;
                case "tut_window3" when !save.Tutorial(5):
                    tutorial.Show("Теперь можно брать любой из трёх крайних товаров", belts[0].windowFrame);
                    Tween.Delay(tutorial, 4f, () => { if (tutorial.Visible) tutorial.Hide(); });
                    save.SetTutorial(5);
                    break;
                case "tut_hint" when !save.Tutorial(6):
                    if (!save.hintGranted) { save.hintGranted = true; save.hint += Economy.StartHint; }
                    RefreshHud();
                    tutorial.Show("Не знаешь, что делать? Подсказка покажет лучший ход", hud.hintButton.GetComponent<RectTransform>());
                    Tween.Delay(tutorial, 4f, () => { if (tutorial.Visible) tutorial.Hide(); });
                    save.SetTutorial(6);
                    break;
            }
            GameApp.I.MarkDirty();
        }

        /// <summary>
        /// Товар в тележке, которому есть куда встать по-настоящему: его вид уже лежит на полке.
        /// Пустая полка не считается — занять её чужим товаром значит проиграть.
        /// </summary>
        int ReadyFromCart()
        {
            if (_st == null) return -1;
            for (int i = 0; i < _st.Cart.Count; i++)
            {
                var c = _st.Cart[i];
                if (c.Spoiled) continue;
                int own = _st.SectionOfType(c.Type);
                if (own >= 0 && Rules.IsLegal(_st, Move.FromCart(i, TargetKind.Section, own))) return i;
            }
            return -1;
        }

        void PointFirstBelt(string text)
        {
            var belt = _st.Belts[0];
            if (belt.Count == 0) return;
            if (_views.TryGetValue(belt[0].Uid, out var v) && v != null)
                tutorial.Show(text, v.rect);
        }

        /// <summary>Обучение drag: пузырь у енота + рука циклично тащит первый товар ленты
        /// в подходящую секцию (Update-анимация с затемнением — см. TutorialOverlay.ShowDragDemo).</summary>
        void DragTutorial(string text)
        {
            var belt = _st.Belts[0];
            if (belt.Count == 0) return;
            if (!_views.TryGetValue(belt[0].Uid, out var v) || v == null) return;
            // куда этот товар встанет: своя секция → пустая → тележка (как в авто-цели)
            var m = Rules.ResolveAuto(_st, Move.FromBelt(0, 0, TargetKind.Auto));
            RectTransform to = m.Dst == TargetKind.Section ? sections[m.DstIndex].rect : cart.rect;
            if (to != null) tutorial.ShowDragDemo(text, v.rect, to);
        }

        void TutorialOnMove()
        {
            var save = GameApp.I.Save;
            switch (_data.tutorial)
            {
                case "tut_tap" when !save.Tutorial(0):
                    _tutorialStep++;
                    if (_st.SetsSold > 0)
                    {
                        tutorial.Show("Три одинаковых — раскуплено!", null);
                        Tween.Delay(tutorial, 2.2f, () => { if (tutorial.Visible) tutorial.Hide(); });
                        save.SetTutorial(0);
                        GameApp.I.MarkDirty();
                    }
                    else PointFirstBelt("Ещё раз: нажми на товар или перетащи его на полку");
                    break;
                // тележке учим уже на 2-м уровне (просьба 26.09.2026): там трёх видов товара
                // на две полки, без тележки уровень не собрать
                case "tut_goal" when !save.Tutorial(2):
                case "tut_cart" when !save.Tutorial(2):
                    // товар без места → в тележку; обратно на полку зовём, ТОЛЬКО если у товара уже
                    // есть своя секция. Иначе совет «переложи из тележки» занимает пустую полку чужим
                    // видом товара и ведёт к проигрышу (баг 26.09.2026)
                    int ready = ReadyFromCart();
                    if (ready >= 0)
                    {
                        if (_views.TryGetValue(_st.Cart[ready].Uid, out var cv))
                            tutorial.Show("У этого товара уже есть своя полка — переложи его туда", cv.rect);
                        _tutorialStep = 2;
                    }
                    else if (_st.Cart.Count == 0 && _tutorialStep == 2)
                    {
                        tutorial.Hide();
                        save.SetTutorial(2);
                        GameApp.I.MarkDirty();
                    }
                    else if (_st.Belts[0].Count > 0 && Rules.ResolveAuto(_st, Move.FromBelt(0, 0, TargetKind.Auto)).Dst == TargetKind.Cart && _tutorialStep == 0)
                    {
                        _tutorialStep = 1;
                        PointFirstBelt("Нет места на полке? Нажми на товар — он подождёт в тележке. Или перетащи");
                    }
                    else tutorial.Hide(); // товар ждёт в тележке — молчим, пока для него не освободится полка
                    break;
                default:
                    if (tutorial.Visible && _st.Moves > 1 && _data.tutorial != "tut_goal") tutorial.Hide();
                    break;
            }
        }

        // ================================================================ отмена и подсказка

        void OnUndo()
        {
            if (_inputLocked || _finished || IsRush) return;
            if (_noUndo) { AudioService.Play("sfx_nope"); Toast.Show("Испытание дня: сегодня без отмен", "icon_rule_no_undo"); return; }
            if (_undo.Count == 0) { AudioService.Play("sfx_nope"); Toast.Show("Отменять пока нечего"); return; }
            var app = GameApp.I;
            if (_freeUndo > 0) { _freeUndo--; DoUndo(); return; }
            if (app.ConsumeUndo()) { DoUndo(); return; }
            // отмены кончились: монеты, реклама или набор помощника (раньше — сразу реклама)
            void Use() { RefreshHud(); if (app.ConsumeUndo()) DoUndo(); }
            OpenBooster(new BoosterPopup.Offer
            {
                Title = "Отмены закончились", Icon = "icon_undo",
                Text = "Отмена возвращает последний ход — товар вернётся на ленту.",
                Price = Economy.UndoPackPrice, CoinsLabel = $"×3 · {Economy.UndoPackPrice}",
                AdLabel = "+1 за видео", IapId = "helpers_pack",
                OnCoins = () => { app.AddUndo(3); Use(); },
                OnAd = () => Platform.ShowRewarded("reward_undo", () => { app.AddUndo(1); Use(); }),
                OnIap = () => Platform.Buy("helpers_pack", _ => Use()),
            });
        }

        /// <summary>Окно бустера: пока оно открыто, ходить нельзя.</summary>
        void OpenBooster(BoosterPopup.Offer o)
        {
            if (booster == null) { o.OnAd?.Invoke(); return; }
            _inputLocked = true;
            void Unlock() { _inputLocked = false; }
            var coins = o.OnCoins; var ad = o.OnAd; var iap = o.OnIap;
            o.OnCoins = coins == null ? null : (System.Action)(() => { Unlock(); AudioService.Play("sfx_coins_fly"); coins(); });
            o.OnAd = ad == null ? null : (System.Action)(() => { Unlock(); ad(); });
            o.OnIap = iap == null ? null : (System.Action)(() => { Unlock(); iap(); });
            o.OnClose = Unlock;
            booster.Open(o);
        }

        void DoUndo()
        {
            StopHintGlow();
            _undosUsed++;
            _st = _undo.Pop();
            AudioService.Play("sfx_undo");
            if (hud != null) Vfx.At("vfx_undo_swirl", hud.undoButton.transform, 220f);
            ClearViews();
            Layout();
            goals.Build(_st);
            Sync(true);
            customer.HideInstant();   // очередь приходов/уходов — с чистого листа
            if (_st.Customer.Active) customer.Arrive(_st.Customer.Type, _st.Customer.Patience, _st.Customer.MaxPatience, _st.Customer.Look);
            else customer.HideInstant();
            RefreshHud();
            tutorial.Hide();
        }

        /// <summary>
        /// Бесплатный откат из тупика: отматываем ходы назад, пока не вернёмся в положение, из
        /// которого уровень ещё выигрывается. Если такого не нашли — начинаем уровень заново.
        /// Просьба 26.09.2026: сложно — да, но упираться в рекламу, чтобы пройти, игрок не должен.
        /// </summary>
        void FreeRewind()
        {
            _challengeFail = false;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            LevelState safe = null;
            int steps = 0;
            while (_undo.Count > 0 && sw.ElapsedMilliseconds < 1200)
            {
                var st = _undo.Pop();
                steps++;
                var solver = new Solver { NodeLimit = 40000, TimeLimitMs = 120 };
                // «Только на 3 звезды»: годится лишь положение, где тележка ещё не переполнялась
                int peak = _starCap > 0 ? _starCap : st.CartCapacity;
                if (st.PeakCart <= peak && solver.Solve(st, peak).Solved) { safe = st; break; }
            }
            if (safe == null)
            {
                GameApp.I.RegisterLose(_data, _st, _mode);
                Restart(false);
                return;
            }
            _st = safe;
            _finished = false;
            _inputLocked = false;
            if (_timeLeft >= 0f) _timeLeft = Mathf.Max(_timeLeft, 30f);   // «На время»: после отмотки — ещё полминуты
            RefreshRulePlate();
            AudioService.Play("sfx_undo");
            Vfx.Play("vfx_rewind_wave", canvas.transform.position, 1900f);
            ClearViews();
            Layout();
            goals.Build(_st);
            Sync(true);
            customer.HideInstant();   // очередь приходов/уходов — с чистого листа
            if (_st.Customer.Active) customer.Arrive(_st.Customer.Type, _st.Customer.Patience, _st.Customer.MaxPatience, _st.Customer.Look);
            else customer.HideInstant();
            RefreshHud();
            tutorial.Hide();
            Platform.GameplayStart();
            Toast.Show(steps == 1 ? "Вернули ход назад — отсюда ещё можно выиграть"
                                  : $"Отмотали {steps} хода назад — отсюда ещё можно выиграть");
        }

        /// <summary>«+1 полка за рекламу» (просьба 23.09.2026): секций в уровне мало намеренно, это выход.</summary>
        bool ExtraShelfAvailable =>
            !IsRush && !_finished && _st != null
            && _st.Sections.Length < Mathf.Min(sections.Length, Core.Economy.ExtraShelfMaxSections)
            && _extraShelves < Core.Economy.ExtraShelfMax
            && (_mode != PlayMode.Career || _data == null || _data.id >= Core.Economy.ExtraShelfFrom);

        /// <summary>Выход из уровня на карту без окна паузы.</summary>
        void OnMap()
        {
            if (_inputLocked) return;
            Platform.GameplayStop();
            GameApp.I.LastOutcome = null;
            GameApp.I.GoHub();
        }

        void OnExtraShelf()
        {
            if (_inputLocked || !ExtraShelfAvailable) return;
            var app = GameApp.I;
            OpenBooster(new BoosterPopup.Offer
            {
                Title = "Ещё одна полка", Icon = SectionView.Sprite,
                Text = "Новая полка до конца уровня — места станет больше.",
                Price = Economy.ShelfPrice, CoinsLabel = $"Полка · {Economy.ShelfPrice}",
                AdLabel = "За видео",
                OnCoins = AddExtraShelf,
                OnAd = () => Platform.ShowRewarded("reward_shelf", AddExtraShelf),
            });
        }

        void AddExtraShelf()
        {
            {
                if (!ExtraShelfAvailable) return;
                _extraShelves++;
                _st.AddSection();
                // купленную полку не должна отбирать отмена хода: добавляем её и в сохранённые состояния
                foreach (var st in _undo) st.AddSection();
                Layout();
                Sync(false);
                RefreshHud();
                AudioService.Play("sfx_unlock");
                var view = sections[_st.Sections.Length - 1];
                view.transform.localScale = Vector3.zero;
                Tween.Scale(view.transform, Vector3.one, 0.45f, Ease.OutBack);
                Toast.Show("Полка добавлена!");
            }
        }

        void OnHint()
        {
            if (_inputLocked || _finished || IsRush) return;
            var app = GameApp.I;
            if (_noHint) { AudioService.Play("sfx_nope"); Toast.Show("Испытание дня: сегодня без подсказок", "icon_rule_no_hint"); return; }
            if (_streakHint > 0) { _streakHint--; ShowHintMove(true); return; }   // подсказка от серии — даром
            if (app.ConsumeHint()) { ShowHintMove(false); return; }
            void Use() { RefreshHud(); if (app.ConsumeHint()) ShowHintMove(false); }
            OpenBooster(new BoosterPopup.Offer
            {
                Title = "Подсказки закончились", Icon = "icon_hint",
                Text = "Подсказка покажет лучший ход прямо сейчас.",
                Price = Economy.HintPackPrice, CoinsLabel = $"×3 · {Economy.HintPackPrice}",
                AdLabel = "+1 за видео", IapId = "helpers_pack",
                OnCoins = () => { app.AddHint(3); Use(); },
                OnAd = () => Platform.ShowRewarded("reward_hint", () => { app.AddHint(1); Use(); }),
                OnIap = () => Platform.Buy("helpers_pack", _ => Use()),
            });
        }

        void ShowHintMove(bool free)
        {
            Move? best = null;
            // быстрый путь: сохранённое решение уровня, если мы на нём
            best = BestMove(150);
            if (best == null)
            {
                if (!free)
                {
                    GameApp.I.AddHint(1);   // подсказки нет — возвращаем потраченную
                    Toast.Show("Выхода нет — отмени пару ходов");
                    tutorial.Show("Нажми «Отмена»", hud.undoButton.GetComponent<RectTransform>());
                    Tween.Delay(tutorial, 3f, () => { if (tutorial.Visible) tutorial.Hide(); });
                }
                return;
            }
            var m = Rules.ResolveAuto(_st, best.Value);
            Rules.TryGetSource(_st, m, out var tok);
            if (!_views.TryGetValue(tok.Uid, out var v) || v == null) return;
            // подсказка по простою — тихий «блип», по кнопке — свой звук
            AudioService.Play(free ? "sfx_hint_idle" : "sfx_hint");
            // v4 (03.10.2026): вокруг товара «дышит» кольцо с искрами и прыгает стрелка (цикл, живёт до хода),
            // на месте, куда класть, — кольцо «приземления». Раньше — разовая волна золотых точек vfx_hint_trail
            StopHintGlow();
            float gs = Mathf.Max(v.rect.rect.width, v.rect.rect.height) * 2.1f;
            _hintGlow = Vfx.Play("vfx_hint_glow", v.rect.position, gs, parent: v.rect, loop: true);
            if (_hintGlow != null) _hintGlow.transform.SetSiblingIndex(0);   // позади товара
            ClearTargetHighlights();
            if (m.Dst == TargetKind.Section) sections[m.DstIndex].SetHighlight(1);
            else cart.SetHighlight(1);
            var dst = m.Dst == TargetKind.Section ? sections[m.DstIndex].transform : cart.transform;
            Vfx.At("vfx_hint_target", dst, 300f, delay: 0.2f);
            tutorial.PointAt(v.rect);
            Tween.Punch(v.visual, 0.25f, 0.5f);
            int seq = ++_hintSeq;
            Tween.Delay(hud, 2.5f, () =>
            {
                tutorial.PointAt(null);
                ClearTargetHighlights();
            });
            Tween.Delay(hud, 6f, () => { if (seq == _hintSeq) StopHintGlow(); });
        }

        void RefreshHud()
        {
            if (hud == null || _data == null && !IsRush) return;
            bool hintVisible = !IsRush && (GameApp.I.Save.maxReached >= Economy.HintAt || GameApp.I.Save.hintGranted);
            hud.RefreshWallet(_freeUndo, hintVisible && !_noHint);
            hud.undoButton.gameObject.SetActive(!IsRush && !_noUndo);
        }

        // ================================================================ пауза

        void OnPause()
        {
            if (_finished) return;
            _inputLocked = true;
            Platform.GameplayStop();
            if (IsRush) Time.timeScale = 0f;
            pausePopup.Setup(
                onResume: () => { pausePopup.Hide(); Resume(); },
                onRestart: () =>
                {
                    Time.timeScale = 1f;
                    if (!IsRush) GameApp.I.RegisterLose(_data, _st, _mode);
                    Restart(false);
                },
                onMap: () =>
                {
                    Time.timeScale = 1f;
                    if (!IsRush) GameApp.I.RegisterLose(_data, _st, _mode);
                    else FinishRushRegistration();
                    Platform.TryInterstitial("pause_exit", () => GameApp.I.GoHub());
                },
                onHelp: () =>
                {
                    pausePopup.Hide(() => mechanicPopup.Setup(string.IsNullOrEmpty(_data?.mechanic) ? "rules" : _data.mechanic, () => pausePopup.Show()));
                });
        }

        void Resume()
        {
            Time.timeScale = 1f;
            _inputLocked = false;
            Platform.GameplayStart();
        }

        void Restart(bool cartBonus)
        {
            if (IsRush) { GameApp.I.PlayRush(); return; }
            GameApp.I.PlayLevel(_data.id, _mode, cartBonus);
        }

        // ================================================================ победа

        void OnWin()
        {
            _finished = true;
            Platform.GameplayStop();
            var app = GameApp.I;
            // заказ выполнен, а на ленте, в тележке и на полках ещё лежит товар — енот уносит остатки на склад
            // (01.10.2026: «ещё не всё собрал, а уже победил» выглядело как баг). 1 монета за каждые 3 товара
            int leftovers = SweepLeftovers();
            if (leftovers > 0) _st.SetCoins += (leftovers + 2) / 3;
            // смена: Кролик удваивает чаевые, Белка и Кот добавляют процент к монетам за победу
            if (_shift.Count > 0) _st.Tips = (int)(_st.Tips * Team.TipsMult(_shift));
            app.ShiftCoinPercent = Team.CoinPercent(_shift);
            var outcome = app.RegisterWin(_data, _st, _mode);
            if (_undosUsed == 0 && !IsRush) Quests.Add("no_undo");
            int mult = Economy.RewardMultiplier(_data.id);
            bool freeGift = _data.id == 2 && _mode == PlayMode.Career && !app.Save.Tutorial(9);
            bool packReady = app.Save.pendingPacks > 0 && app.Save.maxReached >= Economy.AlbumAt;   // пока альбома нет — не обещаем пачку
            // «Дальше» есть всегда (22.09.2026). На первых уровнях и в конце района оно ведёт на карту —
            // там енот продолжает обучение; в остальных случаях сразу открывает следующий уровень (GoNext)
            string nextLabel = "Дальше";

            Tween.Delay(this, 0.7f, () =>
            {
                // только прежнее падающее конфетти: взрыв разноцветных бумажек vfx_level_win
                // пользователь убрал (29.09.2026)
                Confetti();
                AudioService.Play("jingle_win");
                if (_mode == PlayMode.Daily) Vfx.At("vfx_challenge_win", victory.panel, 560f, delay: 0.25f);
                if (outcome.StreakStep >= 0) Vfx.At("vfx_streak_up", victory.raccoon, 300f, delay: 1.5f);
                if (outcome.StreakStep >= 0)
                    Tween.Delay(this, 1.6f, () => Toast.Show($"Серия ×{Streak.Wins}! {Streak.Names[outcome.StreakStep]}", Streak.Icons[outcome.StreakStep], 3f));
                victory.Setup(outcome.Stars, outcome.Coins, mult, freeGift, _mode != PlayMode.Daily, packReady, nextLabel,
                    onMap: () => Platform.TryInterstitial("win_map", () => app.GoHub()),
                    onDouble: () =>
                    {
                        void Grant()
                        {
                            app.AddCoins(outcome.Coins * (mult - 1), "x2");
                            victory.ShowDoubled(outcome.Coins * mult);
                        }
                        if (freeGift) { app.Save.SetTutorial(9); app.MarkDirty(); Grant(); }
                        else Platform.ShowRewarded("reward_x2", Grant);
                    },
                    onNext: () => Platform.TryInterstitial("win_next", () => GoNext(outcome)),
                    // «Заново» после победы: переиграть на три звезды (просьба 26.09.2026)
                    onRetry: _mode == PlayMode.Career ? () => Restart(false) : (System.Action)null);
                victory.SetPiggy(outcome.Piggy, AllOnShelves.Piggy.Unlocked && AllOnShelves.Piggy.Full);
            });
        }

        /// <summary>Остатки после выполненного заказа улетают к монетам. Возвращает число товаров.</summary>
        int SweepLeftovers()
        {
            var uids = new List<int>();
            foreach (var belt in _st.Belts)
                foreach (var t in belt)
                {
                    uids.Add(t.Uid);
                    if (t.Kind == TokenKind.Bundle) uids.Add(t.Uid2);
                }
            foreach (var c in _st.Cart) if (!c.Spoiled) uids.Add(c.Uid);
            foreach (var sec in _st.Sections) uids.AddRange(sec.Uids);
            int n = 0;
            for (int i = 0; i < uids.Count; i++)
            {
                int uid = uids[i];
                if (!_views.ContainsKey(uid)) continue;
                n++;
                Tween.Delay(this, 0.15f + n * 0.04f, () => { if (hud != null) Vanish(uid, hud.coinsIcon.position, 0.45f); });
            }
            // покупатель, который ещё ждал, уходит довольным — не стоит за окном победы
            if (_st.Customer.Active && customer != null) customer.Leave(true);
            if (n > 0)
            {
                AudioService.Play("sfx_coins_fly");
                Toast.Show($"Остатки — на склад: +{(n + 2) / 3}", "icon_coin");
            }
            return n;
        }

        void GoNext(LevelOutcome o)
        {
            var app = GameApp.I;
            // на карту — когда там открылось новое (ремонт, альбом, завоз, «Час пик»): енот покажет пальцем
            int id = o.LevelId;
            // Торговый дом: открылся (14) и первая новая комната — урок стройки (18, Подсобка)
            // в Среднем и Сложном всё это уже открыто — новостей на карте нет
            bool hubNews = app.Mode == 0 && (id <= Economy.RenoAt || id == Economy.AlbumAt || id == Economy.DailyAt || id == Economy.RushAt
                           || id == Economy.HouseAt || id == House.All[1].UnlockLevel);
            if (_mode == PlayMode.Career && !hubNews && app.CanPlay(id + 1) && !o.DistrictFinished)
                app.PlayLevel(o.LevelId + 1);
            else app.GoHub();
        }

        // ================================================================ поражение

        void OnLose()
        {
            if (TryStaffRescue()) return;
            _finished = true;
            Platform.GameplayStop();
            if (IsRush) { Tween.Delay(this, 0.6f, ShowRushResult); return; }

            var app = GameApp.I;
            bool canLoader = Rules.LoaderCanHelp(_st);
            bool rewardedUsed = _st.ContinueUsed;
            int attempts = app.AttemptsOn(_data.id);
            bool spoiled = _st.Reason == LoseReason.OutOfStock;
            string loseTitle = spoiled ? "Товар испортился!" : "Тележка переполнена!";
            string reason = spoiled ? "Заказ уже не собрать — не хватит товара" : "Ходить больше некуда";
            if (_challengeFail)
            {
                if (_starCap > 0) { loseTitle = "Тележка полновата!"; reason = $"Испытание дня: не больше {_starCap} мест в тележке"; }
                else if (_timeLeft >= 0f) { loseTitle = "Время вышло!"; reason = "Испытание дня: заказ надо было собрать быстрее"; }
                else { loseTitle = "Ходы кончились!"; reason = $"Испытание дня: было {_movesLimit} ходов"; }
            }
            // «почти выиграл»: если до заказа 1–2 набора, говорим об этом — так чаще продолжают, а не бросают
            int setsLeft = 0;
            for (int g = 0; g < _st.GoalNeed.Length; g++) setsLeft += Mathf.Max(0, _st.GoalNeed[g] - _st.GoalDone[g]);
            if (!spoiled && setsLeft > 0 && setsLeft <= 2)
                reason = setsLeft == 1 ? "Остался всего 1 набор!" : "Осталось всего 2 набора!";
            AudioService.Play("sfx_cart_tip");
            Vfx.Play("vfx_cart_overflow", cart.rect.position, 460f);
            // серия побед: сдашься — сгорит; отмотать или грузчик — сохранит (повод продолжить, 03.10.2026)
            int streak = _mode == PlayMode.Career ? Streak.Wins : 0;
            _streakKept = false;

            Tween.Delay(this, 0.8f, () =>
            {
                AudioService.Play("jingle_lose");
                ShowStreakLine(streak);
                if (streak > 0 && defeat.streakLine != null)
                    Vfx.At("vfx_streak_lost", defeat.streakLine.transform.Find("Icon") ?? defeat.streakLine.transform, 200f, delay: 0.45f);
                defeat.Setup(loseTitle, reason,
                    freeUndo: _undo.Count > 0 && !_noUndo,
                    loaderAd: canLoader && !rewardedUsed && !_challengeFail,
                    loaderCoins: canLoader && rewardedUsed && !_loaderCoinsUsed && !_challengeFail,
                    loaderPrice: Economy.LoaderPrice,
                    bonusRetry: attempts >= 1,
                    onFreeUndo: () => defeat.Hide(FreeRewind),
                    onLoaderAd: () => Platform.ShowRewarded("reward_continue", ApplyLoader),
                    onLoaderCoins: () =>
                    {
                        if (app.TrySpend(Economy.LoaderPrice)) { _loaderCoinsUsed = true; ApplyLoader(); }
                        else if (booster != null) booster.Open(BoosterPopup.Shortage(Economy.LoaderPrice - app.Coins, () =>
                        {
                            // монеты пришли — сразу зовём грузчика, игроку не нужно жать ещё раз
                            if (!_loaderCoinsUsed && app.TrySpend(Economy.LoaderPrice)) { _loaderCoinsUsed = true; ApplyLoader(); }
                        }));
                        else Toast.Show("Не хватает монет", "icon_coin");
                    },
                    onBonusRetry: () => Platform.ShowRewarded("reward_cart_plus", () =>
                    {
                        app.RegisterLose(_data, _st, _mode, _streakKept);
                        Restart(true);
                    }),
                    onRetry: () =>
                    {
                        app.RegisterLose(_data, _st, _mode, _streakKept);
                        Platform.TryInterstitial("lose_retry", () => Restart(false));
                    },
                    onMap: () =>
                    {
                        app.RegisterLose(_data, _st, _mode, _streakKept);
                        Platform.TryInterstitial("lose_exit", () => app.GoHub());
                    });
            });
        }

        bool _streakKept;   // «Сохранить серию» нажали в этом окне поражения

        /// <summary>Плашка серии в окне поражения и кнопка «Сохранить» (💎 8 или видео раз за серию).</summary>
        void ShowStreakLine(int streak)
        {
            defeat.ShowStreak(streak, _streakKept, () =>
            {
                var app = GameApp.I;
                if (app.TrySpendGems(Economy.GemStreakSave)) KeepStreak(streak, "gems");
                else if (Streak.CanKeepByVideo) Platform.ShowRewarded("reward_streak_keep", () => { Streak.KeptByVideo(); KeepStreak(streak, "video"); });
                else { AudioService.Play("sfx_nope"); Toast.Show("Не хватает алмазов", "icon_gem_small"); }
            });
        }

        void KeepStreak(int streak, string how)
        {
            _streakKept = true;
            AudioService.Play("sfx_coins_spend");
            Vfx.At("vfx_streak_up", defeat.streakLine.transform.Find("Icon") ?? defeat.streakLine.transform, 240f);
            ShowStreakLine(streak);
            Platform.Metrica("streak_keep", new System.Collections.Generic.Dictionary<string, object> { { "wins", streak }, { "how", how } });
        }

        /// <summary>
        /// Ёж-грузчик на смене: тележка переполнилась — он освобождает место (+1), если от этого появится ход.
        /// Сколько раз за уровень — по его уровню. Окно поражения тогда не показывается.
        /// </summary>
        bool TryStaffRescue()
        {
            if (IsRush || _rescues <= 0 || _st.Reason != LoseReason.Deadlock) return false;
            var probe = _st.Clone();
            probe.CartCapacity += 1;
            probe.Result = GameResult.Playing;
            probe.Reason = LoseReason.None;
            Rules.Evaluate(probe, null);
            if (probe.Result != GameResult.Playing) return false;
            _rescues--;
            _st.CartCapacity += 1;
            _st.Result = GameResult.Playing;
            _st.Reason = LoseReason.None;
            Rules.Evaluate(_st, null);
            _finished = false;
            cart.Configure(_st.CartCapacity, CartSkin());
            AudioService.Play("sfx_loader");
            Vfx.Play("vfx_build_in", cart.rect.position, 380f);
            Toast.Show("Ёж-грузчик освободил место в тележке!", Team.Face("hedgehog"), 2.5f);
            PunchShiftFace("hedgehog");
            // после текущего разбора событий: OnLose вызван изнутри HandleEvents
            Tween.Delay(cart, 0.05f, () => { Sync(false); RefreshHud(); });
            return true;
        }

        /// <summary>Портреты сотрудников на смене — под заказом слева.</summary>
        void BuildShiftRow()
        {
            if (_shiftRow == null)
            {
                var g = (RectTransform)goalsPanel.transform;
                var go = new GameObject("ShiftRow", typeof(RectTransform));
                _shiftRow = (RectTransform)go.transform;
                _shiftRow.SetParent(g.parent, false);
                _shiftRow.anchorMin = g.anchorMin;
                _shiftRow.anchorMax = g.anchorMax;
                _shiftRow.pivot = new Vector2(0.5f, 1f);
                _shiftRow.sizeDelta = new Vector2(g.rect.width, 170f);
                float bottom = g.anchoredPosition.y - g.rect.height * g.pivot.y;
                _shiftRow.anchoredPosition = new Vector2(g.anchoredPosition.x + g.rect.width * (0.5f - g.pivot.x), bottom - 20f);
            }
            foreach (Transform c in _shiftRow) Destroy(c.gameObject);
            _shiftRow.gameObject.SetActive(_shift.Count > 0);
            // бейдж крупный, под мордочкой — звёзды уровня, как в карточке сотрудника в доме (просьба 03.10.2026):
            // раньше был кружок с цифрой, и было непонятно, что она значит. Нажал — что он делает в этом уровне
            // v4: рамка ui_staff_badge_level (244×256) — мордочка в кремовом окошке (x 43…200, y 61…196),
            // на дощечке снизу 4 нарисованных гнезда для звёзд (x 69 + 35,3·k, y 220) — золотые кладём только в открытые
            float bw = _shift.Count > 2 ? 132f : 150f, step = bw + 10f, s = bw / 244f;
            Vector2 B(float px, float py) => new Vector2((px - 122f) * s, (128f - py) * s);
            for (int i = 0; i < _shift.Count; i++)
            {
                var m = _shift[i];
                float x = (i - (_shift.Count - 1) / 2f) * step;
                var frame = UiNode("Staff_" + m.Info.Id, _shiftRow, "ui_staff_badge_level", new Vector2(x, -128f * s - 2f), new Vector2(bw, 256f * s));
                UiNode("Face", frame, Team.Face(m.Info.Id), B(122, 129), new Vector2(128f * s, 128f * s));
                for (int k = 0; k < House.MaxLevel && k < m.Level; k++)
                    UiNode("Star" + (k + 1), frame, "ui_star_big", B(69f + 35.3f * k, 220), new Vector2(31f * s, 31f * s));
                var img = frame.GetComponent<Image>();
                img.raycastTarget = true;
                var member = m;
                frame.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                    Toast.Show($"{member.Info.Name} (ур. {member.Level}): {Team.EffectHere(member, _st)}", Team.Face(member.Info.Id), 3f));
            }
        }

        // ---------------------------------------------------------------- плашка правила Испытания дня (v4.1)

        /// <summary>Плашка ui_plate_value (512×156) под заказом: значок правила слева, справа — что осталось
        /// (время, ходы, места в тележке) или название правила. Только в Испытании дня.</summary>
        const float RuleGap = 6f;     // зазор от низа доски заказа (или ряда сотрудников) до плашки
        readonly Vector3[] _corners = new Vector3[4];

        void BuildRulePlate()
        {
            if (_rulePlate == null)
            {
                var g = (RectTransform)goalsPanel.transform;
                _rulePlate = UiNode("RulePlate", g.parent, "ui_plate_value", Vector2.zero, new Vector2(300f, 300f * 156f / 512f));
                _rulePlate.anchorMin = g.anchorMin;
                _rulePlate.anchorMax = g.anchorMax;
                float bottom = g.anchoredPosition.y - g.rect.height * g.pivot.y;
                _rulePlate.anchoredPosition = new Vector2(g.anchoredPosition.x + g.rect.width * (0.5f - g.pivot.x), bottom - RuleGap);
                float h = _rulePlate.sizeDelta.y;
                var icon = UiNode("Icon", _rulePlate, Challenge.Icons[0], new Vector2(-150f + h * 0.15f, 2f), new Vector2(h * 1.15f, h * 1.15f));
                // текст — по кремовой середине плашки (у краёв деревянные «гвоздики»)
                _ruleText = PlateText("Text", _rulePlate, new Vector2(h * 0.25f, 1f), new Vector2(150f, h * 0.6f), 34f);
                PlateText("Num", icon, new Vector2(0, -h * 0.1f), new Vector2(h * 0.5f, h * 0.5f), 30f);
            }
            _rulePlate.gameObject.SetActive(IsChallenge);
            if (!IsChallenge) return;
            int r = (int)Challenge.Today;
            var ruleIcon = _rulePlate.Find("Icon");
            ruleIcon.GetComponent<Image>().sprite = ArtLibrary.S(Challenge.Icons[r]);
            string num = Challenge.IconNumber(Challenge.Today);
            var numText = ruleIcon.Find("Num").GetComponent<TextMeshProUGUI>();
            numText.gameObject.SetActive(num != null);
            numText.text = num ?? "";
            _ruleShown = -1;
            RefreshRulePlate();
        }

        // высоту доски заказа раскладка меняет позже, чем строится плашка, — ставим её под доску каждый кадр
        void LateUpdate()
        {
            if (_rulePlate == null || !_rulePlate.gameObject.activeSelf) return;
            // низ нарисованной доски (её фон Bg больше контейнера), а при смене — низ ряда сотрудников под ней
            var g = (RectTransform)goalsPanel.transform;
            var board = g.Find("Bg") as RectTransform ?? g;
            board.GetWorldCorners(_corners);
            float cx = (_corners[0].x + _corners[3].x) / 2f, bottom = _corners[0].y;
            if (_shiftRow != null && _shiftRow.gameObject.activeSelf && _shiftRow.childCount > 0)
            {
                var face = (RectTransform)_shiftRow.GetChild(0);
                face.GetWorldCorners(_corners);
                bottom = Mathf.Min(bottom, _corners[0].y);
            }
            var parent = (RectTransform)_rulePlate.parent;
            Vector3 p = parent.InverseTransformPoint(new Vector3(cx, bottom, 0f));
            _rulePlate.localPosition = new Vector3(p.x, p.y - _rulePlate.sizeDelta.y / 2f - RuleGap, 0f);
        }

        TextMeshProUGUI PlateText(string name, Transform parent, Vector2 pos, Vector2 size, float max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = hud.levelText.font;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color32(0x5A, 0x3A, 0x22, 255);
            t.enableAutoSizing = true;
            t.fontSizeMin = 14f;
            t.fontSizeMax = max;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            return t;
        }

        void RefreshRulePlate()
        {
            if (_rulePlate == null || !IsChallenge || _st == null) return;
            string text;
            if (_timeLeft >= 0f) { int sec = Mathf.CeilToInt(_timeLeft); text = $"{sec / 60}:{sec % 60:00}"; }
            else if (_movesLimit > 0) text = $"Ходов: {Mathf.Max(0, _movesLimit - _st.Moves)}";
            else if (_starCap > 0) text = $"Мест: {_st.CartUsed}/{_starCap}";
            else text = Challenge.Titles[(int)Challenge.Today];
            _ruleText.text = text;
            _ruleText.color = _timeLeft >= 0f && _timeLeft <= 10f || _starCap > 0 && _st.CartUsed >= _starCap
                ? new Color32(0xC8, 0x32, 0x28, 255) : new Color32(0x5A, 0x3A, 0x22, 255);
        }

        void TickTimer()
        {
            _timeLeft -= Time.deltaTime;
            int sec = Mathf.CeilToInt(_timeLeft);
            if (sec != _ruleShown)
            {
                _ruleShown = sec;
                RefreshRulePlate();
                if (sec == 30 || sec == 10) { Toast.Show($"Осталось {sec} секунд!", "icon_rule_timer"); Tween.Punch(_rulePlate, 0.15f, 0.35f); }
            }
            if (_timeLeft <= 0f)
            {
                _timeLeft = 0f;
                RefreshRulePlate();
                _challengeFail = true;
                OnLose();
            }
        }

        static RectTransform UiNode(string name, Transform parent, string sprite, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = ArtLibrary.S(sprite);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return rt;
        }

        void PunchShiftFace(string id)
        {
            var f = _shiftRow != null ? _shiftRow.Find("Staff_" + id) : null;
            if (f != null) { Tween.Punch(f, 0.3f, 0.5f); Vfx.At("vfx_staff_skill", f, 190f); }
        }

        void ApplyLoader()
        {
            defeat.Hide(() =>
            {
                _ev.Clear();
                Rules.ApplyLoader(_st, _ev);
                cart.Configure(_st.CartCapacity, CartSkin());
                AudioService.Play("sfx_loader");
                _finished = _st.Result != GameResult.Playing;
                HandleEvents(_ev);
                Sync(false);
                RefreshHud();
                Platform.GameplayStart();
                if (!_finished) Toast.Show("Грузчик принёс ящики: +2 места!");
            });
        }

        // ================================================================ отладка (автотесты из редактора)

        public LevelState DebugState => _st;
        public bool DebugFinished => _finished;

        /// <summary>Делает ход, который предлагает солвер. Для автопрогонов уровней.</summary>
        public bool DebugAutoMove()
        {
            if (_st == null || _finished || _inputLocked) return false;
            var m = BestMove(400);
            if (m == null) return false;
            var r = Rules.ResolveAuto(_st, m.Value);
            Rules.TryGetSource(_st, r, out var tok);
            _views.TryGetValue(tok.Uid, out var v);
            return TryApply(r, v);
        }

        /// <summary>Готовый ход из Rules.LegalMoves — для ботов с разными стилями игры (BotBridge).</summary>
        public bool DebugApplyMove(Move m)
        {
            if (_st == null || _finished || _inputLocked) return false;
            var r = Rules.ResolveAuto(_st, m);
            if (!Rules.IsLegal(_st, r)) return false;
            Rules.TryGetSource(_st, r, out var tok);
            _views.TryGetValue(tok.Uid, out var v);
            return TryApply(r, v);
        }

        /// <summary>Кладёт крайний товар ленты в тележку (для теста поражения).</summary>
        public bool DebugToCart()
        {
            if (_st == null || _finished) return false;
            var m = Move.FromBelt(0, 0, TargetKind.Cart);
            _views.TryGetValue(_st.Belts[0].Count > 0 ? _st.Belts[0][0].Uid : -1, out var v);
            return TryApply(m, v);
        }

        public void DebugUndo() => OnUndo();

        static readonly Random _dbgRng = new Random(12345);

        /// <summary>Случайный допустимый ход (для теста поражения).</summary>
        public bool DebugRandomMove()
        {
            if (_st == null || _finished) return false;
            var moves = Rules.LegalMoves(_st);
            if (moves.Count == 0) return false;
            var m = moves[_dbgRng.Next(moves.Count)];
            Rules.TryGetSource(_st, m, out var tok);
            _views.TryGetValue(tok.Uid, out var v);
            return TryApply(m, v);
        }

        /// <summary>Ход в тележку, если он есть (для теста переполнения и умения Ежа).</summary>
        public bool DebugCartMove()
        {
            if (_st == null || _finished) return false;
            var moves = Rules.LegalMoves(_st);
            var cartMoves = moves.Where(x => x.Dst == TargetKind.Cart && x.Src == SourceKind.Belt).ToList();
            if (cartMoves.Count > 0) moves = cartMoves;
            if (moves.Count == 0) return false;
            var m = moves[_dbgRng.Next(moves.Count)];
            Rules.TryGetSource(_st, m, out var tok);
            _views.TryGetValue(tok.Uid, out var v);
            return TryApply(m, v);
        }

        public void DebugUnlockInput()
        {
            _inputLocked = false;
            mechanicPopup.HideInstant();
            if (shiftPopup != null) shiftPopup.HideInstant();
            tutorial.HideInstant();
        }

        /// <summary>Бот и проверки: окно смены открыто — ставим советованных (или никого) и начинаем.</summary>
        public bool DebugStartShift(bool advice = true)
        {
            if (shiftPopup == null || !shiftPopup.IsOpen) return false;
            tutorial.HideInstant();
            if (advice) foreach (var id in shiftPopup.Advice) shiftPopup.DebugPick(id);
            shiftPopup.startButton.onClick.Invoke();
            return true;
        }

        // ================================================================ подсказка по решению

        /// <summary>
        /// Ходы, ведущие к победе, по состоянию поля. Заполняется решением уровня из генератора и каждым путём,
        /// который подсказка нашла сама. Пока игрок на таком пути (или вернулся на него отменой), подсказка
        /// не ищет заново: на трудных уровнях поиск не успевал и давал «примерный» ход, заводивший в тупик.
        /// </summary>
        readonly Dictionary<ulong, Move> _hintBook = new Dictionary<ulong, Move>();

        void LearnLine(LevelState from, List<Move> moves)
        {
            var s = from.Clone();
            var ev = new List<GameEvent>();
            foreach (var m in moves)
            {
                _hintBook[s.Hash()] = m;
                ev.Clear();
                if (!Rules.Apply(s, m, ev) || s.Result != GameResult.Playing) break;
            }
        }

        Move? BestMove(long timeMs)
        {
            if (_st == null) return null;
            if (_hintBook.TryGetValue(_st.Hash(), out var known) && Rules.IsLegal(_st, Rules.ResolveAuto(_st, known))) return known;
            var line = new List<Move>();
            var m = Solver.Hint(_st, timeMs, line);
            if (line.Count > 0) LearnLine(_st, line);
            return m;
        }

        // ================================================================ предпросмотр в редакторе (без Play)
#if UNITY_EDITOR
        public LevelData PreviewData => Application.isPlaying ? null : _data;
        public LevelState PreviewState => Application.isPlaying ? null : _st;

        /// <summary>Расставляет уровень в сцене без запуска игры: товары создаются как временные (не сохраняются).</summary>
        public void EditorPreview(LevelData data, int moves = 0)
        {
            EditorClearPreview();
            _data = data;
            _mode = PlayMode.Career;
            _st = LevelState.FromData(data, false);
            _finished = false;
            rushHud.SetActive(false);
            goalsPanel.SetActive(true);
            ApplyTheme();
            Layout();
            goals.Build(_st);
            customer.HideInstant();
            for (int i = 0; i < moves; i++)
            {
                var m = Solver.Hint(_st, 400);
                if (m == null) break;
                _ev.Clear();
                if (!Rules.Apply(_st, Rules.ResolveAuto(_st, m.Value), _ev)) break;
            }
            Sync(true);
            customer.HideInstant();   // очередь приходов/уходов — с чистого листа
            if (_st.Customer.Active) customer.Arrive(_st.Customer.Type, _st.Customer.Patience, _st.Customer.MaxPatience, _st.Customer.Look);
            hud.SetLevel($"Уровень {_data.id}", _data.revision ? "hard" : _data.difficulty);
        }

        public void EditorClearPreview()
        {
            ClearViews();
            if (itemsRoot != null)
                for (int i = itemsRoot.childCount - 1; i >= 0; i--)
                {
                    var ch = itemsRoot.GetChild(i).gameObject;
                    if ((ch.hideFlags & HideFlags.DontSave) != 0) DestroyImmediate(ch);
                }
        }

        /// <summary>Переносит текущие положения лент, стеллажа и тележки в BoardLayout (для текущего числа лент).</summary>
        public bool EditorCaptureLayout()
        {
            if (_st == null) return false;
            var c = LayoutConfig;
            var b1 = (RectTransform)belts[0].transform;
            c.belt1Pos = b1.anchoredPosition;
            c.beltScale = b1.localScale.x;
            if (_st.Belts.Length > 1) c.belt2Pos = ((RectTransform)belts[1].transform).anchoredPosition;
            c.shelfPos = shelfRoot.anchoredPosition;
            c.shelfScale = shelfRoot.localScale.x;
            c.cartPos = cart.rect.anchoredPosition;
            c.cartScale = cart.rect.localScale.x;
            return true;
        }

        /// <summary>Положения поля в сцене отличаются от BoardLayout (значит, их подвинули мышкой).</summary>
        public bool EditorLayoutMoved()
        {
            if (_st == null) return false;
            var c = LayoutConfig;
            bool Diff(Vector2 a, Vector2 b) => (a - b).sqrMagnitude > 0.25f;
            bool DiffF(float a, float b) => Mathf.Abs(a - b) > 0.001f;
            var b1 = (RectTransform)belts[0].transform;
            return Diff(b1.anchoredPosition, c.belt1Pos) || DiffF(b1.localScale.x, c.beltScale)
                || (_st.Belts.Length > 1 && Diff(((RectTransform)belts[1].transform).anchoredPosition, c.belt2Pos))
                || Diff(shelfRoot.anchoredPosition, c.shelfPos) || DiffF(shelfRoot.localScale.x, c.shelfScale)
                || Diff(cart.rect.anchoredPosition, c.cartPos) || DiffF(cart.rect.localScale.x, c.cartScale);
        }

        /// <summary>Объекты, которые расставляет код (их положение из сцены не берётся).</summary>
        public IEnumerable<Transform> EditorDrivenTransforms()
        {
            // сцену пересобирают и когда она в полуразобранном виде — ни одну ссылку не считаем гарантированной
            if (belts != null)
                foreach (var b in belts) if (b != null) { yield return b.transform; if (b.windowFrame != null) yield return b.windowFrame; }
            if (shelfRoot != null) yield return shelfRoot;
            if (shelfFrame != null) yield return shelfFrame.transform;
            if (sections != null)
                foreach (var s in sections) if (s != null) yield return s.transform;
            if (cart != null)
            {
                yield return cart.transform;
                if (cart.slots != null)
                    foreach (var s in cart.slots) if (s != null) yield return s;
            }
            if (customer != null) yield return customer.transform;
            if (goals != null && goals.rows != null)
                foreach (var r in goals.rows) if (r != null) yield return r.transform;   // строки заказа расставляет код
            if (tutorial != null && tutorial.hand != null) yield return tutorial.hand;
        }
#endif

        // ================================================================ «Час пик» (ГДД 8.6)

        void StartRush()
        {
            _data = new LevelData { id = 0, district = 1 };
            _rushRng = new Random(System.Environment.TickCount);
            _st = RushRules.Create(_rushRng.Next());
            _finished = false;
            _score = 0;
            _combo = 0;
            _rushTimer = 0f;
            _rushContinueUsed = false;
            _rushRegistered = false;
            _setsWithoutDrop = 0;
            rushHud.SetActive(true);
            goalsPanel.SetActive(false);
            _shift.Clear();
            _rescues = 0;
            if (_shiftRow != null) _shiftRow.gameObject.SetActive(false);
            ApplyTheme();
            Layout();
            customer.HideInstant();
            ClearViews();
            Sync(true);
            hud.SetLevel("Час пик", "");
            RefreshHud();
            AudioService.Music("music_level_loop");
            Platform.GameplayStart();
        }

        void UpdateRush()
        {
            if (_inputLocked || Platform.AdShowing) return;
            float interval = RushRules.Interval(_st);
            _rushTimer += Time.deltaTime;
            rushTimerFill.fillAmount = 1f - Mathf.Clamp01(_rushTimer / interval);
            rushScore.text = _score.ToString();
            rushCombo.text = _combo > 1 && Time.time - _lastSoldTime < 4f ? "×" + _combo : "";
            jamFill.fillAmount = _st.Jam / (float)_st.JamMax;
            if (_rushTimer < interval) return;
            _rushTimer = 0f;
            CompleteItemTweens();
            int jamBefore = _st.Jam;
            _ev.Clear();
            RushRules.Tick(_st, _rushRng, _ev);
            if (_st.Jam > jamBefore) { _setsWithoutDrop = 0; AudioService.Play("sfx_jam_warning"); Tween.Shake((RectTransform)jamFill.transform.parent, 10f, 0.4f); }
            HandleEvents(_ev);
            Sync(false);
        }

        void ShowRushResult()
        {
            var app = GameApp.I;
            bool canContinue = !_rushContinueUsed;
            int coins = 0;
            if (!canContinue) coins = FinishRushRegistration();
            AudioService.Play("jingle_lose");
            rushResult.Setup(_score, Mathf.Max(_score, app.Save.rushBest), coins, canContinue,
                onContinue: () => Platform.ShowRewarded("reward_rush", () =>
                {
                    _rushContinueUsed = true;
                    rushResult.Hide(() =>
                    {
                        var removed = _st.Cart.Select(c => c.Uid).ToArray();
                        _st.Cart.Clear();
                        _st.Jam = 0;
                        _st.Result = GameResult.Playing;
                        _st.Reason = LoseReason.None;
                        _finished = false;
                        foreach (var uid in removed) Vanish(uid, cart.rect.position + new Vector3(900f, 0f, 0f), 0.4f);
                        Sync(false);
                        Platform.GameplayStart();
                    });
                }),
                onAgain: () =>
                {
                    FinishRushRegistration();
                    Platform.TryInterstitial("rush_end", () => GameApp.I.PlayRush());
                },
                onMap: () =>
                {
                    FinishRushRegistration();
                    Platform.TryInterstitial("rush_end", () => GameApp.I.GoHub());
                });
        }

        int FinishRushRegistration()
        {
            if (_rushRegistered) return 0;
            _rushRegistered = true;
            Platform.Metrica("rush_end", new Dictionary<string, object> { { "score", _score }, { "sets", _st.SetsSold } });
            return GameApp.I.RegisterRush(_score);
        }
    }
}
