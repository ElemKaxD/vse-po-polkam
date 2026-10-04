using System.Linq;
using AllOnShelves.Core;
using AllOnShelves.Game;
using AllOnShelves.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AllOnShelves.EditorTools
{
    /// <summary>
    /// «Всё по полкам → Просмотр сцен и уровней». Без запуска игры показывает в сцене любой экран хаба, окно,
    /// магазин ремонта, район карты или уровень (с товарами на ленте, полках и в тележке).
    /// Показанное можно двигать мышкой (окно Scene, инструмент Rect — клавиша T) и сохранять сцену (Ctrl+S):
    /// правки переживают пересборку сцен (см. UserLayout).
    /// </summary>
    public class ScenePreviewWindow : EditorWindow
    {
        const string HubPath = SceneBuilder.ScenesDir + "/Hub.unity";
        const string GamePath = SceneBuilder.ScenesDir + "/Game.unity";

        static readonly (string id, string name)[] Mechanics =
        {
            ("lock", "Замки"), ("box", "Коробки «?»"), ("extra", "Лишний товар"), ("customer", "Покупатель"),
            ("perish", "Скоропорт"), ("freezer", "Морозилка"), ("big", "Арбуз (крупный)"), ("pallet", "Паллета"),
            ("sale", "Акция"), ("bundle", "Связка"), ("belt2", "Две ленты"), ("door", "Дверца морозилки"), ("night", "Ночь"),
        };

        Vector2 _scroll;
        int _level = 1;
        int _moves;
        int _renoDistrict = 1;
        bool _renovated = true;
        bool _allItems = true;
        LevelData[] _levels;
        string _status = "";

        [MenuItem("Всё по полкам/Просмотр сцен и уровней", priority = 0)]
        static void Open()
        {
            // вкладкой рядом с Inspector, чтобы не закрывать окно Game
            var inspector = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var w = GetWindow<ScenePreviewWindow>("Просмотр сцен", true, inspector);
            w.minSize = new Vector2(340, 400);
        }

        // ============================================================ интерфейс

        void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Остановите игру (Play). Просмотр работает в обычном режиме редактора — " +
                                        "только там сделанные мышкой правки сохраняются.", MessageType.Warning);
                return;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox("1) Выберите, что показать.\n2) Двигайте и масштабируйте объекты мышкой в окне Scene " +
                                    "(инструмент Rect — клавиша T; удобнее в режиме 2D).\n3) Ctrl+S — сохранить сцену.\n" +
                                    "Правки сохраняются при пересборке сцен. Товары уровня временные и в сцену не записываются.", MessageType.Info);
            if (GUILayout.Button("Настроить окно Scene для интерфейса (2D, Rect, показать всё)")) SetupSceneView();

            // ---------------- уровень
            Header("Уровень (сцена Game)");
            _levels ??= LoadLevels();
            int max = Mathf.Max(1, _levels.Length);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(30))) { _level = Mathf.Max(1, _level - 1); _moves = 0; ShowLevel(); }
            int lv = EditorGUILayout.IntSlider(_level, 1, max);
            if (lv != _level) { _level = lv; _moves = 0; }
            if (GUILayout.Button("▶", GUILayout.Width(30))) { _level = Mathf.Min(max, _level + 1); _moves = 0; ShowLevel(); }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Показать уровень " + _level, GUILayout.Height(28))) { _moves = 0; ShowLevel(); }
            if (GUILayout.Button("+1 ход", GUILayout.Width(70), GUILayout.Height(28))) { _moves++; ShowLevel(); }
            if (GUILayout.Button("+5", GUILayout.Width(40), GUILayout.Height(28))) { _moves += 5; ShowLevel(); }
            EditorGUILayout.EndHorizontal();
            var data = _levels.Length >= _level ? _levels[_level - 1] : null;
            if (data != null)
                EditorGUILayout.LabelField($"Район {data.district} · {data.difficulty}" + (string.IsNullOrEmpty(data.mechanic) ? "" : $" · новая механика: {data.mechanic}") +
                                           $" · ходов сделано: {_moves}", EditorStyles.miniLabel);

            EditorGUILayout.LabelField("Первый уровень с механикой:", EditorStyles.miniBoldLabel);
            Grid(Mechanics.Select(m => (m.name + " (" + LevelPlanner.MechanicIntro[m.id] + ")", (System.Action)(() =>
            {
                _level = LevelPlanner.MechanicIntro[m.id];
                _moves = 0;
                ShowLevel();
            }))).ToArray(), 2);

            EditorGUILayout.LabelField("Окна уровня:", EditorStyles.miniBoldLabel);
            Grid(new (string, System.Action)[]
            {
                ("Победа", () => GamePopup(g => g.victory, VictoryLook)),
                ("Поражение", () => GamePopup(g => g.defeat)),
                ("Пауза", () => GamePopup(g => g.pausePopup)),
                ("Новая механика", () => GamePopup(g => g.mechanicPopup, g => g.mechanicPopup.Setup(MechanicOfLevel(), null))),
                ("Смена перед уровнем", () => GamePopup(g => g.shiftPopup)),
                ("Обучение (облачко)", ShowTutorial),
                ("Итоги «Часа пик»", () => GamePopup(g => g.rushResult)),
                ("Интерфейс «Часа пик»", ShowRushHud),
                ("Скрыть окна", () => WithGame(HideGamePopups)),
            }, 2);

            // ---------------- хаб
            Header("Хаб (сцена Hub)");
            Grid(new (string, System.Action)[]
            {
                ("Стартовый экран", () => HubScreen(h => h.startScreen)),
                ("Альбом", () => HubScreen(h => h.albumScreen)),
                ("Магазин", () => HubScreen(h => h.shopScreen)),
                ("Завоз дня", () => HubScreen(h => h.dailyScreen)),
                ("Рейтинг", () => HubScreen(h => h.leaderboardScreen)),
                ("Торговый дом", () => HubScreen(h => h.houseScreen)),
                ("Комната дома", () => HubScreen(h => h.roomScreen)),
            }, 2);
            // у магазина и рейтинга по пять вкладок-страниц (01.10.2026)
            EditorGUILayout.LabelField("Магазин — вкладка:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int t = 0; t < AllOnShelves.Hub.ShopScreen.TabCount; t++)
            {
                int tt = t;
                if (GUILayout.Button(AllOnShelves.Hub.ShopScreen.TabNames[t], GUILayout.MinWidth(40))) HubPage(h => h.shopScreen, h => h.shopScreen.pages, tt);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Рейтинг — вкладка:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int t = 0; t < AllOnShelves.Hub.LeaderboardScreen.TabCount; t++)
            {
                int tt = t;
                if (GUILayout.Button(AllOnShelves.Hub.LeaderboardScreen.TabNames[t], GUILayout.MinWidth(40))) HubPage(h => h.leaderboardScreen, h => h.leaderboardScreen.pages, tt);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Карта — район:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int d = 1; d <= LevelPlanner.DistrictCount; d++)
            {
                int dd = d;
                if (GUILayout.Button(d.ToString(), GUILayout.MinWidth(24))) ShowMap(dd);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Ремонт — район:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int d = 1; d <= LevelPlanner.DistrictCount; d++)
            {
                int dd = d;
                if (GUILayout.Button(d.ToString(), GUILayout.MinWidth(24))) { _renoDistrict = dd; ShowReno(); }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            _renovated = EditorGUILayout.ToggleLeft("Здание после ремонта", _renovated);
            _allItems = EditorGUILayout.ToggleLeft("Показать все предметы магазина (иначе — только этого района)", _allItems);
            if (EditorGUI.EndChangeCheck()) ShowReno();

            EditorGUILayout.LabelField("Окна хаба:", EditorStyles.miniBoldLabel);
            Grid(new (string, System.Action)[]
            {
                ("Настройки", () => HubPopup(h => h.settingsPopup)),
                ("Пачка наклеек", () => HubPopup(h => h.packPopup)),
                ("Спецпредложение", () => HubPopup(h => h.offerPopup)),
                ("Подтверждение", () => HubPopup(h => h.confirmPopup)),
                ("Окно этажа дома", () => HubPopup(h => h.floorPopup)),
                ("Копилка", () => HubPopup(h => h.piggyPopup)),
                ("Алмазы", () => HubPopup(h => h.gemShop)),
                ("Серия побед", () => HubPopup(h => h.streakPopup)),
                ("Сложность (режимы)", () => HubPopup(h => h.diffPopup)),
            }, 2);

            // ---------------- раскладка
            Header("Раскладка");
            if (GUILayout.Button("Запомнить положение поля (ленты, стеллаж, тележка)")) CaptureBoard(true);
            EditorGUILayout.LabelField("Секции стеллажа расставляются сами по их числу — двигайте стеллаж целиком.", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Сохранить мои правки сейчас")) UserLayout.CaptureAllMenu();
            EditorGUILayout.LabelField($"Запомнено ручных правок: {UserLayout.Overrides.Count}", EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        static void Header(string t)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(t, EditorStyles.boldLabel);
        }

        static void Grid((string label, System.Action act)[] items, int cols)
        {
            for (int i = 0; i < items.Length; i += cols)
            {
                EditorGUILayout.BeginHorizontal();
                for (int k = i; k < i + cols && k < items.Length; k++)
                    if (GUILayout.Button(items[k].label)) items[k].act();
                EditorGUILayout.EndHorizontal();
            }
        }

        // ============================================================ сцены

        static bool EnsureScene(string path)
        {
            if (SceneManager.GetActiveScene().path == path) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return true;
        }

        static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        static void Focus(GameObject go)
        {
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        static void SetupSceneView()
        {
            var sv = SceneView.lastActiveSceneView ?? GetWindow<SceneView>();
            sv.in2DMode = true;
            Tools.current = Tool.Rect;
            var canvas = Find<Canvas>();
            if (canvas != null)
            {
                Selection.activeGameObject = canvas.gameObject;
                sv.FrameSelected();
            }
            sv.Focus();
        }

        // ============================================================ уровень

        static LevelData[] LoadLevels()
        {
            var ta = Resources.Load<TextAsset>("Levels/levels");
            if (ta == null) return new LevelData[0];
            return JsonUtility.FromJson<LevelSet>(ta.text).levels.OrderBy(l => l.id).ToArray();
        }

        string MechanicOfLevel()
        {
            var d = _levels != null && _levels.Length >= _level ? _levels[_level - 1] : null;
            return d != null && !string.IsNullOrEmpty(d.mechanic) ? d.mechanic : "rules";
        }

        void WithGame(System.Action<GameController> act)
        {
            if (!EnsureScene(GamePath)) return;
            var gc = Find<GameController>();
            if (gc == null) { _status = "В сцене Game нет GameController — соберите сцены."; return; }
            act(gc);
        }

        void ShowLevel()
        {
            WithGame(gc =>
            {
                _levels ??= LoadLevels();
                if (_level < 1 || _level > _levels.Length) return;
                CaptureBoard(false);
                HideGamePopups(gc);
                gc.EditorPreview(_levels[_level - 1], _moves);
                _status = $"Показан уровень {_level}. Ленту, стеллаж и тележку можно двигать — положение запомнится.";
                Focus(gc.boardRoot.gameObject);
            });
        }

        /// <summary>Если поле подвинули мышкой — перенести положение в BoardLayout, чтобы оно не сбросилось.</summary>
        void CaptureBoard(bool explicitly)
        {
            if (SceneManager.GetActiveScene().path != GamePath) { if (explicitly) _status = "Сначала покажите уровень."; return; }
            var gc = Find<GameController>();
            if (gc == null) return;
            if (gc.PreviewState == null) { if (explicitly) _status = "Сначала покажите уровень."; return; }
            if (!explicitly && !gc.EditorLayoutMoved()) return;
            var asset = gc.boardLayout != null ? gc.boardLayout : SceneBuilder.BoardLayoutAsset();
            Undo.RecordObject(asset, "Положение поля");
            gc.boardLayout = asset;
            gc.EditorCaptureLayout();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            _status = "Положение поля запомнено (" + (gc.PreviewState.Belts.Length > 1 ? "уровни с двумя лентами" : "уровни с одной лентой") + ").";
        }

        static void HideGamePopups(GameController gc)
        {
            gc.victory.HideInstant();
            gc.defeat.HideInstant();
            gc.pausePopup.HideInstant();
            gc.mechanicPopup.HideInstant();
            if (gc.shiftPopup != null) gc.shiftPopup.HideInstant();
            gc.rushResult.HideInstant();
            gc.tutorial.HideInstant();
            gc.rushHud.SetActive(false);
            gc.goalsPanel.SetActive(true);
        }

        void GamePopup(System.Func<GameController, Popup> pick, System.Action<GameController> setup = null)
        {
            WithGame(gc =>
            {
                if (gc.PreviewState == null) ShowLevel();
                HideGamePopups(gc);
                var p = pick(gc);
                p.gameObject.SetActive(true);
                p.transform.SetAsLastSibling();
                p.group.alpha = 1f;
                if (p.panel != null) p.panel.localScale = Vector3.one;
                setup?.Invoke(gc);
                p.group.alpha = 1f;
                if (p.panel != null) p.panel.localScale = Vector3.one;
                Focus(p.panel != null ? p.panel.gameObject : p.gameObject);
            });
        }

        static void VictoryLook(GameController gc)
        {
            var v = gc.victory;
            foreach (var s in v.stars) if (s != null && v.starOn != null) s.sprite = v.starOn;
            v.nextButton.gameObject.SetActive(true);
            v.doubleButton.gameObject.SetActive(true);
        }

        void ShowTutorial()
        {
            WithGame(gc =>
            {
                if (gc.PreviewState == null) ShowLevel();
                HideGamePopups(gc);
                var t = gc.tutorial;
                t.gameObject.SetActive(true);
                t.transform.SetAsLastSibling();
                t.bubble.gameObject.SetActive(true);
                t.bubble.localScale = Vector3.one;
                t.raccoon.gameObject.SetActive(true);
                t.bubbleText.text = "Нажми на товар — он сам встанет на свою полку!";
                var first = gc.itemsRoot.childCount > 0 ? gc.itemsRoot.GetChild(0) : null;
                if (first != null)
                {
                    t.hand.gameObject.SetActive(true);
                    t.hand.position = first.position;
                }
                Focus(t.bubble.gameObject);
            });
        }

        void ShowRushHud()
        {
            WithGame(gc =>
            {
                if (gc.PreviewState == null) ShowLevel();
                HideGamePopups(gc);
                gc.rushHud.SetActive(true);
                gc.goalsPanel.SetActive(false);
                Focus(gc.rushHud);
            });
        }

        // ============================================================ хаб

        void WithHub(System.Action<HubController> act)
        {
            if (!EnsureScene(HubPath)) return;
            var hub = Find<HubController>();
            if (hub == null) { _status = "В сцене Hub нет HubController — соберите сцены."; return; }
            act(hub);
        }

        static HubScreen[] Screens(HubController h) =>
            new HubScreen[] { h.startScreen, h.mapScreen, h.renovationScreen, h.albumScreen, h.shopScreen, h.dailyScreen, h.leaderboardScreen,
                              h.houseScreen, h.roomScreen };

        static Popup[] HubPopups(HubController h) => new Popup[] { h.settingsPopup, h.packPopup, h.offerPopup, h.confirmPopup, h.floorPopup, h.piggyPopup, h.gemShop, h.streakPopup, h.diffPopup };

        static void ActivateScreen(HubController h, HubScreen screen)
        {
            foreach (var s in Screens(h)) if (s != null) s.gameObject.SetActive(s == screen);
            foreach (var p in HubPopups(h)) if (p != null) p.HideInstant();
            if (screen.group != null) screen.group.alpha = 1f;
            h.navBar.SetActive(screen == h.mapScreen);
            h.backButton.gameObject.SetActive(screen != h.startScreen && screen != h.mapScreen);
        }

        void HubScreen(System.Func<HubController, HubScreen> pick)
        {
            WithHub(h =>
            {
                var s = pick(h);
                ActivateScreen(h, s);
                _status = "Показан экран: " + s.name;
                Focus(s.gameObject);
            });
        }

        /// <summary>Экран с вкладками-страницами (магазин, рейтинг): показать одну страницу.</summary>
        void HubPage(System.Func<HubController, HubScreen> pick, System.Func<HubController, GameObject[]> pages, int tab)
        {
            WithHub(h =>
            {
                var s = pick(h);
                ActivateScreen(h, s);
                var list = pages(h);
                for (int i = 0; i < list.Length; i++) if (list[i] != null) list[i].SetActive(i == tab);
                _status = $"Показан экран: {s.name}, вкладка {tab + 1}";
                Focus(list[tab]);
            });
        }

        void ShowMap(int district)
        {
            WithHub(h =>
            {
                var m = h.mapScreen;
                ActivateScreen(h, m);
                for (int i = 0; i < m.pages.Length; i++)
                {
                    bool on = i == district - 1;
                    m.pages[i].gameObject.SetActive(on);
                    if (!on) continue;
                    m.pages[i].localScale = Vector3.one;
                    var cg = m.pages[i].GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 1f;
                }
                var store = MetaCatalog.StoreOf(district);
                if (m.districtTitle != null) m.districtTitle.text = $"Район {district} · {store.Name}";
                m.prevButton.gameObject.SetActive(district > 1);
                m.nextButton.gameObject.SetActive(district < m.pages.Length);
                _status = $"Карта, район {district}. Ценники уровней лежат в District{district:00}/…/Levels.";
                Focus(m.pages[district - 1].gameObject);
            });
        }

        void ShowReno()
        {
            WithHub(h =>
            {
                var r = h.renovationScreen;
                ActivateScreen(h, r);
                RenoStore shown = null;
                foreach (var st in r.stores)
                {
                    if (st == null) continue;
                    var info = MetaCatalog.Stores.First(x => x.Id == st.storeId);
                    bool on = info.Districts.Contains(_renoDistrict);
                    st.gameObject.SetActive(on);
                    if (!on) continue;
                    shown = st;
                    st.buildingWorn.gameObject.SetActive(!_renovated);
                    st.buildingNew.gameObject.SetActive(_renovated);
                    if (st.sparkle != null) st.sparkle.gameObject.SetActive(false);
                    for (int k = 0; k < st.items.Length; k++)
                    {
                        if (st.items[k] == null) continue;
                        bool show = _allItems || st.itemAttached[k] && st.itemDistrict[k] <= _renoDistrict || st.itemDistrict[k] == _renoDistrict;
                        st.items[k].gameObject.SetActive(show);
                        st.items[k].transform.localScale = st.items[k].transform.localScale == Vector3.zero ? Vector3.one : st.items[k].transform.localScale;
                    }
                }
                if (r.storeTitle != null && shown != null) r.storeTitle.text = MetaCatalog.StoreOf(_renoDistrict).Name;
                _status = shown != null ? $"Ремонт: {shown.name}, район {_renoDistrict}. Предметы — дочерние объекты Scene/Items (имя = id предмета)." : "Магазин не найден.";
                if (shown != null) Focus(shown.gameObject);
            });
        }

        void HubPopup(System.Func<HubController, Popup> pick)
        {
            WithHub(h =>
            {
                foreach (var p in HubPopups(h)) if (p != null) p.HideInstant();
                if (!Screens(h).Any(s => s != null && s.gameObject.activeSelf)) ActivateScreen(h, h.mapScreen);
                var pop = pick(h);
                pop.gameObject.SetActive(true);
                pop.transform.SetAsLastSibling();
                pop.group.alpha = 1f;
                if (pop.panel != null) pop.panel.localScale = Vector3.one;
                Focus(pop.panel != null ? pop.panel.gameObject : pop.gameObject);
            });
        }
    }
}
