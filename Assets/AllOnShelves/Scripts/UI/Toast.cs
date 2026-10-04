using TMPro;
using UnityEngine;

namespace AllOnShelves
{
    /// <summary>Короткое сообщение внизу экрана. Один объект Toast в каждой сцене (под Canvas).</summary>
    public class Toast : MonoBehaviour
    {
        public CanvasGroup group;
        public TextMeshProUGUI label;
        public UnityEngine.UI.Image icon;

        static Toast _current;

        void Awake()
        {
            _current = this;
            if (group != null) group.alpha = 0f;
        }

        void OnDestroy() { if (_current == this) _current = null; }

        /// <summary>Сообщение со значком слева (например, монетой).</summary>
        public static void Show(string text, string iconSprite, float seconds = 2f)
        {
            if (_current != null && _current.icon != null)
            {
                var sp = ArtLibrary.S(iconSprite);
                _current.icon.gameObject.SetActive(sp != null);
                if (sp != null) { _current.icon.sprite = sp; _current.icon.preserveAspect = true; }
            }
            ShowCore(text, seconds);
        }

        public static void Show(string text, float seconds = 2f)
        {
            // сообщение без значка: значок предыдущего (если тот ещё не погас) убираем — раньше он оставался
            if (_current != null && _current.icon != null) _current.icon.gameObject.SetActive(false);
            ShowCore(text, seconds);
        }

        static void ShowCore(string text, float seconds)
        {
            if (_current == null) { Debug.Log("[Toast] " + text); return; }
            var t = _current;
            t.label.text = text;
            // текст сдвигается, когда слева стоит значок
            bool withIcon = t.icon != null && t.icon.gameObject.activeSelf;
            t.label.rectTransform.anchoredPosition = new Vector2(withIcon ? 32f : 0f, 0f);
            t.transform.SetAsLastSibling();
            Tween.Fade(t.group, 1f, 0.15f);
            Tween.Run(t, Tween.ChCustom, seconds, Ease.Linear, _ => { }, () =>
            {
                Tween.Fade(t.group, 0f, 0.3f);
                if (t.icon != null) t.icon.gameObject.SetActive(false);
            });
        }
    }
}
