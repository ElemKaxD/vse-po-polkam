using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>Карточка сотрудника в окне смены: значок, имя, звёзды уровня, «Советую», замок.</summary>
    public class StaffCardView : MonoBehaviour
    {
        public Button button;
        public Image bg;
        public Image face;
        public TextMeshProUGUI nameText;
        public Image[] stars = new Image[4];
        public GameObject recommend;          // «Советую»: этот сотрудник помогает в этом уровне
        public GameObject lockMark;
        public TextMeshProUGUI lockText;      // когда придёт («Ур. 24», «Золотой путь»)
        public GameObject onShift;            // галочка: уже на смене
        [HideInInspector] public string id;
    }
}
