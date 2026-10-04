using System;
using TMPro;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>Итог забега «Часа пик»: очки, рекорд, монеты, продолжение за рекламу.</summary>
    public class RushResultPopup : Popup
    {
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI bestText;
        public TextMeshProUGUI coinsText;
        public Button continueButton;
        public Button againButton;
        public Button mapButton;

        public void Setup(int score, int best, int coins, bool canContinue, Action onContinue, Action onAgain, Action onMap)
        {
            scoreText.text = score.ToString();
            bestText.text = "Рекорд: " + best;
            coinsText.text = canContinue ? "Продолжи забег или заверши его" : coins > 0 ? "+" + coins + " монет" : "Монеты за забеги сегодня уже получены";
            continueButton.gameObject.SetActive(canContinue);
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() => onContinue?.Invoke());
            againButton.onClick.RemoveAllListeners();
            againButton.onClick.AddListener(() => onAgain?.Invoke());
            mapButton.onClick.RemoveAllListeners();
            mapButton.onClick.AddListener(() => onMap?.Invoke());
            Show();
        }
    }
}
