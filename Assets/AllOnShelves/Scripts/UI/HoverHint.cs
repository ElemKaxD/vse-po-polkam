using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AllOnShelves
{
    /// <summary>
    /// Значок «?» у плитки: навёл мышкой — показывается подсказка, убрал — прячется.
    /// На телефоне мыши нет, поэтому нажатие тоже показывает подсказку (просьба 26.09.2026).
    /// </summary>
    public class HoverHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [HideInInspector] public Action<bool> onHover;

        public void OnPointerEnter(PointerEventData e) => onHover?.Invoke(true);
        public void OnPointerExit(PointerEventData e) => onHover?.Invoke(false);
        public void OnPointerClick(PointerEventData e) => onHover?.Invoke(true);
    }
}
