using System.Collections.Generic;
using UnityEngine;

namespace AllOnShelves
{
    /// <summary>
    /// Звук и музыка (ГДД 12.4). Клипы ищутся по имени файла в списке clips — его заполняет меню
    /// «Всё по полкам → Обновить звуки» из папки Assets/AllOnShelves/Audio. Нет файла — тишина.
    /// Пауза при рекламе/скрытии вкладки делается плагином YG2 (AudioListener.pause).
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        public static AudioService I { get; private set; }

        public AudioClip[] clips = new AudioClip[0];
        public AudioSource musicSource;
        public AudioSource sfxSource;

        readonly Dictionary<string, AudioClip> _map = new Dictionary<string, AudioClip>();
        string _currentMusic;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            foreach (var c in clips) if (c != null) _map[c.name] = c;
            if (musicSource == null) { musicSource = gameObject.AddComponent<AudioSource>(); musicSource.loop = true; musicSource.playOnAwake = false; }
            if (sfxSource == null) { sfxSource = gameObject.AddComponent<AudioSource>(); sfxSource.playOnAwake = false; }
        }

        float MusicVol => GameApp.I != null ? GameApp.I.Save.music : 0.7f;
        float SfxVol => GameApp.I != null ? GameApp.I.Save.sfx : 1f;

        public static void Play(string name, float pitch = 1f, float volume = 1f)
        {
            if (I == null || !I._map.TryGetValue(name, out var clip)) return;
            I.sfxSource.pitch = pitch;
            I.sfxSource.PlayOneShot(clip, volume * I.SfxVol);
        }

        public static void Music(string name)
        {
            if (I == null || I._currentMusic == name) return;
            I._currentMusic = name;
            if (!I._map.TryGetValue(name, out var clip)) { I.musicSource.Stop(); return; }
            I.musicSource.clip = clip;
            I.musicSource.volume = I.MusicVol * 0.6f;
            I.musicSource.Play();
        }

        public static void ApplyVolumes()
        {
            if (I == null) return;
            I.musicSource.volume = I.MusicVol * 0.6f;
        }
    }
}
