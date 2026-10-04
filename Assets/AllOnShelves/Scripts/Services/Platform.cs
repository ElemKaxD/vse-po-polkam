using System;
using System.Collections.Generic;
using UnityEngine;
using YG;

namespace AllOnShelves
{
    /// <summary>
    /// Единственное место вызовов SDK Яндекс Игр (плагин YG2): реклама, покупки, лидерборды, метрика,
    /// Gameplay API, GameReady (ГДД 10, 11, 14.5). В редакторе YG2 сам симулирует рекламу и покупки.
    /// </summary>
    public static class Platform
    {
        public const float FullscreenGap = 60f;
        public const float AfterRewardedGap = 45f;
        public const float SessionGraceTime = 60f;
        public const int FirstInterstitialLevel = 4;

        static float _lastFullscreen = -999f;
        static float _lastRewarded = -999f;
        static bool _gameReady;
        static bool _initialized;
        static Action _pendingReward;
        static Action _pendingRewardFail;
        static Action<string> _pendingPurchase;
        static readonly HashSet<string> _grantedThisSession = new HashSet<string>();

        public static bool AdShowing => YG2.nowAdsShow;
        public static int InterstitialsThisSession { get; private set; }

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            YG2.onRewardAdv += OnReward;
            YG2.onErrorRewardedAdv += OnRewardError;
            YG2.onPurchaseSuccess += OnPurchaseSuccess;
            YG2.onPurchaseFailed += OnPurchaseFailed;
            YG2.onGetPayments += RestorePurchases;
            YG2.onGetLeaderboard += League.OnData;
            RestorePurchases();
        }

        /// <summary>Вызывается, когда показан первый интерактивный экран (ГДД 14.5).</summary>
        public static void GameReady()
        {
            if (_gameReady) return;
            _gameReady = true;
            try { YG2.GameReadyAPI(); } catch (Exception e) { Debug.LogWarning(e.Message); }
        }

        public static void GameplayStart() { try { YG2.GameplayStart(); } catch { } }
        public static void GameplayStop() { try { YG2.GameplayStop(); } catch { } }

        /// <summary>Sticky-баннер: в мете — да, в уровне — нет; выключен покупкой «Без рекламы» (ГДД 10.3).</summary>
        public static void Sticky(bool show)
        {
            bool on = show && GameApp.I != null && !GameApp.I.Save.noAds;
            try { YG2.StickyAdActivity(on); } catch { }
        }

        // ------------------------------------------------------------------ interstitial

        public static bool CanShowInterstitial
        {
            get
            {
                var app = GameApp.I;
                if (app == null || app.Save.noAds) return false;
                if (app.Save.maxReached < FirstInterstitialLevel - 1) return false;
                float now = Time.realtimeSinceStartup;
                if (now - app.SessionStart < SessionGraceTime) return false;
                if (now - _lastFullscreen < FullscreenGap) return false;
                if (now - _lastRewarded < AfterRewardedGap) return false;
                return true;
            }
        }

        /// <summary>Показывает полноэкранную рекламу, если можно (ГДД 10.1). done вызывается в любом случае.</summary>
        public static void TryInterstitial(string trigger, Action done)
        {
            if (!CanShowInterstitial) { done?.Invoke(); return; }
            _lastFullscreen = Time.realtimeSinceStartup;
            InterstitialsThisSession++;
            GameApp.I.Save.interstitials++;
            Metrica("ad_interstitial", new Dictionary<string, object> { { "trigger", trigger } });
            Action onClose = null;
            onClose = () =>
            {
                YG2.onCloseInterAdv -= onClose;
                YG2.onErrorInterAdv -= onClose;
                done?.Invoke();
            };
            YG2.onCloseInterAdv += onClose;
            YG2.onErrorInterAdv += onClose;
            try { YG2.InterstitialAdvShow(); }
            catch (Exception e) { Debug.LogWarning(e.Message); onClose(); }
        }

        // ------------------------------------------------------------------ rewarded

        /// <summary>Реклама за награду (ГДД 10.2). ID места — для аналитики.</summary>
        public static void ShowRewarded(string id, Action onReward, Action onFail = null)
        {
            _pendingReward = onReward;
            _pendingRewardFail = onFail;
            Metrica("ad_rewarded_request", new Dictionary<string, object> { { "placement", id } });
            try { YG2.RewardedAdvShow(id); }
            catch (Exception e) { Debug.LogWarning(e.Message); OnRewardError(); }
        }

        static void OnReward(string id)
        {
            _lastRewarded = Time.realtimeSinceStartup;
            _lastFullscreen = Mathf.Max(_lastFullscreen, _lastRewarded - FullscreenGap + AfterRewardedGap);
            if (GameApp.I != null) { GameApp.I.Save.rewardedWatched++; GameApp.I.MarkDirty(); }
            Metrica("ad_rewarded", new Dictionary<string, object> { { "placement", id }, { "result", "success" } });
            var cb = _pendingReward;
            _pendingReward = null;
            _pendingRewardFail = null;
            cb?.Invoke();
        }

        static void OnRewardError()
        {
            var fail = _pendingRewardFail;
            _pendingReward = null;
            _pendingRewardFail = null;
            Toast.Show("Реклама сейчас недоступна");
            fail?.Invoke();
        }

        // ------------------------------------------------------------------ покупки

        public static bool PaymentsAvailable
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return YG2.purchases != null && YG2.purchases.Length > 0;
#endif
            }
        }

        public static string PriceOf(string id)
        {
            var p = YG2.PurchaseByID(id);
            if (p != null && !string.IsNullOrEmpty(p.price)) return p.price;
            return MetaCatalog.Product(id)?.FallbackPrice ?? "";
        }

        public static void Buy(string id, Action<string> onSuccess)
        {
            _pendingPurchase = onSuccess;
            Metrica("iap_click", new Dictionary<string, object> { { "product", id } });
            try { YG2.BuyPayments(id); }
            catch (Exception e) { Debug.LogWarning(e.Message); }
        }

        static void OnPurchaseSuccess(string id)
        {
            var p = MetaCatalog.Product(id);
            if (p == null) return;
            if (p.Type == ProductType.Consumable)
            {
                GameApp.I?.GrantProduct(p);
                var cat = YG2.PurchaseByID(id);
                if (cat != null) cat.consumed = true;
                try { YG2.ConsumePurchaseByID(id, false); } catch { }
            }
            else
            {
                if (!_grantedThisSession.Contains(id)) GameApp.I?.GrantProduct(p);
                _grantedThisSession.Add(id);
            }
            Metrica("iap_purchase", new Dictionary<string, object> { { "product", id } });
            var cb = _pendingPurchase;
            _pendingPurchase = null;
            cb?.Invoke(id);
        }

        static void OnPurchaseFailed(string id) { _pendingPurchase = null; }

        /// <summary>
        /// Восстановление при запуске: постоянные — применяем флаги (не потребляем),
        /// непотреблённые расходуемые — начисляем и потребляем (ГДД 11.3).
        /// </summary>
        public static void RestorePurchases()
        {
            if (GameApp.I == null || YG2.purchases == null) return;
            // YG2.purchases — каталог товаров; consumed == false означает, что у игрока есть необработанная покупка
            foreach (var pur in YG2.purchases)
            {
                if (pur == null || string.IsNullOrEmpty(pur.id) || pur.consumed) continue;
                var p = MetaCatalog.Product(pur.id);
                if (p == null) continue;
                if (p.Type == ProductType.Permanent)
                {
                    GameApp.I.ApplyPermanent(p);
                    _grantedThisSession.Add(p.Id);
                }
                else
                {
                    GameApp.I.GrantProduct(p);
                    pur.consumed = true;
                    try { YG2.ConsumePurchaseByID(pur.id, false); } catch { }
                }
            }
        }

        // ------------------------------------------------------------------ лидерборды и метрика

        /// <summary>Имя игрока из Яндекса (пусто — гость).</summary>
        public static string PlayerName
        {
            get { try { return YG2.player != null ? YG2.player.name : ""; } catch { return ""; } }
        }

        // Яндекс принимает запросы к лидербордам не чаще раза в секунду: после победы их набирается
        // до шести (levels, stars, day, week и два запроса места) — шлём по очереди (01.10.2026)
        static readonly Queue<Action> _lbQueue = new Queue<Action>();
        static float _lastLb = -999f;
        const float LbGap = 1.1f;

        public static void SetLeaderboard(string name, int score) => SetLeaderboard(name, score, null);

        public static void SetLeaderboard(string name, int score, string extraData)
        {
            Enqueue(() =>
            {
                if (extraData == null) YG2.SetLeaderboard(name, score);
                else YG2.SetLeaderboard(name, score, extraData);
            });
        }

        /// <summary>Запросить таблицу; ответ придёт в YG2.onGetLeaderboard (экран рейтинга и League.OnData).</summary>
        public static void RequestLeaderboard(string name, int top, int around) =>
            Enqueue(() => YG2.GetLeaderboard(name, top, around, "small"));

        static void Enqueue(Action a)
        {
            _lbQueue.Enqueue(a);
            Tick();
        }

        /// <summary>Каждый кадр (GameApp.Update): отправляет следующий запрос к лидербордам, если пора.</summary>
        public static void Tick()
        {
            if (_lbQueue.Count == 0 || Time.realtimeSinceStartup - _lastLb < LbGap) return;
            _lastLb = Time.realtimeSinceStartup;
            var a = _lbQueue.Dequeue();
            try { a(); } catch (Exception e) { Debug.LogWarning(e.Message); }
        }

        public static void Metrica(string name, Dictionary<string, object> data = null)
        {
            try
            {
                if (data == null) YG2.MetricaSend(name);
                else YG2.MetricaSend(name, data);
            }
            catch { }
        }
    }
}
