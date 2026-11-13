using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 설정(로컬). 음량 슬라이더(값 저장 + AudioListener 반영), 전체화면 토글, 내 기본 이름, 프롤로그 다시 보기, 뒤로.
    /// </summary>
    public class SettingsScreen : ScreenBase
    {
        private Slider _volume;
        private Text _volumeText;
        private Button _fullscreen;
        private InputField _name;
        private bool _nameLocked;
        private float _lastClickAt = -1f;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            UI.Bg(Root, Skin.Bg);
            Skin.Vignette(Root, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1200f, 800f), 0.12f);

            UI.Label(Root, "설정", 36, Skin.Amber, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(600f, 54f), TextAnchor.MiddleCenter);

            var panel = Skin.Bordered(Root, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(640f, 500f), Skin.Border, Skin.Panel, 2f);
            UI.VStack(panel, 32, 12, false, TextAnchor.UpperLeft);

            // 음량
            UI.Text(panel, "음량", 16, Skin.Ink, TextAnchor.MiddleLeft, false, 24);
            var vrow = UI.Rect(panel, "VolumeRow");
            UI.HStack(vrow, 0, 14, false, TextAnchor.MiddleLeft);
            UI.LE(vrow, -1, 36);
            _volume = MakeSlider(vrow);
            UI.LE(_volume, 380, 30, 1f);
            _volumeText = UI.Text(vrow, "100%", 16, Skin.Amber, TextAnchor.MiddleRight, false);
            UI.LE(_volumeText, 70, 30, 0f);
            _volume.value = Progress.volume;
            _volume.onValueChanged.AddListener(v =>
            {
                Progress.volume = v; Progress.ApplyAudio(); RefreshVolumeText();
                // 슬라이더를 움직일 때 "click"(QA D-A1) — 드래그 중 매 프레임 울리지 않게 0.08초 간격
                if (Time.unscaledTime - _lastClickAt >= 0.08f) { _lastClickAt = Time.unscaledTime; Sfx.Play("click"); }
            });

            // 전체화면
            UI.Text(panel, "화면", 16, Skin.Ink, TextAnchor.MiddleLeft, false, 24);
            _fullscreen = Skin.Outline(panel, "", () =>
            {
                bool on = !Screen.fullScreen;
                Screen.fullScreen = on;
                Progress.fullscreen = on;
                RefreshFullscreen(on);
            }, 17, 46, 200);
            RefreshFullscreen(Screen.fullScreen);

            // 내 기본 이름
            UI.Text(panel, "내 기본 이름 (10자까지) — 이름 설정 화면에 미리 채워져요", 16, Skin.Ink, TextAnchor.MiddleLeft, false, 24);
            _name = UI.Input(panel, Roles.DefaultName(0), Progress.myName, 18, 10);
            var nimg = _name.GetComponent<Image>();
            if (nimg != null) nimg.color = Skin.C("0A0F18");
            UI.LE(_name, -1, 46);
            _name.onEndEdit.AddListener(s => { Progress.myName = (s ?? "").Trim(); });

            // 프롤로그 다시 보기 / 기록 지우기
            var spacer = UI.Rect(panel, "Spacer"); UI.LE(spacer, -1, 6);
            var brow = UI.Rect(panel, "Buttons");
            UI.HStack(brow, 0, 12, true, TextAnchor.MiddleLeft);
            UI.LE(brow, -1, 48);
            Skin.Outline(brow, "프롤로그 다시 보기", () => LocalNav.Go(LocalScreen.Prologue), 16, 46, 200, Skin.Amber, Skin.Amber);
            Skin.Outline(brow, "진행 기록 지우기", () =>
            {
                Progress.ResetAll();
                if (ScreenManager.Instance != null) ScreenManager.Instance.ShowToast("프롤로그·클리어 기록을 지웠어요", GameLogic.ToneInfo);
            }, 16, 46, 200);

            var back = Skin.Outline(Root, "◀  뒤로", () => LocalNav.Go(LocalScreen.Title), 15, 42, 120);
            var brt = (RectTransform)back.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 0f); brt.pivot = new Vector2(0f, 0f);
            brt.anchoredPosition = new Vector2(36f, 36f); brt.sizeDelta = new Vector2(120f, 42f);

            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 60f), true);
            RefreshVolumeText();
        }

        /// <summary>UGUI Slider 를 코드로 조립한다(배경 띠 + 앰버 채움 + 손잡이).</summary>
        private static Slider MakeSlider(Transform parent)
        {
            var rt = UI.Rect(parent, "Slider");
            var slider = rt.gameObject.AddComponent<Slider>();
            var bg = UI.Stretch(rt, "Background", 0, 10, 0, 10);
            UI.Bg(bg, Skin.Border, false);
            var fillArea = UI.Stretch(rt, "FillArea", 0, 10, 0, 10);
            var fill = UI.Stretch(fillArea, "Fill");
            var fillImg = UI.Bg(fill, Skin.Amber, false);
            var handleArea = UI.Stretch(rt, "HandleArea", 10, 0, 10, 0);
            var handle = UI.Rect(handleArea, "Handle");
            handle.sizeDelta = new Vector2(20f, 0f);
            var himg = handle.gameObject.AddComponent<Image>();
            himg.color = Skin.Ink;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = himg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.wholeNumbers = false;
            return slider;
        }

        private void RefreshVolumeText()
        {
            if (_volumeText != null && _volume != null) _volumeText.text = $"{Mathf.RoundToInt(_volume.value * 100f)}%";
        }

        private void RefreshFullscreen(bool on)
        {
            if (_fullscreen == null) return;
            var t = UI.ButtonLabel(_fullscreen);
            if (t != null) t.text = on ? "전체화면: <color=#F2C572>켜짐</color>  (누르면 창 모드)" : "전체화면: <color=#8A96A8>꺼짐</color>  (누르면 전체화면)";
        }

        public override void Dispose()
        {
            if (_nameLocked) { InteractionState.PopLock(); _nameLocked = false; }
            base.Dispose();
        }

        public override void Tick()
        {
            bool focused = _name != null && _name.isFocused;
            if (focused && !_nameLocked) { InteractionState.PushLock(); _nameLocked = true; }
            else if (!focused && _nameLocked) { InteractionState.PopLock(); _nameLocked = false; }
        }
    }
}
