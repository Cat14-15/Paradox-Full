using Paradox.Language;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Paradox.Stage
{
    /// <summary>
    /// 재난(침수) 타이머. StageConfig.TimeLimit > 0 인 스테이지에서만 작동한다.
    /// 시간이 흐르면 화면 아래에서 물이 차오르고, 0이 되면 실패 → R로 재시작.
    /// 스테이지를 클리어하면 멈춘다. (튜토리얼 Stage 1은 TimeLimit=0이라 비활성)
    /// </summary>
    public class StageTimer : MonoBehaviour
    {
        private float _limit;
        private float _elapsed;
        private bool _active;
        private bool _failed;

        private Text _timerText;
        private RectTransform _water;
        private RectTransform _surface;
        private Image _waterImg;
        private Color _floodColor = new Color(0.22f, 0.42f, 0.85f);
        private StageManager _mgr;

        private void Start()
        {
            var cfg = StageConfig.Current;
            _limit = cfg != null ? cfg.TimeLimit : 0f;
            _mgr = Object.FindFirstObjectByType<StageManager>();

            if (_limit > 0f)
            {
                _active = true;
                BuildUI();
            }
        }

        private void BuildUI()
        {
            var canvas = PuzzleOverlay.GetCanvas();
            var font = UIFontHelper.Load();

            // 차오르는 물 (아래에서 위로)
            var waterGo = new GameObject("FloodWater", typeof(RectTransform));
            _water = (RectTransform)waterGo.transform;
            _water.SetParent(canvas, false);
            _water.anchorMin = new Vector2(0f, 0f);
            _water.anchorMax = new Vector2(1f, 0f);
            _water.pivot = new Vector2(0.5f, 0f);
            _water.offsetMin = Vector2.zero;
            _water.offsetMax = Vector2.zero;
            _water.sizeDelta = new Vector2(0f, 0f);
            var cfg = StageConfig.Current;
            if (cfg != null) _floodColor = cfg.FloodColor;

            _waterImg = waterGo.AddComponent<Image>();
            _waterImg.color = new Color(_floodColor.r, _floodColor.g, _floodColor.b, 0.4f);
            _waterImg.raycastTarget = false;
            _water.SetAsFirstSibling(); // 다른 UI 뒤로

            // 수면 하이라이트 라인
            var surfGo = new GameObject("FloodSurface", typeof(RectTransform));
            _surface = (RectTransform)surfGo.transform;
            _surface.SetParent(canvas, false);
            _surface.anchorMin = new Vector2(0f, 0f);
            _surface.anchorMax = new Vector2(1f, 0f);
            _surface.pivot = new Vector2(0.5f, 0f);
            _surface.sizeDelta = new Vector2(0f, 5f);
            var simg = surfGo.AddComponent<Image>();
            simg.color = new Color(Mathf.Min(1f, _floodColor.r + 0.35f), Mathf.Min(1f, _floodColor.g + 0.35f), Mathf.Min(1f, _floodColor.b + 0.35f), 0.85f);
            simg.raycastTarget = false;
            _surface.SetSiblingIndex(1);

            // 카운트다운
            var tGo = new GameObject("TimerText", typeof(RectTransform));
            var rt = (RectTransform)tGo.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -78f);
            rt.sizeDelta = new Vector2(300f, 34f);
            _timerText = tGo.AddComponent<Text>();
            _timerText.font = font;
            _timerText.fontSize = 24;
            _timerText.alignment = TextAnchor.MiddleCenter;
            _timerText.color = new Color(0.9f, 0.95f, 1f);
        }

        private void Update()
        {
            if (!_active) return;

            if (_failed)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.rKey.wasPressedThisFrame) Restart();
                return;
            }

            if (_mgr != null && _mgr.Cleared)
            {
                _active = false;
                if (_water != null) _water.gameObject.SetActive(false);
                if (_surface != null) _surface.gameObject.SetActive(false);
                if (_timerText != null) _timerText.gameObject.SetActive(false);
                return;
            }

            _elapsed += Time.deltaTime;
            float remain = Mathf.Max(0f, _limit - _elapsed);
            float f = Mathf.Clamp01(_elapsed / _limit);

            if (_timerText != null)
            {
                _timerText.text = $"⏱ {remain:0}s";
                _timerText.color = remain < 10f ? new Color(1f, 0.5f, 0.5f) : new Color(0.9f, 0.95f, 1f);
            }
            if (_water != null)
            {
                _water.sizeDelta = new Vector2(0f, f * 900f); // 기준 해상도 높이만큼 차오름
                if (_waterImg != null)
                    _waterImg.color = new Color(_floodColor.r, _floodColor.g, _floodColor.b, 0.35f + 0.3f * f);
            }
            if (_surface != null)
                _surface.anchoredPosition = new Vector2(0f, f * 900f); // 수면은 물 꼭대기에

            if (remain <= 0f)
            {
                _failed = true;
                InteractionState.PushLock();
                ShowFailBanner();
            }
        }

        private void ShowFailBanner()
        {
            var canvas = PuzzleOverlay.GetCanvas();
            var go = new GameObject("FailBanner", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(640f, 120f);
            go.AddComponent<Image>().color = new Color(0.20f, 0.10f, 0.12f, 0.95f);

            var txtGo = new GameObject("Text", typeof(RectTransform));
            var trt = (RectTransform)txtGo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var t = txtGo.AddComponent<Text>();
            t.font = UIFontHelper.Load();
            t.text = "물에 잠겼다…\nR 키로 다시 시도";
            t.fontSize = 28;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 0.85f, 0.85f);
        }

        private void Restart()
        {
            if (_failed) InteractionState.PopLock();
            try
            {
                var s = SceneManager.GetActiveScene();
                SceneManager.LoadScene(s.name);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[StageTimer] 재시작 실패(씬이 Build Settings에 없을 수 있음): " + e.Message);
            }
        }
    }
}
