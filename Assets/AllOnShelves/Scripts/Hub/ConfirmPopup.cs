using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Универсальное окно: сообщение (Info) или вопрос (Ask).</summary>
    public class ConfirmPopup : Popup
    {
        public Image icon;
        public TextMeshProUGUI title;
        public TextMeshProUGUI text;
        public Button okButton;
        public TextMeshProUGUI okText;
        public Button cancelButton;
        VfxPlayer _rays;

        public void Info(string t, string body, string sprite, Action onOk)
        {
            Setup(t, body, sprite, "Отлично!", onOk, false);
        }

        /// <summary>Сообщение со своей подписью кнопки («Забрать»).</summary>
        public void Info(string t, string body, string sprite, string okLabel, Action onOk)
        {
            Setup(t, body, sprite, okLabel, onOk, false);
        }

        public void Ask(string t, string body, string sprite, Action onOk)
        {
            Setup(t, body, sprite, "Да", onOk, true);
        }

        void Setup(string t, string body, string sprite, string ok, Action onOk, bool cancel)
        {
            title.text = t;
            text.text = body;
            var sp = ArtLibrary.S(sprite);
            icon.gameObject.SetActive(sp != null);
            icon.sprite = sp;
            icon.preserveAspect = true;
            okText.text = ok;
            // награда (Info с картинкой) — лучи позади неё, пока окно открыто
            Vfx.Keep(ref _rays, false, null);
            if (!cancel && sp != null) Vfx.Keep(ref _rays, true, () => Vfx.Behind("vfx_reward_rays", icon.transform, icon.rectTransform.rect.height * 2.6f));
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(() => { Vfx.Keep(ref _rays, false, null); Hide(onOk); });
            cancelButton.gameObject.SetActive(cancel);
            var okRt = (RectTransform)okButton.transform;
            okRt.anchoredPosition = new Vector2(cancel ? 150f : 0f, okRt.anchoredPosition.y);
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => { Vfx.Keep(ref _rays, false, null); Hide(); });
            Show();
        }
    }
}
