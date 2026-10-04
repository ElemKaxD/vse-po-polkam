using TMPro;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>
    /// Окно «Команда» (03.10.2026): все сотрудники карточками 5×2. Открывается из окна смены по «+» в пустом
    /// месте; нажал на карточку — сотрудник встаёт на смену, окно закрывается. Логика карточек — в ShiftPopup.
    /// </summary>
    public class StaffPickerPopup : Popup
    {
        public TextMeshProUGUI hintText;
        public Button closeButton;

        void Awake()
        {
            closeButton.onClick.AddListener(() => Hide());
        }
    }
}
