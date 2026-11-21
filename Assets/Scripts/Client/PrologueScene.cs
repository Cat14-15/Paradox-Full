using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Paradox.Client
{
    [Serializable]
    public class PrologueLine
    {
        public string speaker = "";   // 빈 문자열 = 내레이션
        [TextArea] public string text = "";
        public int kind;              // 0 대사, 1 내레이션, 2 속마음
        public int portrait = -1;     // -1 없음, 0 서하린(그림), 1~ 그림 없는 인물(실루엣 자리표시)
        public string note = "";      // 지문(이름 옆 작은 글씨: "어색하게 웃으며")
        public int focus = -1;       // 장면 속 실루엣 번호(0~3 식탁, 4 지호) — 말할 때 살짝 올라온다
        public float chatter = 0.35f; // 식탁의 들썩임 세기(0 정지 ~ 1 왁자지껄)
        public string chapter = "";   // 비어 있지 않으면 이 줄 앞에서 화면이 어두워지고 제목 카드가 뜬다("다음 날 · 조별 과제")
        public string chapterSub = "";
        public string ambience = "";  // 이 줄(장면)부터 깔 배경 소리(Resources/Sfx 이름). "" = 그대로, "none" = 끄기
        public float ambVol = 0.3f;
        public string sfx = "";       // 이 줄이 뜰 때 한 번 울릴 효과음(학교 종 등)
        public float sfxVol = 0.8f;
        public bool operatorOnly;     // true 면 조작자(서하린) 화면에만 글이 보이고, 나머지 역할에는 ??? 로 보인다(언어 장벽)
        public float dark = -1f;      // 이 줄부터의 어둠: -1 그대로, 0 밝게, 1 완전한 암전, 2 손전등 불빛만 남은 어둠
        public string bgm = "";       // 이 줄부터 바꿀 배경 음악. "none" = 끄기, "" = 그대로
        public int bg = -1;           // 제목 카드 뒤에 바꿀 배경(bgSprites 번호), -1 = 그대로
    }

    /// <summary>
    /// 프롤로그(씬 1 점심시간). 씬에 놓인 실루엣 + 대화창을 재생하고, 끝나면 nextScene 으로 넘어간다.
    /// Enter/Space/E/클릭 = 다음(타이핑 중이면 한 번에 표시), X(또는 Esc) = 건너뛰기.
    /// </summary>
    public class PrologueScene : MonoBehaviour
    {
        public CanvasGroup black;
        public RectTransform[] sils;          // 말하는 사람 머리 위 위치(0~3 식탁, 4 지호)
        public RectTransform marker;          // 말하는 사람 머리 위 ▼ 표시
        public RectTransform dialogue;        // 대화창 전체(처음엔 숨김)
        public CanvasGroup dialogueGroup;
        public Image portrait;                // 말하는 사람 초상
        public RectTransform portraitRect;
        public Sprite[] portraitSprites;      // 0 서하린 / 1~ 자리표시(실루엣)
        public PixelText nameText, tagText, bodyText, nextMark;
        public AudioSource crowd;             // 점심시간 왁자지껄(첫 글을 넘기기 전까지)
        public PixelText waitText;            // 모두 끝나길 기다리는 동안 검은 화면에 뜨는 글
        public RectTransform nameRect;
        public Image bgImage;                 // 배경 그림
        public Sprite[] bgSprites;            // 0 점심시간 급식실 / 1 교실
        public PixelText titleText, subText;  // 장면 제목 카드(검은 화면 위)
        public CanvasGroup darkGroup;
        public Image darkFill;                // 완전 암전용 검은 판(손전등이 켜지면 사라진다)         // 정전: 손전등 구멍만 뚫린 어둠
        public string nextScene = "Paradox";
        [Tooltip("true 면 캠프 씬: 전원 대기에 campDone 을 쓴다")] public bool isCamp;
        public string startAmbience = "cafeteria_crowd";
        public float startAmbVol = 0.28f;
        public string bgmTrack = "prologue";
        public PrologueLine[] lines;

        private int _i = -1;
        private string _full = "";
        private float _typed;
        private bool _typing, _busy, _ending;
        private float _chatter = 1f, _chatterTarget = 1f;
        private int _focus = -1;
        private float _t;
        private Vector2[] _base;
        private float _portraitAlpha;
        private const float CharsPerSec = 32f;
        private const int WrapDots = 500;     // 본문 폭(도트) — scale 3 기준 약 1500px

        private RectTransform _skipRect;
        private Image _skipBg;
        private float _sceneStart;

        private void Start()
        {
            _sceneStart = Time.unscaledTime;
            lines = StoryText.Trim(lines, isCamp);   // 시작 전 이야기 줄이기(덜 중요한 줄은 뺀다)
            var sk = GameObject.Find("SkipBg");     // 오른쪽 위 건너뛰기: 마우스로도 누를 수 있게
            if (sk != null)
            {
                _skipRect = sk.GetComponent<RectTransform>();
                _skipBg = sk.GetComponent<Image>();
                StoryText.StyleSkip(_skipRect, sk.GetComponentInChildren<PixelText>(true), _skipBg);
            }
            _base = new Vector2[sils.Length];
            for (int i = 0; i < sils.Length; i++) _base[i] = sils[i].anchoredPosition;
            dialogueGroup.alpha = 0f;
            black.alpha = 1f;
            Bgm.Play(bgmTrack, 0.3f);
            Bgm.Preload(bgmTrack == "prologue" ? "camp" : "story1");   // 다음 장면 곡을 미리 불러 둔다   // 음악 위에 왁자지껄이 은은하게 깔린다
            if (crowd != null) { crowd.loop = true; crowd.volume = 0f; if (!string.IsNullOrEmpty(startAmbience)) SetAmbience(startAmbience, startAmbVol); }
            StartCoroutine(Intro());
        }

        private IEnumerator Intro()
        {
            yield return Fade(0f, 1.4f);
            yield return new WaitForSeconds(1.2f);   // 왁자지껄만 먼저 보여 준다
            Next();
        }

        private IEnumerator Fade(float to, float dur)
        {
            float from = black.alpha, t = 0f;
            while (t < dur) { t += Time.deltaTime; black.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / dur)); yield return null; }
            black.alpha = to;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            StoryText.FitWidth(dialogue, 1800f);   // 좁은 화면 · 다른 비율에서도 대화창이 화면 밖으로 잘리지 않게
            float cw = StoryText.CanvasWidth(dialogue) - 48f;   // 장면 제목 · 기다림 글씨도 화면 폭 안에(글씨를 줄인다)
            if (titleText != null) titleText.maxWidth = Mathf.Min(1700f, cw);
            if (subText != null) subText.maxWidth = Mathf.Min(1500f, cw);
            if (waitText != null) waitText.maxWidth = Mathf.Min(1500f, cw);
            AnimateCast();
            if (darkGroup != null && !_flickering) { _darkValue = Mathf.MoveTowards(_darkValue, _darkTarget, Time.deltaTime * 1.5f); darkGroup.alpha = _darkValue; }
            if (darkFill != null) { var fc = darkFill.color; fc.a = Mathf.MoveTowards(fc.a, _fillTarget, Time.deltaTime * (_fillTarget > fc.a ? 6f : 3f)); darkFill.color = fc; }
            if (crowd != null)
            {
                float want = Mathf.Clamp01(_ambVol * Paradox.Core.AudioPrefs.SfxMul);
                if (_ambClip != crowd.clip)
                {
                    want = 0f;   // 다른 소리로 바뀌는 중 — 먼저 서서히 줄이고, 조용해지면 갈아 끼운다
                    if (crowd.volume <= 0.001f) { crowd.Stop(); crowd.clip = _ambClip; if (_ambClip != null) crowd.Play(); }
                }
                crowd.volume = Mathf.MoveTowards(crowd.volume, want, Time.deltaTime * (want > crowd.volume ? 0.3f : 0.2f));
            }

            if (_ending) return;
            var kb = Keyboard.current; var ms = Mouse.current;
            StoryText.HoverSkip(_skipRect, _skipBg);
            // 건너뛰기: X · Esc 또는 버튼 클릭(웹에서는 Esc 가 전체화면을 풀어서 X 를 기본으로 안내한다). 이전 장면에서 연타한 X 는 무시한다
            if (StoryText.SkipPressed(_skipRect, Time.unscaledTime - _sceneStart)) { Paradox.Core.Sfx.Play("ui_back"); StartCoroutine(End()); return; }
            bool onSkip = StoryText.Over(_skipRect);
            bool go = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                      || (StoryText.Tap() && !onSkip);   // 마우스·터치 공통
            if (go && !_busy && _i >= 0) { if (_typing) Reveal(); else Next(); }

            if (_typing)
            {
                _typed += Time.deltaTime * CharsPerSec;
                int n = Mathf.Min(_full.Length, (int)_typed);
                bodyText.text = _full.Substring(0, n);
                if (n >= _full.Length) { _typing = false; }
            }
            nextMark.gameObject.SetActive(!_typing && _i >= 0);
            if (nextMark.gameObject.activeSelf)
            {
                var r = nextMark.rectTransform;
                r.anchoredPosition = new Vector2(r.anchoredPosition.x, -466f + Mathf.Round(Mathf.Sin(_t * 5f)) * 5f);
            }
            _portraitAlpha = Mathf.MoveTowards(_portraitAlpha, _wantPortrait ? 1f : 0f, Time.deltaTime * 6f);
            var c = portrait.color; c.a = _portraitAlpha; portrait.color = c;
        }

        private float _darkTarget, _darkValue, _fillTarget;
        private bool _flickering;

        private IEnumerator Flicker()
        {
            // 정전: 번쩍 꺼졌다가 한 번 더 깜빡이고 완전히 어두워진다
            _flickering = true;
            float[] a = { 0.95f, 0.1f, 0.9f, 0.25f, 0.97f };
            foreach (float v in a) { _darkValue = v; darkGroup.alpha = v; yield return new WaitForSeconds(v > 0.5f ? 0.16f : 0.09f); }
            _flickering = false;
        }

        private bool _wantPortrait;
        private AudioClip _ambClip;
        private float _ambVol;

        /// <summary>배경 소리(왁자지껄·교실·거리)를 서서히 바꾼다. name 이 비어 있으면 끈다.</summary>
        private void SetAmbience(string name, float vol)
        {
            _ambClip = string.IsNullOrEmpty(name) ? null : Resources.Load<AudioClip>("Sfx/" + name);
            _ambVol = vol;
            if (_ambClip == null) _ambVol = 0f;
        }

        private void StopCrowd() { _ambVol = 0f; }   // 첫 글을 넘기면 왁자지껄이 음악 속으로 천천히 스며든다

        private void AnimateCast()
        {
            // 말하는 사람 머리 위에 ▼ 가 통통 뛴다(그림 배경이라 실루엣을 직접 움직이지 않는다)
            if (marker == null) return;
            bool show = _focus >= 0 && _focus < sils.Length && _i >= 0 && !_ending;
            marker.gameObject.SetActive(show);
            if (!show) return;
            var p = sils[_focus].anchoredPosition;
            marker.anchoredPosition = new Vector2(p.x, p.y + Mathf.Round(Mathf.Abs(Mathf.Sin(_t * 4f)) * 3f) * 4f);
        }

        private void Next()
        {
            if (_ending) return;
            _i++;
            if (_i > 0 && !_resume) Paradox.Core.Sfx.Play("ui_next", 0.8f);
            _resume = false;
            if (_i >= 1 && _chapterDone < 0 && !isCamp) StopCrowd();
            if (lines == null || _i >= lines.Length) { StartCoroutine(End()); return; }
            var l = lines[_i];
            if (!string.IsNullOrEmpty(l.chapter) && _chapterDone != _i) { _chapterDone = _i; StartCoroutine(Chapter(l)); return; }
            _focus = l.focus; _chatterTarget = l.chatter;
            if (!string.IsNullOrEmpty(l.sfx)) Paradox.Core.Sfx.Play(l.sfx, l.sfxVol);
            if (!string.IsNullOrEmpty(l.bgm)) { if (l.bgm == "none") Bgm.Stop(); else Bgm.Play(l.bgm, 0.3f); }
            if (l.dark >= 0f && darkGroup != null)
            {
                float want = l.dark > 0f ? 1f : 0f;
                if (want > 0.5f && _darkTarget < 0.1f) StartCoroutine(Flicker());
                _darkTarget = want; _fillTarget = l.dark > 0f && l.dark < 2f ? 0.97f : 0f;
            }
            if (!string.IsNullOrEmpty(l.ambience) && _chapterDone != _i && !_resume) SetAmbience(l.ambience == "none" ? "" : l.ambience, l.ambVol);
            dialogueGroup.alpha = 1f;

            bool narr = l.kind == 1;
            string speaker = StoryText.Speaker(l.speaker);   // 로비에서 정한 이름으로
            nameText.text = narr ? "" : speaker;
            string note = l.kind == 2 ? "속마음" : StoryText.Apply(l.note);
            tagText.text = note ?? "";
            nameText.color = l.portrait == 0 || l.kind == 2 ? new Color(0.95f, 0.77f, 0.45f) : new Color(0.9f, 0.95f, 0.96f);
            bodyText.color = narr ? new Color(0.78f, 0.8f, 0.84f) : l.kind == 2 ? new Color(0.72f, 0.86f, 1f) : new Color(0.96f, 0.96f, 0.94f);

            // 초상
            _wantPortrait = l.portrait >= 0 && portraitSprites != null && l.portrait < portraitSprites.Length;
            if (_wantPortrait)
            {
                portrait.sprite = portraitSprites[l.portrait];
                portrait.color = new Color(1, 1, 1, 0f);
                var sp = portraitSprites[l.portrait];
                float ph = 514f;
                portraitRect.sizeDelta = new Vector2(ph * sp.rect.width / sp.rect.height, ph);
                // 내 캐릭터는 왼쪽, 상대는 오른쪽(오른쪽은 좌우를 뒤집어 안쪽을 보게)
                bool left = StoryText.IsMine(l.portrait, isCamp);
                portraitRect.anchoredPosition = new Vector2(left ? -680f : 680f, portraitRect.anchoredPosition.y);
                portraitRect.localScale = new Vector3(left ? 1f : -1f, 1f, 1f);
            }

            // 초상이 없으면(내레이션) 글을 왼쪽 끝부터 시작한다
            // 초상은 대화창 위에 얹히므로 글은 항상 대화창 왼쪽 끝(여백 80)부터 시작한다
            const float shift = -220f;
            string shown = StoryText.Apply(l.text);   // 이름 · 조사("서하린아" → "하린아")
            if (l.operatorOnly && Session.Connected && Session.MySlot >= 0 && Session.MySlot != (int)Paradox.Game.GameRole.Operator)
            {
                var sb = new StringBuilder();
                foreach (char ch in shown) sb.Append(char.IsWhiteSpace(ch) ? ch : '?');
                shown = sb.ToString();
            }
            _full = Wrap(shown);
            // 이름표 · 대사 자리는 고정(말하는 사람이나 줄 수가 바뀌어도 움직이지 않는다)
            bodyText.maxWidth = 1600f;   // 혹시 넘치는 줄은 글씨를 줄여 상자 안에
            bodyText.rectTransform.anchoredPosition = new Vector2(220f + shift, StoryText.BodyTop - bodyText.rectTransform.sizeDelta.y * 0.5f);
            nameRect.anchoredPosition = new Vector2(-550f, StoryText.NameY);
            if (!string.IsNullOrEmpty(note)) tagText.rectTransform.anchoredPosition = new Vector2(nameRect.anchoredPosition.x - nameRect.sizeDelta.x * 0.5f + PixelFontData.Measure(speaker) * 3 + 30f + tagText.rectTransform.sizeDelta.x * 0.5f, nameRect.anchoredPosition.y - 4f);

            _typed = 0f; _typing = true; bodyText.text = "";
        }

        private int _chapterDone = -1;
        private bool _resume;

        /// <summary>장면 전환: 어두워지고 → 배경을 바꾸고 → 제목 카드 → 다시 밝아지며 대사를 이어 간다.</summary>
        private IEnumerator Chapter(PrologueLine l)
        {
            _busy = true; _typing = false;
            dialogueGroup.alpha = 0f; _wantPortrait = false;
            yield return Fade(1f, 0.8f);
            if (!string.IsNullOrEmpty(l.ambience)) SetAmbience(l.ambience == "none" ? "" : l.ambience, l.ambVol);
            if (l.bg >= 0 && bgImage != null && bgSprites != null && l.bg < bgSprites.Length)
            {
                var sp = bgSprites[l.bg]; bgImage.sprite = sp;
                float k = Mathf.Max(1920f / sp.rect.width, 1080f / sp.rect.height);   // 화면을 꽉 채우고 넘치는 쪽은 잘라낸다
                bgImage.rectTransform.sizeDelta = new Vector2(sp.rect.width * k, sp.rect.height * k);
            }
            if (titleText != null) titleText.text = l.chapter;
            if (subText != null) subText.text = l.chapterSub ?? "";
            yield return new WaitForSeconds(2.0f);
            if (titleText != null) titleText.text = "";
            if (subText != null) subText.text = "";
            yield return Fade(0f, 1.0f);
            _busy = false; _resume = true; _i--; Next();
        }

        private void Reveal() { _typed = _full.Length; }

        private static string Wrap(string s)
        {
            var sb = new StringBuilder();
            int lineW = 0;
            string[] words = s.Split(' ');
            for (int w = 0; w < words.Length; w++)
            {
                int ww = PixelFontData.Measure(words[w]);
                int sp = PixelFontData.Advance(' ');
                if (lineW > 0 && lineW + sp + ww > WrapDots) { sb.Append('\n'); lineW = 0; }
                else if (lineW > 0) { sb.Append(' '); lineW += sp; }
                sb.Append(words[w]); lineW += ww;
            }
            return sb.ToString();
        }

        private IEnumerator End()
        {
            if (_ending) yield break;
            _ending = true;
            if (isCamp) Progress.prologueSeen = true;   // 타이틀 "이어하기"가 켜진다(혼자 하기는 다음부터 박물관으로 바로)
            if (_skipRect != null) _skipRect.gameObject.SetActive(false);
            _ambVol = 0f;
            Bgm.Stop();   // 화면이 어두워지는 동안 음악도 같이 줄어든다
            dialogueGroup.alpha = 0f;
            yield return Fade(1f, 0.9f);

            // 각자 자기 속도로 본 뒤, 접속한 사람이 전부 끝날 때까지 검은 화면에서 기다린다
            if (Session.Connected && Session.MySlot >= 0)
            {
                float lastSend = -99f;
                while (Session.Connected)
                {
                    var S = Session.State;
                    int total = 0, done = 0;
                    foreach (var p in S.players) if (p.present && !p.isBot) { total++; if (isCamp ? p.campDone : p.prologueDone) done++; }
                    bool me = Session.MySlot < S.players.Length && (isCamp ? S.players[Session.MySlot].campDone : S.players[Session.MySlot].prologueDone);
                    if (!me && Time.unscaledTime - lastSend > 2f) { lastSend = Time.unscaledTime; Session.Send(isCamp ? "CampDone" : "PrologueDone"); }
                    if (waitText != null) waitText.text = done < total ? "다른 친구들을 기다리는 중   " + done + " / " + total : "";
                    if (me && total > 0 && done >= total) break;
                    yield return null;
                }
            }
            Bgm.Stop();
            SceneManager.LoadSceneAsync(nextScene);
        }
    }
}
