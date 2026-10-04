using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.EditorTools
{
    /// <summary>
    /// «Всё по полкам → Иконки и графика». Показывает всю графику игры с именами, ищет по названию,
    /// ставит выбранный спрайт выделенному в сцене объекту и подгоняет рамку под пропорции картинки.
    /// </summary>
    public class ArtBrowserWindow : EditorWindow
    {
        [MenuItem("Всё по полкам/Иконки и графика", priority = 1)]
        static void Open()
        {
            var inspector = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var w = GetWindow<ArtBrowserWindow>("Графика", true, inspector);
            w.minSize = new Vector2(380, 420);
        }

        Sprite[] _all = new Sprite[0];
        string[] _folders = { "Все" };
        int _folder;
        string _search = "";
        float _cell = 104f;
        Vector2 _scroll;
        Sprite _picked;

        void OnEnable() { Reload(); }

        void Reload()
        {
            _all = AssetDatabase.FindAssets("t:Sprite", new[] { ProjectSetup.Root + "/Art" })
                .Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null).OrderBy(s => s.name).ToArray();
            _folders = new[] { "Все" }.Concat(_all.Select(FolderOf).Distinct().OrderBy(s => s)).ToArray();
            _folder = Mathf.Clamp(_folder, 0, _folders.Length - 1);
        }

        static string FolderOf(Sprite s)
        {
            var parts = AssetDatabase.GetAssetPath(s).Split('/');
            return parts.Length >= 2 ? parts[parts.Length - 2] : "";
        }

        void OnGUI()
        {
            // после переимпорта графики ссылки на спрайты умирают — перечитываем список
            if (_all.Length == 0 || System.Array.Exists(_all, s => s == null)) { Reload(); _picked = null; }

            // ---------------- панель управления
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(80));
            _folder = EditorGUILayout.Popup(_folder, _folders, EditorStyles.toolbarPopup, GUILayout.Width(120));
            _cell = GUILayout.HorizontalSlider(_cell, 64f, 200f, GUILayout.Width(80));
            if (GUILayout.Button("Обновить", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                AssetDatabase.Refresh();
                ProjectSetup.BuildArtLibrary();
                UiBuild.ResetCache();
                Reload();
            }
            EditorGUILayout.EndHorizontal();

            // ---------------- что выделено в сцене
            var target = Selection.gameObjects.Select(g => g.GetComponent<Image>()).FirstOrDefault(i => i != null);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (target == null)
                EditorGUILayout.LabelField("Выделите в сцене картинку или кнопку — её спрайт можно будет заменить.", EditorStyles.wordWrappedMiniLabel);
            else
            {
                EditorGUILayout.LabelField("Выделено: " + target.name + "  ·  сейчас: " +
                                           (target.sprite != null ? target.sprite.name : "пусто"), EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                GUI.enabled = _picked != null;
                if (GUILayout.Button(_picked != null ? "Поставить «" + _picked.name + "»" : "Сначала выберите картинку ниже", GUILayout.Height(24)))
                    Apply(target, _picked);
                GUI.enabled = target.sprite != null;
                if (GUILayout.Button("Подогнать размер под пропорции", GUILayout.Height(24), GUILayout.Width(220)))
                    FitToSprite(target);
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            if (_picked != null)
            {
                var r = _picked.rect;
                EditorGUILayout.LabelField($"Выбрано: {_picked.name}  ·  {(int)r.width}×{(int)r.height}  ·  имя скопировано в буфер",
                                           EditorStyles.miniLabel);
            }

            // ---------------- сетка картинок
            var list = _all.Where(s => (_folder == 0 || FolderOf(s) == _folders[_folder]) &&
                                       (string.IsNullOrEmpty(_search) || s.name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0))
                           .ToArray();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            float width = Mathf.Max(position.width - 24f, _cell);
            int cols = Mathf.Max(1, Mathf.FloorToInt(width / _cell));
            int rows = Mathf.CeilToInt(list.Length / (float)cols);
            float cellH = _cell + 18f;
            var area = GUILayoutUtility.GetRect(width, rows * cellH);
            for (int i = 0; i < list.Length; i++)
            {
                var sp = list[i];
                var cell = new Rect(area.x + (i % cols) * _cell, area.y + (i / cols) * cellH, _cell, cellH);
                if (cell.yMax < _scroll.y || cell.y > _scroll.y + position.height) continue; // не рисуем невидимое
                if (_picked == sp) EditorGUI.DrawRect(cell, new Color(0.3f, 0.5f, 0.9f, 0.35f));
                var img = new Rect(cell.x + 4, cell.y + 2, cell.width - 8, cell.height - 22);
                DrawSprite(img, sp);
                GUI.Label(new Rect(cell.x, cell.yMax - 18, cell.width, 16), sp.name, EditorStyles.miniLabel);
                if (Event.current.type == EventType.MouseDown && cell.Contains(Event.current.mousePosition))
                {
                    _picked = sp;
                    EditorGUIUtility.systemCopyBuffer = sp.name;
                    EditorGUIUtility.PingObject(sp);
                    if (Event.current.clickCount == 2 && target != null) Apply(target, sp);
                    Event.current.Use();
                    Repaint();
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField($"Всего картинок: {list.Length} из {_all.Length}", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = _picked != null;
            if (GUILayout.Button(_picked != null ? "Добавить «" + _picked.name + "» на экран" : "Выберите картинку", GUILayout.Height(26)))
                AddToScreen(_picked);
            GUI.enabled = true;
            if (GUILayout.Button("Починить невидимые", GUILayout.Height(26), GUILayout.Width(150)))
                UiDropTool.FixInvisibleMenu();
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Открыть «Просмотр сцен и уровней»"))
                EditorApplication.ExecuteMenuItem("Всё по полкам/Просмотр сцен и уровней");
        }

        /// <summary>Кладёт картинку в центр открытого экрана — сразу видимой.</summary>
        static void AddToScreen(Sprite sp)
        {
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.isRootCanvas);
            if (canvas == null) { EditorUtility.DisplayDialog("Графика", "Откройте сцену с интерфейсом (Hub или Game).", "OK"); return; }
            var img = UiDropTool.Create(sp, (RectTransform)canvas.transform, canvas.transform.position);
            Selection.activeGameObject = img.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        static void DrawSprite(Rect r, Sprite sp)
        {
            if (sp == null) return;
            var tex = sp.texture;
            if (tex == null) return;
            // рисуем в пропорциях картинки, без растяжения
            float a = sp.rect.width / sp.rect.height, box = r.width / r.height;
            if (a > box) { float h = r.width / a; r = new Rect(r.x, r.y + (r.height - h) / 2f, r.width, h); }
            else { float w = r.height * a; r = new Rect(r.x + (r.width - w) / 2f, r.y, w, r.height); }
            var uv = new Rect(sp.rect.x / tex.width, sp.rect.y / tex.height, sp.rect.width / tex.width, sp.rect.height / tex.height);
            GUI.DrawTextureWithTexCoords(r, tex, uv, true);
        }

        static void Apply(Image img, Sprite sp)
        {
            Undo.RecordObject(img, "Смена картинки");
            img.sprite = sp;
            EditorUtility.SetDirty(img);
            FitToSprite(img);
        }

        /// <summary>Приводит рамку к пропорциям картинки (для 9-slice размер не трогаем).</summary>
        static void FitToSprite(Image img)
        {
            if (img.sprite == null || img.type == Image.Type.Sliced || img.type == Image.Type.Tiled) return;
            var rt = img.rectTransform;
            Undo.RecordObject(rt, "Подгонка размера");
            rt.sizeDelta = UiBuild.Fit(img.sprite, rt.sizeDelta);
            EditorUtility.SetDirty(rt);
        }
    }
}
