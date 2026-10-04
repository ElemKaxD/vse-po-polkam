using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.EditorTools
{
    /// <summary>
    /// Перетаскивание картинок мышкой прямо в сцену.
    /// Unity по умолчанию делает из PNG «мировой» объект (SpriteRenderer) — интерфейс рисуется поверх него,
    /// и картинка не видна. Этот обработчик подменяет поведение: картинка добавляется в канву как UI Image,
    /// в родных пропорциях, туда, куда её бросили. Плюс лечит уже добавленные невидимые объекты.
    /// </summary>
    [InitializeOnLoad]
    public static class UiDropTool
    {
        static UiDropTool()
        {
            SceneView.duringSceneGui -= OnScene;
            SceneView.duringSceneGui += OnScene;
        }

        // ============================================================ перетаскивание в окно Scene

        static void OnScene(SceneView view)
        {
            var e = Event.current;
            if (e == null || (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)) return;

            var sprites = Dragged();
            if (sprites.Count == 0) return;
            var canvas = FindCanvas();
            if (canvas == null) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type != EventType.DragPerform) { e.Use(); return; }

            DragAndDrop.AcceptDrag();
            var parent = DropParent(canvas);
            var pos = PointOnCanvas(canvas, e.mousePosition);
            var made = new List<Object>();
            float shift = 0f;
            foreach (var sp in sprites)
            {
                var img = Create(sp, parent, pos + new Vector3(shift, -shift, 0f));
                made.Add(img.gameObject);
                shift += 24f;
            }
            Selection.objects = made.ToArray();
            e.Use();
        }

        static List<Sprite> Dragged()
        {
            var list = new List<Sprite>();
            foreach (var o in DragAndDrop.objectReferences)
            {
                if (o is Sprite s) { list.Add(s); continue; }
                if (o is Texture2D t)
                {
                    var sp = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(t));
                    if (sp != null) list.Add(sp);
                }
            }
            return list;
        }

        /// <summary>Канва открытой сцены (берём ту, где строится интерфейс игры).</summary>
        static Canvas FindCanvas()
        {
            var all = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return all.FirstOrDefault(c => c.isRootCanvas && c.name.EndsWith("Canvas")) ?? all.FirstOrDefault(c => c.isRootCanvas);
        }

        /// <summary>Куда класть: в выделенный объект канвы, иначе в открытый экран, иначе в корень канвы.</summary>
        static RectTransform DropParent(Canvas canvas)
        {
            var sel = Selection.activeTransform as RectTransform;
            if (sel != null && sel.GetComponentInParent<Canvas>() == canvas) return sel;
            var screens = canvas.transform.Find("Screens");
            if (screens != null)
                foreach (Transform t in screens)
                    if (t.gameObject.activeSelf) return (RectTransform)t;
            return (RectTransform)canvas.transform;
        }

        /// <summary>Точка под курсором в плоскости канвы.</summary>
        static Vector3 PointOnCanvas(Canvas canvas, Vector2 mouse)
        {
            var t = canvas.transform;
            var plane = new Plane(t.forward, t.position);
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            return plane.Raycast(ray, out float d) ? ray.GetPoint(d) : t.position;
        }

        // ============================================================ создание картинки

        /// <summary>Добавляет спрайт в канву как UI Image в родных пропорциях.</summary>
        public static Image Create(Sprite sprite, RectTransform parent, Vector3 worldPos)
        {
            var go = new GameObject(sprite.name, typeof(RectTransform), typeof(Image)) { layer = 5 };
            Undo.RegisterCreatedObjectUndo(go, "Добавить картинку");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            rt.sizeDelta = sprite.rect.size;
            rt.position = worldPos;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            var p = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
            EditorUtility.SetDirty(go);
            return img;
        }

        // ============================================================ лечение невидимых

        [MenuItem("Всё по полкам/Починить невидимые картинки", priority = 22)]
        public static void FixInvisibleMenu()
        {
            int n = FixInvisible();
            EditorUtility.DisplayDialog("Невидимые картинки", n > 0
                ? $"Переведено в интерфейс: {n}. Теперь их видно, можно двигать мышкой."
                : "Невидимых картинок не нашлось — всё уже в интерфейсе.", "OK");
        }

        /// <summary>
        /// Картинки, брошенные в сцену как «мировые» (SpriteRenderer), превращает в UI Image
        /// с тем же местом и размером. Возвращает, сколько починил.
        /// </summary>
        public static int FixInvisible()
        {
            var canvas = FindCanvas();
            if (canvas == null) return 0;
            int n = 0;
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (sr.sprite == null) continue;
                var t = sr.transform;
                var parent = t.parent as RectTransform ?? (RectTransform)canvas.transform;
                var img = Create(sr.sprite, parent, t.position);
                img.name = sr.name.Replace(" (1)", "").Trim();
                img.color = sr.color;
                img.raycastTarget = false;
                // размер в точках интерфейса = размер картинки в юнитах × собственный масштаб объекта
                float k = sr.sprite.pixelsPerUnit;
                img.rectTransform.sizeDelta = new Vector2(sr.sprite.rect.width / k * Mathf.Abs(t.localScale.x),
                                                          sr.sprite.rect.height / k * Mathf.Abs(t.localScale.y));
                Undo.DestroyObjectImmediate(sr.gameObject);
                n++;
            }
            return n;
        }
    }
}
