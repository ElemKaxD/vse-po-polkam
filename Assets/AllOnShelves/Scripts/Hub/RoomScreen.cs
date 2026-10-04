using System.Linq;
using TMPro;
using AllOnShelves.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Комната Торгового дома во весь экран (v3.1, 03.10.2026). Построена — в ней живёт сотрудник, снизу доска
    /// с шестью предметами декора (по порядку), на местах будущих предметов — метки; каждые два предмета —
    /// уровень сотрудника. Не построена — пустая комната в строительных лесах: «Построить» → таймер →
    /// «Заселить!». В Кассе ещё и выручка, которая копится, пока игрока нет.
    /// </summary>
    public class RoomScreen : HubScreen
    {
        public RoomView view;
        public RectTransform frame;              // кадр комнаты (накрывает экран)
        public Image staffFigure;                // жилец в комнате
        public Image buildOverlay;               // леса поверх не построенной комнаты
        public Image darken;
        public GameObject lockOverlay;           // цепи с замком поверх закрытой комнаты (v3.2)
        public TextMeshProUGUI lockLevel;        // «Уровень 18» на бирке замка

        /// <summary>Рост жильца и где его ноги — в пикселях кадра комнаты (низ доски с декором его не закрывает).</summary>
        public const float StaffHeight = 400f, StaffFeet = -235f;
        public RectTransform[] markers = new RectTransform[6];
        public Button[] markerButtons = new Button[6];   // «+» под значком стройки: нажал — предмет строится
        [OptionalRef] public Image buildBg;              // фон стройки во весь экран
        public TextMeshProUGUI roomTitle;
        public TextMeshProUGUI floorText;
        public Button prevRoom, nextRoom;

        [Header("Карточка сотрудника")]
        public RectTransform staffCard;
        public Image staffFace;
        public TextMeshProUGUI staffName;
        public Image[] stars = new Image[4];
        public TextMeshProUGUI skillText;
        public TextMeshProUGUI nextText;

        [Header("Декор")]
        public RectTransform tilesRoot;
        public DecorTile[] tiles = new DecorTile[6];

        [Header("Стройка")]
        public RectTransform buildPanel;
        public TextMeshProUGUI buildTitle;
        public TextMeshProUGUI buildText;
        public Image buildFill;
        public Button buildButton;
        public TextMeshProUGUI buildCost;
        public Button speedAdButton;
        public TextMeshProUGUI speedAdText;
        public Button hammerButton;
        public TextMeshProUGUI hammerText;
        public Button gemFinishButton;                    // «Готово сейчас» за алмазы (v4)
        public TextMeshProUGUI gemFinishText;
        public GameObject boostsRoot;                     // полоса ускорителей под панелью стройки
        public Button[] boostButtons = new Button[5];
        public TextMeshProUGUI[] boostTexts = new TextMeshProUGUI[5];
        public Button moveInButton;

        [Header("Касса")]
        public RectTransform cashPanel;
        public TextMeshProUGUI cashText;
        public Button cashTake, cashDouble;

        int _room;
        float _tick;
        VfxPlayer _dust;
        RoomState _shown;

        public int Room => _room;

        void Awake()
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = tiles[i];
                t.item = i + 1;
                t.buyButton.onClick.AddListener(() => BuyDecor(t));
            }
            prevRoom.onClick.AddListener(() => Step(-1));
            nextRoom.onClick.AddListener(() => Step(1));
            buildButton.onClick.AddListener(Build);
            speedAdButton.onClick.AddListener(SpeedAd);
            hammerButton.onClick.AddListener(Hammer);
            gemFinishButton.onClick.AddListener(GemFinish);
            for (int i = 0; i < boostButtons.Length; i++)
            {
                int k = i;
                boostButtons[i].onClick.AddListener(() => Boost(k));
            }
            moveInButton.onClick.AddListener(MoveIn);
            cashTake.onClick.AddListener(() => TakeCash(false));
            cashDouble.onClick.AddListener(() => Platform.ShowRewarded("house_cash_x2", () => TakeCash(true)));
            for (int i = 0; i < markerButtons.Length; i++)
            {
                int idx = i;
                if (markerButtons[i] != null) markerButtons[i].onClick.AddListener(() => MarkerTap(idx));
            }
        }

        public void OpenRoom(int room)
        {
            _room = room;
            hub.Show(this);
        }

        public override void Open()
        {
            base.Open();
            if (hub != null && hub.tutorial != null) Tween.Delay(this, 0.5f, () => hub.tutorial.RunOnRoom(this));
        }

        void Update()
        {
            _tick -= Time.unscaledDeltaTime;
            if (_tick > 0f) return;
            _tick = 1f;
            var st = House.State(_room);
            if (st != _shown)
            {
                // стройка закончилась, пока игрок смотрит на комнату: кнопка «Заселить» и шаг обучения
                Refresh();
                if (hub != null && hub.tutorial != null) Tween.Delay(this, 0.3f, () => hub.tutorial.RunOnRoom(this));
            }
            else if (st == RoomState.Building || st == RoomState.Ready) RefreshBuild(st);
            if (_room == 0) RefreshCash();
        }

        public override void Refresh()
        {
            var info = House.All[_room];
            var st = House.State(_room);
            _shown = st;
            bool built = st == RoomState.Built;
            int decor = built ? House.DecorCount(_room) : 0;
            view.Show(_room, decor);
            roomTitle.text = info.Name;
            floorText.text = $"Этаж {info.Floor} · {House.FloorNames[info.Floor - 1]}";
            bool locked = st == RoomState.Locked;
            // не построена: фон стройки во весь экран; под замком — кадр комнаты под цепями (видно, что будет)
            bool building = !built && !locked;
            if (buildBg != null) buildBg.gameObject.SetActive(building);
            darken.gameObject.SetActive(!built && !(building && buildBg != null));
            buildOverlay.gameObject.SetActive(building && buildBg == null);
            lockOverlay.SetActive(locked);
            if (locked) lockLevel.text = "Уровень " + info.UnlockLevel;   // кто придёт — в карточке слева
            prevRoom.interactable = Neighbour(-1) >= 0;
            nextRoom.interactable = Neighbour(1) >= 0;

            RefreshStaff(built);
            tilesRoot.gameObject.SetActive(built);
            for (int i = 0; i < markers.Length; i++) markers[i].gameObject.SetActive(false);
            if (built) RefreshDecor(decor);
            buildPanel.gameObject.SetActive(!built && !locked);
            boostsRoot.SetActive(st == RoomState.Building);
            // идёт стройка — пыль и искры молотков над комнатой; эффект — дочерний лесам, уходит вместе с экраном
            if (_dust != null) Vfx.Keep(ref _dust, false, null);
            var dustParent = buildBg != null ? buildBg.transform : buildOverlay.transform;
            Vfx.Keep(ref _dust, st == RoomState.Building, () => Vfx.Play("vfx_build_dust",
                dustParent.TransformPoint(new Vector3(0f, -300f, 0f)), 760f, parent: dustParent));
            if (!built && !locked) RefreshBuild(st);
            cashPanel.gameObject.SetActive(_room == 0 && built);
            if (_room == 0 && built) RefreshCash();
        }

        void RefreshStaff(bool built)
        {
            var staff = House.StaffOf(_room);
            var info = House.All[_room];
            int lvl = staff != null ? House.LevelOf(staff.Id) : 0;
            bool present = built && staff != null && lvl > 0;
            var figure = staff != null ? ArtLibrary.S("staff_" + staff.Id) : null;
            staffFigure.gameObject.SetActive(present && figure != null);
            if (present && figure != null)
            {
                staffFigure.sprite = figure;
                staffFigure.preserveAspect = true;
                var size = new Vector2(StaffHeight * figure.rect.width / figure.rect.height, StaffHeight);
                staffFigure.rectTransform.sizeDelta = size;
                staffFigure.rectTransform.anchoredPosition = new Vector2(RoomView.StaffX(_room, size, StaffFeet), StaffFeet);
            }

            staffCard.gameObject.SetActive(true);
            if (staff == null)
            {
                staffFace.sprite = ArtLibrary.S("icon_decor");
                staffName.text = info.Name;
                skillText.text = info.Note;
                foreach (var s in stars) s.gameObject.SetActive(false);
                nextText.text = built ? $"Декор {House.DecorCount(_room)}/{House.Decor}" : "";
                return;
            }
            staffFace.sprite = FloorPopup.StaffFace(staff.Id);
            staffFace.preserveAspect = true;
            staffFace.color = lvl > 0 || built ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);   // ещё не пришёл — серый
            staffName.text = staff.Name;
            int shown = Mathf.Max(1, lvl);
            for (int k = 0; k < stars.Length; k++)
            {
                stars[k].gameObject.SetActive(true);
                stars[k].sprite = ArtLibrary.S(k < lvl ? "ui_star_big" : "ui_star_big_empty");
            }
            skillText.text = staff.Skill[shown - 1];
            if (staff.Gold && !GameApp.I.Save.goldPath) nextText.text = "Приходит с Золотым путём";
            else if (House.State(_room) == RoomState.Locked) nextText.text = $"Придёт после уровня {info.UnlockLevel}";
            else if (!built) nextText.text = "Въедет, когда комната будет готова";
            else if (lvl >= House.MaxLevel) nextText.text = "Максимальный уровень!";
            else
            {
                int left = House.DecorToNextLevel(_room);
                nextText.text = $"До {lvl + 1}-го уровня: ещё {left} {Plural(left)}";
            }
        }

        static string Plural(int n) => n == 1 ? "предмет" : n < 5 ? "предмета" : "предметов";

        void RefreshDecor(int decor)
        {
            int coins = GameApp.I.Coins;
            for (int i = 0; i < tiles.Length; i++)
            {
                int item = i + 1;
                bool bought = item <= decor, next = item == decor + 1;
                int cost = House.DecorPrice(_room, i);
                tiles[i].Set(RoomView.ItemIcon(_room, item), bought, next, cost, coins >= cost);
                // метки — где встанет ещё не купленный предмет; следующий крупнее
                var m = markers[i];
                m.gameObject.SetActive(!bought);
                if (!bought)
                {
                    m.anchoredPosition = RoomView.ItemCenter(_room, item);
                    m.localScale = Vector3.one * (next ? 1.15f : 0.85f);
                }
            }
        }

        void RefreshBuild(RoomState st)
        {
            var info = House.All[_room];
            buildButton.gameObject.SetActive(st == RoomState.Available);
            speedAdButton.gameObject.SetActive(st == RoomState.Building && House.CanSpeedUpAd(_room));
            boostsRoot.SetActive(st == RoomState.Building);
            gemFinishButton.gameObject.SetActive(st == RoomState.Building);
            // видео есть — две кнопки рядом, кончилось — «Готово сейчас» по центру
            ((RectTransform)gemFinishButton.transform).anchoredPosition = new Vector2(House.CanSpeedUpAd(_room) ? 160f : 0f, -122f);
            moveInButton.gameObject.SetActive(st == RoomState.Ready);
            buildFill.transform.parent.gameObject.SetActive(st == RoomState.Building);
            var staff = House.StaffOf(_room);
            string who = staff != null ? staff.Name : "команда";
            switch (st)
            {
                case RoomState.Locked:
                    buildTitle.text = "Комната под замком";
                    buildText.text = $"Откроется после уровня {info.UnlockLevel}" + (staff != null ? $" — придёт {staff.Name}." : ".");
                    break;
                case RoomState.Available:
                    int busy = House.BusyRoom;
                    buildTitle.text = "Комната не построена";
                    buildText.text = busy >= 0
                        ? $"Сейчас строится «{House.All[busy].Name}». Стройка — одна за раз."
                        : $"Построй комнату — сюда въедет {who}. Стройка {Economy.Dur(info.BuildMinutes)}.";
                    buildButton.interactable = busy < 0;
                    buildCost.text = info.BuildCost.ToString();
                    break;
                case RoomState.Building:
                    buildTitle.text = "Идёт стройка";
                    buildText.text = "Осталось " + House.Time(House.BuildLeft(_room));
                    buildFill.fillAmount = House.BuildProgress(_room);
                    speedAdText.text = $"−{House.SpeedAdMinutes} мин";
                    gemFinishText.text = House.GemFinishCost(_room).ToString();
                    RefreshBoosts();
                    break;
                case RoomState.Ready:
                    buildTitle.text = "Комната готова!";
                    buildText.text = $"Можно заселять: {who} уже ждёт у двери.";
                    break;
            }
        }

        void RefreshCash()
        {
            int cash = House.CashReady, cap = Mathf.Max(1, House.CashCap);
            cashText.text = $"{cash} / {cap}";
            cashTake.interactable = cash > 0;
            cashDouble.gameObject.SetActive(cash >= 20);
        }

        int Neighbour(int dir)
        {
            for (int r = _room + dir; r >= 0 && r < House.Rooms; r += dir)
                if (House.State(r) != RoomState.Locked) return r;
            return -1;
        }

        void Step(int dir)
        {
            int r = Neighbour(dir);
            if (r < 0) return;
            _room = r;
            AudioService.Play("sfx_button");
            if (group != null) { group.alpha = 0.4f; Tween.Fade(group, 1f, 0.2f); }
            Refresh();
        }

        // ------------------------------------------------------------------ действия

        void BuyDecor(DecorTile t)
        {
            int before = House.Level(_room);
            int cost = House.NextDecorPrice(_room);
            bool free = _room == 0 && House.DecorCount(0) == 0 && !GameApp.I.Save.Tutorial(TutorialFreeDecor);
            if (!free && GameApp.I.Coins < cost)
            {
                AudioService.Play("sfx_nope");
                hub.CoinShortage(cost - GameApp.I.Coins, Refresh);
                return;
            }
            if (free) { GameApp.I.Save.SetTutorial(TutorialFreeDecor); GameApp.I.MarkDirty(); }
            if (!House.BuyDecor(_room, free)) return;
            AudioService.Play("sfx_renovate_build");
            hub.RefreshTop();
            Refresh();
            Vfx.At("vfx_coins_spend", t.transform, 240f);
            Vfx.At("vfx_tile_bought", t.transform, 280f, delay: 0.1f);
            foreach (var img in view.PiecesOf(t.item))
            {
                img.transform.localScale = Vector3.one * 0.6f;
                Tween.Scale(img.transform, Vector3.one, 0.45f, Ease.OutBack);
            }
            Vfx.Play("vfx_build_in", view.transform.TransformPoint(RoomView.ItemCenter(_room, t.item)), 420f, delay: 0.15f);
            int after = House.Level(_room);
            var staff = House.StaffOf(_room);
            if (after > before && staff != null && House.LevelOf(staff.Id) > 0)
            {
                AudioService.Play("jingle_stage_complete");
                Celebration.Confetti(40, 0.3f);
                // новый уровень — на бейдже слева: загорается звезда (просьба 03.10.2026: не на фигуре в комнате)
                Vfx.At("vfx_staff_level_up", staffFace.transform, 300f, delay: 0.2f);
                if (after - 1 < stars.Length && stars[after - 1] != null)
                {
                    var star = stars[after - 1].transform;
                    star.localScale = Vector3.zero;
                    Tween.Scale(star, Vector3.one, 0.45f, Ease.OutBack, null, 0.35f);
                    Vfx.At("vfx_staff_badge_up", star, 200f, delay: 0.05f);
                }
                Tween.Delay(this, 0.9f, () => hub.confirmPopup.Info($"{staff.Name}: {after} уровень!",
                    staff.Skill[after - 1], FaceName(staff.Id), Refresh));
            }
            else if (House.DecorCount(_room) == House.Decor) { Celebration.Confetti(40, 0.3f); AudioService.Play("sfx_confetti"); }
            if (hub.tutorial != null) Tween.Delay(this, 1.2f, () => hub.tutorial.RunOnRoom(this));
        }

        public const int TutorialFreeDecor = 36;

        /// <summary>«+» на месте будущего предмета: строится следующий по порядку, иначе подсказка.</summary>
        void MarkerTap(int i)
        {
            int next = House.DecorCount(_room);   // индекс следующего по порядку предмета (0…5)
            if (i == next) { BuyDecor(tiles[i]); return; }
            AudioService.Play("sfx_nope");
            Toast.Show("Сначала — предмет, что подсвечен на полке снизу", "icon_decor");
        }

        void Build()
        {
            var info = House.All[_room];
            if (GameApp.I.Coins < info.BuildCost)
            {
                AudioService.Play("sfx_nope");
                hub.CoinShortage(info.BuildCost - GameApp.I.Coins, Refresh);
                return;
            }
            if (!House.StartBuild(_room)) return;
            AudioService.Play("sfx_renovate_build");
            Vfx.At("vfx_coins_spend", buildButton.transform, 260f);
            hub.RefreshTop();
            Refresh();
            if (hub.tutorial != null) Tween.Delay(this, 0.8f, () => hub.tutorial.RunOnRoom(this));
        }

        void SpeedAd() => Platform.ShowRewarded("house_speed", () =>
        {
            House.SpeedUpAd(_room);
            Toast.Show($"Стройка быстрее на {House.SpeedAdMinutes} минут", "icon_speed_up");
            Refresh();
        });

        void RefreshBoosts()
        {
            var app = GameApp.I;
            for (int i = 0; i < boostButtons.Length; i++)
            {
                int n = app.Boosts(i);
                boostTexts[i].text = n > 0 ? "×" + n : "+";
                boostButtons[i].GetComponent<Image>().color = n > 0 ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            }
            hammerText.text = app.Save.hammers > 0 ? "×" + app.Save.hammers : "+";
            hammerButton.GetComponent<Image>().color = app.Save.hammers > 0 ? Color.white : new Color(1f, 1f, 1f, 0.6f);
        }

        /// <summary>«Готово сейчас» за алмазы; не хватает — окно «Алмазы», после него — обратно к стройке.</summary>
        void GemFinish()
        {
            int cost = House.GemFinishCost(_room);
            if (!House.FinishForGems(_room))
            {
                AudioService.Play("sfx_nope");
                Toast.Show($"Нужно {cost} алмазов", "icon_gem");
                hub.gemShop.Open(hub, 0, Refresh);
                return;
            }
            AudioService.Play("sfx_renovate_build");
            Vfx.At("vfx_gem_burst", buildPanel, 380f);
            Vfx.At("vfx_build_in", buildPanel, 420f, delay: 0.1f);
            Refresh();
        }

        void Boost(int k)
        {
            if (!House.UseBoost(_room, k))
            {
                AudioService.Play("sfx_button");
                hub.gemShop.Open(hub, 1, Refresh);
                return;
            }
            AudioService.Play("sfx_renovate_build");
            Vfx.At("vfx_boost_time", buildPanel, 300f);
            Vfx.At("vfx_staff_skill", boostButtons[k].transform, 220f);
            Toast.Show("Стройка быстрее на " + Economy.Dur(Economy.BoostMinutes[k]), Economy.BoostIcons[k]);
            Refresh();
        }

        void Hammer()
        {
            if (GameApp.I.Save.hammers <= 0) { AudioService.Play("sfx_button"); hub.gemShop.Open(hub, 1, Refresh); return; }
            if (!House.UseHammer(_room)) return;
            AudioService.Play("sfx_renovate_build");
            Vfx.At("vfx_hammer_instant", buildPanel, 420f);
            Vfx.At("vfx_build_in", buildPanel, 420f, delay: 0.3f);
            Refresh();
        }

        void MoveIn()
        {
            if (!House.MoveIn(_room)) return;
            AudioService.Play("jingle_stage_complete");
            Celebration.Flash();
            Celebration.Confetti(50);
            Refresh();
            var staff = House.StaffOf(_room);
            if (staff != null && House.LevelOf(staff.Id) > 0)
            {
                Tween.Punch(staffFigure.transform, 0.15f, 0.5f);
                Vfx.Play("vfx_staff_arrive", StaffCenter(), 520f);
                Tween.Delay(this, 1.0f, () => hub.confirmPopup.Info("Новый жилец!",
                    $"{staff.Name} теперь живёт в комнате «{House.All[_room].Name}» и выходит на смену. Умение: {staff.Skill[0]}",
                    FaceName(staff.Id), Refresh));
            }
            else Tween.Delay(this, 1.0f, () => hub.confirmPopup.Info("Комната готова!", House.All[_room].Note ?? "", "icon_decor", Refresh));
        }

        void TakeCash(bool doubled)
        {
            int c = House.CollectCash(doubled);
            if (c <= 0) return;
            Quests.Add("cash");
            hub.FlyCoinsToTop(cashTake.transform.position, 6);
            Toast.Show($"Выручка кассы: +{c}", "icon_coin");
            RefreshCash();
        }

        /// <summary>Середина фигуры жильца на экране — сюда ставятся эффекты въезда и нового уровня.</summary>
        Vector3 StaffCenter()
        {
            var rt = staffFigure.rectTransform;
            return rt.TransformPoint(rt.rect.center);
        }

        /// <summary>Имя спрайта портрета для окна (ConfirmPopup берёт картинку по имени).</summary>
        public static string FaceName(string id)
        {
            foreach (var n in new[] { $"staff_{id}_face", "av_" + (id == "rabbit" ? "bunny" : id) })
                if (ArtLibrary.S(n) != null) return n;
            return "icon_staff_badge";
        }

        /// <summary>Первая плитка с ценой — для обучения.</summary>
        public DecorTile NextTile => tiles.FirstOrDefault(t => t.buyButton.gameObject.activeSelf && t.buyButton.interactable);
    }
}
