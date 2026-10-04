using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Ячейка комнаты в окне этажа: превью комнаты, табличка с названием, состояние.</summary>
    public class RoomCell : MonoBehaviour
    {
        public Button button;
        public RoomView view;
        public Image shade;                 // затемнение закрытой / не построенной комнаты
        public Image stateIcon;             // замок, доски, стройка, «заселить»
        public TextMeshProUGUI stateText;
        public TextMeshProUGUI nameText;
        public Image face;                  // жилец
        public Image[] stars = new Image[4];
        public Image frame;                 // рамка ui_room_cell (обставлена целиком — золотая)
        public Sprite frameNormal, frameGold;
        public GameObject lockOverlay;      // цепи с замком поверх закрытой комнаты
        public TextMeshProUGUI lockText;    // уровень на бирке замка
        [HideInInspector] public int room;
    }
}
