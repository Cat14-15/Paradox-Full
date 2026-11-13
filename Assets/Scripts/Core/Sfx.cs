using System.Collections.Generic;
using UnityEngine;

namespace Paradox.Core
{
    /// <summary>
    /// 효과음(QA D-A1). 소리 파일이 없으므로 AudioClip.Create(44.1kHz, 모노)로 만든 프로시저럴 클립을 쓴다.
    /// Resources/Sfx/{id}.wav 가 있으면 그것을 우선 사용(선택). 음량은 AudioListener.volume(Progress.ApplyAudio)을 그대로 따른다.
    /// 전용 GameObject(DontDestroyOnLoad)에 AudioSource 2개(효과 / 루프). AudioListener 가 없거나 배치 모드 예외가 나면 무음(try/catch).
    /// id: alarm(버저 2회) · siren(상승·하강) · wind(노이즈) · thud(정전 '쿵') · clear(아르페지오) · click(짧은 틱) · chime(카드·신호 수신).
    /// "wind,siren" 처럼 쉼표로 이으면 차례로 재생한다(앞 클립 길이의 60% 뒤에 다음).
    /// </summary>
    /// <summary>
    /// 소리 설정. 슬라이더 값(0~1)이 0.5 일 때가 지금까지의 기본 음량이다(배율 = 값 × 2). 기본값은 0.5.
    /// </summary>
    public static class AudioPrefs
    {
        public static float Sfx
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat("stranger_vol_sfx", 0.5f));
            set { PlayerPrefs.SetFloat("stranger_vol_sfx", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }
        public static float Bgm
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat("stranger_vol_bgm", 0.5f));
            set { PlayerPrefs.SetFloat("stranger_vol_bgm", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }
        public static float SfxMul => Sfx * 2f;
        public static float BgmMul => Bgm * 2f;
    }

    public static class Sfx
    {
        public const int SampleRate = 44100;

        private class Host : MonoBehaviour
        {
            public AudioSource fx, loop;
            public readonly List<Pending> queue = new List<Pending>();

            private void Update()
            {
                if (queue.Count == 0) return;
                float now = Time.unscaledTime;
                for (int i = queue.Count - 1; i >= 0; i--)
                {
                    var p = queue[i];
                    if (now < p.at) continue;
                    queue.RemoveAt(i);
                    try { if (fx != null && p.clip != null) fx.PlayOneShot(p.clip, p.volume); } catch { }
                }
            }
        }

        private struct Pending { public AudioClip clip; public float volume; public float at; }

        private static Host _host;
        private static bool _dead;   // 한 번 실패하면 이후 호출은 조용히 무시
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        private static Host Ensure()
        {
            if (_dead) return null;
            if (_host != null) return _host;
            try
            {
                var go = new GameObject("Sfx");
                Object.DontDestroyOnLoad(go);
                _host = go.AddComponent<Host>();
                _host.fx = go.AddComponent<AudioSource>();
                _host.fx.playOnAwake = false; _host.fx.spatialBlend = 0f;
                _host.loop = go.AddComponent<AudioSource>();
                _host.loop.playOnAwake = false; _host.loop.spatialBlend = 0f; _host.loop.loop = true;
                return _host;
            }
            catch { _dead = true; return null; }
        }

        /// <summary>효과음 재생. id 가 "a,b" 면 a 뒤에 b. 실패는 무음.</summary>
        public static void Play(string id, float volume = 1f)
        {
            if (string.IsNullOrEmpty(id)) return;
            try
            {
                var host = Ensure();
                if (host == null) return;
                float delay = 0f;
                foreach (var raw in id.Split(','))
                {
                    string one = raw.Trim();
                    if (one.Length == 0) continue;
                    var clip = Get(one);
                    if (clip == null) continue;
                    float v = Mathf.Clamp01(volume) * AudioPrefs.SfxMul;
                    if (delay <= 0f) host.fx.PlayOneShot(clip, v);
                    else host.queue.Add(new Pending { clip = clip, volume = v, at = Time.unscaledTime + delay });
                    delay += clip.length * 0.6f;
                }
            }
            catch { }
        }

        /// <summary>루프 채널에 반복 재생(바람 등). 같은 id 면 유지.</summary>
        public static void PlayLoop(string id, float volume = 1f)
        {
            try
            {
                var host = Ensure();
                if (host == null) return;
                var clip = Get(id);
                if (clip == null) return;
                if (host.loop.isPlaying && host.loop.clip == clip) return;
                host.loop.clip = clip; host.loop.volume = Mathf.Clamp01(Mathf.Clamp01(volume) * AudioPrefs.SfxMul); host.loop.Play();
            }
            catch { }
        }

        /// <summary>효과·루프·예약 전부 정지.</summary>
        public static void Stop()
        {
            try
            {
                if (_host == null) return;
                _host.queue.Clear();
                if (_host.fx != null) _host.fx.Stop();
                if (_host.loop != null) { _host.loop.Stop(); _host.loop.clip = null; }
            }
            catch { }
        }

        /// <summary>클립 길이(초). 없으면 0.</summary>
        public static float Length(string id)
        {
            try { var c = Get(id); return c != null ? c.length : 0f; } catch { return 0f; }
        }

        private static AudioClip Get(string id)
        {
            if (Clips.TryGetValue(id, out var c) && c != null) return c;
            c = null;
            try { c = Resources.Load<AudioClip>("Sfx/" + id); } catch { c = null; }
            if (c == null) c = Generate(id);
            if (c != null) Clips[id] = c;
            return c;
        }

        // ------------------------------------------------------------------ 프로시저럴 합성

        private static AudioClip Generate(string id)
        {
            float[] data;
            switch (id)
            {
                case "alarm": data = Alarm(); break;
                case "siren": data = Siren(); break;
                case "wind": data = Wind(); break;
                case "thud": data = Thud(); break;
                case "clear": data = Clear(); break;
                case "click": data = Click(); break;
                case "ui_type": data = TypeTick(1900f); break;
                case "ui_type2": data = TypeTick(2250f); break;
                case "ui_type3": data = TypeTick(1650f); break;
                case "chime": data = Chime(); break;
                case "fuse_pop": data = FusePop(); break;
                case "power_on": data = PowerOn(); break;
                default: return null;
            }
            var clip = AudioClip.Create("sfx_" + id, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float[] Buf(float seconds) => new float[Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate))];
        private static float T(int i) => i / (float)SampleRate;
        private static float Square(float phase) => (phase % 1f) < 0.5f ? 1f : -1f;
        private static float Sine(float phase) => Mathf.Sin(phase * Mathf.PI * 2f);

        /// <summary>양끝을 부드럽게(클릭 방지).</summary>
        private static float Edge(float t, float len, float ramp = 0.006f)
        {
            float a = Mathf.Clamp01(t / ramp);
            float b = Mathf.Clamp01((len - t) / ramp);
            return Mathf.Min(a, b);
        }

        private static uint _seed = 2463534242u;
        private static float Noise()
        {
            _seed ^= _seed << 13; _seed ^= _seed >> 17; _seed ^= _seed << 5;
            return (_seed / (float)uint.MaxValue) * 2f - 1f;
        }

        /// <summary>0.5초 버저 2회: 사각파 440Hz(0~0.2s) → 330Hz(0.27~0.47s).</summary>
        private static float[] Alarm()
        {
            var d = Buf(0.5f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                float v = 0f;
                if (t < 0.2f) v = Square(t * 440f) * Edge(t, 0.2f);
                else if (t >= 0.27f && t < 0.47f) v = Square((t - 0.27f) * 330f) * Edge(t - 0.27f, 0.2f);
                d[i] = v * 0.22f;
            }
            return d;
        }

        /// <summary>2.5초 사인 사이렌: 600 → 900 → 600Hz 를 한 번 오르내린다.</summary>
        private static float[] Siren()
        {
            var d = Buf(2.5f);
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                float k = 0.5f - 0.5f * Mathf.Cos(t / 2.5f * Mathf.PI * 2f);   // 0 → 1 → 0
                float f = Mathf.Lerp(600f, 900f, k);
                phase += f / SampleRate;
                float env = Mathf.Min(Mathf.Clamp01(t / 0.15f), Mathf.Clamp01((2.5f - t) / 0.4f));
                d[i] = Sine(phase) * env * 0.26f;
            }
            return d;
        }

        /// <summary>2초 바람: 화이트노이즈 → 1극 저역 필터 → 느린 진폭 변조(0.7Hz).</summary>
        private static float[] Wind()
        {
            var d = Buf(2f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                lp += 0.06f * (Noise() - lp);
                float mod = 0.55f + 0.45f * Mathf.Sin(t * 0.7f * Mathf.PI * 2f - 1.2f);
                float env = Mathf.Min(Mathf.Clamp01(t / 0.4f), Mathf.Clamp01((2f - t) / 0.6f));
                d[i] = lp * 2.2f * mod * env * 0.5f;
            }
            return d;
        }

        /// <summary>0.35초 '쿵': 60Hz 감쇠 사인 + 첫 20ms 노이즈 클릭.</summary>
        private static float[] Thud()
        {
            var d = Buf(0.35f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                float body = Sine(t * 60f) * Mathf.Exp(-t * 11f);
                float click = t < 0.02f ? Noise() * (1f - t / 0.02f) * 0.5f : 0f;
                d[i] = (body * 0.9f + click) * 0.6f * Edge(t, 0.35f, 0.003f);
            }
            return d;
        }

        /// <summary>0.9초 도·미·솔·도 아르페지오(C5 E5 G5 C6), 겹치며 감쇠.</summary>
        private static float[] Clear()
        {
            var d = Buf(0.9f);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                float v = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float st = n * 0.18f;
                    if (t < st) continue;
                    float lt = t - st;
                    float env = Mathf.Exp(-lt * 4.5f) * Mathf.Clamp01(lt / 0.008f);
                    v += (Sine(lt * notes[n]) + 0.35f * Sine(lt * notes[n] * 2f)) * env;
                }
                d[i] = v * 0.16f * Mathf.Clamp01((0.9f - t) / 0.05f);
            }
            return d;
        }

        /// <summary>퓨즈가 터지는 소리: 파직 하는 잡음 + 낮은 퍽 (약 0.45초).</summary>
        private static float[] FusePop()
        {
            var d = Buf(0.45f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                float crackle = Noise() * Mathf.Exp(-t * 9f) * (Noise() > 0.2f ? 1f : 0.25f);
                float pop = Sine(t * 90f) * Mathf.Exp(-t * 14f);
                d[i] = (crackle * 0.55f + pop * 0.7f) * Edge(t, 0.45f, 0.002f) * 0.7f;
            }
            return d;
        }

        /// <summary>전원이 들어오는 소리: 찰칵 → 낮은 웅 소리가 올라갔다 안정된다 (약 1.2초).</summary>
        private static float[] PowerOn()
        {
            var d = Buf(1.2f);
            float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                float f = 50f + 110f * (1f - Mathf.Exp(-t * 3.2f));
                ph += f / SampleRate;
                float hum = (Sine(ph) + 0.4f * Sine(ph * 2f) + 0.2f * Sine(ph * 3f)) * Mathf.Clamp01(t / 0.25f) * Mathf.Exp(-Mathf.Max(0f, t - 0.5f) * 2.2f);
                float click = t < 0.03f ? Noise() * (1f - t / 0.03f) : 0f;
                float ting = t > 0.45f ? Sine((t - 0.45f) * 1480f) * Mathf.Exp(-(t - 0.45f) * 7f) * 0.35f : 0f;
                d[i] = (hum * 0.4f + click * 0.5f + ting) * Edge(t, 1.2f, 0.004f) * 0.8f;
            }
            return d;
        }

        /// <summary>0.05초 짧은 틱.</summary>
        private static float[] Click()
        {
            var d = Buf(0.05f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                d[i] = (Sine(t * 1200f) * 0.8f + Noise() * 0.2f) * Mathf.Exp(-t * 90f) * 0.35f;
            }
            return d;
        }

        /// <summary>경쾌한 자판 소리: 높은 '톡' (약 45ms). 변형 3종(음높이가 조금씩 다름)을 번갈아 쓴다.</summary>
        private static float[] TypeTick(float freq)
        {
            var d = Buf(0.045f);
            float hp = 0f, prev = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                float n = Noise(); hp = 0.82f * (hp + n - prev); prev = n;           // 고역만 남긴 노이즈 = 키캡 '딱'
                float tone = Sine(t * freq) * Mathf.Exp(-t * 110f);                  // 짧게 울리는 윗음 = 경쾌함
                float thock = Sine(t * freq * 0.5f) * Mathf.Exp(-t * 160f) * 0.5f;
                d[i] = (hp * Mathf.Exp(-t * 230f) * 0.55f + tone * 0.6f + thock) * 0.55f * Edge(t, 0.045f, 0.001f);
            }
            return d;
        }

        /// <summary>0.25초 차임(880 + 1320Hz).</summary>
        private static float[] Chime()
        {
            var d = Buf(0.25f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = T(i);
                float env = Mathf.Exp(-t * 12f) * Mathf.Clamp01(t / 0.004f);
                d[i] = (Sine(t * 880f) + 0.6f * Sine(t * 1320f)) * env * 0.22f * Mathf.Clamp01((0.25f - t) / 0.03f);
            }
            return d;
        }
    }
}
