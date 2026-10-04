using System;
using AllOnShelves.Game;
using AllOnShelves.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Обучение в хабе. На карте — по одному шагу, когда нужная кнопка уже открылась
    /// (ремонт — с 3-го уровня, альбом — с 6-го, завоз — с 8-го, «Час пик» — с 12-го; см. Economy).
    /// Нажатие на подсказку с кнопкой меню сразу открывает этот экран, а внутри ремонта и альбома
    /// енот показывает, что куда жать и что это даёт. Каждый шаг — один раз (tutorialBits, биты 20+).
    /// </summary>
    public class HubTutorial : MonoBehaviour
    {
        public TutorialOverlay overlay;
        public HubController hub;

        class Step
        {
            public int bit;
            public int after;                              // сколько уровней уже пройдено
            public int until = int.MaxValue;               // после этого уровня шаг протух и больше не нужен
            public string text;
            public Func<MapScreen, RectTransform> target;
            public bool opens;                             // нажатие открывает экран под рукой
            public Func<GameApp, bool> when;               // дополнительное условие (не по номеру уровня)
            public bool focus;                             // затемнить экран, жать только на цель
        }

        // порядок — по уровню, с которого шаг доступен
        static readonly Step[] Steps =
        {
            new Step { bit = 20, after = 0, until = 0, text = "Это карта района. Жми на ценник с номером — там уровень.",
                       target = m => Node(m, 1) },
            new Step { bit = 21, after = 1, until = 1, text = "Готово! Следующий ценник уже открыт — иди дальше.",
                       target = m => Node(m, 2) },
            new Step { bit = 22, after = HubController.RenoAt, text = "Ларёк пора подлатать! Заходи в ремонт — покажу, что там.",
                       target = m => Rt(m.hub != null ? m.hub.navRenovation : null), opens = true },
            new Step { bit = 24, after = HubController.AlbumAt, text = "Открылся альбом! За победы капают наклейки — заглянем?",
                       target = m => Rt(m.hub != null ? m.hub.navAlbum : null), opens = true },
            new Step { bit = 23, after = Core.Economy.DailyAt, text = "Сюда каждый день привозят подарок. Не забывай забирать!",
                       target = m => Rt(m.dailyButton), opens = true },
            new Step { bit = 25, after = Core.Economy.RushAt, text = "«Час пик» — быстрый режим на время: ходов нет, есть секунды.",
                       target = m => Rt(m.rushButton) },
            // Торговый дом (03.10.2026): дом нашей команды, этажи открываются вместе с сотрудниками
            new Step { bit = HouseMap, after = Core.Economy.HouseAt, focus = true,
                       text = "Открылся Торговый дом! Там живёт наша команда: сотрудники помогают проходить уровни. Жми сюда!",
                       target = m => Rt(m.hub != null ? m.hub.navHouse : null), opens = true },
            // новая комната открылась (первый раз после Кассы) — ведём строить
            new Step { bit = HouseBuildMap, after = Core.Economy.HouseAt, focus = true, when = a => House.Unlocked && FirstAvailable() >= 0 && House.BusyRoom < 0,
                       text = "К нам просится новый сотрудник! Ему нужна комната — пойдём построим.",
                       target = m => Rt(m.hub != null ? m.hub.navHouse : null), opens = true },
            // серия побед (03.10.2026): после первой серии из 2 побед — что это и как не потерять
            new Step { bit = 57, after = Streak.ShowAt, when = a => Streak.Wins >= 2,
                       text = "Ты выигрываешь подряд — это серия! Пока она горит, в начале уровня даю подсказку и место в тележке. Сдашься — сгорит.",
                       target = m => Rt(m.hub != null ? m.hub.streakButton : null), opens = true },
            // награда за звёзды: показываем кнопку один раз, дальше игрок забирает сам, когда захочет
            new Step { bit = 31, after = 0, when = a => a.StarRewardsReady > 0,
                       text = "За звёзды с уровней дают награды. Вот здесь их копят — забирай, когда захочешь.",
                       target = m => Rt(m.starChestButton), opens = true },
        };

        /// <summary>Рот енота с валиком (chr_raccoon_paint) в долях картинки — сюда смотрит хвостик облачка.</summary>
        static readonly Vector2 PaintRaccoonMouth = new Vector2(0.64f, 0.60f);

        // биты шагов внутри экранов
        const int RenoBuy = 26, RenoStage = 27, RenoPlay = 28, AlbumPack = 29, AlbumProgress = 30;
        // Торговый дом (обучение в фокусе, 03.10.2026). Первый вход: кнопка на карте → что такое дом → этаж →
        // замки → Касса → карточка Белки → первый предмет (бесплатно — RoomScreen.TutorialFreeDecor) → уровень →
        // выручка → «на смену перед уровнем». Первая стройка: карта → этаж → ячейка → «Построить» → ускорение →
        // «Заселить». Перед уровнем — окно смены (биты 49–51, GameController).
        const int HouseMap = 32, HouseFloor = 33, HouseCell = 34, HouseDecor = 35, HouseLevel = 37;
        const int HouseIntro = 38, HouseLocks = 39, HouseStaff = 40, HouseCash = 41, HouseShift = 42;
        const int HouseBuildMap = 43, HouseBuildFloor = 44, HouseBuildCell = 45, HouseBuild = 46, HouseSpeed = 47, HouseMoveIn = 48;
        const int HousePiggy = 52;   // копилка (49–51 — окно смены в GameController)
        const int HouseIntro2 = 53;  // вторая реплика вступления (03.10.2026)

        /// <summary>Рот енота с указкой (chr_raccoon_point) в долях картинки — сюда смотрит хвостик облачка.</summary>
        static readonly Vector2 PointRaccoonMouth = new Vector2(0.60f, 0.66f);

        static RectTransform Rt(Component c) =>
            c != null && c.gameObject.activeInHierarchy ? (RectTransform)c.transform : null;

        static RectTransform Node(MapScreen m, int level)
        {
            if (m.nodes == null || level < 1 || level > m.nodes.Length) return null;
            var n = m.nodes[level - 1];
            return n != null && n.gameObject.activeInHierarchy ? (RectTransform)n.transform : null;
        }

        bool Busy => overlay == null || overlay.Visible || GameApp.I == null || (hub != null && hub.AnyPopupOpen);

        bool _retryMap;

        /// <summary>Шаг на карте ждал закрытия окна — показываем, как только окно закрылось.</summary>
        void Update()
        {
            if (!_retryMap || Busy || hub == null || !hub.mapScreen.IsOpen) return;
            _retryMap = false;
            RunOnMap(hub.mapScreen);
        }

        /// <summary>Показывает шаг один раз; onTap — что сделать по нажатию.</summary>
        bool Once(int bit, string text, RectTransform target, Action onTap = null)
        {
            var save = GameApp.I.Save;
            if (save.Tutorial(bit) || target == null) return false;
            save.SetTutorial(bit);
            GameApp.I.MarkDirty();
            overlay.Show(text, target, onTap ?? (() => { }));
            return true;
        }

        /// <summary>
        /// Шаг в фокусе: экран темнеет, цель светится, рука показывает. strict — дальше только нажатием на цель
        /// (onTap делает то же, что кнопка), иначе — нажатием куда угодно.
        /// </summary>
        bool Focus(int bit, string text, RectTransform target, Action onTap = null, bool strict = false, RectTransform light = null)
        {
            var save = GameApp.I.Save;
            if (save.Tutorial(bit) || target == null) return false;
            save.SetTutorial(bit);
            GameApp.I.MarkDirty();
            overlay.Focus(text, target, onTap, strict, light);
            return true;
        }

        /// <summary>Первая открытая, но не построенная комната (для обучения стройке).</summary>
        static int FirstAvailable()
        {
            for (int r = 1; r < House.Rooms; r++)
                if (House.State(r) == RoomState.Available) return r;
            return -1;
        }

        /// <summary>Идёт обучение первой стройке (ещё не нажали «Построить»).</summary>
        static bool BuildLesson => GameApp.I.Save.Tutorial(HouseBuildMap) && !GameApp.I.Save.Tutorial(HouseBuild);

        /// <summary>Вызывается картой после Refresh: показывает первый неподсказанный шаг.</summary>
        public void RunOnMap(MapScreen map)
        {
            if (map == null) return;
            if (Busy) { _retryMap = overlay != null && !overlay.Visible; return; }
            overlay.SetSpeaker(null, Vector2.zero);
            overlay.StandByTarget = true;   // на карте енот встаёт у ценника, облачко над ним
            var s = FindStep(map, out var target);
            if (s == null) return;
            var button = target.GetComponent<Button>();
            Action tap = s.opens && button != null ? () => button.onClick.Invoke() : (Action)null;
            if (s.focus) Focus(s.bit, s.text, target, tap, strict: tap != null);
            else Once(s.bit, s.text, target, tap);
            map.RefreshStars();   // «Звёздный путь» оживает только на своём шаге обучения
        }

        /// <summary>Есть ли на карте неподсказанный шаг (тогда окна-награды подождут: сначала — палец).</summary>
        public bool HasMapStep(MapScreen map) => GameApp.I != null && map != null && FindStep(map, out _) != null;

        Step FindStep(MapScreen map, out RectTransform target)
        {
            target = null;
            var save = GameApp.I.Save;
            foreach (var s in Steps)
            {
                if (save.Tutorial(s.bit)) continue;
                // шаг про первый ценник бессмысленен, когда уровень уже пройден: гасим его молча,
                // иначе палец учит первому уровню, а енот на карте стоит уже на втором (баг 23.09.2026)
                if (save.maxReached > s.until) { save.SetTutorial(s.bit); GameApp.I.MarkDirty(); continue; }
                if (save.maxReached < s.after) continue;
                if (s.when != null && !s.when(GameApp.I)) continue;
                // первые два шага — про карту первого района
                if (s.after <= 1 && GameApp.I.CurrentDistrict != 1) continue;
                target = s.target(map);
                if (target == null) continue;
                return s;
            }
            return null;
        }

        /// <summary>Ремонт: купить предмет → что даёт полоса этапа → где брать монеты.</summary>
        public void RunOnReno(RenovationScreen reno)
        {
            if (Busy || reno == null || !reno.IsOpen) return;
            // в ремонте говорит енот с валиком, который уже стоит на экране, — второго енота не выводим
            overlay.SetSpeaker(reno.raccoon, PaintRaccoonMouth);
            var app = GameApp.I;
            // палец — на плитку нижней панели: там у улучшения есть и картинка, и название, и цена
            var first = reno.tiles.Length > 0 ? reno.tiles[0] : null;
            if (app.Save.boughtItems.Count == 0 && first != null && first.item != null && first.buyButton.gameObject.activeInHierarchy)
            {
                Once(RenoBuy, $"Это ремонт ларька. Жми кнопку с ценой — «{first.item.Name}» появится на магазине. " +
                              "Платишь монетами за уровни.",
                     (RectTransform)first.buyButton.transform, () => first.buyButton.onClick.Invoke());
                return;
            }
            if (app.Save.boughtItems.Count == 0) return;
            // шаг про полосу ремонта убран вместе с полосой (03.10.2026): про этап говорит строка на табличке
            if (!app.Save.Tutorial(RenoStage)) { app.Save.SetTutorial(RenoStage); app.MarkDirty(); }
            Once(RenoPlay, "Монет не хватает? Играй уровни — за каждую победу дают монеты.",
                 (RectTransform)reno.playButton.transform);
        }

        /// <summary>Альбом: где открыть пачку и что даёт собранный отдел.</summary>
        public void RunOnAlbum(AlbumScreen album)
        {
            if (Busy || album == null || !album.IsOpen) return;
            overlay.SetSpeaker(null, Vector2.zero);
            if (Once(AlbumPack, "Это альбом наклеек. Жми сюда — откроешь пачку, наклейки лягут на страницы.",
                     (RectTransform)album.openPackButton.transform, () => Later(() => RunOnAlbum(album))))
                return;
            Once(AlbumProgress, "Собери все 9 наклеек отдела — получишь золотую наклейку и монеты.",
                 (RectTransform)album.progressFill.transform.parent);
        }

        const int LeadTabs = 54, LeadMe = 55, LeadRewards = 56;   // рейтинг (03.10.2026)
        const int DailyChallengeBit = 58, DailyClaimBit = 59;     // «Завоз дня» и Испытание дня (v4)

        /// <summary>«Завоз дня»: где забрать ящик → что такое Испытание дня.</summary>
        public void RunOnDaily(DailyScreen d)
        {
            if (Busy || d == null || !d.IsOpen || !GameApp.I.DailyUnlocked) return;
            overlay.SetSpeaker(null, Vector2.zero);
            if (GameApp.I.CanClaimDaily && Once(DailyClaimBit, "Каждый день грузовичок привозит ящик. Забирай! Седьмой день подряд — большой сундук.",
                                                 (RectTransform)d.claimButton.transform, () => Later(() => RunOnDaily(d))))
                return;
            Once(DailyChallengeBit, "А это Испытание дня: каждый день новое правило — без отмен, без подсказок или ходов в обрез. " +
                                    "Пройдёшь — сундук с алмазами, 7 дней подряд — большой сундук!", d.challengeCard);
        }

        /// <summary>Рейтинг: что за вкладки → где ты → где награды. Каждый шаг — по нажатию.</summary>
        public void RunOnLeaders(LeaderboardScreen lb)
        {
            if (Busy || lb == null || !lb.IsOpen) return;
            overlay.SetSpeaker(null, Vector2.zero);
            if (Once(LeadTabs, "Это рейтинг! Вкладки сверху: за сегодня, за неделю, соседи, весь мир и награды. " +
                               "Справа написано, что считает вкладка.",
                     (RectTransform)lb.tabs[0].transform, () => Later(() => RunOnLeaders(lb))))
                return;
            var me = lb.MyRowTarget;
            if (me != null && Once(LeadMe, "Это ты. Каждая победа даёт очки: новый уровень +10, звезда +5. Поднимайся выше!",
                                   me, () => Later(() => RunOnLeaders(lb))))
                return;
            Once(LeadRewards, "За места дают монеты, наклейки и подарки. Итоги дня — в полночь, недели — в понедельник. " +
                              "Забирай награды на этой вкладке.", (RectTransform)lb.tabs[4].transform);
        }

        /// <summary>Дом снаружи: что это за дом → жми на первый этаж; позже — этаж с новой комнатой.</summary>
        public void RunOnHouse(HouseScreen house)
        {
            if (Busy || house == null || !house.IsOpen) return;
            // говорит енот, который стоит на площади (03.10.2026): второй енот обучения поверх него был лишним
            overlay.SetSpeaker(house.raccoon, PointRaccoonMouth);
            // реплики короткие и по две: длинная раздувала облачко, и оно заходило на дом
            if (Focus(HouseIntro, "Это наш Торговый дом! Здесь живёт команда — сотрудники помогают в уровнях.",
                      house.house, () => Later(() => RunOnHouse(house))))
                return;
            if (Focus(HouseIntro2, "На каждом этаже — 3 комнаты. Этажи в цепях откроются позже: на цепи — нужный уровень.",
                      house.house, () => Later(() => RunOnHouse(house))))
                return;
            if (Focus(HouseFloor, "Первый этаж уже открыт! Жми на него.",
                      house.FloorTarget(1), () => house.OpenFloor(1), strict: true, light: house.house))
                return;
            int r = FirstAvailable();
            // копилка: после первой победы с открытым домом в ней уже есть монеты. Урок стройки важнее —
            // копилку покажем после него (раньше она вклинивалась и обрывала урок, прогон 03.10.2026)
            if (!(BuildLesson && r >= 0) && Piggy.Coins > 0 && GameApp.I.Save.Tutorial(HouseShift) &&
                Focus(HousePiggy, "Это копилка: с каждой победы сюда падают монеты. Накопится — разбей её!",
                      house.PiggyTarget, () => Later(() => RunOnHouse(house))))
                return;
            if (BuildLesson && r >= 0)
            {
                int f = House.All[r].Floor;
                Focus(HouseBuildFloor, $"Комната для нового сотрудника — на {f}-м этаже. Жми на этаж!",
                      house.FloorTarget(f), () => house.OpenFloor(f), strict: true, light: house.house);
            }
        }

        /// <summary>Окно этажа: замки → Касса; позже — ячейка новой комнаты.</summary>
        public void RunOnFloor(FloorPopup floor)
        {
            if (overlay == null || overlay.Visible || floor == null || !floor.IsOpen) return;
            overlay.SetSpeaker(null, Vector2.zero);
            if (floor.Floor == 1 && !GameApp.I.Save.Tutorial(HouseCell))
            {
                var locked = floor.cells[1];
                if (House.State(locked.room) == RoomState.Locked &&
                    Focus(HouseLocks, $"Комнаты под замком откроются, когда к нам придут новые сотрудники. " +
                                      $"«{House.All[locked.room].Name}» — после уровня {House.All[locked.room].UnlockLevel}.",
                          (RectTransform)locked.transform, () => Later(() => RunOnFloor(floor))))
                    return;
                var cell = floor.cells[0];
                Focus(HouseCell, "А Касса уже готова — в ней живёт Белка-кассир. Заходи!",
                      (RectTransform)cell.transform, () => cell.button.onClick.Invoke(), strict: true);
                return;
            }
            int r = FirstAvailable();
            if (BuildLesson && r >= 0 && House.All[r].Floor == floor.Floor)
            {
                var cell = floor.Cell(r);
                Focus(HouseBuildCell, $"«{House.All[r].Name}» открыта — сюда въедет {House.StaffOf(r)?.Name ?? "команда"}. Заходи!",
                      (RectTransform)cell.transform, () => cell.button.onClick.Invoke(), strict: true);
            }
        }

        /// <summary>
        /// Комната. Касса: кто тут живёт → первый предмет за счёт енота → уровни → выручка → смена в уровнях.
        /// Новая комната: «Построить» → ускорение; готова — «Заселить».
        /// </summary>
        public void RunOnRoom(RoomScreen room)
        {
            if (Busy || room == null || !room.IsOpen) return;
            overlay.SetSpeaker(null, Vector2.zero);
            var st = House.State(room.Room);
            if (room.Room == 0 && st == RoomState.Built)
            {
                if (Focus(HouseStaff, "Это Белка-кассир. В карточке — её умение: на смене она приносит больше монет за победу.",
                          room.staffCard, () => Later(() => RunOnRoom(room))))
                    return;
                var tile = room.NextTile;
                if (House.DecorCount(0) == 0 && tile != null)
                {
                    Focus(HouseDecor, "Обставляй комнату — сотрудник растёт! Каждые 2 предмета — новый уровень. Первый предмет — за мой счёт.",
                          (RectTransform)tile.buyButton.transform, () => tile.buyButton.onClick.Invoke(), strict: true);
                    return;
                }
                if (House.DecorCount(0) == 0) return;
                if (Focus(HouseLevel, "Звёзды — уровень Белки. Ещё предмет — и будет 2-й уровень: умение станет сильнее.",
                          room.stars.Length > 0 ? (RectTransform)room.stars[0].transform.parent : room.staffCard,
                          () => Later(() => RunOnRoom(room))))
                    return;
                if (Focus(HouseCash, "А это касса: копит монеты, пока тебя нет (до 8 часов). Заходи забирать!",
                          room.cashPanel, () => Later(() => RunOnRoom(room))))
                    return;
                Focus(HouseShift, "Перед каждым уровнем ты сам выбираешь, кого поставить на смену, — я подскажу, кто там пригодится. " +
                                  "Пойдём играть!", hub != null ? (RectTransform)hub.backButton.transform : null);
                return;
            }
            if (st == RoomState.Available && BuildLesson && room.buildButton.gameObject.activeInHierarchy)
            {
                var info = House.All[room.Room];
                bool money = GameApp.I.Coins >= info.BuildCost;
                Focus(HouseBuild, money
                        ? $"Стройка стоит {info.BuildCost} монет и идёт {Economy.Dur(info.BuildMinutes)} — даже пока ты играешь. Жми «Построить»!"
                        : $"Стройка стоит {info.BuildCost} монет. Пройди пару уровней — и возвращайся строить!",
                      (RectTransform)room.buildButton.transform,
                      money ? () => room.buildButton.onClick.Invoke() : (Action)null, strict: money);
                return;
            }
            if (st == RoomState.Building)
            {
                var speed = room.speedAdButton.gameObject.activeInHierarchy ? (RectTransform)room.speedAdButton.transform : room.buildPanel;
                Focus(HouseSpeed, "Стройка идёт сама. Хочешь быстрее — посмотри видео (−30 минут) или стукни золотым молотком.", speed);
                return;
            }
            if (st == RoomState.Ready && room.moveInButton.gameObject.activeInHierarchy)
                Focus(HouseMoveIn, "Комната готова! Жми «Заселить» — сотрудник въедет и сможет выходить на смену.",
                      (RectTransform)room.moveInButton.transform, () => room.moveInButton.onClick.Invoke(), strict: true);
        }

        /// <summary>Следующий шаг — после того как закроется облачко текущего.</summary>
        void Later(Action next) => Tween.Delay(this, 0.4f, next);
    }
}
