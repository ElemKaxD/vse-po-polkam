using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Hub
{
    /// <summary>
    /// «Звёздный путь» (просьба 22.09.2026): шкала наград за общее число звёзд — куда тратятся звёзды
    /// и зачем проходить уровни на три. Лента наград листается вбок, полоса показывает, сколько набрано.
    /// </summary>
    public class StarTrackPopup : Popup
    {
        public TextMeshProUGUI info;
        public ScrollRect scroll;
        public RectTransform content;
        public RectTransform trackFill;
        public StarTrackNode[] nodes = new StarTrackNode[0];
        public Button closeButton;
        public Button wardrobeButton;
        public Button goldButton;            // Золотой путь: значок слева от второй строки — купить
        public TextMeshProUGUI goldPrice;
        public GameObject goldOwned;

        HubController _hub;
        Action _closed;

        void Awake()
        {
            closeButton.onClick.AddListener(Close);
            if (wardrobeButton != null) wardrobeButton.onClick.AddListener(() => _hub?.wardrobePopup.Open());
            for (int i = 0; i < nodes.Length; i++)
            {
                int idx = i;
                nodes[i].claimButton.onClick.AddListener(() => Claim(idx));
                nodes[i].goldClaim.onClick.AddListener(() => ClaimGold(idx));
            }
            goldButton.onClick.AddListener(BuyGold);
        }

        public void Open(HubController hub, Action closed = null)
        {
            _hub = hub;
            _closed = closed;
            var app = GameApp.I;
            app.Save.starTrackSeen = Mathf.Max(app.Save.starTrackSeen, app.StarRewardsReachedCount);
            app.MarkDirty();
            Show();
            Refresh();
            Canvas.ForceUpdateCanvases();
            // к первой готовой награде, иначе — к следующей цели
            int focus = -1;
            for (int i = 0; i < nodes.Length && focus < 0; i++) if (app.StarRewardReady(i) || app.GoldRewardReady(i)) focus = i;
            if (focus < 0) focus = Mathf.Max(0, app.NextStarReward);
            ScrollTo(focus);
        }

        void Refresh()
        {
            var app = GameApp.I;
            int stars = app.Save.StarsTotal;
            var track = MetaCatalog.StarTrack;
            for (int i = 0; i < nodes.Length && i < track.Length; i++)
            {
                nodes[i].Set(track[i], app.StarRewardReached(i), app.StarRewardClaimed(i));
                nodes[i].SetGold(MetaCatalog.GoldTrack[i], app.Save.goldPath, app.StarRewardReached(i), app.GoldRewardClaimed(i));
            }
            goldOwned.SetActive(app.Save.goldPath);
            goldPrice.transform.parent.gameObject.SetActive(!app.Save.goldPath && Platform.PaymentsAvailable);
            goldPrice.text = Platform.PriceOf("gold_path");

            if (wardrobeButton != null) wardrobeButton.gameObject.SetActive(true);
            int next = app.NextStarReward;
            info.text = next < 0
                ? $"Собрано звёзд: {stars}. Все награды открыты — ты мастер полок!"
                : $"Собрано звёзд: {stars}. До следующей награды — ещё {track[next].Stars - stars}. Три звезды за уровень — быстрее!";

            // полоса: от начала ленты до точки между наградами пропорционально звёздам
            float x = 0f;
            for (int i = 0; i < nodes.Length && i < track.Length; i++)
            {
                float nx = ((RectTransform)nodes[i].transform).anchoredPosition.x;
                int prevStars = i == 0 ? 0 : track[i - 1].Stars;
                float px = i == 0 ? 0f : ((RectTransform)nodes[i - 1].transform).anchoredPosition.x;
                if (stars >= track[i].Stars) { x = nx; continue; }
                x = Mathf.Lerp(px, nx, (stars - prevStars) / (float)Mathf.Max(1, track[i].Stars - prevStars));
                break;
            }
            float w = x - trackFill.anchoredPosition.x;   // заливка начинается внутри рамки
            trackFill.sizeDelta = new Vector2(Mathf.Max(0f, w), trackFill.sizeDelta.y);
            trackFill.gameObject.SetActive(w > 12f);
        }

        void ScrollTo(int index)
        {
            if (index < 0 || index >= nodes.Length) return;
            float view = ((RectTransform)scroll.viewport).rect.width;
            float width = content.rect.width;
            if (width <= view) { scroll.horizontalNormalizedPosition = 0f; return; }
            float x = ((RectTransform)nodes[index].transform).anchoredPosition.x;
            scroll.horizontalNormalizedPosition = Mathf.Clamp01((x - view / 2f) / (width - view));
        }

        void Claim(int index)
        {
            var app = GameApp.I;
            var r = MetaCatalog.StarTrack[index];
            if (!app.ClaimStarReward(index)) return;
            AudioService.Play("sfx_chest_open");
            var from = nodes[index].card.transform.position;
            // вспышка — компактная и НАД иконкой (просьба 29.09.2026): 380 точек закрывали всю карточку.
            // Лучи на листе расходятся из точки на ~21 % высоты ячейки ниже её центра — ставим эту точку
            // чуть выше верхнего края иконки (замер по кадру), размер — полторы ширины иконки
            var iconRt = nodes[index].icon.rectTransform;
            float px = iconRt.GetComponentInParent<Canvas>().rootCanvas.transform.lossyScale.y;
            float size = iconRt.rect.width * iconRt.lossyScale.x / px * 1.5f;
            var top = iconRt.TransformPoint(new Vector3(iconRt.rect.center.x, iconRt.rect.yMax, 0f));
            Vfx.Play("vfx_chest_open", top + Vector3.up * size * 0.26f * px, size);
            if (r.Kind == "coins") _hub?.FlyCoinsToTop(from, 6);
            else Toast.Show("Получено: " + r.Title);
            Tween.Punch(nodes[index].card.transform, 0.25f, 0.4f);
            Refresh();
            _hub?.RefreshTop();
            // пачку открываем сразу, если альбом уже доступен; иначе она дождётся альбома
            if (r.Kind == "pack" && app.Save.maxReached >= Core.Economy.AlbumAt && _hub != null)
                _hub.packPopup.OpenPending(_hub, Refresh);
        }

        void ClaimGold(int index)
        {
            var app = GameApp.I;
            var r = MetaCatalog.GoldTrack[index];
            if (!app.ClaimGoldReward(index)) return;
            AudioService.Play("sfx_chest_open");
            Vfx.At("vfx_gift_pop", nodes[index].goldCard.transform, 260f);
            if (r.Kind == "coins") _hub?.FlyCoinsToTop(nodes[index].goldCard.transform.position, 6);
            else Toast.Show("Золотой путь: " + r.Title, r.Icon);
            Tween.Punch(nodes[index].goldCard.transform, 0.25f, 0.4f);
            Refresh();
            _hub?.RefreshTop();
        }

        /// <summary>Купить Золотой путь: Кот приходит сразу, награды пройденных шагов — забрать здесь же.</summary>
        void BuyGold()
        {
            var app = GameApp.I;
            if (app.Save.goldPath)
            {
                AudioService.Play("sfx_button");
                Toast.Show("Золотой путь уже твой — забирай награды второй строки", "gold_path_card");
                return;
            }
            if (!Platform.PaymentsAvailable) { AudioService.Play("sfx_nope"); return; }
            Platform.Buy("gold_path", _ =>
            {
                AudioService.Play("jingle_stage_complete");
                Celebration.Flash();
                Vfx.At("vfx_chest_open", goldButton.transform, 360f);
                if (nodes.Length > 0 && scroll != null)
                    Vfx.Play("vfx_gold_path_unlock", new Vector3(scroll.viewport.position.x, nodes[0].goldCard.transform.position.y, 0f), 760f, delay: 0.2f);
                Toast.Show("Золотой путь открыт! Кот-управляющий уже в Торговом доме", "gold_path_card");
                Refresh();
                _hub?.RefreshTop();
            });
        }

        void Close()
        {
            Hide(() =>
            {
                var c = _closed;
                _closed = null;
                c?.Invoke();
            });
        }
    }
}
