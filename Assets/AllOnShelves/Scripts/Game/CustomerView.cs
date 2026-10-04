using System.Collections.Generic;
using AllOnShelves.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>
    /// Покупатель сбоку: облачко с товаром и терпение.
    /// Приходы и уходы идут ОЧЕРЕДЬЮ (03.10.2026): каждый покупатель обязательно выходит на экран и стоит хотя бы
    /// мгновение, даже если его обслужили тем же ходом, когда он пришёл. Раньше такой покупатель при быстрых
    /// нажатиях не показывался вовсе — цель засчитывалась, а покупателя «не было» (баг от игрока).
    /// </summary>
    public class CustomerView : MonoBehaviour
    {
        public RectTransform rect;
        public Image body;
        public RectTransform bubble;
        public Image wantIcon;
        public Image[] hearts = new Image[6];
        public CanvasGroup group;

        Vector2 _home;
        bool _init;

        const float ArriveTime = 0.45f;   // выход из-за края
        const float MinStay = 0.55f;      // сколько стоит, даже если обслужили сразу
        const float LeaveTime = 0.75f;    // уход за край

        struct Step
        {
            public bool arrive;
            public int type, patience, max, look;
            public bool happy;
        }

        readonly Queue<Step> _queue = new Queue<Step>();
        float _busyUntil;        // текущая анимация идёт до этого времени
        bool _shown;             // покупатель стоит на экране
        int _look;
        int _patience = -1, _max;   // терпение, пришедшее, пока покупатель ещё в очереди

        void EnsureInit()
        {
            if (_init) return;
            _init = true;
            _home = rect.anchoredPosition;
        }

        public void HideInstant()
        {
            EnsureInit();
            _queue.Clear();
            _shown = false;
            _busyUntil = 0f;
            Tween.Kill(rect);
            Tween.Kill(group);
            Tween.Kill(bubble);
            rect.anchoredPosition = _home;
            group.alpha = 0f;
            gameObject.SetActive(false);
        }

        public void Arrive(int type, int patience, int maxPatience, int look)
        {
            EnsureInit();
            gameObject.SetActive(true);
            _queue.Enqueue(new Step { arrive = true, type = type, patience = patience, max = maxPatience, look = look });
            _patience = -1;
            Pump();
        }

        public void Leave(bool happy)
        {
            EnsureInit();
            if (!_shown && _queue.Count == 0) return;
            _queue.Enqueue(new Step { arrive = false, happy = happy });
            Pump();
        }

        public void SetPatience(int patience, int max)
        {
            // покупатель ещё ждёт своей очереди выйти — сердечки покажем, когда выйдет
            if (_queue.Count > 0) { _patience = patience; _max = max; return; }
            ShowHearts(patience, max);
        }

        void ShowHearts(int patience, int max)
        {
            for (int i = 0; i < hearts.Length; i++)
            {
                hearts[i].gameObject.SetActive(i < max);
                hearts[i].color = i < patience ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            }
        }

        void Update()
        {
            if (_queue.Count > 0) Pump();
        }

        void Pump()
        {
            while (_queue.Count > 0 && Time.time >= _busyUntil)
            {
                var s = _queue.Dequeue();
                if (s.arrive) PlayArrive(s);
                else PlayLeave(s.happy);
            }
            if (_queue.Count == 0 && _patience >= 0 && _shown) { ShowHearts(_patience, _max); _patience = -1; }
        }

        void PlayArrive(Step s)
        {
            _shown = true;
            _look = s.look;
            gameObject.SetActive(true);
            body.sprite = ArtLibrary.Customer(s.look, false);
            wantIcon.sprite = ArtLibrary.Item(s.type);
            ShowHearts(s.patience, s.max);
            rect.anchoredPosition = _home + new Vector2(420f, 0f);
            group.alpha = 0f;
            Tween.Fade(group, 1f, 0.25f);
            Tween.MoveAnchored(rect, _home, ArriveTime, Ease.OutCubic);
            bubble.localScale = Vector3.zero;
            Tween.Scale(bubble, Vector3.one, 0.3f, Ease.OutBack, null, 0.35f);
            _busyUntil = Time.time + ArriveTime + MinStay;
        }

        void PlayLeave(bool happy)
        {
            if (!_shown) return;
            _shown = false;
            body.sprite = ArtLibrary.Customer(_look, happy);
            Tween.Scale(bubble, Vector3.zero, 0.15f, Ease.InQuad);
            float delay = happy ? 0.3f : 0.1f;
            Tween.MoveAnchored(rect, _home + new Vector2(420f, 0f), 0.4f, Ease.InQuad,
                () => { if (!_shown && _queue.Count == 0) gameObject.SetActive(false); }, delay);
            Tween.Fade(group, 0f, 0.25f, null, delay + 0.15f);
            _busyUntil = Time.time + LeaveTime;
        }

        /// <summary>Где облачко покупателя, когда он стоит на месте (туда летит товар, даже если он ещё выходит).</summary>
        public Vector3 WantPoint
        {
            get
            {
                EnsureInit();
                return wantIcon.rectTransform.position + rect.parent.TransformVector(_home - rect.anchoredPosition);
            }
        }

        /// <summary>Где сам покупатель, когда стоит на месте.</summary>
        public Vector3 HomePoint
        {
            get
            {
                EnsureInit();
                return rect.position + rect.parent.TransformVector(_home - rect.anchoredPosition);
            }
        }
    }
}
