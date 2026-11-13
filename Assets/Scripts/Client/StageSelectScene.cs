using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// StageSelect.unity 전용: "국제 청소년 교류 캠프" 스테이지 선택. 카드·프레임·안내문은 모두 씬에 직접 배치돼 있고,
    /// 이 컴포넌트는 선택 이동(A/D·화살표·마우스 호버), 시작(Enter/Space/E·클릭), 잠금 안내, 장면 전환만 맡는다.
    /// </summary>
    public class StageSelectScene : MonoBehaviour
    {
        [Header("씬에 배치된 오브젝트")]
        public CanvasGroup black;
        public RectTransform[] cards;       // 스테이지 카드 4장 (Image 포함)
        public Image[] cardImages;
        public RectTransform frame;         // 선택 테두리
        public Image frameImage;
        public CanvasGroup toast;
        public bool[] unlocked = { true, false, false, false };
        public string[] sceneNames = { "Story1", "", "", "" };
        public string backScene = "Intro";

        public float selectedScale = 1.06f;
        public float liftPixels = 14f;

        private int _sel;
        private bool _busy;
        private Vector2[] _basePos;
        private Coroutine _toast;
        private static readonly Color Mint = new Color(0.376f, 0.984f, 0.796f);

        private void Start()
        {
            _basePos = new Vector2[cards.Length];
            for (int i = 0; i < cards.Length; i++) _basePos[i] = cards[i].anchoredPosition;
            toast.alpha = 0f;
            black.alpha = 1f;
            StartCoroutine(Fade(0f, 0.6f));
            Apply(true);
        }

        private IEnumerator Fade(float to, float seconds)
        {
            float from = black.alpha;
            for (float t = 0f; t < seconds; t += Time.deltaTime) { black.alpha = Mathf.Lerp(from, to, t / seconds); yield return null; }
            black.alpha = to;
        }

        private void Update()
        {
            if (_busy) return;
            var kb = Keyboard.current;
            if (kb != null && !SettingsPanel.IsOpen)
            {
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Select((_sel + 1) % cards.Length);
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Select((_sel + cards.Length - 1) % cards.Length);
                if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) Activate(_sel);
                if (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame) {   // 웹에서는 Esc 가 전체화면을 풀어서 X 도 쓴다
                Paradox.Core.Sfx.Play("ui_back"); StartCoroutine(Leave(backScene)); }
            }
            Apply(false);
        }

        public void Select(int i)
        {
            if (_busy) return;
            if (i != _sel) Paradox.Core.Sfx.Play("ui_hover", 0.7f);
            _sel = i;
        }

        public void Activate(int i)
        {
            if (_busy) return;
            _sel = i;
            if (unlocked[i] && !string.IsNullOrEmpty(sceneNames[i])) { Paradox.Core.Sfx.Play("ui_select"); StartCoroutine(Leave(sceneNames[i])); }
            else
            {
                Paradox.Core.Sfx.Play("ui_back");
                if (_toast != null) StopCoroutine(_toast);
                _toast = StartCoroutine(ToastRoutine());
            }
        }

        // 선택된 카드는 살짝 커지며 떠오르고, 나머지는 어두워진다. 테두리는 숨 쉬듯 깜빡인다.
        private void Apply(bool snap)
        {
            float k = snap ? 1f : Mathf.Clamp01(Time.deltaTime * 14f);
            for (int i = 0; i < cards.Length; i++)
            {
                bool on = i == _sel;
                float s = Mathf.Lerp(cards[i].localScale.x, on ? selectedScale : 1f, k);
                cards[i].localScale = new Vector3(s, s, 1f);
                var target = _basePos[i] + new Vector2(0f, on ? liftPixels : 0f);
                cards[i].anchoredPosition = Vector2.Lerp(cards[i].anchoredPosition, target, k);
                float b = on ? 1f : (unlocked[i] ? 0.62f : 0.5f);
                cardImages[i].color = new Color(b, b, b, 1f);
            }
            frame.anchoredPosition = cards[_sel].anchoredPosition;
            frame.localScale = cards[_sel].localScale;
            var c = unlocked[_sel] ? Mint : new Color(0.78f, 0.8f, 0.85f);
            c.a = 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
            frameImage.color = c;
        }

        private IEnumerator ToastRoutine()
        {
            toast.alpha = 1f;
            yield return new WaitForSeconds(1.3f);
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) { toast.alpha = 1f - t / 0.4f; yield return null; }
            toast.alpha = 0f;
        }

        private IEnumerator Leave(string scene)
        {
            _busy = true;
            if (scene == backScene) IntroSequence.SkipToMenu = true;   // 로고 연출 없이 타이틀 메뉴로 복귀
            yield return Fade(1f, 0.5f);
            SceneManager.LoadScene(scene);
        }
    }
}
