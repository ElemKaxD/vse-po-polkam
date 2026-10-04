using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AllOnShelves
{
    /// <summary>Кнопка с откликом: наведение 105%, нажатие 95%, звук клика (ГДД 12.3).</summary>
    [RequireComponent(typeof(Button))]
    public class UiButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        Vector3 _base = Vector3.one;
        bool _over;

        void Awake()
        {
            _base = transform.localScale;
            GetComponent<Button>().onClick.AddListener(() =>
            {
                AudioService.Play("sfx_button");
                var rt = (RectTransform)transform;
                Vfx.Play("vfx_button_press", rt.position, Mathf.Clamp(Mathf.Max(rt.rect.width, rt.rect.height) * 1.4f, 120f, 320f));
            });
        }

        bool Active => GetComponent<Button>().interactable;

        public void OnPointerEnter(PointerEventData e)
        {
            _over = true;
            if (Active) Tween.Scale(transform, _base * 1.05f, 0.1f);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _over = false;
            Tween.Scale(transform, _base, 0.1f);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Active) Tween.Scale(transform, _base * 0.95f, 0.06f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            Tween.Scale(transform, _over ? _base * 1.05f : _base, 0.1f);
        }
    }
}
