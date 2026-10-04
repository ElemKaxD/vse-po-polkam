using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AllOnShelves.Game
{
    /// <summary>Запоминает, куда нажали (у Button.onClick точки нет) — для обучения «жми только сюда».</summary>
    public class TapPoint : MonoBehaviour, IPointerDownHandler
    {
        public Action<Vector2, Camera> Pressed;

        public void OnPointerDown(PointerEventData e) => Pressed?.Invoke(e.position, e.pressEventCamera);
    }
}
