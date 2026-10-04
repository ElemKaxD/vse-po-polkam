using System;
using UnityEngine;

namespace AllOnShelves
{
    /// <summary>Базовое всплывающее окно: затемнение + панель с «пружинным» появлением.</summary>
    public class Popup : MonoBehaviour
    {
        public CanvasGroup group;
        public RectTransform panel;

        public bool IsOpen => gameObject.activeSelf;

        public virtual void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            group.alpha = 0f;
            group.interactable = true;
            group.blocksRaycasts = true;
            Tween.Fade(group, 1f, 0.18f);
            if (panel != null)
            {
                panel.localScale = Vector3.one * 0.8f;
                Tween.Scale(panel, Vector3.one, 0.3f, Ease.OutBack);
            }
            AudioService.Play("sfx_popup_open");
        }

        public virtual void Hide(Action done = null)
        {
            if (!gameObject.activeSelf) { done?.Invoke(); return; }
            group.interactable = false;
            AudioService.Play("sfx_popup_close");
            Tween.Fade(group, 0f, 0.15f, () =>
            {
                gameObject.SetActive(false);
                done?.Invoke();
            });
        }

        public virtual void HideInstant()
        {
            gameObject.SetActive(false);
        }
    }
}
