using UnityEngine;

namespace Paradox.Client
{
    /// <summary>씬에 놓아 두면 그 씬이 열릴 때 배경음악(Resources/Music/{track})을 틀어 준다. 같은 곡이면 끊김 없이 이어지고, 다른 곡이면 크로스페이드.</summary>
    public class SceneMusic : MonoBehaviour
    {
        public string track = "";
        [Range(0f, 1f)] public float volume = 0.35f;

        private void Start() { Bgm.Play(track, volume); }
    }

    /// <summary>씬을 넘나드는 배경음악 재생기(자동 생성, 루프). 곡이 바뀔 때는 앞 곡이 먼저 완전히 줄어든 뒤 다음 곡이 천천히 들어온다(겹치지 않는다).</summary>
    public class Bgm : MonoBehaviour
    {
        private const float FadeOut = 1.6f, FadeIn = 2.6f, Gap = 0.35f;
        private static Bgm _inst;
        private AudioSource _a, _b;       // _a = 현재 곡, _b = 사라지는 곡
        private string _cur = "";
        private float _vol, _outRate = 0.3f, _gap;
        private AudioClip _pending;

        private static Bgm Get()
        {
            if (_inst != null) return _inst;
            var g = new GameObject("Bgm"); DontDestroyOnLoad(g);
            _inst = g.AddComponent<Bgm>();
            _inst._a = g.AddComponent<AudioSource>(); _inst._b = g.AddComponent<AudioSource>();
            foreach (var s in new[] { _inst._a, _inst._b }) { s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f; }
            return _inst;
        }

        public static void Play(string track, float volume = 0.35f)
        {
            var m = Get();
            if (string.IsNullOrEmpty(track)) { Stop(); return; }
            if (m._cur == track) { m._vol = volume; return; }
            var clip = Resources.Load<AudioClip>("Music/" + track);
            if (clip == null) { Debug.LogWarning("[Bgm] 곡을 찾을 수 없음: " + track); return; }
            m.StartSwap();
            m._pending = clip; m._cur = track; m._vol = volume; m._gap = Gap;
        }

        // 지금 곡을 _b 로 넘겨 서서히 끈다. 이미 끄는 중인 곡이 있으면 그것도 같이 끝낸다.
        private void StartSwap()
        {
            if (_a.isPlaying)
            {
                if (_b.isPlaying) _b.Stop();
                var t = _b; _b = _a; _a = t;
                _outRate = Mathf.Max(0.05f, _b.volume) / FadeOut;
            }
            _a.Stop(); _a.volume = 0f;
        }

        public static void Stop()
        {
            if (_inst == null) return;
            var m = _inst;
            if (m._cur != "" || m._pending != null)
            {
                m._pending = null; m._cur = ""; m._vol = 0f;
                if (m._a.isPlaying) { if (m._b.isPlaying) m._b.Stop(); var t = m._b; m._b = m._a; m._a = t; m._outRate = Mathf.Max(0.05f, m._b.volume) / FadeOut; }
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_b.isPlaying)
            {
                _b.volume = Mathf.MoveTowards(_b.volume, 0f, dt * _outRate);
                if (_b.volume <= 0.001f) _b.Stop();
            }
            if (_pending != null && !_b.isPlaying)
            {
                _gap -= dt;
                if (_gap <= 0f) { _a.clip = _pending; _a.volume = 0f; _a.Play(); _pending = null; }
            }
            if (_a.isPlaying && _cur != "")
            {
                float target = Mathf.Clamp01(_vol * Paradox.Core.AudioPrefs.BgmMul);
                _a.volume = Mathf.MoveTowards(_a.volume, target, dt * Mathf.Max(0.1f, target) / FadeIn);
            }
        }
    }
}
