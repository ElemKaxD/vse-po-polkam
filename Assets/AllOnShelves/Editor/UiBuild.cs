using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.EditorTools
{
    /// <summary>Помощники построения UI-объектов в сценах (только редактор).</summary>
    public static class UiBuild
    {
        public static TMP_FontAsset Font;
        public static Material OutlineMat;
        public static readonly Color Brown = new Color32(0x4A, 0x2F, 0x24, 255);
        public static readonly Color Cream = new Color32(0xFF, 0xF4, 0xE3, 255);
        public static readonly Color Orange = new Color32(0xF2, 0x66, 0x3D, 255);

        static System.Collections.Generic.Dictionary<string, Sprite> _cache;

        /// <summary>
        /// Единственные спрайты, которые можно резать 9-slice: простые панели и рамки без рисунка на краях.
        /// Всё остальное (кнопки, плашки, ценники, облачка, иконки) — только в родных пропорциях.
        /// </summary>
        public static readonly System.Collections.Generic.HashSet<string> Sliceable = new System.Collections.Generic.HashSet<string>
        {
            // только гладкие рамки: у них по краям нет рисунка, который поедет при растягивании.
            // ui_panel_card и ui_panel_popup сюда НЕ входят — у них листья по краям и сердечко снизу
            // полосы и ползунки: середина ровная, поэтому тянутся 9-slice (Tools/ArtPipeline/gen_bars.py)
            "ui_bar_track", "ui_bar_fill",
            // вертикальный ползунок рейтинга (v4): «таблетка» стоя — режется по высоте
            "ui_scroll_track_v", "ui_scroll_knob_v",
            "ui_progress_frame", "ui_progress_fill",
            "brd_slot_glow", "brd_slot_outline", "brd_belt_tile",
            "ui_tile_button", "ui_tile_button_flat2d_generated",
            // плашки из пака 26.09: середина ровная, рисунок только по краям
            "ui_toast", "ui_plate_wide", "ui_tooltip", "ui_tile_slot", "ui_tile_slot_gold", "ui_ribbon_title",
        };

        public static bool CanSlice(Sprite sp) => sp != null && Sliceable.Contains(sp.name) && sp.border != Vector4.zero;

        public static float Aspect(Sprite sp) => sp == null ? 1f : sp.rect.width / sp.rect.height;

        /// <summary>Наибольший размер с пропорциями спрайта, вписанный в box.</summary>
        public static Vector2 Fit(Sprite sp, Vector2 box)
        {
            if (sp == null || box.x <= 0f || box.y <= 0f) return box;
            float a = Aspect(sp);
            return box.x / box.y > a ? new Vector2(box.y * a, box.y) : new Vector2(box.x, box.x / a);
        }

        /// <summary>
        /// Полоса прогресса: жёлоб + заливка одним куском. Высота — как у картинки жёлоба в игре,
        /// края не плющатся (pixelsPerUnitMultiplier). Заливка белая, цвет задаёт вызывающий.
        /// </summary>
        public static Image Bar(string name, Transform parent, Vector2 pos, float width, float height, Color fill,
                                out Image track)
        {
            track = Img(name, parent, "ui_bar_track", pos, new Vector2(width, height), sliced: true);
            float inset = height * 0.22f;
            var f = Img("Fill", track.transform, "ui_bar_fill", Vector2.zero,
                        new Vector2(width - inset * 2f, height - inset), sliced: true, color: fill);
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            SlicedFill.Attach(f);   // 9-slice сохраняется, ширина — по заполнению (03.10.2026)
            return f;
        }

        /// <summary>Размер по высоте с родными пропорциями.</summary>
        public static Vector2 ByHeight(string sprite, float h) => new Vector2(h * Aspect(S(sprite)), h);
        public static Vector2 ByWidth(string sprite, float w) => new Vector2(w, w / Aspect(S(sprite)));

        public static void ResetCache() => _cache = null;

        public static Sprite S(string name)
        {
            if (_cache == null)
            {
                _cache = new System.Collections.Generic.Dictionary<string, Sprite>();
                foreach (var g in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/AllOnShelves/Art" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sp != null) _cache[System.IO.Path.GetFileNameWithoutExtension(path)] = sp;
                }
            }
            if (_cache.TryGetValue(name, out var s)) return s;
            Debug.LogWarning("Sprite not found: " + name);
            return null;
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Rect(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            var img = rt.GetComponent<Image>();
            // размер уже подогнан под спрайт в Img/Button — не перезаписываем его искажённым
            bool keep = img != null && img.sprite != null && img.type == Image.Type.Simple && !Sliceable.Contains(img.sprite.name);
            rt.sizeDelta = keep ? Fit(img.sprite, size) : size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Img(string name, Transform parent, string sprite, Vector2 pos, Vector2 size, bool raycast = false,
                                bool sliced = false, Color? color = null, bool preserve = true)
        {
            var rt = Rect(Node(name, parent), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = string.IsNullOrEmpty(sprite) ? null : S(sprite);
            img.raycastTarget = raycast;
            if (sliced && CanSlice(img.sprite))
            {
                img.type = Image.Type.Sliced;
                // 9-slice режет края в пикселях спрайта: если плашку рисуют ниже её картинки,
                // торцы и уголки остаются крупными и «плющатся». Масштабируем края вместе с высотой.
                float nativeH = img.sprite.rect.height;
                if (nativeH > 0f && size.y > 0f) img.pixelsPerUnitMultiplier = Mathf.Max(1f, nativeH / size.y);
                // стоячая «таблетка» тянется в высоту — края масштабируем по ширине
                if (img.sprite.name.StartsWith("ui_scroll_") && size.x > 0f)
                    img.pixelsPerUnitMultiplier = Mathf.Max(1f, img.sprite.rect.width / size.x);
            }
            else if (img.sprite != null && Sliceable.Contains(img.sprite.name)) img.preserveAspect = false; // простые заливки и рамки
            else if (img.sprite != null)
            {
                // никаких растяжений: рамка = пропорции картинки
                img.preserveAspect = true;
                rt.sizeDelta = Fit(img.sprite, size);
            }
            if (color.HasValue) img.color = color.Value;
            return img;
        }

        /// <summary>Полноэкранный фон, заполняющий экран с сохранением пропорций.</summary>
        public static Image Background(string name, Transform parent, string sprite)
        {
            var rt = Stretch(Node(name, parent));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = S(sprite);
            img.raycastTarget = false;
            var fit = rt.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = img.sprite != null ? img.sprite.rect.width / img.sprite.rect.height : 16f / 9f;
            return img;
        }

        public static Image Dim(Transform parent, float alpha = 0.55f)
        {
            var rt = Stretch(Node("Dim", parent));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.12f, 0.08f, 0.06f, alpha);
            img.raycastTarget = true;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Vector2 pos, Vector2 box,
                                           Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.Center,
                                           bool outline = false, bool autoSize = false)
        {
            var rt = Rect(Node(name, parent), pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color ?? Brown;
            t.alignment = align;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            t.overflowMode = TextOverflowModes.Overflow;
            if (outline && OutlineMat != null) t.fontSharedMaterial = OutlineMat;
            if (autoSize)
            {
                t.enableAutoSizing = true;
                t.fontSizeMax = size;
                t.fontSizeMin = Mathf.Max(14f, size * 0.45f);
                // TMP ужимает текст только тогда, когда рамка его ограничивает:
                // при Overflow строки просто вылезают за плашку (облачко обучения, подписи кнопок)
                t.overflowMode = TextOverflowModes.Truncate;
            }
            return t;
        }

        public static Button Button(string name, Transform parent, string sprite, Vector2 pos, Vector2 size, string label = null,
                                    float labelSize = 40f, string icon = null, Color? labelColor = null, bool outline = true)
        {
            var img = Img(name, parent, sprite, pos, size, raycast: true, sliced: true, preserve: false);
            size = img.rectTransform.sizeDelta;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.ColorTint;
            var cols = b.colors;
            cols.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.8f);
            b.colors = cols;
            img.gameObject.AddComponent<UiButton>();
            // область подписи: внутренние 84% кнопки; с иконкой — от правого края иконки
            float left = -size.x * 0.38f, right = size.x * 0.38f; // края кнопок декоративные (кант, «гвоздики»)
            if (!string.IsNullOrEmpty(icon))
            {
                float isz = Mathf.Min(size.y * 0.62f, 90f);
                if (string.IsNullOrEmpty(label)) Img("Icon", img.transform, icon, Vector2.zero, new Vector2(isz, isz));
                else
                {
                    float ix = -size.x / 2f + size.y * 0.2f + isz / 2f;
                    Img("Icon", img.transform, icon, new Vector2(ix, 2f), new Vector2(isz, isz));
                    left = ix + isz / 2f + 6f;
                }
            }
            float textX = (left + right) / 2f;
            if (!string.IsNullOrEmpty(label))
            {
                var t = Text("Label", img.transform, label, labelSize, new Vector2(textX, 3f), new Vector2(right - left, size.y * 0.7f),
                             labelColor ?? Color.white, TextAlignmentOptions.Center, outline, autoSize: true);
                t.enableWordWrapping = false; // одна строка: длинный текст уменьшается, а не переносится
                t.fontSizeMin = Mathf.Max(14f, labelSize * 0.4f);
            }
            return b;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>();
        }

        public static TextMeshProUGUI Label(Button b) => b.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
        public static Image Icon(Button b) => b.transform.Find("Icon")?.GetComponent<Image>();

        /// <summary>Маленький кружок-счётчик в углу кнопки.</summary>
        public static TextMeshProUGUI Badge(Transform parent, Vector2 pos, string text, Color color)
        {
            // простой красный кружок: на icon_badge_count нарисован «%», цифра ложилась поверх него
            var size = ByHeight("ui_badge_notification_flat2d", 58f);
            var bg = Img("Badge", parent, "ui_badge_notification_flat2d", pos, size);
            return Text("Count", bg.transform, text, 28, new Vector2(0, 1), size, Color.white, TextAlignmentOptions.Center, true);
        }

        public static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"Field {field} not found on {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
