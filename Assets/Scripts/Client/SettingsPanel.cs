using Paradox.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 소리 설정 창(씬에 놓인 UI). 효과음/배경음악 슬라이더. 50% 가 기본 음량.
    /// 열기: 설정 버튼 · F1 / 닫기: Esc · Enter · 닫기 버튼 / 키보드: ↑↓ 줄 선택, ←→ 5% 조절.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        public static bool IsOpen => _open || Time.frameCount == _closedFrame;
        private static bool _open;
        private static int _closedFrame = -1;
        private int _openedFrame = -1;

        public CanvasGroup group, buttonGroup;
        private IntroSequence _intro;
        public PixelSlider sfxSlider, bgmSlider;
        public PixelText sfxLabel, bgmLabel, sfxPct, bgmPct;
        public Image sfxHandle, bgmHandle;
        private int _sel;
        private float _lastTick;

        private static readonly Color Mint = new Color(0.376f, 0.984f, 0.796f);
        private static readonly Color Ink = new Color(0.9f, 0.95f, 0.96f);

        private void Awake() { _open = false; }
        private void OnDestroy() { _open = false; }

        private void Start()
        {
            sfxSlider.Apply(AudioPrefs.Sfx, false); bgmSlider.Apply(AudioPrefs.Bgm, false);
            sfxSlider.changed = v => { _sel = 0; AudioPrefs.Sfx = v; Tick(); Refresh(); };
            bgmSlider.changed = v => { _sel = 1; AudioPrefs.Bgm = v; Refresh(); };
            _intro = Object.FindFirstObjectByType<IntroSequence>();
            SetShown(false);
            Refresh();
        }

        public void Open() { if (_open) return; _open = true; _openedFrame = Time.frameCount; _sel = 0; SetShown(true); Sfx.Play("ui_select"); Refresh(); }
        public void Close() { if (!_open) return; _open = false; _closedFrame = Time.frameCount; SetShown(false); Sfx.Play("ui_back"); }

        private void SetShown(bool on)
        {
            group.alpha = on ? 1f : 0f; group.blocksRaycasts = on; group.interactable = on;
        }

        private void Tick() { if (Time.unscaledTime - _lastTick > 0.08f) { _lastTick = Time.unscaledTime; Sfx.Play("ui_hover", 0.8f); } }

        private void Update()
        {
            if (buttonGroup != null)
            {
                float want = _intro == null && !_open ? 1f : 0f;   // 인트로에서는 메뉴의 "설정" 항목으로 연다
                buttonGroup.alpha = Mathf.MoveTowards(buttonGroup.alpha, want, Time.unscaledDeltaTime * 4f);
                buttonGroup.blocksRaycasts = buttonGroup.alpha > 0.5f;
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!_open) { if (kb.f1Key.wasPressedThisFrame) Open(); return; }
            if (Time.frameCount == _openedFrame) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame) { Close(); return; }
            if (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame) { _sel = 1 - _sel; Sfx.Play("ui_hover", 0.7f); Refresh(); }
            int d = (kb.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (kb.leftArrowKey.wasPressedThisFrame ? 1 : 0);
            if (kb.dKey.wasPressedThisFrame) d++; if (kb.aKey.wasPressedThisFrame) d--;
            if (d != 0)
            {
                var s = _sel == 0 ? sfxSlider : bgmSlider;
                s.Apply(Mathf.Round(s.value * 20f) / 20f + d * 0.05f, true);
            }
        }

        private void Refresh()
        {
            sfxPct.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
            bgmPct.text = Mathf.RoundToInt(bgmSlider.value * 100f) + "%";
            sfxLabel.color = _sel == 0 ? Mint : Ink; bgmLabel.color = _sel == 1 ? Mint : Ink;
            if (sfxHandle != null) sfxHandle.color = _sel == 0 ? Mint : Ink;
            if (bgmHandle != null) bgmHandle.color = _sel == 1 ? Mint : Ink;
        }
    }
}
