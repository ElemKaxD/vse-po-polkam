using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AllOnShelves.Game;
using AllOnShelves.Hub;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AllOnShelves.EditorTools
{
    /// <summary>
    /// Ручные правки раскладки переживают пересборку сцен.
    /// После сборки запоминается «как построено» (UserSettings/AllOnShelves_built_*.json).
    /// Перед следующей сборкой всё, что пользователь подвинул/растянул/повернул мышкой или чему поменял размер шрифта,
    /// записывается в Editor/Data/user_layout.json, а после сборки применяется к новым объектам.
    /// </summary>
    public static class UserLayout
    {
        public const string OverridesPath = ProjectSetup.Root + "/Editor/Data/user_layout.json";
        static readonly string[] Scenes = { "Boot", "Hub", "Game" };
        static string BuiltPath(string scene) => "UserSettings/AllOnShelves_built_" + scene + ".json";

        [Serializable]
        public class Entry
        {
            public string scene, path;
            public Vector2 pos, size;
            public Vector3 scale = Vector3.one;
            public float rot;
            public float font = -1f;
            /// <summary>Точка привязки, от которой считается pos (с 29.09.2026; в старых записях её нет —
            /// тогда pos считается от привязки, которую дал объекту SceneBuilder).</summary>
            public Vector2 anchor;
            public bool hasAnchor;
            /// <summary>Размер не в пропорциях спрайта: след пересборки графики, а не правка мышкой.</summary>
            [NonSerialized] public bool badAspect;
        }

        [Serializable] class Store { public List<Entry> items = new List<Entry>(); }

        // ------------------------------------------------------------ чтение/запись

        static Store Load(string path)
        {
            if (!File.Exists(path)) return new Store();
            try { return JsonUtility.FromJson<Store>(File.ReadAllText(path)) ?? new Store(); }
            catch { return new Store(); }
        }

        static void Write(string path, Store s)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(s, true));
        }

        public static List<Entry> Overrides => Load(OverridesPath).items;

        // ------------------------------------------------------------ обход сцены

        public static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x != null; x = x.parent)
            {
                string n = x.name;
                if (x.parent != null)
                {
                    int same = 0, idx = 0;
                    for (int i = 0; i < x.parent.childCount; i++)
                    {
                        var c = x.parent.GetChild(i);
                        if (c.name != n) continue;
                        if (c == x) idx = same;
                        same++;
                    }
                    if (same > 1) n += "#" + idx;
                }
                parts.Add(n);
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static Transform Find(Scene scene, string path)
        {
            var parts = path.Split('/');
            Transform cur = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == parts[0]) { cur = root.transform; break; }
            for (int k = 1; k < parts.Length && cur != null; k++)
            {
                string name = parts[k];
                int want = 0, hash = name.LastIndexOf('#');
                if (hash > 0 && int.TryParse(name.Substring(hash + 1), out want)) name = name.Substring(0, hash);
                Transform next = null;
                int seen = 0;
                for (int i = 0; i < cur.childCount; i++)
                {
                    var c = cur.GetChild(i);
                    if (c.name != name) continue;
                    if (seen++ == want) { next = c; break; }
                }
                cur = next;
            }
            return cur;
        }

        /// <summary>Объекты, которые расставляет код во время игры, — их положение из сцены не запоминаем.</summary>
        static HashSet<Transform> Driven(Scene scene)
        {
            var set = new HashSet<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var gc in root.GetComponentsInChildren<GameController>(true))
                {
                    // вместе с детьми: размеры секций и подписи на них пересчитываются кодом под каждый уровень
                    foreach (var t in gc.EditorDrivenTransforms())
                        if (t != null)
                            foreach (var ch in t.GetComponentsInChildren<Transform>(true)) set.Add(ch);
                    if (gc.itemsRoot != null) foreach (Transform t in gc.itemsRoot.GetComponentsInChildren<Transform>(true)) if (t != gc.itemsRoot) set.Add(t);
                    if (gc.fxRoot != null) foreach (Transform t in gc.fxRoot.GetComponentsInChildren<Transform>(true)) if (t != gc.fxRoot) set.Add(t);
                }
                foreach (var m in root.GetComponentsInChildren<MapScreen>(true))
                {
                    if (m.avatar != null) set.Add(m.avatar);
                    foreach (var p in m.pages) if (p != null) set.Add(p);
                }
                foreach (var b in root.GetComponentsInChildren<BootController>(true))
                    if (b.raccoon != null) set.Add(b.raccoon); // енота по полосе двигает код
                // экран ремонта: плитки, метки, кружки полосы и подсказку расставляет код под число улучшений
                foreach (var r in root.GetComponentsInChildren<RenovationScreen>(true))
                {
                    foreach (var c in r.tiles) if (c != null) set.Add(c.transform);
                    foreach (var c in r.cards) if (c != null) foreach (var ch in c.GetComponentsInChildren<Transform>(true)) set.Add(ch);
                    foreach (var n in r.railNodes) if (n != null) set.Add(n);
                    if (r.railTrack != null) set.Add(r.railTrack);
                    if (r.tipPlate != null) set.Add(r.tipPlate);
                    if (r.legoRoot != null) foreach (var ch in r.legoRoot.GetComponentsInChildren<Transform>(true)) set.Add(ch);
                    if (r.legoBg != null) set.Add(r.legoBg.transform);
                }
                // «книги» страниц магазина и рейтинга сами подгоняют размер и масштаб под экран (ScaleToParent)
                foreach (var f in root.GetComponentsInChildren<ScaleToParent>(true))
                    if (f.name == "Pages" && (f.GetComponentInParent<ShopScreen>(true) != null || f.GetComponentInParent<LeaderboardScreen>(true) != null))
                        set.Add(f.transform);
                foreach (var st in root.GetComponentsInChildren<StarTrackPopup>(true))
                {
                    if (st.trackFill != null) set.Add(st.trackFill);
                    if (st.content != null) set.Add(st.content);
                }
                foreach (var c in root.GetComponentsInChildren<Canvas>(true)) set.Add(c.transform);
                foreach (var c in root.GetComponentsInChildren<ScaleToParent>(true)) set.Add(c.transform);
                // фон «во весь экран»: размер считает AspectRatioFitter по окну Game — это не правка руками
                foreach (var f in root.GetComponentsInChildren<AspectRatioFitter>(true)) set.Add(f.transform);
                foreach (var lg in root.GetComponentsInChildren<LayoutGroup>(true))
                    foreach (Transform ch in lg.transform) set.Add(ch);
                foreach (var p in root.GetComponentsInChildren<Popup>(true)) if (p.panel != null) set.Add(p.panel); // масштаб панели анимируется
            }
            return set;
        }

        static Dictionary<string, Entry> Snapshot(Scene scene, string sceneName)
        {
            var driven = Driven(scene);
            var map = new Dictionary<string, Entry>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (driven.Contains(rt) || (rt.gameObject.hideFlags & HideFlags.DontSave) != 0) continue;
                    var e = new Entry
                    {
                        scene = sceneName, path = PathOf(rt), pos = rt.anchoredPosition, size = rt.sizeDelta,
                        scale = rt.localScale, rot = rt.localEulerAngles.z,
                        anchor = rt.anchorMin, hasAnchor = rt.anchorMin == rt.anchorMax,
                    };
                    var tmp = rt.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) e.font = tmp.enableAutoSizing ? tmp.fontSizeMax : tmp.fontSize;
                    var img = rt.GetComponent<Image>();
                    if (img != null && img.sprite != null && img.type == Image.Type.Simple &&
                        Mathf.Abs(e.size.x) > 0.01f && Mathf.Abs(e.size.y) > 0.01f)
                    {
                        float want = img.sprite.rect.width / img.sprite.rect.height;
                        e.badAspect = Mathf.Abs(Mathf.Abs(e.size.x / e.size.y) - want) / want > 0.02f;
                    }
                    map[e.path] = e;
                }
            return map;
        }

        static bool Same(Entry a, Entry b) =>
            (a.pos - b.pos).sqrMagnitude < 0.25f && (a.size - b.size).sqrMagnitude < 0.25f &&
            (!a.hasAnchor || !b.hasAnchor || (a.anchor - b.anchor).sqrMagnitude < 1e-6f) &&
            (a.scale - b.scale).sqrMagnitude < 1e-5f && Mathf.Abs(Mathf.DeltaAngle(a.rot, b.rot)) < 0.05f &&
            Mathf.Abs(a.font - b.font) < 0.05f;

        // ------------------------------------------------------------ API

        /// <summary>Что изменил пользователь в сцене по сравнению с последней сборкой (записывает в user_layout.json).</summary>
        public static int Capture(Scene scene, string sceneName)
        {
            var built = Load(BuiltPath(sceneName)).items.ToDictionary(e => e.path);
            if (built.Count == 0) return 0;
            var store = Load(OverridesPath);
            int n = 0;
            foreach (var kv in Snapshot(scene, sceneName))
            {
                if (!built.TryGetValue(kv.Key, out var was)) continue;
                var now = kv.Value;
                // размер «не в пропорциях» пользователь мышкой не задаёт — это следствие смены картинки
                if (now.badAspect) now.size = was.size;
                if (Same(was, now)) continue;
                store.items.RemoveAll(e => e.scene == sceneName && e.path == kv.Key);
                store.items.Add(now);
                n++;
            }
            if (n > 0)
            {
                Write(OverridesPath, store);
                // запомнить новое «как есть», чтобы следующий захват видел только новые правки
                var snap = Load(BuiltPath(sceneName));
                snap.items = Snapshot(scene, sceneName).Values.ToList();
                Write(BuiltPath(sceneName), snap);
            }
            return n;
        }

        /// <summary>Собирает правки со всех сцен игры (открытые берутся из памяти — с несохранёнными правками).</summary>
        [MenuItem("Всё по полкам/Раскладка/Сохранить мои правки (перемещения мышкой)", priority = 40)]
        public static void CaptureAllMenu()
        {
            int n = CaptureAll();
            EditorUtility.DisplayDialog("Раскладка", n > 0
                ? $"Запомнено правок: {n}. Они сохранятся при пересборке сцен."
                : "Новых правок не найдено (или сцены ещё не собирались этой версией).", "OK");
        }

        public static int CaptureAll()
        {
            int total = 0;
            foreach (var name in Scenes)
            {
                string path = SceneBuilder.ScenesDir + "/" + name + ".unity";
                if (!File.Exists(path)) continue;
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = false;
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    opened = true;
                }
                int n = Capture(scene, name);
                if (n > 0) Debug.Log($"[AllOnShelves] Раскладка: запомнено правок в {name}: {n}");
                total += n;
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            return total;
        }

        /// <summary>После сборки сцены: применить правки пользователя и запомнить «как построено».</summary>
        public static void ApplyAndSnapshot(Scene scene, string sceneName)
        {
            int applied = 0;
            var missing = new List<string>();
            var stale = new List<Entry>();
            foreach (var e in Overrides.Where(e => e.scene == sceneName))
            {
                var t = Find(scene, e.path) as RectTransform;
                if (t == null) { missing.Add(e.path); continue; }
                // размер такого объекта считает компонент под размер окна Game: запись — след окна, а не рука
                // (29.09.2026 фоны «запомнились» растянутыми после проверки экрана 4:3)
                if (SizeDriven(t)) { stale.Add(e); continue; }
                bool point = t.anchorMin == t.anchorMax;
                if (point && e.hasAnchor) t.anchorMin = t.anchorMax = e.anchor;
                t.anchoredPosition = e.pos;
                // перетащенное к другому краю экрана — привязать к этому краю (иначе на экранах
                // шире/уже 16:9 кнопка уезжала от края: «Звёздный путь» и «Завоз дня», 29.09.2026)
                if (point && ScreenSized(t.parent as RectTransform)) NearestEdge(t);
                t.sizeDelta = e.size;
                t.localScale = e.scale;
                t.localEulerAngles = new Vector3(0f, 0f, e.rot);
                var tmp = t.GetComponent<TextMeshProUGUI>();
                if (tmp != null && e.font > 0f)
                {
                    if (tmp.enableAutoSizing) tmp.fontSizeMax = e.font;
                    else tmp.fontSize = e.font;
                }
                applied++;
            }
            if (applied > 0) Debug.Log($"[AllOnShelves] Раскладка: применено ручных правок в {sceneName}: {applied}");
            if (stale.Count > 0)
            {
                var store = Load(OverridesPath);
                store.items.RemoveAll(x => stale.Any(y => y.scene == x.scene && y.path == x.path));
                Write(OverridesPath, store);
                Debug.Log($"[AllOnShelves] Раскладка: убраны записи о размерах, которые считает код, в {sceneName}: {stale.Count}");
            }
            foreach (var m in missing) Debug.LogWarning($"[AllOnShelves] Раскладка: объект не найден (переименован?) {sceneName}: {m}");
            Write(BuiltPath(sceneName), new Store { items = Snapshot(scene, sceneName).Values.ToList() });
        }

        // ------------------------------------------------------------ для редактора интерфейса (29.09.2026)

        static readonly Dictionary<UnityEngine.SceneManagement.Scene, (double time, HashSet<Transform> set)> _drivenCache =
            new Dictionary<UnityEngine.SceneManagement.Scene, (double, HashSet<Transform>)>();

        /// <summary>Место этого объекта задаёт код — правка мышкой не сохранится.</summary>
        public static bool IsDriven(Transform t)
        {
            if (t == null) return false;
            var scene = t.gameObject.scene;
            if (!scene.IsValid()) return false;
            // список пересчитываем не чаще раза в секунду: окно редактора спрашивает его на каждой перерисовке
            double now = EditorApplication.timeSinceStartup;
            if (!_drivenCache.TryGetValue(scene, out var c) || now - c.time > 1.0)
            {
                c = (now, Driven(scene));
                _drivenCache[scene] = c;
            }
            return c.set.Contains(t);
        }

        /// <summary>Запись о текущем состоянии объекта (как её пишет захват правок).</summary>
        public static Entry EntryOf(RectTransform rt, string sceneName)
        {
            var e = new Entry
            {
                scene = sceneName, path = PathOf(rt), pos = rt.anchoredPosition, size = rt.sizeDelta,
                scale = rt.localScale, rot = rt.localEulerAngles.z,
                anchor = rt.anchorMin, hasAnchor = rt.anchorMin == rt.anchorMax,
            };
            var tmp = rt.GetComponent<TextMeshProUGUI>();
            if (tmp != null) e.font = tmp.enableAutoSizing ? tmp.fontSizeMax : tmp.fontSize;
            return e;
        }

        /// <summary>Добавить/заменить правки в user_layout.json (по сцене и пути).</summary>
        public static void Record(IEnumerable<Entry> entries)
        {
            var store = Load(OverridesPath);
            foreach (var e in entries)
            {
                store.items.RemoveAll(x => x.scene == e.scene && x.path == e.path);
                store.items.Add(e);
            }
            Write(OverridesPath, store);
        }

        /// <summary>Забыть правки этих объектов: после пересборки сцен они вернутся на места из кода.</summary>
        public static int Forget(string sceneName, IEnumerable<string> paths)
        {
            var set = new HashSet<string>(paths);
            var store = Load(OverridesPath);
            int n = store.items.RemoveAll(x => x.scene == sceneName && set.Contains(x.path));
            if (n > 0) Write(OverridesPath, store);
            return n;
        }

        /// <summary>Масштаб объекта «в покое» (без анимации): из правки пользователя или из последней сборки.</summary>
        public static Vector3 RestScale(string sceneName, string path)
        {
            var o = Overrides.FirstOrDefault(e => e.scene == sceneName && e.path == path);
            if (o != null) return o.scale;
            var b = Load(BuiltPath(sceneName)).items.FirstOrDefault(e => e.path == path);
            return b != null ? b.scale : Vector3.one;
        }

        /// <summary>Есть ли у объекта запомненная правка.</summary>
        public static bool HasOverride(string sceneName, string path) =>
            Overrides.Any(e => e.scene == sceneName && e.path == path);

        /// <summary>Применить все правки к файлам сцен Boot/Hub/Game (в режиме редактора, не в игре).</summary>
        public static void ApplyToSceneFiles()
        {
            foreach (var name in Scenes)
            {
                string path = SceneBuilder.ScenesDir + "/" + name + ".unity";
                if (!File.Exists(path)) continue;
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = false;
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    opened = true;
                }
                ApplyAndSnapshot(scene, name);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static bool SizeDriven(Transform t) => t.GetComponent<AspectRatioFitter>() != null || t.GetComponent<ScaleToParent>() != null;

        /// <summary>Родитель во весь экран: цепочка растянутых без отступов объектов до холста.</summary>
        static bool ScreenSized(RectTransform p)
        {
            for (var x = p; x != null; x = x.parent as RectTransform)
            {
                if (x.GetComponent<Canvas>() != null && x.parent == null) return true;
                if (x.anchorMin != Vector2.zero || x.anchorMax != Vector2.one) return false;
                if (x.offsetMin.sqrMagnitude > 0.01f || x.offsetMax.sqrMagnitude > 0.01f) return false;
                if (x.GetComponent<ScaleToParent>() != null || x.GetComponent<AspectRatioFitter>() != null) return false;
            }
            return false;
        }

        /// <summary>
        /// Привязка к ближайшему краю по месту на макете 1920×1080: левая треть экрана — к левому краю,
        /// правая — к правому, середина — к центру (и так же по высоте). Место на макете не меняется.
        /// </summary>
        public static void NearestEdge(RectTransform t)
        {
            var size = new Vector2(1920f, 1080f);
            var a = t.anchorMin;
            var at = (a - new Vector2(0.5f, 0.5f)) * size + t.anchoredPosition;   // от центра экрана
            float Pick(float v, float half) => v < -half / 3f ? 0f : v > half / 3f ? 1f : 0.5f;
            var na = new Vector2(Pick(at.x, size.x / 2f), Pick(at.y, size.y / 2f));
            t.anchorMin = t.anchorMax = na;
            t.anchoredPosition = at - (na - new Vector2(0.5f, 0.5f)) * size;
        }

        [MenuItem("Всё по полкам/Раскладка/Показать файл правок", priority = 41)]
        static void Reveal()
        {
            if (!File.Exists(OverridesPath)) Write(OverridesPath, new Store());
            AssetDatabase.Refresh();
            var a = AssetDatabase.LoadAssetAtPath<TextAsset>(OverridesPath);
            if (a != null) { Selection.activeObject = a; EditorGUIUtility.PingObject(a); }
        }

        [MenuItem("Всё по полкам/Раскладка/Забыть все мои правки", priority = 42)]
        static void ForgetAll()
        {
            if (!EditorUtility.DisplayDialog("Раскладка", "Удалить все запомненные ручные правки? При следующей сборке сцены вернутся к раскладке из кода.", "Удалить", "Отмена")) return;
            Write(OverridesPath, new Store());
            AssetDatabase.Refresh();
        }
    }
}
