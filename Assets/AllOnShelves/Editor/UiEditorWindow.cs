using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AllOnShelves.Core;
using AllOnShelves.Game;
using AllOnShelves.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AllOnShelves.EditorTools
{
    /// <summary>
    /// «Всё по полкам → Редактор интерфейса» (просьба 29.09.2026). В запущенной игре открывает любой экран,
    /// окно или награду с настоящими данными; объекты двигают, растягивают, масштабируют мышкой в окне Scene
    /// (инструмент Rect — клавиша T) или цифрами в инспекторе, размер шрифта — в инспекторе текста.
    /// «Сохранить правки» пишет их в Editor/Data/user_layout.json; после выхода из игры правки сами
    /// переносятся в сцены и переживают пересборку сцен.
    /// Запоминаются только те объекты, которые выделяли: двигать можно лишь выделенное, а масштаб
    /// «дышащих» кнопок меняет анимация, а не пользователь.
    /// </summary>
    [InitializeOnLoad]
    public class UiEditorWindow : EditorWindow
    {
        static readonly string[] GameScenes = { "Boot", "Hub", "Game" };
        const string PendingKey = "AllOnShelves.UiEditor.Pending";

        // исходное состояние выделенных объектов: при сохранении сравниваем с ним
        static readonly Dictionary<RectTransform, UserLayout.Entry> _tracked = new Dictionary<RectTransform, UserLayout.Entry>();
        // выделены во время анимации: «как было» запомним, когда анимация кончится, иначе её конец сочтём правкой
        static readonly HashSet<RectTransform> _pending = new HashSet<RectTransform>();
        static readonly List<string> _saved = new List<string>();
        static string _status = "";
        static readonly List<(Func<bool> ready, Action act, double until)> _waits = new List<(Func<bool>, Action, double)>();

        Vector2 _scroll;
        int _level = 7;
        int _renoBought = 1;     // 0 — ничего, 1 — половина, 2 — всё

        static UiEditorWindow()
        {
            Selection.selectionChanged += Track;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += TrackPending;
        }

        [MenuItem("Всё по полкам/Редактор интерфейса", priority = -10)]
        static void Open()
        {
            var inspector = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var w = GetWindow<UiEditorWindow>("Редактор интерфейса", true, inspector);
            w.minSize = new Vector2(360, 480);
        }

        // ============================================================ запись правок

        static bool IsGameScene(UnityEngine.SceneManagement.Scene s) => s.IsValid() && GameScenes.Contains(s.name);

        /// <summary>Выделили объекты — запомнить, какими они были до правки.</summary>
        static void Track()
        {
            if (!EditorApplication.isPlaying) return;
            foreach (var go in Selection.gameObjects)
            {
                if (go == null || !(go.transform is RectTransform rt) || !IsGameScene(go.scene)) continue;
                if (_tracked.ContainsKey(rt)) continue;
                if (Tween.Animating(rt, Tween.ChPos)) _pending.Add(rt);
                else _tracked[rt] = UserLayout.EntryOf(rt, go.scene.name);
            }
            foreach (var w in Resources.FindObjectsOfTypeAll<UiEditorWindow>()) w.Repaint();
        }

        static void TrackPending()
        {
            if (_pending.Count == 0 || !EditorApplication.isPlaying) return;
            foreach (var rt in _pending.ToList())
            {
                if (rt == null) { _pending.Remove(rt); continue; }
                if (Tween.Animating(rt, Tween.ChPos)) continue;
                _pending.Remove(rt);
                if (!_tracked.ContainsKey(rt)) _tracked[rt] = UserLayout.EntryOf(rt, rt.gameObject.scene.name);
            }
        }

        static bool IsPopupPanel(RectTransform rt)
        {
            var p = rt.GetComponentInParent<Popup>(true);
            return p != null && p.panel == rt;
        }

        static bool Same(UserLayout.Entry a, UserLayout.Entry b, bool ignoreScale) =>
            (a.pos - b.pos).sqrMagnitude < 0.25f && (a.size - b.size).sqrMagnitude < 0.25f &&
            (a.anchor - b.anchor).sqrMagnitude < 1e-6f && Mathf.Abs(Mathf.DeltaAngle(a.rot, b.rot)) < 0.05f &&
            Mathf.Abs(a.font - b.font) < 0.05f && (ignoreScale || (a.scale - b.scale).sqrMagnitude < 1e-5f);

        /// <summary>Записать правки выделявшихся объектов в user_layout.json. Возвращает, сколько записано.</summary>
        static int SaveEdits(bool quiet = false)
        {
            if (!EditorApplication.isPlaying) return 0;
            var entries = new List<UserLayout.Entry>();
            var skipped = new List<string>();
            foreach (var rt in _tracked.Keys.ToList())
            {
                if (rt == null) continue;
                var was = _tracked[rt];
                var now = UserLayout.EntryOf(rt, rt.gameObject.scene.name);
                // у окна масштаб анимирует появление, у «дышащих» кнопок — анимация: масштаб не наш
                bool animScale = IsPopupPanel(rt) || Tween.Animating(rt, Tween.ChScale);
                if (animScale) now.scale = was.scale = UserLayout.RestScale(now.scene, now.path);
                if (Tween.Animating(rt, Tween.ChRot)) now.rot = was.rot;
                if (Same(now, was, animScale)) continue;
                if (UserLayout.IsDriven(rt) && !IsPopupPanel(rt)) { skipped.Add(rt.name); continue; }
                entries.Add(now);
                _tracked[rt] = now;
            }
            if (entries.Count > 0)
            {
                UserLayout.Record(entries);
                SessionState.SetBool(PendingKey, true);
                foreach (var e in entries) if (!_saved.Contains(e.path)) _saved.Add(e.path);
            }
            _status = entries.Count > 0
                ? $"Сохранено правок: {entries.Count}. После выхода из игры они перенесутся в сцены."
                : "Новых правок нет.";
            if (skipped.Count > 0)
                _status += "\nНе сохранено — место задаёт код: " + string.Join(", ", skipped.Distinct());
            if (!quiet) Debug.Log("[AllOnShelves] Редактор интерфейса: " + _status);
            return entries.Count;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    _tracked.Clear();
                    _pending.Clear();
                    _saved.Clear();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    // игру закрыли, не нажав «Сохранить», — правки не пропадают
                    if (_tracked.Count > 0) SaveEdits(true);
                    _tracked.Clear();
                    _pending.Clear();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    if (!SessionState.GetBool(PendingKey, false)) break;
                    SessionState.SetBool(PendingKey, false);
                    EditorApplication.delayCall += () =>
                    {
                        UserLayout.ApplyToSceneFiles();
                        _status = "Правки перенесены в сцены Boot/Hub/Game и сохранятся при пересборке.";
                        Debug.Log("[AllOnShelves] Редактор интерфейса: " + _status);
                        foreach (var w in Resources.FindObjectsOfTypeAll<UiEditorWindow>()) w.Repaint();
                    };
                    break;
            }
        }

        // ============================================================ ожидание загрузки сцены

        static void WaitFor(Func<bool> ready, Action act, double seconds = 20)
        {
            _waits.Add((ready, act, EditorApplication.timeSinceStartup + seconds));
            if (_waits.Count == 1) EditorApplication.update += Pump;
        }

        static void After(double seconds, Action act)
        {
            double at = EditorApplication.timeSinceStartup + seconds;
            WaitFor(() => EditorApplication.timeSinceStartup >= at, act, seconds + 5);
        }

        static void Pump()
        {
            for (int i = _waits.Count - 1; i >= 0; i--)
            {
                var w = _waits[i];
                bool ok;
                try { ok = EditorApplication.isPlaying && w.ready(); } catch { ok = false; }
                if (ok)
                {
                    _waits.RemoveAt(i);
                    try { w.act(); } catch (Exception e) { Debug.LogException(e); }
                }
                else if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > w.until) _waits.RemoveAt(i);
            }
            if (_waits.Count == 0) EditorApplication.update -= Pump;
        }

        // ============================================================ интерфейс

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(
                "1) «Запустить игру для правки».\n" +
                "2) Выберите, что показать: экран, окно, награду или уровень.\n" +
                "3) Двигайте, растягивайте и масштабируйте объекты мышкой в окне Scene (инструмент Rect — клавиша T) " +
                "или цифрами в инспекторе; размер шрифта — в инспекторе текста.\n" +
                "4) «Сохранить правки». После выхода из игры правки сами перенесутся в сцены и сохранятся при пересборке.",
                MessageType.Info);

            if (!EditorApplication.isPlaying) EditModeGui();
            else PlayModeGui();

            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void EditModeGui()
        {
            Header("Игра не запущена");
            Resolutions();
            if (GUILayout.Button("▶  Запустить игру для правки", GUILayout.Height(34)))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(SceneBuilder.ScenesDir + "/Boot.unity", OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Запомнено ручных правок: {UserLayout.Overrides.Count}", EditorStyles.miniLabel);
            if (GUILayout.Button("Перенести правки в сцены сейчас")) { UserLayout.ApplyToSceneFiles(); _status = "Правки перенесены в сцены."; }
            if (GUILayout.Button("Пересобрать сцены (правки сохранятся)"))
            {
                SceneBuilder.BuildAllSilent();
                _status = "Сцены пересобраны, ручные правки применены.";
            }
            if (GUILayout.Button("Показать файл правок")) EditorApplication.ExecuteMenuItem("Всё по полкам/Раскладка/Показать файл правок");
            EditorGUILayout.LabelField("Без запуска игры экраны можно смотреть в окне «Просмотр сцен и уровней».", EditorStyles.wordWrappedMiniLabel);
        }

        void PlayModeGui()
        {
            var app = GameApp.I;
            if (app == null) { EditorGUILayout.HelpBox("Игра загружается…", MessageType.None); return; }

            SelectionGui();
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.55f, 0.9f, 0.55f);
            if (GUILayout.Button($"Сохранить правки", GUILayout.Height(32))) SaveEdits();
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Вернуть выделенное\nк раскладке из кода", GUILayout.Height(32), GUILayout.Width(150))) ForgetSelected();
            EditorGUILayout.EndHorizontal();
            if (_saved.Count > 0)
                EditorGUILayout.LabelField("Сохранено в этой игре: " + string.Join(", ", _saved.Select(p => p.Split('/').Last())),
                                           EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Настроить окно Scene (2D, инструмент Rect)")) SetupSceneView();
            Resolutions();

            Header("Прогресс игры");
            if (GUILayout.Button("Середина игры: всё открыто, монеты и звёзды есть")) { MidGameSave(); _status = "Прогресс: середина игры."; }

            Header("Экраны хаба");
            Grid(new (string, Action)[]
            {
                ("Главное меню", () => InHub(h => h.Show(h.startScreen))),
                ("Карта", () => InHub(h => h.Show(h.mapScreen))),
                ("Альбом", () => InHub(h => h.Show(h.albumScreen))),
                ("Магазин", () => InHub(h => h.Show(h.shopScreen))),
                ("Завоз дня", () => InHub(h => h.Show(h.dailyScreen))),
                ("Рейтинг", () => InHub(h => h.Show(h.leaderboardScreen))),
            }, 3);
            // магазин и рейтинг (01.10.2026): у каждой вкладки своя страница-картинка и свои карточки/строки —
            // открываем нужную вкладку и выделяем её страницу, правки сохраняются по каждой вкладке отдельно
            EditorGUILayout.LabelField("Магазин — вкладка:", EditorStyles.miniBoldLabel);
            Tabs(ShopScreen.TabNames, t => InHub(h => { h.shopScreen.OpenTab(t); Pick(h.shopScreen.pages[t].transform); }));
            EditorGUILayout.LabelField("Рейтинг — вкладка:", EditorStyles.miniBoldLabel);
            Tabs(LeaderboardScreen.TabNames, t => InHub(h => { h.leaderboardScreen.OpenTab(t); Pick(h.leaderboardScreen.pages[t].transform); }));
            EditorGUILayout.LabelField("Карта — район:", EditorStyles.miniBoldLabel);
            Districts(ShowMap);
            EditorGUILayout.LabelField("Ремонт — район:", EditorStyles.miniBoldLabel);
            _renoBought = GUILayout.Toolbar(_renoBought, new[] { "ничего не куплено", "половина", "всё куплено" });
            Districts(ShowReno);

            // Торговый дом (03.10.2026): дом снаружи, окно этажа с ячейками, комната во всех состояниях
            Header("Торговый дом");
            Grid(new (string, Action)[]
            {
                ("Дом снаружи", () => InHub(h => { HouseDemo(); h.Show(h.houseScreen); })),
                ("Окно этажа 1", () => InHub(h => { HouseDemo(); h.Show(h.houseScreen); h.floorPopup.Open(h, 1); Pick(h.floorPopup.panel); }, true)),
                ("Окно этажа 2", () => InHub(h => { HouseDemo(); h.Show(h.houseScreen); h.floorPopup.Open(h, 2); Pick(h.floorPopup.panel); }, true)),
                ("Комната: обставлена", () => InHub(h => { HouseDemo(); h.roomScreen.OpenRoom(0); })),
                ("Комната: стройка", () => InHub(h => { HouseDemo(); h.roomScreen.OpenRoom(4); })),
                ("Комната: не построена", () => InHub(h => { HouseDemo(); h.roomScreen.OpenRoom(5); })),
                ("Копилка", () => InHub(h => { HouseDemo(); GameApp.I.Save.piggyCoins = 2350; h.Show(h.houseScreen); h.piggyPopup.Open(h); Pick(h.piggyPopup.panel); }, true)),
                ("Алмазы", () => InHub(h => { h.gemShop.Open(h); Pick(h.gemShop.panel); }, true)),
                ("Алмазы: для дома", () => InHub(h => { h.gemShop.Open(h, 1); Pick(h.gemShop.panel); }, true)),
                ("Серия побед", () => InHub(h => { GameApp.I.Save.streak = 3; h.RefreshTop(); h.streakPopup.Open(); Pick(h.streakPopup.panel); }, true)),
                ("Сложность: новый режим", () => InHub(h => { h.diffPopup.Open(h, 1); Pick(h.diffPopup.panel); }, true)),
                ("Копилка разбита", () => InHub(h => { HouseDemo(); h.Show(h.houseScreen); h.piggyPopup.Open(h); h.piggyPopup.PlayBreak(4000); Pick(h.piggyPopup.panel); }, true)),
            }, 3);

            Header("Окна хаба");
            Grid(new (string, Action)[]
            {
                ("Настройки", () => InHub(h => h.settingsPopup.Open(), true)),
                ("Профиль", () => InHub(h => h.profilePopup.Open(h), true)),
                ("Гардероб", () => InHub(h => { GiveCosmetics(); h.wardrobePopup.Open(); }, true)),
                ("Гардероб: аватарка", () => InHub(h => { GiveCosmetics(); h.wardrobePopup.Open(2); }, true)),
                ("Итоги рейтинга", () => InHub(h => { FakeLeagueResult(); LeaderboardScreen.ClaimWithPopup(h, null, h.RefreshTop); }, true)),
                ("Подтверждение", () => InHub(h => h.confirmPopup.Ask("Уровень 12", "Переиграть уровень? Звёзды обновятся, монет — 30%.", "icon_retry", null), true)),
                ("Спецпредложение", () => InHub(h => h.offerPopup.Open(MetaCatalog.Products[0], GameApp.NowUnix + 3 * 3600), true)),
            }, 2);

            Header("Награды");
            Grid(new (string, Action)[]
            {
                ("Звёздный путь: награда готова", () => InHub(h => { ReadyStarReward(); h.OpenStarTrack(); }, true)),
                ("Пачка наклеек", () => InHub(h => { GameApp.I.Save.pendingPacks = Mathf.Max(1, GameApp.I.Save.pendingPacks); h.packPopup.OpenPending(h); }, true)),
                ("Победа ★★★ и ×2", () => Victory(3, false)),
                ("Победа ★★", () => Victory(2, false)),
                ("Победа ★", () => Victory(1, false)),
                ("Победа после удвоения", () => Victory(3, true)),
            }, 2);

            Header("Уровень");
            EditorGUILayout.BeginHorizontal();
            _level = EditorGUILayout.IntSlider(_level, 1, LevelPlanner.LevelCount);
            if (GUILayout.Button("Открыть", GUILayout.Width(80))) OpenLevel(_level, null);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("+5 ходов бота")) InGame(gc => BotMoves(gc, 5));
            Grid(new (string, Action)[]
            {
                ("Поражение", () => InGame(gc => { HideGamePopups(gc); gc.defeat.Setup("Тележка переполнена!", "Ходить больше некуда", true, true, true, 150, true, Noop, Noop, Noop, Noop, Noop, Noop); Pick(gc.defeat.panel); })),
                ("Пауза", () => InGame(gc => { HideGamePopups(gc); Invoke(gc, "OnPause"); Pick(gc.pausePopup.panel); })),
                ("Новая механика", () => InGame(gc => { HideGamePopups(gc); gc.mechanicPopup.Setup("customer", () => gc.mechanicPopup.Hide()); Pick(gc.mechanicPopup.panel); })),
                ("Смена перед уровнем", () => InGame(gc => { HideGamePopups(gc); HouseDemo(); gc.shiftPopup.Open(gc.DebugState, "Уровень · смена", null, _ => { }); Pick(gc.shiftPopup.panel); })),
                ("Бустер кончился", () => InGame(gc => { HideGamePopups(gc); Invoke(gc, "OnHint"); Pick(gc.booster.panel); })),
                ("Итоги «Часа пик»", () => InGame(gc => { HideGamePopups(gc); gc.rushResult.Setup(1234, 2000, 120, true, Noop, Noop, Noop); Pick(gc.rushResult.panel); })),
                ("Облачко обучения", () => InGame(gc => { HideGamePopups(gc); gc.tutorial.Show("Нажми на товар — он сам встанет на свою полку!", (RectTransform)gc.cart.transform); })),
                ("Скрыть окна", () => InGame(HideGamePopups)),
            }, 2);
        }

        void SelectionGui()
        {
            var t = Selection.activeTransform as RectTransform;
            if (t == null || !IsGameScene(t.gameObject.scene))
            {
                EditorGUILayout.HelpBox("Выделите объект интерфейса в окне Scene или Hierarchy.", MessageType.None);
                return;
            }
            string path = UserLayout.PathOf(t);
            bool driven = UserLayout.IsDriven(t) && !IsPopupPanel(t);
            bool has = UserLayout.HasOverride(t.gameObject.scene.name, path);
            var msg = $"{t.gameObject.scene.name}: {path}";
            if (driven) EditorGUILayout.HelpBox(msg + "\nМесто этого объекта задаёт код — правка не сохранится. Двигайте родителя.", MessageType.Warning);
            else EditorGUILayout.HelpBox(msg + (has ? "\nУ объекта есть сохранённая правка." : ""), MessageType.None);
        }

        static void Header(string t)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(t, EditorStyles.boldLabel);
        }

        static void Grid((string label, Action act)[] items, int cols)
        {
            for (int i = 0; i < items.Length; i += cols)
            {
                EditorGUILayout.BeginHorizontal();
                for (int k = i; k < i + cols && k < items.Length; k++)
                    if (GUILayout.Button(items[k].label)) items[k].act();
                EditorGUILayout.EndHorizontal();
            }
        }

        static void Tabs(string[] names, Action<int> act)
        {
            EditorGUILayout.BeginHorizontal();
            for (int t = 0; t < names.Length; t++)
            {
                int tt = t;
                if (GUILayout.Button(names[t], GUILayout.MinWidth(40))) act(tt);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Как будто неделя кончилась: 7-е место недели и 2-е место дня ждут «Забрать».</summary>
        static void FakeLeagueResult()
        {
            var s = GameApp.I.Save;
            s.lbPendWeek = League.WeekNumber - 1; s.lbPendWeekRank = 7; s.lbPendWeekPts = 1500;
            s.lbPendDay = League.DayNumber - 1; s.lbPendDayRank = 2; s.lbPendDayPts = 300;
            GameApp.I.MarkDirty();
        }

        static void Districts(Action<int> act)
        {
            EditorGUILayout.BeginHorizontal();
            for (int d = 1; d <= LevelPlanner.DistrictCount; d++)
            {
                int dd = d;
                if (GUILayout.Button(d.ToString(), GUILayout.MinWidth(24))) act(dd);
            }
            EditorGUILayout.EndHorizontal();
        }

        static void Resolutions()
        {
            EditorGUILayout.LabelField("Размер экрана игры:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (var (label, w, h) in new[] { ("16:9", 1920, 1080), ("21:9", 2560, 1080), ("16:10", 1920, 1200), ("4:3", 1440, 1080) })
                if (GUILayout.Button(label)) PlayModeWindow.SetCustomRenderingResolution((uint)w, (uint)h, "Редактор интерфейса " + label);
            EditorGUILayout.EndHorizontal();
        }

        static void SetupSceneView()
        {
            var sv = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
            sv.in2DMode = true;
            Tools.current = Tool.Rect;
            if (Selection.activeGameObject != null) sv.FrameSelected();
            sv.Focus();
        }

        static void Pick(Component c)
        {
            if (c == null) return;
            Selection.activeGameObject = c.gameObject;
            EditorGUIUtility.PingObject(c.gameObject);
        }

        static void ForgetSelected()
        {
            int n = 0;
            foreach (var go in Selection.gameObjects)
                if (go != null && go.transform is RectTransform && IsGameScene(go.scene))
                    n += UserLayout.Forget(go.scene.name, new[] { UserLayout.PathOf(go.transform) });
            _status = n > 0
                ? $"Забыто правок: {n}. Объект вернётся на место из кода после пересборки сцен (кнопка в окне вне игры)."
                : "У выделенного нет сохранённых правок.";
        }

        static void Noop() { }

        static void Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(target, args);

        // ============================================================ хаб

        static HubController FindHub() => Object.FindAnyObjectByType<HubController>();
        static GameController FindGame() => Object.FindAnyObjectByType<GameController>();

        static void CloseHubPopups(HubController h)
        {
            foreach (var p in new Popup[] { h.settingsPopup, h.packPopup, h.offerPopup, h.confirmPopup, h.starTrackPopup, h.wardrobePopup, h.profilePopup })
                if (p != null) p.HideInstant();
        }

        /// <summary>Выполнить в хабе (если открыт уровень — сначала выйти в хаб и дождаться его).</summary>
        static void InHub(Action<HubController> act, bool popup = false)
        {
            var hub = FindHub();
            if (hub != null)
            {
                if (!popup) CloseHubPopups(hub);
                else CloseHubPopups(hub);
                act(hub);
                return;
            }
            _status = "Выходим в хаб…";
            GameApp.I.GoHub();
            // хаб сам выбирает первый экран и может открыть окно: ждём, пока он устроится
            WaitFor(() => FindHub() != null, () => After(1.2, () =>
            {
                var h = FindHub();
                if (h == null) return;
                CloseHubPopups(h);
                if (popup) h.Show(h.mapScreen);
                act(h);
                _status = "";
            }));
        }

        static void ShowMap(int district)
        {
            InHub(h =>
            {
                // карта листается только до текущего района: для правки открываем нужный
                var s = GameApp.I.Save;
                int first = LevelPlanner.DistrictStart[district - 1];
                if (s.maxReached + 1 < first) { s.maxReached = first - 1; GameApp.I.MarkDirty(); }
                h.Show(h.mapScreen);
                Invoke(h.mapScreen, "ShowPage", district - 1, false);
                Pick(h.mapScreen.pages[district - 1]);
            });
        }

        void ShowReno(int district)
        {
            int bought = _renoBought;
            InHub(h =>
            {
                var s = GameApp.I.Save;
                var items = MetaCatalog.ItemsOf(district).ToList();
                int n = bought == 0 ? 0 : bought == 1 ? items.Count / 2 : items.Count;
                for (int i = 0; i < items.Count; i++)
                {
                    s.boughtItems.Remove(items[i].Id);
                    if (i < n) s.boughtItems.Add(items[i].Id);
                }
                GameApp.I.MarkDirty();
                var r = h.renovationScreen;
                h.Show(r);
                typeof(RenovationScreen).GetField("_district", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(r, district);
                r.Refresh();
                Pick(r);
            });
        }

        static void GiveCosmetics()
        {
            var s = GameApp.I.Save;
            foreach (var r in MetaCatalog.StarTrack)
                if (!string.IsNullOrEmpty(r.CosmeticId) && !s.cosmetics.Contains(r.CosmeticId)) s.cosmetics.Add(r.CosmeticId);
            GameApp.I.MarkDirty();
        }

        /// <summary>Чтобы в «Звёздном пути» была награда «забрать»: последняя полученная снова становится готовой,
        /// а если звёзд не хватает даже на первую — добавляем звёзды за пройденные уровни.</summary>
        static void ReadyStarReward()
        {
            var app = GameApp.I;
            int lastClaimed = -1;
            for (int i = 0; i < MetaCatalog.StarTrack.Length; i++)
            {
                if (!app.StarRewardReached(i)) break;
                if (!app.StarRewardClaimed(i)) return;   // готовая уже есть
                lastClaimed = i;
            }
            if (lastClaimed >= 0) app.Save.starRewards.Remove(lastClaimed);
            else
            {
                var s = app.Save;
                if (s.stars.Length < LevelPlanner.LevelCount + 1) Array.Resize(ref s.stars, LevelPlanner.LevelCount + 1);
                for (int i = 1; i <= LevelPlanner.LevelCount && !app.StarRewardReached(0); i++) s.stars[i] = 3;
                s.maxReached = Mathf.Max(s.maxReached, 1);
            }
            app.MarkDirty();
        }

        /// <summary>Как сейв «середина игры» в Tools/ui_tour.py: всё открыто, монеты и звёзды есть.</summary>
        /// <summary>Торговый дом в середине игры: этаж 1 и Стойка построены, Кладовая строится, касса копится.</summary>
        static void HouseDemo()
        {
            var s = GameApp.I.Save;
            if (s.maxReached < 45) MidGameSave();
            s.houseBuilt = 0b1111;
            s.houseDecor = new[] { 6, 3, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            s.houseBuilding = 4;
            s.houseBuildEnd = GameApp.NowUnix + 5000;
            s.houseFloorsSeen = 2;
            s.houseCashSince = GameApp.NowUnix - 3 * 3600;
            s.hammers = 1;
            GameApp.I.MarkDirty();
        }

        static void MidGameSave()
        {
            var app = GameApp.I;
            var s = app.Save;
            s.maxReached = 45; s.coins = 4321; s.undo = 5; s.hint = 3;
            s.tutorialBits = -1; s.mechanicsSeen = -1; s.activeTheme = "";
            s.stars = new int[LevelPlanner.LevelCount + 1];
            for (int i = 1; i <= 45; i++) s.stars[i] = 1 + i % 3;
            s.boughtItems.Clear();
            foreach (var it in MetaCatalog.Items) if (it.District <= 3) s.boughtItems.Add(it.Id);
            s.renovationDistrict = 4;
            s.stickers.Clear();
            foreach (var st in MetaCatalog.StickersOf(0)) { s.stickers.Add(st); if (s.stickers.Count > 5) break; }
            s.pendingPacks = 0;
            s.starsChestClaimed = 999;
            s.starterShownAtLevel = 999; s.starterBought = true; s.noAds = true;
            app.MarkDirty();
            var hub = FindHub();
            if (hub != null) { hub.RefreshTop(); hub.Show(hub.mapScreen); }
        }

        // ============================================================ уровень

        static void HideGamePopups(GameController gc)
        {
            gc.victory.HideInstant();
            gc.defeat.HideInstant();
            gc.pausePopup.HideInstant();
            gc.mechanicPopup.HideInstant();
            if (gc.shiftPopup != null) gc.shiftPopup.HideInstant();
            gc.rushResult.HideInstant();
            gc.tutorial.HideInstant();
            if (gc.booster != null) gc.booster.HideInstant();
        }

        static void OpenLevel(int level, Action<GameController> then)
        {
            _status = $"Открываем уровень {level}…";
            GameApp.I.PlayLevel(level);
            double since = EditorApplication.timeSinceStartup;
            // старая сцена ещё жива первые кадры — ждём новую и её первый показ
            WaitFor(() => EditorApplication.timeSinceStartup - since > 1.0 && FindGame() != null && FindGame().DebugState != null,
                () => After(0.6, () =>
                {
                    var gc = FindGame();
                    if (gc == null) return;
                    gc.DebugUnlockInput();
                    _status = "";
                    then?.Invoke(gc);
                }));
        }

        void InGame(Action<GameController> act)
        {
            var gc = FindGame();
            if (gc != null && gc.DebugState != null) { act(gc); return; }
            OpenLevel(_level, act);
        }

        static void BotMoves(GameController gc, int n)
        {
            if (n <= 0 || gc == null) return;
            gc.DebugUnlockInput();
            gc.DebugAutoMove();
            After(0.45, () => BotMoves(FindGame(), n - 1));
        }

        void Victory(int stars, bool doubled)
        {
            InGame(gc =>
            {
                HideGamePopups(gc);
                var v = gc.victory;
                v.Setup(stars, 37, 2, false, true, false, "Дальше", () => v.ShowDoubled(74), Noop, Noop, Noop);
                if (doubled) After(1.3, () => v.ShowDoubled(74));
                Pick(v.panel);
            });
        }
    }
}

