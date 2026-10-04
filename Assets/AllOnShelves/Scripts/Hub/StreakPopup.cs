using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Окно серии «Лучший продавец» (03.10.2026, картинка streak_empty): пять ступеней с бонусами, горят пройденные;
    /// внизу — что даёт серия и как её не потерять.
    /// </summary>
    public class StreakPopup : Popup
    {
        public Image[] stepsOn = new Image[5];        // огонь на ступени (ui_streak_step_on поверх нарисованной)
        public TextMeshProUGUI[] stepNums = new TextMeshProUGUI[5];
        public Image[] icons = new Image[5];
        public TextMeshProUGUI title;
        public TextMeshProUGUI info;
        public Button okButton;

        void Awake() { okButton.onClick.AddListener(() => Hide()); }

        public void Open()
        {
            Refresh();
            Show();
            for (int i = 0; i < stepsOn.Length; i++)
                if (stepsOn[i].gameObject.activeSelf) Tween.Punch(stepsOn[i].transform, 0.12f, 0.3f + 0.05f * i);
        }

        void Refresh()
        {
            int w = Streak.Wins;
            title.text = w > 0 ? $"Лучший продавец · ×{w}" : "Лучший продавец";
            for (int i = 0; i < stepsOn.Length; i++)
            {
                bool lit = w >= Streak.Steps[i];
                stepsOn[i].gameObject.SetActive(lit);
                stepNums[i].text = Streak.Steps[i].ToString();
                icons[i].sprite = ArtLibrary.S(Streak.Icons[i]);
                icons[i].preserveAspect = true;
                icons[i].color = lit ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            }
            int next = -1;
            for (int i = 0; i < Streak.Steps.Length && next < 0; i++) if (w < Streak.Steps[i]) next = i;
            string tail = next >= 0
                ? $"До «{Streak.Names[next]}» — побед: {Streak.Steps[next] - w}."
                : "Все бонусы горят! Ещё 5 побед — снова большой сундук.";
            info.text = (w == 0 ? "Выигрывай уровни подряд — каждая победа зажигает ступень и даёт бонус в начале уровня. "
                                : "Бонусы работают, пока серия горит. Проиграл — отмотай назад или позови грузчика, и серия цела. ") + tail;
        }
    }
}
