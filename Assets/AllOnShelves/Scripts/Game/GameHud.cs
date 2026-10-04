using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>HUD уровня: номер уровня, монеты, отмена, подсказка, пауза.</summary>
    public class GameHud : MonoBehaviour
    {
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI coinsText;
        public RectTransform coinsIcon;
        public Button undoButton;
        public TextMeshProUGUI undoCount;
        public GameObject undoVideo;
        public Button hintButton;
        public TextMeshProUGUI hintCount;
        public GameObject hintVideo;
        public Button pauseButton;
        public Button mapButton;
        public GameObject difficultyBadge;
        [OptionalRef] public Image levelBadge;   // звёздочка у номера; в Среднем и Сложном — перчик режима (v4)
        Sprite _star;
        public TextMeshProUGUI difficultyText;

        /// <summary>Значок у номера уровня: перчик режима сложности или звёздочка (null).</summary>
        public void SetModeIcon(string sprite)
        {
            if (levelBadge == null) return;
            if (_star == null) _star = levelBadge.sprite;
            var s = sprite != null ? ArtLibrary.S(sprite) : null;
            levelBadge.sprite = s != null ? s : _star;
            levelBadge.preserveAspect = true;
        }

        public void SetLevel(string text, string difficulty)
        {
            levelText.text = text;
            bool show = difficulty == "hard" || difficulty == "superhard";
            difficultyBadge.SetActive(show);
            if (show)
            {
                difficultyText.text = difficulty == "hard" ? "Сложно" : "Очень сложно";
                difficultyBadge.GetComponent<Image>().color = difficulty == "hard" ? new Color32(0xD9, 0x3B, 0x3B, 255) : new Color32(0x8E, 0x4B, 0xD6, 255);
            }
        }

        public void RefreshWallet(int freeUndoLeft, bool hintVisible)
        {
            var app = GameApp.I;
            if (app == null) return;
            coinsText.text = app.Coins.ToString();
            bool unlimited = app.UndoUnlimited;
            int undo = unlimited ? 99 : app.Save.undo + freeUndoLeft;
            undoCount.text = unlimited ? "∞" : undo.ToString();
            undoCount.transform.parent.gameObject.SetActive(undo > 0 || unlimited);
            undoVideo.SetActive(undo <= 0 && !unlimited);
            hintButton.gameObject.SetActive(hintVisible);
            hintCount.text = app.Save.hint.ToString();
            hintCount.transform.parent.gameObject.SetActive(app.Save.hint > 0);
            hintVideo.SetActive(app.Save.hint <= 0);
        }

        public void SetButtonsInteractable(bool on)
        {
            undoButton.interactable = on;
            hintButton.interactable = on;
            pauseButton.interactable = on;
            if (mapButton != null) mapButton.interactable = on;
        }
    }
}
