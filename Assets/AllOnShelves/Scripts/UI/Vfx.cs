using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves
{
    /// <summary>
    /// Эффекты-листы кадров из Arcaidia Effector (27.09.2026): Resources/VFX + VfxCatalog.
    /// Vfx.Play("vfx_section_done", позиция, размер) — вспышка поверх всего экрана (свой холст),
    /// с parent — внутри нужного экрана (тогда эффект закрывают окна этого экрана).
    /// Размер — в точках макета 1920×1080, как у всего интерфейса.
    /// </summary>
    public static class Vfx
    {
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static RectTransform _overlay;

        /// <summary>Выключатель на случай слабых устройств.</summary>
        public static bool Enabled = true;

        public static VfxPlayer Play(string name, Vector3 world, float size, Color? tint = null,
                                     float delay = 0f, Transform parent = null, bool? loop = null)
        {
            if (!Enabled || !VfxCatalog.TryGet(name, out var info)) return null;
            var tex = Texture(name);
            if (tex == null) return null;
            var root = parent != null ? parent : Overlay();
            if (root == null) return null;
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage), typeof(VfxPlayer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            float aspect = info.CellW / (float)Mathf.Max(1, info.CellH);
            rt.sizeDelta = aspect >= 1f ? new Vector2(size, size / aspect) : new Vector2(size * aspect, size);
            rt.position = world;
            var img = go.GetComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            img.color = tint ?? Color.white;
            var p = go.GetComponent<VfxPlayer>();
            p.Begin(img, info, loop ?? info.Loop, delay);
            return p;
        }

        /// <summary>Слой эффектов поверх всех экранов (на нём же конфетти Celebration).</summary>
        public static RectTransform Layer => Overlay();

        /// <summary>Эффект позади элемента (сияние за свинкой): тот же родитель, слоем ниже — элемент поверх.</summary>
        public static VfxPlayer Behind(string name, Transform target, float size, bool? loop = null)
        {
            if (target == null || target.parent == null) return null;
            var p = Play(name, target.position, size, parent: target.parent, loop: loop);
            if (p != null) p.transform.SetSiblingIndex(target.GetSiblingIndex());
            return p;
        }

        /// <summary>Цикл-эффект живёт, пока on: включился — start(), выключился — Stop() (03.10.2026).</summary>
        public static void Keep(ref VfxPlayer p, bool on, System.Func<VfxPlayer> start)
        {
            if (on && p == null) p = start();
            else if (!on && p != null) { p.Stop(); p = null; }
        }

        /// <summary>Эффект по центру элемента интерфейса.</summary>
        public static VfxPlayer At(string name, Transform target, float size, Color? tint = null, float delay = 0f)
            => target == null ? null : Play(name, target.position, size, tint, delay);

        static Texture2D Texture(string name)
        {
            if (_tex.TryGetValue(name, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>("VFX/" + name);
            _tex[name] = t;
            return t;
        }

        /// <summary>Свой холст поверх всех экранов — эффекты не зависят от того, какой экран открыт.</summary>
        static RectTransform Overlay()
        {
            if (_overlay != null) return _overlay;
            var go = new GameObject("VfxOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Object.DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            go.AddComponent<AdaptiveScaler>();
            _overlay = (RectTransform)go.transform;
            return _overlay;
        }
    }

    /// <summary>Проигрывает лист кадров на RawImage: вспышка удаляется сама, цикл — по Stop().</summary>
    public class VfxPlayer : MonoBehaviour
    {
        RawImage _img;
        VfxCatalog.Info _info;
        bool _loop;
        float _t, _delay;

        public void Begin(RawImage img, VfxCatalog.Info info, bool loop, float delay)
        {
            _img = img; _info = info; _loop = loop; _delay = delay; _t = 0f;
            _img.enabled = delay <= 0f;
            Show(0);
        }

        void Update()
        {
            // таблица кадров не сериализуется: после перезагрузки скриптов в Play она пустая — такой эффект убираем
            if (_info.Frames <= 0 || _img == null) { Destroy(gameObject); return; }
            if (_delay > 0f)
            {
                _delay -= Time.unscaledDeltaTime;
                if (_delay > 0f) return;
                _img.enabled = true;
            }
            _t += Time.unscaledDeltaTime;
            int f = Mathf.FloorToInt(_t * Mathf.Max(1f, _info.Fps));
            if (f >= _info.Frames)
            {
                if (!_loop) { Destroy(gameObject); return; }
                f %= _info.Frames;
            }
            Show(f);
        }

        void Show(int f)
        {
            int c = f % _info.Cols, r = f / _info.Cols;
            // кадры считаем в пикселях от левого верхнего угла: лист может быть добит справа и снизу
            // до кратного 4 ради сжатия в WebGL (fix_tex4.py), и тогда 1/Cols — уже неточно
            var tex = _img.texture;
            float w = tex != null ? _info.CellW / (float)tex.width : 1f / _info.Cols;
            float h = tex != null ? _info.CellH / (float)tex.height : 1f / _info.Rows;
            // первая строка листа — сверху, а у uvRect ноль внизу
            _img.uvRect = new Rect(c * w, 1f - (r + 1) * h, w, h);
        }

        public void Stop()
        {
            if (this != null && gameObject != null) Destroy(gameObject);
        }
    }
}
