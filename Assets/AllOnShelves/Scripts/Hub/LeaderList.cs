using TMPro;
using UnityEngine;

namespace AllOnShelves.Hub
{
    /// <summary>Вкладка рейтинга с таблицей: пьедестал (если есть), строки, прибитая строка игрока, статус.</summary>
    public class LeaderList : MonoBehaviour
    {
        public PodiumSlot[] podium = new PodiumSlot[0];
        public LeaderRow[] rows = new LeaderRow[0];
        [OptionalRef] public LeaderRow myRow;
        public TextMeshProUGUI status;
        [OptionalRef] public TextMeshProUGUI timer;
        [OptionalRef] public UnityEngine.UI.ScrollRect scroll;   // таблица листается (до 20 мест)   // «Неделя»: сколько осталось до итогов
    }
}
