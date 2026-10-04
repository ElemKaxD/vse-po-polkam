using System.Linq;
using AllOnShelves.Game;
using TMPro;
using AllOnShelves.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Сцена Hub (ГДД 12.1): стартовый экран, карта, ремонт, альбом, магазин, ежедневки, лидерборды.
    /// Экраны — объекты в сцене, переключаются этим контроллером.
    /// </summary>
    public class HubController : MonoBehaviour
    {
        [Header("Экраны")]
        public StartScreen startScreen;
        public MapScreen mapScreen;
        public RenovationScreen renovationScreen;
        public AlbumScreen albumScreen;
        public ShopScreen shopScreen;
        public DailyScreen dailyScreen;
        public LeaderboardScreen leaderboardScreen;
        public HouseScreen houseScreen;      // Торговый дом снаружи (03.10.2026)
        public RoomScreen roomScreen;        // комната дома во весь экран

        [Header("Окна")]
        public SettingsPopup settingsPopup;
        public PackPopup packPopup;
        public OfferPopup offerPopup;
        public ConfirmPopup confirmPopup;
        public StarTrackPopup starTrackPopup;
        public WardrobePopup wardrobePopup;
        public ProfilePopup profilePopup;
        public FloorPopup floorPopup;        // окно этажа с ячейками комнат
        public PiggyPopup piggyPopup;        // копилка енота (03.10.2026)
        public GemShopPopup gemShop;         // окно «Алмазы» (экономика v4, 03.10.2026)
        public StreakPopup streakPopup;      // серия побед «Лучший продавец»
        VfxPlayer _streakFlame, _gemShine;
        public DiffPopup diffPopup;          // режимы сложности (v4)
        public Button streakButton;
        public TextMeshProUGUI streakText;
        public Button gemsButton;
        public TextMeshProUGUI gemsText;
        [OptionalRef] public BoosterPopup booster;    // «Не хватает монет» (экономика v2, 30.09.2026)

        [Header("Верхняя панель")]
        public TextMeshProUGUI coinsText;
        public RectTransform coinsIcon;
        public TextMeshProUGUI starsText;
        public Button coinsPlus;
        public Button starsButton;
        public Button settingsButton;
        public Button backButton;
        [Header("Профиль игрока")]
        public Button profileButton;
        public Image profileAvatar;
        public Image profileHalo;
        public TextMeshProUGUI profileLevel;

        [Header("Обучение")]
        public HubTutorial tutorial;

        [Header("Нижнее меню")]
        public GameObject navBar;
        public Button navRenovation, navAlbum, navDaily, navLeaders, navShop, navHouse;
        public GameObject renovationDot, albumDot, dailyDot, houseDot;
        public Button navQuests;                 // «Задания» (v4, 03.10.2026)
        public GameObject questsDot;
        public QuestsScreen questsScreen;

        HubScreen _current;
        bool _offerShownThisSession;
        static bool _sessionStartShown;

        /// <summary>
        /// Меню «Играть / Настройки» при входе. Выключено 29.09.2026 по требованию издателя «геймплей за один клик»:
        /// игрок сразу на карте (настройки — шестерёнка на карте). Вернуть меню — true.
        /// </summary>
        public const bool ShowStartMenu = false;

        void Start()
        {
            if (GameApp.I == null)
            {
                var prefab = Resources.Load<GameObject>("App");
                if (prefab != null) Instantiate(prefab).name = "App";
                else { var go = new GameObject("App"); go.AddComponent<GameApp>(); go.AddComponent<AudioService>(); }
                Platform.Init();
            }
            foreach (var s in new HubScreen[] { startScreen, mapScreen, renovationScreen, albumScreen, shopScreen, dailyScreen, leaderboardScreen,
                                                houseScreen, roomScreen, questsScreen })
            {
                s.hub = this;
                s.gameObject.SetActive(false);
            }
            settingsPopup.HideInstant();
            packPopup.HideInstant();
            offerPopup.HideInstant();
            confirmPopup.HideInstant();
            starTrackPopup.HideInstant();
            wardrobePopup.HideInstant();
            profilePopup.HideInstant();
            floorPopup.HideInstant();
            piggyPopup.HideInstant();
            gemShop.HideInstant();
            streakPopup.HideInstant();
            diffPopup.HideInstant();
            streakButton.onClick.AddListener(() => { AudioService.Play("sfx_button"); streakPopup.Open(); });
            gemsButton.onClick.AddListener(() => { AudioService.Play("sfx_button"); gemShop.Open(this); });
            houseScreen.floorPopup = floorPopup;
            if (booster != null) booster.HideInstant();
            profileButton.onClick.AddListener(() => profilePopup.Open(this));
            starsButton.onClick.AddListener(() => OpenStarTrack());

            // закрытые кнопки не прячем, а гасим: пустое меню на старте выглядит поломкой
            navRenovation.onClick.AddListener(() => Open(renovationScreen, RenoAt, "Ремонт откроется на " + RenoAt + "-м уровне"));
            navAlbum.onClick.AddListener(() => Open(albumScreen, AlbumAt, "Альбом откроется на " + AlbumAt + "-м уровне"));
            navDaily.onClick.AddListener(() => Open(dailyScreen, DailyAt, "Завоз дня откроется на " + DailyAt + "-м уровне"));
            navLeaders.onClick.AddListener(() => Open(leaderboardScreen, LeadersAt, "Рейтинг откроется на " + LeadersAt + "-м уровне"));
            navShop.onClick.AddListener(() => Show(shopScreen));
            navQuests.onClick.AddListener(() => Open(questsScreen, Quests.UnlockAt, "Задания откроются на " + Quests.UnlockAt + "-м уровне"));
            navHouse.onClick.AddListener(() => Open(houseScreen, HouseAt, "Торговый дом откроется на " + HouseAt + "-м уровне"));
            settingsButton.onClick.AddListener(() => settingsPopup.Open());
            coinsPlus.onClick.AddListener(() => Show(shopScreen));
            backButton.onClick.AddListener(Back);
            GameApp.WalletChanged += RefreshTop;
            House.Changed += RefreshTop;
            GameApp.ProgressChanged += RefreshTop;

            Platform.Sticky(true);
            Platform.GameplayStop();
            AudioService.Music("music_menu_loop");
            RefreshTop();
            League.RefreshRanks();   // свежие места в «Сегодня / Неделя»: по ним подводятся итоги
            ChooseFirstScreen();
            Platform.GameReady();
        }

        void OnDestroy()
        {
            GameApp.WalletChanged -= RefreshTop;
            GameApp.ProgressChanged -= RefreshTop;
            House.Changed -= RefreshTop;
        }

        /// <summary>«Назад»: из комнаты — к дому, в окно её этажа; отовсюду остальное — на карту.</summary>
        void Back()
        {
            if (_current == roomScreen)
            {
                int floor = House.All[roomScreen.Room].Floor;
                Show(houseScreen);
                floorPopup.Open(this, floor);
                return;
            }
            Show(mapScreen);
        }

        void ChooseFirstScreen()
        {
            var app = GameApp.I;
            var o = app.LastOutcome;
            app.LastOutcome = null;

            // конец района или блокировка ремонтом — сразу в ремонт. После 3-го уровня — на карту:
            // там енот показывает пальцем на ремонт, и игрок сам заходит (просьба 22.09.2026)
            if (o != null && o.Won && o.Mode == PlayMode.Career && o.DistrictFinished)
            { Show(renovationScreen); AfterScreen(); return; }
            if (app.BlockedByRenovation && o != null) { Show(renovationScreen); AfterScreen(); return; }

            if (ShowStartMenu && !_sessionStartShown && o == null) { _sessionStartShown = true; Show(startScreen); AfterScreen(); return; }
            _sessionStartShown = true;
            Show(mapScreen);
            AfterScreen();
        }

        /// <summary>Пачки наклеек, звёздный сундук и офферы после входа (не больше одного оффера за сессию).</summary>
        void AfterScreen()
        {
            var app = GameApp.I;
            // сначала — палец енота на новой кнопке; пачки, награды и офферы подождут следующего входа
            // (пачку при открытии альбома енот и так предложит открыть)
            if (_current == mapScreen && tutorial != null && tutorial.HasMapStep(mapScreen)) return;
            if (app.Save.pendingPacks > 0 && app.Save.maxReached >= AlbumAt)
            {
                Tween.Delay(this, 0.6f, () => packPopup.OpenPending(this));
                return;
            }
            // итоги дня или недели в рейтинге — окно «Итоги недели: 7-е место!» → «Забрать»
            if (League.HasPending && app.Save.maxReached >= LeadersAt)
            {
                Tween.Delay(this, 0.6f, () => LeaderboardScreen.ClaimWithPopup(this, null, RefreshTop));
                return;
            }
            // награду за звёзды игрок открывает сам: енот один раз показывает кнопку на карте,
            // дальше кнопка просто светится точкой (просьба 26.09.2026). Само окно не выскакивает.
            TryOffer();
        }

        /// <summary>«Звёздный путь»; после закрытия карта обновляется (кнопка, обучение).</summary>
        public void OpenStarTrack()
        {
            starTrackPopup.Open(this, () =>
            {
                RefreshTop();
                if (_current == mapScreen) mapScreen.Refresh();
            });
        }

        /// <summary>Открыто ли какое-нибудь окно поверх экрана (обучение в это время ждёт).</summary>
        public bool AnyPopupOpen =>
            settingsPopup.IsOpen || packPopup.IsOpen || offerPopup.IsOpen || confirmPopup.IsOpen || starTrackPopup.IsOpen
            || wardrobePopup.IsOpen || profilePopup.IsOpen || floorPopup.IsOpen || piggyPopup.IsOpen || gemShop.IsOpen || streakPopup.IsOpen || diffPopup.IsOpen || (booster != null && booster.IsOpen);

        /// <summary>
        /// Не хватает монет (ремонт): видео за монеты или пакет монет. Раньше — только сообщение «сыграй уровень».
        /// onGot — монеты пришли (обновить экран).
        /// </summary>
        public void CoinShortage(int need, System.Action onGot)
        {
            if (booster == null || !Platform.PaymentsAvailable && !GameApp.I.CanWatchCoinsAd)
            {
                Toast.Show($"Не хватает {need} монет — сыграй уровень", "icon_coin");
                return;
            }
            booster.Open(BoosterPopup.Shortage(need, () => { RefreshTop(); onGot?.Invoke(); }));
        }

        public void TryOffer()
        {
            var app = GameApp.I;
            // открылся режим сложности — объявляем один раз (важнее покупок)
            if (_current == mapScreen && app.ModeToAnnounce > 0 && !AnyPopupOpen)
            {
                diffPopup.Open(this, app.ModeToAnnounce);
                return;
            }
            if (_offerShownThisSession || app.Save.maxReached < Core.Economy.StarterOfferAt || !Platform.PaymentsAvailable) return;
            var s = app.Save;
            long now = GameApp.NowUnix;
            // стартовый набор: после 6, 20 и 40 уровня, 72 часа (экономика v2: раньше с 10-го —
            // к этому времени игрок уже упирается в ремонт, а набор должен успеть его выручить)
            if (!s.starterBought)
            {
                int[] triggers = { Core.Economy.StarterOfferAt, 20, 40 };
                int trig = triggers.LastOrDefault(t => s.maxReached >= t);
                if (trig > 0 && s.starterShownAtLevel < trig)
                {
                    s.starterShownAtLevel = trig;
                    s.starterExpires = now + 72 * 3600;
                    app.MarkDirty();
                    _offerShownThisSession = true;
                    offerPopup.Open(MetaCatalog.Product("starter_pack"), s.starterExpires);
                    return;
                }
            }
            // копилка впервые наполнилась — показываем один раз за всю игру
            if (Piggy.ShouldAnnounceFull)
            {
                Piggy.MarkAnnounced();
                _offerShownThisSession = true;
                piggyPopup.Open(this);
                return;
            }
            // «Без рекламы» после 3-го interstitial за сессию, не чаще раза в 2 дня
            if (!s.noAds && Platform.InterstitialsThisSession >= 3 && now - s.noAdsLastShown > 2 * 24 * 3600)
            {
                s.noAdsLastShown = now;
                app.MarkDirty();
                _offerShownThisSession = true;
                offerPopup.Open(MetaCatalog.Product("no_ads"), 0);
                return;
            }
            // набор помощника: 3 поражения подряд и запас отмен 0 — раз в сутки
            long day = now / 86400;
            if (s.failsInRow >= 3 && s.undo <= 0 && s.helperOfferDay != day)
            {
                s.helperOfferDay = day;
                app.MarkDirty();
                _offerShownThisSession = true;
                offerPopup.Open(MetaCatalog.Product("helpers_pack"), 0);
            }
        }

        public void Show(HubScreen screen)
        {
            if (_current != null && _current != screen) _current.Close();
            _current = screen;
            screen.Open();
            // меню внизу — только на карте: на остальных экранах оно закрывало бы содержимое (есть кнопка «назад»)
            navBar.SetActive(screen == mapScreen);
            settingsButton.gameObject.SetActive(true);  // шестерёнка на своём месте на всех экранах
            // у рейтинга доска вкладок нарисована до x≈1460 — монеты и звёзды легли бы на вкладку «Награды»
            bool counters = screen != leaderboardScreen;
            coinsText.transform.parent.gameObject.SetActive(counters);
            starsButton.gameObject.SetActive(counters);
            gemsButton.gameObject.SetActive(counters && GameApp.I != null && GameApp.I.Save.maxReached >= Economy.GemsAt);
            streakButton.gameObject.SetActive(screen == mapScreen && GameApp.I != null && GameApp.I.Save.maxReached >= Streak.ShowAt);
            // огонёк серии на плашке и блик на алмазе счётчика (циклы v4)
            bool flame = streakButton.gameObject.activeSelf && Streak.Wins > 0;
            Vfx.Keep(ref _streakFlame, flame, () => Vfx.Play("vfx_streak_flame", streakButton.transform.position
                + streakButton.transform.TransformVector(new Vector3(-((RectTransform)streakButton.transform).rect.width * 0.36f, 4f, 0f)), 120f, parent: streakButton.transform));
            bool shine = gemsButton.gameObject.activeSelf;
            Vfx.Keep(ref _gemShine, shine, () =>
            {
                var gi = gemsButton.transform.Find("GemIcon") ?? gemsButton.transform;
                return Vfx.Play("vfx_gem_shine", gi.position, 90f, parent: gemsButton.transform);
            });
            backButton.gameObject.SetActive(screen != startScreen && screen != mapScreen);
            AudioService.Music("music_menu_loop");
            RefreshTop();
        }

        public void RefreshTop()
        {
            var app = GameApp.I;
            if (app == null) return;
            coinsText.text = app.Coins.ToString();
            gemsText.text = app.Gems.ToString();
            streakText.text = "×" + Streak.Wins;
            starsText.text = app.Save.StarsTotal.ToString();
            RefreshProfile(app);
            var s = app.Save;
            int d = app.StageDistrict;
            renovationDot.SetActive(app.StageOpen(d) && app.Coins >= app.CheapestMissing(d) && app.CheapestMissing(d) > 0);
            albumDot.SetActive(s.pendingPacks > 0 && s.maxReached >= AlbumAt);
            dailyDot.SetActive(app.CanClaimDaily || app.CanClaimChest2 || app.DailyLevelAvailable);
            Gate(navDaily, app.DailyUnlocked);
            Gate(navQuests, Quests.Unlocked);
            questsDot.SetActive(Quests.ReadyCount > 0);
            Gate(navAlbum, s.maxReached >= AlbumAt);
            Gate(navLeaders, s.maxReached >= LeadersAt);
            navShop.gameObject.SetActive(Platform.PaymentsAvailable);
            Gate(navRenovation, s.maxReached >= RenoAt);
            Gate(navHouse, s.maxReached >= HouseAt);
            houseDot.SetActive(House.HasNews);
            coinsPlus.gameObject.SetActive(Platform.PaymentsAvailable);
        }

        /// <summary>Кнопка-аватар: надетый скин енота и ореол, под ним номер уровня.</summary>
        void RefreshProfile(GameApp app)
        {
            if (profileAvatar != null)
            {
                profileAvatar.sprite = app.FaceSprite(raccoonOnly: false);
                profileAvatar.preserveAspect = true;
            }
            if (profileHalo != null)
            {
                var halo = app.WornSprite("halo");
                profileHalo.gameObject.SetActive(halo != null);
                if (halo != null) { profileHalo.sprite = halo; profileHalo.preserveAspect = true; }
            }
            if (profileLevel != null) profileLevel.text = app.CurrentLevel.ToString();
        }

        public const int RenoAt = Core.Economy.RenoAt, AlbumAt = Core.Economy.AlbumAt, DailyAt = Core.Economy.DailyAt,
                         LeadersAt = Core.Economy.LeadersAt, HouseAt = Core.Economy.HouseAt;

        /// <summary>Закрытая кнопка меню видна, но погашена: игрок понимает, что здесь что-то будет.</summary>
        static void Gate(Button b, bool open)
        {
            b.gameObject.SetActive(true);
            if (b.image != null) b.image.color = open ? Color.white : new Color(0.62f, 0.58f, 0.55f, 0.75f);
            foreach (var g in b.GetComponentsInChildren<Graphic>(true))
                if (g != b.image) g.color = open ? g.color : new Color(g.color.r, g.color.g, g.color.b, 0.55f);
        }

        void Open(HubScreen screen, int need, string hint)
        {
            if (GameApp.I.Save.maxReached >= need) { Show(screen); return; }
            Toast.Show(hint);
        }

        /// <summary>Алмазы летят к счётчику алмазов (как монеты к монетам) — с бирюзовым шлейфом vfx_gems_fly.</summary>
        public void FlyGemsToTop(Vector3 from, int count = 5)
        {
            var sp = ArtLibrary.S("icon_gem_small");
            if (sp == null || gemsButton == null) return;
            var target = gemsButton.transform.Find("GemIcon") ?? gemsButton.transform;
            for (int i = 0; i < Mathf.Clamp(count, 1, 8); i++)
            {
                var go = new GameObject("fx_gem", typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>();
                img.sprite = sp; img.raycastTarget = false; img.preserveAspect = true;
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.sizeDelta = new Vector2(52, 52);
                rt.position = from;
                Tween.Move(rt, target.position, 0.6f, Ease.InQuad, 140f, () => Destroy(go), i * 0.05f);
            }
            Vfx.At("vfx_gem_burst", target, 160f, delay: 0.62f);
            AudioService.Play("sfx_coins_fly");
        }

        public void FlyCoinsToTop(Vector3 from, int count = 5)
        {
            var sp = ArtLibrary.S("icon_coin");
            if (sp == null) return;
            for (int i = 0; i < Mathf.Clamp(count, 1, 8); i++)
            {
                var go = new GameObject("fx_coin", typeof(RectTransform), typeof(Image));
                var img = go.GetComponent<Image>();
                img.sprite = sp; img.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.sizeDelta = new Vector2(56, 56);
                rt.position = from;
                Tween.Move(rt, coinsIcon.position, 0.6f, Ease.InQuad, 140f, () => Destroy(go), i * 0.05f);
            }
            AudioService.Play("sfx_coins_fly");
        }
    }
}
