using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves.Game
{
    /// <summary>
    /// Обучение (ГДД 7): затемнение, рука-указатель, облачко енота. Ввод не блокирует —
    /// игрок может кликнуть куда угодно, кроме режима «только эта цель».
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        public CanvasGroup dim;
        public RectTransform hand;
        public RectTransform bubble;
        public TextMeshProUGUI bubbleText;
        public RectTransform raccoon;
        public Button tapCatcher;
        public Image ghost;                  // копия товара, которую рука тащит в обучении

        /// <summary>Кончик пальца на ui_hand_pointer — доля размера руки от её центра.</summary>
        static readonly Vector2 FingerTip = new Vector2(-0.318f, 0.40f);

        Action _onTap;
        Vector2 _bubbleHome, _raccoonHome, _bubbleBase;

        /// <summary>Кегль, которым пишем в облачке: мельче не уменьшаем — лучше увеличим само облачко.</summary>
        const float BubbleFont = 34f;

        /// <summary>
        /// Текст — по центру «тела» облачка (без хвостика), облачко подрастает под длинный текст.
        /// Пивот облачка стоит на кончике хвостика, поэтому при росте хвостик остаётся у рта енота.
        /// </summary>
        void SetBubbleText(string text)
        {
            bubbleText.text = text;
            bubbleText.enableAutoSizing = true;
            bubbleText.fontSizeMax = BubbleFont;
            bubbleText.fontSizeMin = 22f;
            bubbleText.alignment = TextAlignmentOptions.Center;
            bubbleText.enableWordWrapping = true;
            var box = bubbleText.rectTransform;
            box.anchorMin = BodyMin;
            box.anchorMax = BodyMax;
            box.offsetMin = box.offsetMax = Vector2.zero;
            box.anchoredPosition = Vector2.zero;
            // меряем текст обычным кеглем; не влезает — облачко растёт шагами по 5 %
            bubbleText.enableAutoSizing = false;
            bubbleText.fontSize = BubbleFont;
            float k = 1f;
            for (; k < 1.6f; k += 0.05f)
            {
                var inner = Vector2.Scale(_bubbleBase * k, BodyMax - BodyMin);
                var need = bubbleText.GetPreferredValues(text, inner.x, 0f);
                if (need.y <= inner.y && need.x <= inner.x + 1f) break;
            }
            bubble.sizeDelta = _bubbleBase * k;
            bubbleText.enableAutoSizing = true;   // страховка: если и в самом большом облачке тесно — ужмётся
        }

        /// <summary>Внутреннее поле облачка ui_speech_bubble (доли, сняты с картинки; хвостик внизу слева не входит).</summary>
        static readonly Vector2 BodyMin = new Vector2(0.12f, 0.31f), BodyMax = new Vector2(0.88f, 0.87f);
        readonly List<GameObject> _lit = new List<GameObject>();

        void Awake()
        {
            _bubbleHome = bubble.anchoredPosition;
            _raccoonHome = raccoon.anchoredPosition;
            _bubbleBase = bubble.sizeDelta;
            // облачко, енот и рука не должны перехватывать нажатия на товары под ними
            foreach (var r in new[] { bubble, raccoon, hand })
                foreach (var g in r.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            var tp = tapCatcher.gameObject.GetComponent<TapPoint>();
            if (tp == null) tp = tapCatcher.gameObject.AddComponent<TapPoint>();
            tp.Pressed = (pos, cam) => { _pressPos = pos; _pressCam = cam; };
            tapCatcher.onClick.AddListener(() =>
            {
                // «жми сюда»: мимо цели — рука подпрыгивает и ждёт дальше
                if (_strict != null && !RectTransformUtility.RectangleContainsScreenPoint(_strict, _pressPos, _pressCam))
                {
                    Tween.Punch(hand, 0.25f, 0.35f);
                    return;
                }
                var cb = _onTap;
                _onTap = null;
                if (cb != null) { Hide(); cb(); }
            });
            HideInstant();
        }

        public void HideInstant()
        {
            EndDragDemo();
            Unlight();
            dim.alpha = 0f;
            dim.gameObject.SetActive(false);
            hand.gameObject.SetActive(false);
            bubble.gameObject.SetActive(false);
            raccoon.gameObject.SetActive(false);
            tapCatcher.gameObject.SetActive(false);
            Tween.Kill(hand);
        }

        RectTransform _strict;
        Vector2 _pressPos;
        Camera _pressCam;

        /// <summary>
        /// Шаг обучения «в фокусе» (просьба 03.10.2026): экран затемнён, цель светится поверх затемнения, рука
        /// показывает на неё, енот объясняет, что это и зачем. strict — продолжить можно только нажатием на цель
        /// (тогда onTap делает то же, что сама кнопка); иначе — нажатием куда угодно («понятно, дальше»).
        /// </summary>
        /// <param name="light">что подсветить, если сама цель невидима (кнопка-зона поверх картинки этажа)</param>
        public void Focus(string text, RectTransform target, Action onTap = null, bool strict = false, RectTransform light = null)
        {
            Show(text, target, onTap ?? (() => { }), dimScreen: true);
            Tween.Fade(dim, 0.62f, 0.25f);
            _strict = strict ? target : null;
            // говорящий енот экрана (дом) светится вместе с целью — не тонет в затемнении
            var speaker = _speaker != null && _speaker.gameObject.activeInHierarchy ? _speaker : null;
            if (light != null || target != null) Light(false, light != null ? light : target, speaker);
        }

        /// <summary>Облачко с текстом; рука указывает на цель (если задана). tapToClose — ждать клика.</summary>
        public void Show(string text, RectTransform target = null, Action onTap = null, bool dimScreen = false)
        {
            _strict = null;
            Unlight();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            dim.gameObject.SetActive(dimScreen || onTap != null);
            if (dim.gameObject.activeSelf) Tween.Fade(dim, dimScreen ? 0.45f : 0.01f, 0.2f);
            bubble.gameObject.SetActive(true);
            raccoon.gameObject.SetActive(true);
            SetBubbleText(text);
            bubble.localScale = Vector3.zero;
            Tween.Scale(bubble, Vector3.one, 0.3f, Ease.OutBack);

            _onTap = onTap;
            tapCatcher.gameObject.SetActive(onTap != null);
            PlaceBubble(target);
            PointAt(target);
        }

        /// <summary>
        /// Облачко стоит на своём месте из сцены. Если закрывает цель — отходит в ближайшую сторону,
        /// оставаясь на экране: вверх оно уезжает только тогда, когда вбок не разойтись,
        /// иначе облачко отрывается от енота и хвостик показывает в пустоту.
        /// </summary>
        void PlaceBubble(RectTransform target)
        {
            bubble.anchoredPosition = _bubbleHome;
            raccoon.anchoredPosition = _raccoonHome;
            if (_speaker != null && _speaker.gameObject.activeInHierarchy)
            {
                // говорит енот самого экрана: свой енот не нужен, хвостик облачка — у его рта
                raccoon.gameObject.SetActive(false);
                var r = _speaker.rect;
                bubble.position = _speaker.TransformPoint(new Vector3(r.xMin + r.width * _speakerMouth.x, r.yMin + r.height * _speakerMouth.y, 0f));
                return;
            }
            if (target == null) return;
            if (StandByTarget && StandBeside(target)) return;
            var was = bubble.position;
            MoveBubbleOff(target);
            // енот идёт за облачком только вбок и не выше своего места: иначе он забирается
            // в середину экрана и закрывает поле (баг 23.09.2026)
            float dx = bubble.position.x - was.x;
            raccoon.position += new Vector3(dx, 0f, 0f);
            var home = _raccoonHome;
            var now = raccoon.anchoredPosition;
            raccoon.anchoredPosition = new Vector2(Mathf.Clamp(now.x, home.x - 40f, home.x + 260f), home.y);
            // енот сам закрыл цель (нижний ряд плиток слева): отходим вправо за цель вместе с облачком,
            // а если справа не разойтись — енот прячется, остаётся только облачко
            Rect rr = WorldRect(raccoon), tr = WorldRect(target);
            if (rr.Overlaps(tr))
            {
                var area = WorldRect((RectTransform)raccoon.parent);
                float shift = tr.xMax + 16f * raccoon.lossyScale.x - rr.xMin;
                var b = WorldRect(bubble);
                if (b.xMax + shift <= area.xMax && !new Rect(b.x + shift, b.y, b.width, b.height).Overlaps(tr))
                {
                    raccoon.position += new Vector3(shift, 0f, 0f);
                    bubble.position += new Vector3(shift, 0f, 0f);
                }
                else raccoon.gameObject.SetActive(false);
            }
        }

        RectTransform _speaker;
        Vector2 _speakerMouth;

        /// <summary>
        /// Карта (03.10.2026): енот встаёт рядом с целью — прямо на дорогу, облачко у его рта над ценниками.
        /// Раньше облачко уезжало от цели вверх, а енот оставался в углу — они выглядели отдельно.
        /// </summary>
        public bool StandByTarget;

        /// <summary>
        /// Чей голос: енот, который уже стоит на экране (в ремонте — енот с валиком), или null — свой енот обучения.
        /// mouth — точка рта в долях картинки говорящего (от левого нижнего угла).
        /// </summary>
        public void SetSpeaker(RectTransform who, Vector2 mouth)
        {
            _speaker = who;
            _speakerMouth = mouth;
            StandByTarget = false;
        }

        /// <summary>
        /// Енот слева от цели (не влез — справа), ноги на уровне низа цели; облачко — на своём месте относительно
        /// рта, как в сцене. Годится, только если енот и облачко целиком на экране и не закрывают цель.
        /// </summary>
        bool StandBeside(RectTransform target)
        {
            var area = WorldRect((RectTransform)raccoon.parent);
            Rect t = WorldRect(target);
            var scale = bubble.localScale;
            bubble.localScale = Vector3.one;   // облачко сейчас выезжает из нуля — меряем в полном размере
            Rect r0 = WorldRect(raccoon), b0 = WorldRect(bubble);
            bubble.localScale = scale;
            Vector3 bubbleFromRaccoon = bubble.position - raccoon.position;
            Vector3 pivotFromBottom = raccoon.position - new Vector3(r0.center.x, r0.yMin, 0f);
            float gap = 14f * raccoon.lossyScale.x;
            foreach (int side in new[] { -1, 1 })
            {
                float cx = side < 0 ? t.xMin - gap - r0.width / 2f : t.xMax + gap + r0.width / 2f;
                float bottom = t.yMin - r0.height * 0.08f;   // чуть ниже ценника: стоит на дороге
                var racPos = new Vector3(cx, bottom, 0f) + pivotFromBottom;
                var shift = racPos - raccoon.position;
                Rect rr = new Rect(r0.x + shift.x, r0.y + shift.y, r0.width, r0.height);
                Rect bb = new Rect(b0.x + shift.x, b0.y + shift.y, b0.width, b0.height);
                bool inside = rr.xMin >= area.xMin && rr.xMax <= area.xMax && rr.yMin >= area.yMin
                              && bb.xMin >= area.xMin && bb.xMax <= area.xMax && bb.yMax <= area.yMax;
                if (!inside || rr.Overlaps(t) || bb.Overlaps(t)) continue;
                raccoon.position = racPos;
                bubble.position = racPos + bubbleFromRaccoon;
                return true;
            }
            return false;
        }

        void MoveBubbleOff(RectTransform target)
        {
            var scale = bubble.localScale; // облачко сейчас выезжает из нуля — меряем в полном размере
            bubble.localScale = Vector3.one;
            Rect b = WorldRect(bubble), t = WorldRect(target);
            bubble.localScale = scale;
            if (!b.Overlaps(t)) return;

            var area = WorldRect((RectTransform)bubble.parent);
            float margin = 12f * bubble.parent.lossyScale.y;
            float up = t.yMax + margin - b.yMin;
            float right = t.xMax + margin - b.xMin;
            float left = b.xMax - (t.xMin - margin);
            bool canRight = b.xMax + right <= area.xMax;
            bool canLeft = b.xMin - left >= area.xMin;
            bool canUp = b.yMax + up <= area.yMax;

            if (canRight && right <= left && (right <= up || !canUp)) bubble.position += new Vector3(right, 0f, 0f);
            else if (canLeft && (left <= up || !canUp)) bubble.position -= new Vector3(left, 0f, 0f);
            else if (canUp) bubble.position += new Vector3(0f, up, 0f);
            else if (canRight) bubble.position += new Vector3(right, 0f, 0f);
            else if (canLeft) bubble.position -= new Vector3(left, 0f, 0f);
        }

        static Rect WorldRect(RectTransform r)
        {
            var c = new Vector3[4];
            r.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        public void PointAt(RectTransform target)
        {
            if (target == null) { hand.gameObject.SetActive(false); Tween.Kill(hand); return; }
            // рука следует за целью: товар ещё может ехать по ленте после хода
            Follow(() => target != null ? target.position : hand.position);
        }

        RectTransform _demoFrom, _demoTo;
        float _demoT;

        /// <summary>Ставит руку так, чтобы кончик пальца пришёлся на точку.</summary>
        void PutFinger(Vector3 world, float scale = 1f)
        {
            hand.localScale = Vector3.one * scale;
            var s = hand.rect.size;
            var tip = new Vector3(FingerTip.x * s.x, FingerTip.y * s.y, 0f);
            hand.position = world - hand.TransformVector(tip);
        }

        /// <summary>
        /// Подсветка при затемнении: указанные объекты рисуются поверх затемнения (свой Canvas
        /// с большим порядком), остальное поле остаётся приглушённым. Нажатия на них работают.
        /// </summary>
        /// <remarks>Порядок слоёв: каждая следующая цель выше предыдущей, затем копия товара и рука.
        /// Полку передаём первой, товар вторым — иначе товар «проваливается» за полку.</remarks>
        void Light(params RectTransform[] targets) => Light(true, targets);

        /// <param name="clickable">false — цель только светится, нажатие ловит tapCatcher (шаг в фокусе)</param>
        void Light(bool clickable, params RectTransform[] targets)
        {
            Unlight();
            var root = GetComponentInParent<Canvas>().rootCanvas;
            int order = root.sortingOrder + 10;
            foreach (var t in targets)
            {
                if (t == null) continue;
                // у цели уже был свой Canvas — не трогаем его (Unlight снёс бы чужой компонент)
                if (t.GetComponent<Canvas>() != null) continue;
                AddLayer(t.gameObject, order++, clickable);
                _lit.Add(t.gameObject);
            }
            _ghostOrder = order;
            _handOrder = order + 1;
            // копия товара всегда включена (прячется через enabled): Canvas с overrideSorting на выключенном
            // объекте Unity сбрасывает при включении — из-за этого копия уходила под полку
            ghost.gameObject.SetActive(true);
            ghost.enabled = false;
            AddLayer(ghost.gameObject, _ghostOrder);
            AddLayer(hand.gameObject, _handOrder);
            _lit.Add(ghost.gameObject);
            _lit.Add(hand.gameObject);
            // облачко — поверх подсвеченной цели: иначе дом в фокусе закрывал облачко (03.10.2026)
            AddLayer(bubble.gameObject, _handOrder + 1);
            _lit.Add(bubble.gameObject);
        }

        int _ghostOrder, _handOrder;

        /// <summary>Держит копию товара и руку над подсвеченной полкой каждый кадр.</summary>
        void KeepOnTop()
        {
            foreach (var (go, order) in new[] { (ghost.gameObject, _ghostOrder), (hand.gameObject, _handOrder) })
            {
                var c = go.GetComponent<Canvas>();
                if (c == null) continue;
                if (!c.overrideSorting) c.overrideSorting = true;
                if (c.sortingOrder != order) c.sortingOrder = order;
            }
        }

        static void AddLayer(GameObject go, int order, bool clickable = true)
        {
            var c = go.GetComponent<Canvas>();
            if (c == null) c = go.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = order;
            if (clickable && go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        }

        void Unlight()
        {
            foreach (var go in _lit)
            {
                if (go == null) continue;
                // сначала рейкастер: он требует Canvas, и Canvas раньше него не снять
                var r = go.GetComponent<GraphicRaycaster>();
                if (r != null) DestroyImmediate(r);
                var c = go.GetComponent<Canvas>();
                if (c != null) DestroyImmediate(c);
            }
            _lit.Clear();
        }

        /// <summary>Обучение перетаскиванию: пузырь остаётся у енота, рука циклично показывает
        /// «зажми и тяни» — покачивается у товара, тащит к цели и повторяет (Update-цикл, не Tween:
        /// ничем не сбивается и работает одинаково в редакторе и билде). Уровень слегка затемнён,
        /// но товар-источник и целевая секция остаются яркими (просьба 22.09.2026).</summary>
        public void ShowDragDemo(string text, RectTransform from, RectTransform to)
        {
            if (from == null || to == null) return;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            bubble.gameObject.SetActive(true);
            raccoon.gameObject.SetActive(true);
            SetBubbleText(text);
            bubble.localScale = Vector3.one;
            bubble.anchoredPosition = _bubbleHome;   // пузырь на своём месте у енота
            _onTap = null;
            tapCatcher.gameObject.SetActive(false);
            hand.gameObject.SetActive(true);
            hand.SetAsLastSibling();
            Tween.Kill(hand);
            _demoFrom = from;
            _demoTo = to;
            _demoT = 0f;
            PlaceBubble(null);
            // «призрак» — та же картинка, что у товара
            var item = from.GetComponentInParent<ItemView>();
            if (item != null && item.icon != null && item.icon.sprite != null)
            {
                ghost.sprite = item.icon.sprite;
                ghost.preserveAspect = true;
                ghost.rectTransform.sizeDelta = item.icon.rectTransform.rect.size;
            }
            // лёгкое затемнение уровня, а рука, товар и полка — поверх него. Ввод не блокируем
            dim.blocksRaycasts = false;
            dim.gameObject.SetActive(true);
            dim.alpha = 0f;
            Tween.Fade(dim, 0.5f, 0.25f);
            Light(to, from);   // полка ниже, товар над ней
        }

        void EndDragDemo()
        {
            if (_demoFrom != null) Unlight();
            _demoFrom = _demoTo = null;
            if (ghost != null) { ghost.enabled = false; ghost.gameObject.SetActive(false); }
            if (hand != null) hand.localScale = Vector3.one;
            if (dim.gameObject.activeSelf && !dim.blocksRaycasts)
                Tween.Fade(dim, 0f, 0.15f, () => { if (dim != null) dim.gameObject.SetActive(false); });
        }

        void Update()
        {
            if (_demoFrom == null || _demoTo == null || !hand.gameObject.activeSelf) return;
            if (_demoFrom.gameObject == null || _demoTo.gameObject == null) { EndDragDemo(); return; }
            _demoT += Time.deltaTime;
            // два способа хода по очереди (раунд 5): сначала «нажми — товар сам встанет на полку»,
            // потом «или перетащи». Товар-копия показывает, куда он поедет
            const float come = 0.45f;   // рука подходит к товару
            const float tap = 0.3f;     // короткое нажатие
            const float fly = 0.6f;     // товар сам летит на полку, рука остаётся
            const float press = 0.25f;  // зажимает
            const float drag = 0.9f;    // тащит к полке
            const float drop = 0.25f;   // отпускает
            const float rest = 0.55f;   // пауза перед повтором
            float tapCycle = come + tap + fly + rest;
            float dragCycle = come + press + drag + drop + rest;
            float cycle = Mathf.Repeat(_demoT, tapCycle + dragCycle);
            var start = _demoFrom.position;
            var end = _demoTo.position;
            float up = 40f * hand.lossyScale.y;
            KeepOnTop();
            ghost.enabled = false;
            ghost.color = new Color(1f, 1f, 1f, 0.85f);
            if (cycle < tapCycle)
            {
                if (cycle < come) PutFinger(start + new Vector3(0f, up * (1f - cycle / come), 0f));
                else if (cycle < come + tap)
                {
                    float k = (cycle - come) / tap;
                    PutFinger(start, 1f - Mathf.Sin(k * Mathf.PI) * 0.16f);   // нажал и сразу отпустил
                }
                else if (cycle < come + tap + fly)
                {
                    float k = (cycle - come - tap) / fly;
                    k = k * k * (3f - 2f * k);
                    PutFinger(start + new Vector3(0f, up * 0.6f * k, 0f));     // рука отходит — товар едет сам
                    ghost.enabled = true;
                    ghost.rectTransform.position = Vector3.Lerp(start, end, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * up * 2f, 0f);
                }
                else PutFinger(start + new Vector3(0f, up * 0.6f, 0f));
                return;
            }
            cycle -= tapCycle;
            if (cycle < come)
            {
                float k = cycle / come;
                PutFinger(start + new Vector3(0f, up * (1f - k), 0f));
            }
            else if (cycle < come + press)
            {
                float k = (cycle - come) / press;
                PutFinger(start, Mathf.Lerp(1f, 0.86f, k));    // «нажал и держит»
            }
            else if (cycle < come + press + drag)
            {
                float k = (cycle - come - press) / drag;
                k = k * k * (3f - 2f * k);                     // плавный разгон и торможение
                var p = Vector3.Lerp(start, end, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * up, 0f);
                PutFinger(p, 0.86f);
                ghost.enabled = true;                          // товар едет под пальцем
                ghost.rectTransform.position = p;
            }
            else if (cycle < come + press + drag + drop)
            {
                float k = (cycle - come - press - drag) / drop;
                PutFinger(end, Mathf.Lerp(0.86f, 1f, k));      // отпустил
                ghost.enabled = true;
                ghost.rectTransform.position = end;
                ghost.color = new Color(1f, 1f, 1f, 0.85f * (1f - k));
            }
            else PutFinger(end + new Vector3(0f, up * 0.5f, 0f));
        }

        public void PointAtWorld(Vector3 world) => Follow(() => world);

        void Follow(Func<Vector3> where)
        {
            if (_demoFrom != null) EndDragDemo(); // обычный указатель завершает демо
            hand.gameObject.SetActive(true);
            hand.SetAsLastSibling();
            Tween.Kill(hand);
            hand.position = where() + new Vector3(30f, -60f, 0f) * hand.lossyScale.x;
            Tween.Run(hand, Tween.ChPos, 1000f, Ease.Linear, k =>
            {
                if (hand == null) return;
                float t = k * 1000f;
                hand.position = where() + new Vector3(30f, -60f, 0f) * hand.lossyScale.x
                    + new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 3.5f)) * 22f * hand.lossyScale.y, 0f);
            });
        }

        public void Hide()
        {
            EndDragDemo();
            Unlight();
            Tween.Kill(hand);
            hand.gameObject.SetActive(false);
            tapCatcher.gameObject.SetActive(false);
            _onTap = null;
            _strict = null;
            if (bubble.gameObject.activeSelf) Tween.Scale(bubble, Vector3.zero, 0.15f, Ease.InQuad, () => bubble.gameObject.SetActive(false));
            raccoon.gameObject.SetActive(false);
            if (dim.gameObject.activeSelf) Tween.Fade(dim, 0f, 0.15f, () => dim.gameObject.SetActive(false));
        }

        public bool Visible => bubble.gameObject.activeSelf;

        /// <summary>Программный «тап» по обучению — для бота (BotBridge) и отладки.</summary>
        public void TapNow()
        {
            var cb = _onTap;
            _onTap = null;
            if (cb != null) { Hide(); cb(); }
            else Hide();
        }
    }
}
