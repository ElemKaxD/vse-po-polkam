#if UNITY_EDITOR
using System.Collections;
using System.IO;
using AllOnShelves;
using UnityEngine;

namespace AllOnShelves.Game
{
    /// <summary>
    /// Запись видео геймплея для витрины Яндекса (только в редакторе, в билд не попадает).
    /// Бот сам проходит уровни из списка, кадры пишутся JPG-файлами в фиксированном темпе
    /// (Time.captureFramerate): игра идёт в «кадровом» времени, поэтому видео ровное,
    /// сколько бы ни длилось сжатие кадра. Склейка в MP4 — ffmpeg (Tools/promo_capture.py).
    /// </summary>
    public class PromoRecorder : MonoBehaviour
    {
        public static PromoRecorder Current;

        string _dir;
        int _frame;
        int[] _levels;
        float _moveEvery, _holdWin, _maxPerLevel;
        public bool Done;
        public string Log = "";

        public static PromoRecorder Run(string dir, int fps, int[] levels, float moveEvery, float holdWin, float maxPerLevel)
        {
            if (Current != null) Destroy(Current.gameObject);
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.jpg")) File.Delete(f);
            var go = new GameObject("PromoRecorder");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<PromoRecorder>();
            r._dir = dir;
            r._levels = levels;
            r._moveEvery = moveEvery;
            r._holdWin = holdWin;
            r._maxPerLevel = maxPerLevel;
            Time.captureFramerate = fps;
            Current = r;
            r.StartCoroutine(r.Capture());
            r.StartCoroutine(r.Play());
            return r;
        }

        IEnumerator Capture()
        {
            var eof = new WaitForEndOfFrame();
            while (!Done)
            {
                yield return eof;
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(_dir, $"f{_frame:00000}.jpg"), tex.EncodeToJPG(92));
                Destroy(tex);
                _frame++;
            }
        }

        // окна победы могут ставить timeScale = 0 — ждём по немасштабированному времени
        static IEnumerator Wait(float sec)
        {
            float end = Time.unscaledTime + sec;
            while (Time.unscaledTime < end) yield return null;
        }

        IEnumerator Play()
        {
            foreach (var lvl in _levels)
            {
                GameApp.I.PlayLevel(lvl);
                GameController gc = null;
                float t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < 6f)
                {
                    gc = FindFirstObjectByType<GameController>();
                    if (gc != null && gc.DebugState != null && !gc.DebugFinished) break;
                    yield return null;
                }
                if (gc == null) { Log += $"L{lvl}: нет уровня; "; continue; }
                yield return Wait(0.8f);
                gc.DebugUnlockInput();
                yield return Wait(0.4f);
                int moves = 0;
                float start = Time.unscaledTime;
                while (!gc.DebugFinished && Time.unscaledTime - start < _maxPerLevel)
                {
                    if (gc.DebugAutoMove()) { moves++; yield return Wait(_moveEvery); }
                    else yield return Wait(0.1f);
                }
                Log += $"L{lvl}: {moves} ходов, {gc.DebugState.Result}, кадр {_frame}; ";
                yield return Wait(_holdWin);
            }
            Done = true;
            Time.captureFramerate = 0;
            Log += $"всего кадров {_frame}";
        }
    }
}
#endif
