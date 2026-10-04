using System.Collections;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AllOnShelves.EditorTools
{
    /// <summary>«Всё по полкам → Проверить сцены»: ищет незаполненные ссылки в компонентах игры.</summary>
    public static class SceneValidator
    {
        [MenuItem("Всё по полкам/Проверить сцены", priority = 30)]
        public static string Validate()
        {
            var sb = new StringBuilder();
            int issues = 0;
            foreach (var name in new[] { "Boot", "Hub", "Game" })
            {
                var scene = EditorSceneManager.OpenScene($"{SceneBuilder.ScenesDir}/{name}.unity", OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null) { sb.AppendLine($"{name}: missing script on {root.name}"); issues++; continue; }
                        var ns = mb.GetType().Namespace ?? "";
                        if (!ns.StartsWith("AllOnShelves")) continue;
                        foreach (var f in mb.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (f.IsNotSerialized || f.GetCustomAttribute<HideInInspector>() != null) continue;
                            if (f.GetCustomAttribute<OptionalRefAttribute>() != null) continue;
                            var v = f.GetValue(mb);
                            if (typeof(Object).IsAssignableFrom(f.FieldType))
                            {
                                if ((Object)v == null) { sb.AppendLine($"{name}: {Path(mb.transform)}.{mb.GetType().Name}.{f.Name} = null"); issues++; }
                            }
                            else if (v is IList list && f.FieldType.IsArray && typeof(Object).IsAssignableFrom(f.FieldType.GetElementType()))
                            {
                                for (int i = 0; i < list.Count; i++)
                                    if ((Object)list[i] == null) { sb.AppendLine($"{name}: {Path(mb.transform)}.{mb.GetType().Name}.{f.Name}[{i}] = null"); issues++; }
                            }
                        }
                    }
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var img in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    {
                        var why = AspectProblem(img);
                        if (why != null) { sb.AppendLine($"{name}: {Path(img.transform)} [{img.sprite.name}] {why}"); issues++; }
                    }
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                    {
                        var why = TextProblem(t);
                        if (why != null) { sb.AppendLine($"{name}: {Path(t.transform)} «{t.text}» {why}"); issues++; }
                    }
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var img in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    {
                        if (img.sprite == null || !PanelFields.ContainsKey(img.sprite.name)) continue;
                        var fld = PanelFields[img.sprite.name];
                        foreach (Transform ch in img.transform)
                        {
                            var why = OutsideField(img.rectTransform, ch as RectTransform, fld);
                            if (why != null) { sb.AppendLine($"{name}: {Path(ch)} {why}"); issues++; }
                        }
                    }
            }
            issues += AlphaProblems(sb);
            sb.Insert(0, $"Проблем: {issues}\n");
            Debug.Log("[AllOnShelves] " + sb);
            try { System.IO.File.WriteAllText("Temp/validate.txt", sb.ToString()); } catch { }
            return sb.ToString();
        }

        /// <summary>
        /// Картинка с прозрачностью, импортированная без альфы: прозрачный фон в игре станет чёрным
        /// (табличка полки brd_tag_base, 29.09.2026). Смотрим только файлы с «Alpha Source = None».
        /// </summary>
        static int AlphaProblems(StringBuilder sb)
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/AllOnShelves/Art", "Assets/AllOnShelves/Resources" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti) || ti.alphaSource != TextureImporterAlphaSource.None) continue;
                var tex = new Texture2D(2, 2);
                try
                {
                    if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) continue;
                    var px = tex.GetPixels32();
                    int clear = 0;
                    foreach (var c in px) if (c.a < 250) clear++;
                    if (clear > px.Length / 1000)
                    {
                        sb.AppendLine($"{path}: прозрачная картинка импортирована без альфы — прозрачное станет чёрным " +
                                      "(ArtImportPostprocessor, fullFrame)");
                        n++;
                    }
                }
                finally { Object.DestroyImmediate(tex); }
            }
            return n;
        }

        /// <summary>Поле внутри рамки окна (доли высоты/ширины, сняты с самих картинок 26.09.2026).</summary>
        static readonly System.Collections.Generic.Dictionary<string, Vector3> PanelFields =
            new System.Collections.Generic.Dictionary<string, Vector3>
            {
                { "ui_panel_popup", new Vector3(0.25f, -0.34f, 0.39f) },
                { "ui_panel_wood", new Vector3(0.30f, -0.33f, 0.41f) },
                { "ui_panel_paper", new Vector3(0.33f, -0.38f, 0.43f) },
                { "ui_panel_festive", new Vector3(0.17f, -0.38f, 0.41f) },
                { "ui_panel_dark", new Vector3(0.25f, -0.30f, 0.36f) },
                // плашки v4 (03.10.2026), поле снято с картинок
                { "ui_panel_info", new Vector3(0.269f, -0.365f, 0.403f) },
                { "ui_panel_info_wide", new Vector3(0.246f, -0.333f, 0.421f) },
                { "ui_panel_reward", new Vector3(0.237f, -0.347f, 0.392f) },
                { "ui_panel_shop", new Vector3(0.12f, -0.38f, 0.41f) },
            };
        /// <summary>Что лежит на раме по замыслу: заголовок-лента и крестик.</summary>
        static readonly System.Collections.Generic.HashSet<string> OnFrame =
            new System.Collections.Generic.HashSet<string> { "Title", "Ribbon", "Close", "Glow", "Piggy" };

        /// <summary>Содержимое окна вылезло с кремового поля на деревянную раму или зелёную полосу.</summary>
        static string OutsideField(RectTransform panel, RectTransform ch, Vector3 field)
        {
            if (ch == null || OnFrame.Contains(ch.name)) return null;
            if (!ch.gameObject.activeSelf) return null;
            float w = panel.sizeDelta.x, h = panel.sizeDelta.y;
            if (w <= 1f || h <= 1f) return null;
            var p = ch.anchoredPosition;
            var sz = new Vector2(ch.sizeDelta.x * ch.localScale.x, ch.sizeDelta.y * ch.localScale.y);
            float top = p.y + sz.y / 2f, bottom = p.y - sz.y / 2f;
            float side = Mathf.Abs(p.x) + sz.x / 2f;
            if (top > field.x * h + 6f) return $"выше поля рамки на {top - field.x * h:0} — ложится на верхнюю кромку";
            if (bottom < field.y * h - 6f) return $"ниже поля рамки на {field.y * h - bottom:0} — ложится на нижний бортик";
            if (side > field.z * w + 6f) return $"шире поля рамки на {side - field.z * w:0} — заходит на боковину";
            return null;
        }

        /// <summary>Спрайт искажён: 9-slice у запрещённого спрайта или рамка не в пропорциях картинки.</summary>
        static string AspectProblem(UnityEngine.UI.Image img)
        {
            if (img.sprite == null) return null;
            if (img.type == UnityEngine.UI.Image.Type.Sliced || img.type == UnityEngine.UI.Image.Type.Tiled)
                return UiBuild.Sliceable.Contains(img.sprite.name) ? null : "режется 9-slice, хотя это не простая панель";
            if (img.type != UnityEngine.UI.Image.Type.Simple || UiBuild.Sliceable.Contains(img.sprite.name)) return null;
            var rt = img.rectTransform;
            if (rt.anchorMin != rt.anchorMax || img.GetComponent<UnityEngine.UI.AspectRatioFitter>() != null) return null;
            if (!img.preserveAspect) return "растягивается (preserveAspect выключен)";
            var size = rt.sizeDelta;
            if (size.x <= 0f || size.y <= 0f) return null;
            float a = UiBuild.Aspect(img.sprite), r = size.x / size.y;
            return Mathf.Abs(r / a - 1f) > 0.04f ? $"рамка {size.x:0}×{size.y:0} не в пропорциях картинки ({a:0.00})" : null;
        }

        /// <summary>
        /// Надпись вылезает за свою рамку. Автоподбор кегля учитываем по нижней границе:
        /// если и на минимальном кегле не влезает — плашку надо увеличить или текст сократить.
        /// </summary>
        static string TextProblem(TMPro.TextMeshProUGUI t)
        {
            if (t == null || string.IsNullOrEmpty(t.text)) return null;
            var box = t.rectTransform.rect.size;
            if (box.x <= 1f || box.y <= 1f) return null;
            float want = t.enableAutoSizing ? t.fontSizeMin : t.fontSize;
            bool auto = t.enableAutoSizing;
            float was = t.fontSize;
            t.enableAutoSizing = false;
            t.fontSize = want;
            var need = t.GetPreferredValues(t.text, t.enableWordWrapping ? box.x : 0f, 0f);
            t.fontSize = was;
            t.enableAutoSizing = auto;
            if (need.y > box.y + 2f) return $"не влезает по высоте: надо {need.y:0}, рамка {box.y:0} (кегль {want:0})";
            if (!t.enableWordWrapping && need.x > box.x + 2f) return $"не влезает по ширине: надо {need.x:0}, рамка {box.x:0} (кегль {want:0})";
            return null;
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
