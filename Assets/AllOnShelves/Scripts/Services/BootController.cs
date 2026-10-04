using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YG;

namespace AllOnShelves
{
    /// <summary>
    /// Сцена Boot: ждёт данные SDK (сейв из облака), инициализирует сервисы и открывает
    /// уровень 1 при первом запуске или хаб при повторном (ГДД 7.1, 12.1).
    /// </summary>
    public class BootController : MonoBehaviour
    {
        public Image progressFill;
        public RectTransform progressFrame;
        public RectTransform raccoon;
        [Tooltip("Отступ от краёв полосы, чтобы енот не выбегал за неё")]
        public float runInset = 70f;
        [Tooltip("Сколько секунд экран загрузки виден, даже если SDK ответил сразу")]
        public float minShow = 1.8f;
        public float maxWait = 6f;

        float _t;
        float _shown;
        bool _ready;
        bool _started;

        void Start()
        {
            Platform.Init();
            if (YG2.isSDKEnabled) Ready();
            else YG2.onGetSDKData += Ready;
        }

        void OnDestroy() { YG2.onGetSDKData -= Ready; }

        void Ready() { _ready = true; }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            // пока ждём SDK — ползём до 90%, после ответа добегаем до конца
            float target = _ready || _t > maxWait ? 1f : Mathf.Min(0.9f, _t / 1.5f);
            _shown = Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime * 0.9f);
            SetProgress(_shown);
            // уходим, только когда енот добежал до конца полосы и экран успели увидеть
            if (!_started && _shown >= 1f && _t >= minShow) Go();
        }

        /// <summary>Заполняет полосу и ставит енота на неё (0..1). Вызывается и из редактора для предпросмотра.</summary>
        public void SetProgress(float p)
        {
            p = Mathf.Clamp01(p);
            if (progressFill != null) progressFill.fillAmount = p;
            if (raccoon == null || progressFrame == null) return;
            float w = progressFrame.rect.width;
            float from = Mathf.Min(runInset, w / 2f), to = Mathf.Max(w - runInset, w / 2f);
            raccoon.anchoredPosition = new Vector2(Mathf.Lerp(from, to, p), raccoon.anchoredPosition.y);
        }

        void Go()
        {
            if (_started) return;
            _started = true;
            Platform.RestorePurchases();
            var app = GameApp.I;
            AudioService.ApplyVolumes();
            // требование издателя (29.09.2026): геймплей — не дальше одного клика от старта.
            // Новый игрок сразу попадает в уровень 1 (обучение там не блокирует — рука показывает ход),
            // вернувшийся — на карту (меню пропускается, см. HubController.ShowStartMenu): до уровня один клик
            if (app != null && app.Save.maxReached == 0 && app.Level(1) != null) app.PlayLevel(1);
            else SceneManager.LoadScene(GameApp.SceneHub);
        }
    }
}
