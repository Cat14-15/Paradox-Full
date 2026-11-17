using System.Collections;
using Paradox.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// Intro.unity 전용: 검정 → 제작팀 → STRANGER 로고 → (로고가 위로 올라가며) 타이틀 → "아무 키나" → 메뉴.
    /// 오브젝트(배경·먼지·로고·메뉴·커서)는 모두 씬에 직접 배치돼 있고, 이 컴포넌트는 연출 순서와 입력만 맡는다.
    /// 어느 단계에서든 아무 키/클릭으로 타이틀까지 건너뛴다.
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        [Header("씬에 배치된 오브젝트")]
        public CanvasGroup black;       // 화면 전체 검정(장면 나갈 때)
        public CanvasGroup credit;      // 제작팀 문구
        public CanvasGroup background;  // 타이틀 배경(어두운 박물관 홀)
        public RectTransform backgroundRect;
        public CanvasGroup dust;        // 떠다니는 먼지 묶음
        public CanvasGroup logo;
        public RectTransform logoRect;
        public CanvasGroup tagline;
        public CanvasGroup prompt;      // "아무 키나 누르세요"
        public CanvasGroup menu;
        public Graphic[] menuTexts;     // 시작하기 / 이어하기 / 설정 / 종료 (도트 글씨 이미지)
        public bool[] menuEnabled = { true, false, true, true };
        public RectTransform cursor;    // 선택 항목 옆 막대
        public Image cursorImage;
        public CanvasGroup toast;
        public Image toastImage;        // 도트 글씨 안내 문구
        public Sprite[] toastSprites;   // [0]=이어하기, [1]=설정
        public CanvasGroup plates;      // 카펫 위 발판 4개
        public Vector2 zoomPivot = new Vector2(0f, -60f);   // 확대 중심(카펫 중앙의 화면 위치)
        public float zoomStart = 1f;
        private float _zoom = 1f;

        [Header("설정")]
        public string nextScene = "Lobby";
        public Vector2 logoStartPos = new Vector2(0f, 20f);
        public Vector2 logoTitlePos = new Vector2(0f, 250f);
        public float logoTitleScale = 0.8f;

        private enum State { Intro, TitleWait, Menu, Leaving }
        private State _state = State.Intro;
        private Coroutine _run;
        private int _sel;
        private float _t;
        private static readonly Color Mint = new Color(0.376f, 0.984f, 0.796f);
        private static readonly Color Dim = new Color(0.55f, 0.62f, 0.66f);
        private static readonly Color Off = new Color(0.32f, 0.36f, 0.40f);

        private void Start()
        {
            var font = UIFontHelper.Load();
            foreach (var t in GetComponentsInChildren<Text>(true)) t.font = font;
            SetAll(0f, credit, background, dust, logo, tagline, prompt, menu, toast, plates);
            _zoom = zoomStart;
            black.alpha = 0f;
            logoRect.anchoredPosition = logoTitlePos;      // 로고는 처음부터 최종 자리에서 켜진다
            logoRect.localScale = Vector3.one * logoTitleScale;
            if (SkipToMenu) { SkipToMenu = false; _run = StartCoroutine(ResumeAtMenu()); }
            else _run = StartCoroutine(Play());
        }

        /// <summary>다른 화면(스테이지 선택 등)에서 "뒤로" 왔을 땐 로고 연출 없이 타이틀 메뉴 상태로 바로 복귀한다.</summary>
        public static bool SkipToMenu;

        private IEnumerator ResumeAtMenu()
        {
            black.alpha = 1f;
            yield return ToTitle(true);
            yield return OpenMenu();
            yield return Fade(black, 0f, 0.4f);
        }

        private static void SetAll(float a, params CanvasGroup[] gs) { foreach (var g in gs) if (g != null) g.alpha = a; }

        private IEnumerator Fade(CanvasGroup g, float to, float seconds)
        {
            float from = g.alpha;
            for (float t = 0f; t < seconds; t += Time.deltaTime) { g.alpha = Mathf.Lerp(from, to, t / seconds); yield return null; }
            g.alpha = to;
        }

        // 켜지는 전등처럼: 몇 번 깜빡이며 밝아진다
        private IEnumerator Flicker(CanvasGroup g)
        {
            float[] steps = { 0.45f, 0.1f, 0.75f, 0.3f, 1f };
            float[] waits = { 0.09f, 0.07f, 0.1f, 0.06f, 0.0f };
            for (int i = 0; i < steps.Length; i++) { g.alpha = steps[i]; yield return new WaitForSeconds(waits[i]); }
        }

        private IEnumerator Play()
        {
            yield return new WaitForSeconds(0.5f);
            // 제작팀
            yield return Fade(credit, 1f, 0.8f);
            yield return new WaitForSeconds(1.6f);
            yield return Fade(credit, 0f, 0.6f);
            yield return new WaitForSeconds(0.3f);
            // 로고
            yield return Flicker(logo);
            yield return new WaitForSeconds(0.4f);
            yield return Fade(tagline, 1f, 0.9f);
            yield return new WaitForSeconds(1.3f);
            yield return ToTitle(false);
        }

        // 로고가 위로 올라가며 타이틀 배경이 드러난다. instant 면 바로 그 상태로.
        private IEnumerator ToTitle(bool instant)
        {
            if (instant)
            {
                SetAll(0f, credit);
                logo.alpha = 1f; tagline.alpha = 1f;
                logoRect.anchoredPosition = logoTitlePos; logoRect.localScale = Vector3.one * logoTitleScale;
                background.alpha = 1f; dust.alpha = 1f; plates.alpha = 1f; _zoom = 1f;
            }
            else
            {
                // 로고는 제자리에 둔 채, 어둠 속에서 박물관 바닥이 천천히 밝아진다. 이어서 원 4개가 떠오른다.
                yield return new WaitForSeconds(0.3f);
                for (float t = 0f; t < 2.2f; t += Time.deltaTime)
                {
                    float k = t / 2.2f; k = k * k * (3f - 2f * k);
                    background.alpha = k; dust.alpha = k;
                    yield return null;
                }
                background.alpha = 1f; dust.alpha = 1f;
                yield return new WaitForSeconds(0.3f);
                yield return Fade(plates, 1f, 1.0f);
            }
            _state = State.TitleWait;
            _t = 0f;
            yield return null;
        }

        public bool InMenu => _state == State.Menu;

        private bool AnyPress()
        {
            var kb = Keyboard.current; var m = Mouse.current;
            return (kb != null && kb.anyKey.wasPressedThisFrame) || (m != null && m.leftButton.wasPressedThisFrame);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (SettingsPanel.IsOpen) return;
            // 배경은 아주 천천히 움직인다
            if (backgroundRect != null && background.alpha > 0f)
            {
                backgroundRect.localScale = Vector3.one * _zoom;
                backgroundRect.anchoredPosition = zoomPivot * (1f - _zoom)
                    + new Vector2(Mathf.Sin(Time.time * 0.12f) * 28f, Mathf.Cos(Time.time * 0.09f) * 14f) * (_zoom <= 1.001f ? 1f : 0f);
            }

            switch (_state)
            {
                case State.Intro:
                    if (_t > 0.25f && AnyPress()) { if (_run != null) StopCoroutine(_run); _run = StartCoroutine(ToTitle(true)); }
                    break;
                case State.TitleWait:
                    prompt.alpha = Mathf.Clamp01(_t / 0.6f) * (0.55f + 0.45f * Mathf.Sin(Time.time * 3f));
                    if (_t > 0.4f && AnyPress()) StartCoroutine(OpenMenu());
                    break;
                case State.Menu:
                    UpdateMenu();
                    break;
            }
        }

        private IEnumerator OpenMenu()
        {
            _state = State.Menu;
            prompt.alpha = 0f;
            _sel = 0;
            Refresh();
            yield return Fade(menu, 1f, 0.5f);
        }

        private void UpdateMenu()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) Activate(_sel);
            }
            if (cursorImage != null) cursorImage.color = new Color(Mint.r, Mint.g, Mint.b, 0.65f + 0.35f * Mathf.Sin(Time.time * 4f));
        }

        private void Move(int d)
        {
            // 비활성 항목도 선택은 되지만 누르면 안내만 나온다
            _sel = (_sel + d + menuTexts.Length) % menuTexts.Length;
            Paradox.Core.Sfx.Play("ui_hover", 0.7f);
            Refresh();
        }

        public void Select(int i)
        {
            if (_state != State.Menu || i == _sel) return;
            _sel = i; Refresh();
            Paradox.Core.Sfx.Play("ui_hover", 0.7f);
        }

        private void Refresh()
        {
            for (int i = 0; i < menuTexts.Length; i++)
            {
                bool on = menuEnabled[i];
                menuTexts[i].color = i == _sel ? (on ? Mint : Dim) : (on ? new Color(0.82f, 0.88f, 0.90f) : Off);
            }
            if (cursor != null)
            {
                var r = menuTexts[_sel].rectTransform;
                cursor.anchoredPosition = new Vector2(cursor.anchoredPosition.x, r.anchoredPosition.y);
            }
        }

        public void Activate(int i)
        {
            if (_state != State.Menu) return;
            if (i != _sel) { _sel = i; Refresh(); }
            Paradox.Core.Sfx.Play(menuEnabled[i] ? "ui_select" : "ui_back");
            switch (i)
            {
                case 0: StartCoroutine(Leave()); break;
                case 1: Toast(0); break;
                case 2: { var sp = FindFirstObjectByType<SettingsPanel>(); if (sp != null) sp.Open(); else Toast(1); break; }
                case 3:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;
            }
        }

        private Coroutine _toast;
        private void Toast(int which)
        {
            toastImage.sprite = toastSprites[which];
            toastImage.SetNativeSize();
            toastImage.rectTransform.sizeDelta *= 2f;
            if (_toast != null) StopCoroutine(_toast);
            _toast = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            toast.alpha = 1f;
            yield return new WaitForSeconds(1.4f);
            yield return Fade(toast, 0f, 0.4f);
        }

        private IEnumerator Leave()
        {
            _state = State.Leaving;
            yield return Fade(black, 1f, 0.6f);
            SceneManager.LoadScene(nextScene);
        }
    }
}
