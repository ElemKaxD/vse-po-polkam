using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AllOnShelves
{
    public enum Ease { Linear, OutQuad, InQuad, InOutQuad, OutBack, OutCubic, InBack }

    /// <summary>Лёгкие твины без сторонних библиотек. Один твин на (цель, канал) — новый заменяет старый.</summary>
    public class Tween : MonoBehaviour
    {
        class Job
        {
            public UnityEngine.Object Target;
            public int Channel;
            public float Time, Duration, Delay;
            public Ease Ease;
            public Action<float> Step;
            public Action Done;
        }

        static Tween _runner;
        readonly List<Job> _jobs = new List<Job>();
        readonly List<Job> _add = new List<Job>();

        public const int ChPos = 1, ChScale = 2, ChAlpha = 3, ChRot = 4, ChColor = 5, ChCustom = 6;

        static Tween Runner
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("[Tween]");
                    DontDestroyOnLoad(go);
                    _runner = go.AddComponent<Tween>();
                }
                return _runner;
            }
        }

        public static void Run(UnityEngine.Object target, int channel, float duration, Ease ease, Action<float> step,
                               Action done = null, float delay = 0f)
        {
            if (!Application.isPlaying)
            {
                // предпросмотр в редакторе: без анимации, сразу конечное состояние
                try { step(1f); done?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                return;
            }
            var r = Runner;
            Kill(target, channel);
            var job = new Job { Target = target, Channel = channel, Duration = Mathf.Max(0.0001f, duration), Ease = ease, Step = step, Done = done, Delay = delay };
            r._add.Add(job);
        }

        public static void Kill(UnityEngine.Object target, int channel = 0)
        {
            if (_runner == null) return;
            foreach (var j in _runner._jobs) if (j.Target == target && (channel == 0 || j.Channel == channel)) j.Target = null;
            _runner._add.RemoveAll(j => j.Target == target && (channel == 0 || j.Channel == channel));
        }

        /// <summary>Мгновенно доводит все твины до конца (новый ввод во время анимаций, ГДД 3.3).</summary>
        public static void CompleteAll()
        {
            if (_runner == null) return;
            _runner._jobs.AddRange(_runner._add);
            _runner._add.Clear();
            var jobs = _runner._jobs.ToArray();
            _runner._jobs.Clear();
            foreach (var j in jobs)
            {
                if (j.Target == null) continue;
                try { j.Step(1f); j.Done?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>Мгновенно доводит твины конкретной цели.</summary>
        public static void Complete(UnityEngine.Object target)
        {
            if (_runner == null || target == null) return;
            _runner._jobs.AddRange(_runner._add);
            _runner._add.Clear();
            for (int i = _runner._jobs.Count - 1; i >= 0; i--)
            {
                var j = _runner._jobs[i];
                if (j.Target != target) continue;
                _runner._jobs.RemoveAt(i);
                try { j.Step(1f); j.Done?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        public static bool Busy => _runner != null && (_runner._jobs.Exists(j => j.Target != null) || _runner._add.Count > 0);

        /// <summary>Идёт ли у цели анимация (channel 0 — любая). Нужно редактору интерфейса: масштаб
        /// «дышащей» кнопки меняет анимация, а не пользователь.</summary>
        public static bool Animating(UnityEngine.Object target, int channel = 0)
        {
            if (_runner == null || target == null) return false;
            bool Match(Job j) => j.Target == target && (channel == 0 || j.Channel == channel);
            return _runner._jobs.Exists(Match) || _runner._add.Exists(Match);
        }

        void Update()
        {
            if (_add.Count > 0) { _jobs.AddRange(_add); _add.Clear(); }
            float dt = UnityEngine.Time.deltaTime;
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                var j = _jobs[i];
                if (j.Target == null) { _jobs.RemoveAt(i); continue; }
                if (j.Delay > 0) { j.Delay -= dt; continue; }
                j.Time += dt;
                float t = Mathf.Clamp01(j.Time / j.Duration);
                try { j.Step(Evaluate(j.Ease, t)); }
                catch (Exception e) { Debug.LogException(e); _jobs.RemoveAt(i); continue; }
                if (t >= 1f)
                {
                    _jobs.RemoveAt(i);
                    try { j.Done?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                }
            }
        }

        public static float Evaluate(Ease e, float t)
        {
            switch (e)
            {
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InQuad: return t * t;
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.OutBack: { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }
                case Ease.InBack: { const float c1 = 1.70158f, c3 = c1 + 1f; return c3 * t * t * t - c1 * t * t; }
                default: return t;
            }
        }

        // ------------------------------------------------------------------ удобные обёртки

        public static void Move(RectTransform rt, Vector3 worldTo, float duration, Ease ease = Ease.OutQuad, float arc = 0f,
                                Action done = null, float delay = 0f)
        {
            Vector3 from = rt.position;
            Run(rt, ChPos, duration, ease, k =>
            {
                if (rt == null) return;
                var p = Vector3.LerpUnclamped(from, worldTo, k);
                if (arc != 0f) p.y += Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * arc * rt.lossyScale.y;
                rt.position = p;
            }, done, delay);
        }

        public static void MoveAnchored(RectTransform rt, Vector2 to, float duration, Ease ease = Ease.OutQuad, Action done = null, float delay = 0f)
        {
            Vector2 from = rt.anchoredPosition;
            Run(rt, ChPos, duration, ease, k => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k); }, done, delay);
        }

        public static void Scale(Transform tr, Vector3 to, float duration, Ease ease = Ease.OutQuad, Action done = null, float delay = 0f)
        {
            Vector3 from = tr.localScale;
            Run(tr, ChScale, duration, ease, k => { if (tr != null) tr.localScale = Vector3.LerpUnclamped(from, to, k); }, done, delay);
        }

        public static void Punch(Transform tr, float amount = 0.15f, float duration = 0.3f, float baseSize = 1f)
        {
            if (!Application.isPlaying) return; // в редакторе не трогать масштаб, заданный руками
            Vector3 baseScale = Vector3.one * baseSize;   // уменьшенные объекты возвращаются к своему размеру
            Run(tr, ChScale, duration, Ease.Linear, k =>
            {
                if (tr == null) return;
                float s = 1f + Mathf.Sin(k * Mathf.PI) * amount * (1f - k * 0.5f);
                tr.localScale = baseScale * s;
            });
        }

        public static void Shake(RectTransform rt, float amount = 12f, float duration = 0.3f)
        {
            if (!Application.isPlaying) return;
            Vector2 basePos = rt.anchoredPosition;
            Run(rt, ChRot, duration, Ease.Linear, k =>
            {
                if (rt == null) return;
                rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(k * Mathf.PI * 8f) * amount * (1f - k), 0f);
            }, () => { if (rt != null) rt.anchoredPosition = basePos; });
        }

        public static void Fade(CanvasGroup cg, float to, float duration, Action done = null, float delay = 0f)
        {
            float from = cg.alpha;
            Run(cg, ChAlpha, duration, Ease.Linear, k => { if (cg != null) cg.alpha = Mathf.LerpUnclamped(from, to, k); }, done, delay);
        }

        public static void Fade(Graphic g, float to, float duration, Action done = null, float delay = 0f)
        {
            float from = g.color.a;
            Run(g, ChAlpha, duration, Ease.Linear, k =>
            {
                if (g == null) return;
                var c = g.color; c.a = Mathf.LerpUnclamped(from, to, k); g.color = c;
            }, done, delay);
        }

        public static void Delay(UnityEngine.Object owner, float seconds, Action done, int channel = ChCustom) =>
            Run(owner, channel, seconds, Ease.Linear, _ => { }, done);
    }
}
