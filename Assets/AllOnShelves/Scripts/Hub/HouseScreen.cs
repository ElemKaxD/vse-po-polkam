using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Торговый дом снаружи (v3.1/v3.2, 03.10.2026): один дом на площади. Закрытый этаж — в лесах, поперёк цепь
    /// с замком и табличкой «Уровень N». У открытого этажа окно = комната: заколочено (не построена), с лестницей
    /// (строится), светится (построена), с замком (сотрудник ещё не открыт). Нажал на этаж — окно этажа с ячейками.
    /// Дом собран из двух кадров одного рисунка: готовый house_open и house_scaffold, лесами закрыты полосы этажей.
    /// </summary>
    public class HouseScreen : HubScreen
    {
        // полосы этажей в пикселях картинки дома 1024×1536 (сверху вниз): этаж 1 — низ с навесом
        public static readonly Vector2[] FloorBand = { new Vector2(995, 1536), new Vector2(745, 995), new Vector2(495, 745), new Vector2(0, 495) };
        // окна комнат (x0, y0, x1, y1 в пикселях картинки): у этажа 1 — витрина, дверь, витрина
        public static readonly Vector4[] Windows =
        {
            new Vector4(225, 1125, 395, 1330), new Vector4(408, 1135, 612, 1400), new Vector4(632, 1125, 800, 1330),
            new Vector4(218, 795, 362, 920), new Vector4(434, 795, 586, 920), new Vector4(662, 795, 810, 920),
            new Vector4(218, 545, 362, 670), new Vector4(434, 545, 586, 670), new Vector4(662, 545, 810, 670),
            new Vector4(218, 300, 362, 425), new Vector4(434, 300, 586, 425), new Vector4(662, 300, 810, 425),
        };
        public static Vector2 HousePx(float x, float y) => new Vector2(x - 512f, 768f - y);

        public RectTransform house;              // контейнер 1024×1536 в пикселях картинки
        public RectTransform[] scaffold = new RectTransform[4];   // маска полосы этажа с лесами
        public RectTransform[] chains = new RectTransform[4];     // цепь, замок и табличка закрытого этажа
        public TextMeshProUGUI[] chainText = new TextMeshProUGUI[4];
        public Button[] floorButtons = new Button[4];
        public Image[] windows = new Image[12];
        public TextMeshProUGUI[] windowText = new TextMeshProUGUI[12];
        public TextMeshProUGUI signText;
        public Button cashButton;                // выручка кассы — подставка с кассой внизу по центру
        [OptionalRef] public Image cashIcon;
        public TextMeshProUGUI cashText;
        public RectTransform raccoon;            // енот площади — он же говорит в обучении дома
        [OptionalRef] public Image crane;        // крана больше нет (03.10.2026)
        public Button piggyButton;               // копилка енота — подставка внизу по центру
        public Image piggyImage;
        public TextMeshProUGUI piggyText;
        [OptionalRef] public FloorPopup floorPopup;

        float _tick;
        VfxPlayer _piggyGlow, _dust;
        int _dustRoom = -1;

        void Awake()
        {
            for (int i = 0; i < floorButtons.Length; i++)
            {
                int floor = i + 1;
                floorButtons[i].onClick.AddListener(() => OpenFloor(floor));
            }
            cashButton.onClick.AddListener(Collect);
            piggyButton.onClick.AddListener(() => { AudioService.Play("sfx_button"); hub.piggyPopup.Open(hub); });
        }

        public override void Open()
        {
            House.EnsureStarted();
            base.Open();
            RevealNewFloors();
            if (hub != null && hub.tutorial != null) Tween.Delay(this, 1.2f, () => hub.tutorial.RunOnHouse(this));
        }

        void Update()
        {
            _tick -= Time.unscaledDeltaTime;
            if (_tick > 0f) return;
            _tick = 1f;
            RefreshWindows();
            RefreshCash();
            RefreshCrane();
            RefreshPiggy();
        }

        void RefreshPiggy()
        {
            bool on = Piggy.Unlocked && House.IsBuilt(0);
            piggyButton.gameObject.SetActive(on);
            if (!on) { Vfx.Keep(ref _piggyGlow, false, null); return; }
            piggyImage.sprite = ArtLibrary.S(Piggy.Sprite);
            piggyText.text = Piggy.Full ? "Полна!" : Piggy.Coins.ToString();
            Vfx.Keep(ref _piggyGlow, Piggy.Full, () => Vfx.Behind("vfx_piggy_glow", piggyImage.transform, 380f));
        }

        /// <summary>Свинка на площади (для обучения).</summary>
        public RectTransform PiggyTarget => piggyButton.gameObject.activeInHierarchy ? (RectTransform)piggyButton.transform : null;

        public override void Refresh()
        {
            var s = GameApp.I.Save;
            for (int f = 1; f <= House.Floors; f++)
            {
                bool open = House.FloorOpen(f) && f <= Mathf.Max(1, s.houseFloorsSeen);
                scaffold[f - 1].gameObject.SetActive(!open);
                // цепи висят, пока этаж не показан открытым: падают вместе с лесами (RevealNewFloors)
                chains[f - 1].gameObject.SetActive(!open && (f > 1 || !House.FloorOpen(f)));
                chainText[f - 1].text = "Уровень " + House.FloorUnlockLevel(f);
            }
            signText.text = House.AllDone ? "<color=#FFE27A>Торговый дом</color>" : "Торговый дом";
            RefreshWindows();
            RefreshCash();
            if (crane != null) crane.gameObject.SetActive(House.BusyRoom >= 0);
            RefreshDust();
            RefreshPiggy();
        }

        void RefreshWindows()
        {
            for (int i = 0; i < House.Rooms; i++)
            {
                var img = windows[i];
                var t = windowText[i];
                bool floorOpen = House.FloorOpen(House.All[i].Floor);
                string sp = null, label = "";
                switch (House.State(i))
                {
                    case RoomState.Locked: sp = floorOpen ? "icon_lock" : null; label = floorOpen ? "Ур. " + House.All[i].UnlockLevel : ""; break;
                    case RoomState.Available: sp = "house_window_boards"; break;
                    case RoomState.Building: sp = "house_window_build"; label = House.Time(House.BuildLeft(i)); break;
                    case RoomState.Ready: sp = "icon_move_in"; label = "Готово!"; break;
                }
                // на этаже в лесах окна не видны — лишние значки там только мешают
                if (!floorOpen) { sp = null; label = ""; }
                img.gameObject.SetActive(sp != null);
                if (sp != null && img.sprite?.name != sp) { img.sprite = ArtLibrary.S(sp); img.preserveAspect = true; }
                t.text = label;
                t.gameObject.SetActive(label.Length > 0);
            }
        }

        void RefreshCrane()
        {
            bool on = House.BusyRoom >= 0;
            RefreshDust();
            if (crane == null || crane.gameObject.activeSelf == on) return;
            crane.gameObject.SetActive(on);
            if (on) { crane.transform.localScale = Vector3.zero; Tween.Scale(crane.transform, Vector3.one, 0.4f, Ease.OutBack); }
        }

        /// <summary>Пыль и искры молотков у окна комнаты, которая строится (цикл, 03.10.2026).</summary>
        void RefreshDust()
        {
            int room = House.BusyRoom;
            bool on = room >= 0 && House.State(room) == RoomState.Building && House.FloorOpen(House.All[room].Floor);
            if (_dust != null && room != _dustRoom) Vfx.Keep(ref _dust, false, null);
            _dustRoom = room;
            Vfx.Keep(ref _dust, on, () => Vfx.Play("vfx_build_dust", windows[room].transform.position, 380f, parent: windows[room].transform.parent));
        }

        /// <summary>
        /// Выручка кассы — всегда рядом с копилкой; пока Касса (комната Белки) не построена — серая и не собирается.
        /// </summary>
        void RefreshCash()
        {
            bool unlocked = House.Unlocked;
            cashButton.gameObject.SetActive(unlocked);
            if (!unlocked) return;
            bool built = House.IsBuilt(0);
            int cash = built ? House.CashReady : 0;
            cashText.text = built ? (cash > 0 ? "+" + cash : "0") : "Касса";
            var tint = built ? Color.white : new Color(0.6f, 0.58f, 0.55f, 0.8f);
            cashButton.image.color = tint;
            if (cashIcon != null) cashIcon.color = tint;
        }

        /// <summary>Этаж открылся впервые: леса падают (на первом входе — с этажа 1, это и есть обучение).</summary>
        void RevealNewFloors()
        {
            var s = GameApp.I.Save;
            int open = House.FloorsOpen;
            if (open <= s.houseFloorsSeen) return;
            int from = s.houseFloorsSeen + 1;
            s.houseFloorsSeen = open;
            GameApp.I.MarkDirty();
            for (int f = from; f <= open; f++)
            {
                var band = scaffold[f - 1];
                band.gameObject.SetActive(true);
                var home = band.anchoredPosition;
                var cg = band.GetComponent<CanvasGroup>();
                if (cg == null) cg = band.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                float delay = 0.5f + (f - from) * 0.5f;
                var chain = chains[f - 1];
                Tween.Delay(band, delay, () =>
                {
                    AudioService.Play("sfx_unlock");
                    if (chain.gameObject.activeSelf)
                    {
                        chain.gameObject.SetActive(false);
                        Vfx.Play("vfx_chains_fall", chain.position, 460f);
                    }
                    else Vfx.Play("vfx_build_in", band.position, 520f);
                    Tween.MoveAnchored(band, home + new Vector2(0f, -140f), 0.7f, Ease.InQuad);
                    Tween.Fade(cg, 0f, 0.7f, () =>
                    {
                        band.anchoredPosition = home;
                        cg.alpha = 1f;
                        band.gameObject.SetActive(false);
                        Celebration.Confetti(30);
                    });
                });
            }
        }

        public void OpenFloor(int floor)
        {
            if (!House.FloorOpen(floor))
            {
                AudioService.Play("sfx_nope");
                Toast.Show($"Этаж {floor} откроется после уровня {House.FloorUnlockLevel(floor)}", "icon_lock");
                return;
            }
            AudioService.Play("sfx_button");
            floorPopup.Open(hub, floor);
        }

        void Collect()
        {
            if (!House.IsBuilt(0))
            {
                AudioService.Play("sfx_nope");
                Toast.Show("Выручка пойдёт, когда построишь Кассу — комнату Белки", "icon_lock");
                return;
            }
            int c = House.CollectCash(false);
            if (c <= 0) { Toast.Show("Касса пока пустая — монеты капают со временем", "icon_coin"); return; }
            Vfx.At("vfx_cash_collect", cashButton.transform, 320f);
            hub.FlyCoinsToTop(cashButton.transform.position, 6);
            Toast.Show($"Выручка кассы: +{c}", "icon_coin");
            Refresh();
        }

        /// <summary>Где на экране этаж (для обучения).</summary>
        public RectTransform FloorTarget(int floor) => (RectTransform)floorButtons[floor - 1].transform;
    }
}
