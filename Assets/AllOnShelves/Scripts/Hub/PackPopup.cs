using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>Открытие пачки наклеек: 3 карточки переворачиваются по очереди.</summary>
    public class PackPopup : Popup
    {
        public Image packImage;
        [OptionalRef] public GameObject[] slots;   // ячейки под наклейками (окно наград v4)
        public StickerCell[] cards = new StickerCell[3];
        public GameObject[] newBadges = new GameObject[3];
        public TextMeshProUGUI info;
        public Button okButton;
        public TextMeshProUGUI okText;

        HubController _hub;
        Action _done;

        void Awake()
        {
            okButton.onClick.AddListener(OnOk);
        }

        public void OpenPending(HubController hub, Action done = null)
        {
            var app = GameApp.I;
            if (app.Save.pendingPacks <= 0) { done?.Invoke(); return; }
            _hub = hub;
            _done = done;
            app.Save.pendingPacks--;
            bool guaranteedGold = app.Save.pendingGoldPacks > 0;
            if (guaranteedGold) app.Save.pendingGoldPacks--;
            var r = app.OpenPack("pending", guaranteedGold);
            app.Flush();

            Show();
            packImage.gameObject.SetActive(true);
            packImage.transform.localScale = Vector3.one;
            foreach (var c in cards) c.gameObject.SetActive(false);
            if (slots != null) foreach (var s in slots) if (s != null) s.SetActive(false);
            foreach (var b in newBadges) b.SetActive(false);
            info.text = "";
            okButton.gameObject.SetActive(false);
            Tween.Punch(packImage.transform, 0.2f, 0.4f);
            Tween.Delay(packImage, 0.5f, () =>
            {
                packImage.gameObject.SetActive(false);
                AudioService.Play("sfx_box_open");
                Vfx.At("vfx_pack_tear", packImage.transform, 560f);
                for (int i = 0; i < cards.Length && i < r.Stickers.Count; i++)
                {
                    int idx = i;
                    string id = r.Stickers[i];
                    bool gold = id.StartsWith("gold_");
                    cards[idx].Set(id, true, gold);
                    cards[idx].gameObject.SetActive(true);
                    if (slots != null && idx < slots.Length && slots[idx] != null) slots[idx].SetActive(true);
                    cards[idx].transform.localScale = new Vector3(0f, 1f, 1f);
                    Tween.Scale(cards[idx].transform, Vector3.one, 0.3f, Ease.OutBack, () =>
                    {
                        newBadges[idx].SetActive(r.IsNew[idx]);
                        AudioService.Play(gold ? "sfx_sticker_gold" : "sfx_sticker_flip");
                    }, 0.25f * idx);
                }
                string text = r.DuplicateCoins > 0 ? $"Повторки обменяны: +{r.DuplicateCoins} монет" : "Наклейки добавлены в альбом";
                if (r.DepartmentsCompleted.Count > 0) Vfx.At("vfx_dept_complete", panel, 640f, delay: 0.9f);
                if (r.DepartmentsCompleted.Count > 0) text = $"Отдел «{MetaCatalog.DepartmentNames[r.DepartmentsCompleted[0]]}» собран! +{r.DuplicateCoins} монет и +{Core.Economy.AlbumDeptBonusPct} % монет за уровни навсегда и рамка аватарки";
                if (r.AlbumFull) text = $"Весь альбом собран! +{Core.Economy.AlbumFullGems} алмазов и рамка «Мастер альбома»";
                info.text = text;
                okText.text = app.Save.pendingPacks > 0 ? $"Ещё пачка ({app.Save.pendingPacks})" : "Отлично!";
                Tween.Delay(okButton, 1f, () => okButton.gameObject.SetActive(true));
                _hub?.RefreshTop();
            });
        }

        void OnOk()
        {
            var app = GameApp.I;
            if (app.Save.pendingPacks > 0) { OpenPending(_hub, _done); return; }
            Hide(() =>
            {
                var d = _done;
                _done = null;
                d?.Invoke();
                _hub?.RefreshTop();
                _hub?.TryOffer();
            });
        }
    }
}
