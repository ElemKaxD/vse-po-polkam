using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// Окно копилки енота (03.10.2026): свинка (пустая / полная), сколько накоплено из 4000, откуда монеты и
    /// «Разбить» за деньги. Разбили — свинка раскалывается, монеты летят в кошелёк.
    /// </summary>
    public class PiggyPopup : Popup
    {
        public TextMeshProUGUI title;
        public Image picture;
        public TextMeshProUGUI amountText;
        public Image fill;
        public TextMeshProUGUI infoText;
        public Button breakButton;
        public TextMeshProUGUI breakText;
        public Button closeButton;

        HubController _hub;
        bool _breaking;
        VfxPlayer _glow;

        void Awake()
        {
            closeButton.onClick.AddListener(() => { if (!_breaking) Hide(); });
            breakButton.onClick.AddListener(Break);
        }

        public void Open(HubController hub)
        {
            _hub = hub;
            _breaking = false;
            Refresh();
            Show();
        }

        void Refresh()
        {
            int c = Piggy.Coins;
            title.text = Piggy.Full ? "Копилка полна!" : "Копилка енота";
            picture.sprite = ArtLibrary.S(Piggy.Sprite);
            picture.preserveAspect = true;
            amountText.text = $"{c} / {Piggy.Cap}";
            fill.fillAmount = Piggy.Fill;
            Vfx.Keep(ref _glow, Piggy.Full, () => Vfx.Behind("vfx_piggy_glow", picture.transform, picture.rectTransform.rect.height * 2.6f));
            infoText.text = Piggy.Full
                ? "Больше не влезает ни монетки! Разбей копилку — все монеты твои."
                : Piggy.CanBreak
                    ? $"С каждой победы сюда падает +{Piggy.PerWin} и ещё {Piggy.Percent} % выигрыша. Разбей — все монеты твои."
                    : $"С каждой победы сюда падает +{Piggy.PerWin} и ещё {Piggy.Percent} % выигрыша. Разбить можно с {Piggy.MinBreak} монет.";
            bool pay = Platform.PaymentsAvailable;
            breakButton.interactable = Piggy.CanBreak && pay;
            breakText.text = !pay ? "Покупки недоступны"
                : Piggy.CanBreak ? "Разбить · " + Platform.PriceOf(Piggy.ProductId) : $"Разбить с {Piggy.MinBreak}";
        }

        void Break()
        {
            if (_breaking || !Piggy.CanBreak) return;
            AudioService.Play("sfx_button");
            Platform.Buy(Piggy.ProductId, _ => PlayBreak(GameApp.I.LastPiggyBreak));
        }

        /// <summary>Покупка прошла (монеты уже в кошельке): свинка раскалывается, монеты летят наверх.</summary>
        public void PlayBreak(int coins)
        {
            _breaking = true;
            Vfx.Keep(ref _glow, false, null);
            Vfx.At("vfx_piggy_break", picture.transform, picture.rectTransform.rect.height * 1.9f);
            picture.sprite = ArtLibrary.S("piggy_broken");
            picture.preserveAspect = true;
            Tween.Punch(picture.transform, 0.25f, 0.5f);
            AudioService.Play("jingle_stage_complete");
            Celebration.Flash();
            Celebration.Confetti(50);
            amountText.text = "+" + coins;
            fill.fillAmount = 0f;
            infoText.text = "Копилка снова пустая — начинаем копить заново!";
            breakButton.interactable = false;
            if (_hub != null)
            {
                _hub.FlyCoinsToTop(picture.transform.position, 10);
                Tween.Delay(picture, 0.9f, () => _hub.RefreshTop());
            }
            Toast.Show($"Из копилки: +{coins}", "icon_coin");
            Tween.Delay(this, 2.2f, () => { _breaking = false; Refresh(); });
        }
    }
}
