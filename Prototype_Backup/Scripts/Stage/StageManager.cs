using System.Collections.Generic;
using Paradox.Comms;
using Paradox.Language;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Stage
{
    /// <summary>
    /// 순서형 기호 코드 퍼즐. 통역만 아는 '기호 순서'대로 조작자가 레버를 눌러야 한다.
    /// 순서가 틀리면 처음부터. 순서를 완성하고 모든 도착존이 점유되면 클리어.
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        [SerializeField] private string stageTitle = "Stage 1 · 필리핀 · 태풍";
        [Tooltip("정답 기호 순서 (randomize가 꺼져 있을 때만 사용)")]
        [SerializeField] private string[] requiredSequence = { "●", "◆", "▲" };
        [Tooltip("켜면 매 실행마다 레버 기호들로 순서를 무작위 생성")]
        [SerializeField] private bool randomizeSequence = true;
        [SerializeField] private int sequenceLength = 4;

        private ArrivalZone[] _zones;
        private readonly List<string> _input = new List<string>();
        private bool _sequenceDone;
        private bool _cleared;
        private Text _banner;

        public bool Cleared => _cleared;
        public string ProgressLine { get; private set; } = "";
        public string ObjectiveText => "목표: 표지판 기호 순서대로 레버 누르기 → 모두 도착존";
        public string SequenceText => string.Join("  ", requiredSequence);

        private void Start()
        {
            var cfg = StageConfig.Current;
            if (cfg != null) stageTitle = cfg.StageTitle;
            _zones = Object.FindObjectsByType<ArrivalZone>(FindObjectsSortMode.None);

            if (randomizeSequence)
            {
                var syms = new List<string>();
                foreach (var l in Object.FindObjectsByType<SymbolLever>(FindObjectsSortMode.None))
                    if (l != null && !syms.Contains(l.Symbol)) syms.Add(l.Symbol);

                if (syms.Count > 0)
                {
                    int len = Mathf.Max(1, sequenceLength);
                    var seq = new string[len];
                    string prev = null;
                    for (int i = 0; i < len; i++)
                    {
                        string s = syms[Random.Range(0, syms.Count)];
                        // 바로 같은 기호 연속은 피해 변별력 up (기호가 2개 이상일 때)
                        if (syms.Count > 1) { int guard = 0; while (s == prev && guard++ < 8) s = syms[Random.Range(0, syms.Count)]; }
                        seq[i] = s; prev = s;
                    }
                    requiredSequence = seq;
                }
            }
        }

        /// <summary>조작자가 레버를 눌렀을 때 SymbolLever가 호출.</summary>
        public void OnLeverPulled(string symbol)
        {
            if (_cleared || _sequenceDone) return;

            _input.Add(symbol);
            int i = _input.Count - 1;
            if (i >= requiredSequence.Length || _input[i] != requiredSequence[i])
            {
                MessageFeed.Send("!", "순서가 틀렸어요 — 처음부터 다시", new Color(0.95f, 0.5f, 0.5f));
                FeedbackFX.Flash(new Color(0.9f, 0.2f, 0.2f), 0.38f);
                _input.Clear();
                return;
            }

            if (_input.Count == requiredSequence.Length)
            {
                _sequenceDone = true;
                MessageFeed.Send("시스템", "기호 순서 완성! 이제 모두 도착존으로", new Color(0.6f, 0.95f, 0.7f));
                FeedbackFX.Flash(new Color(0.3f, 0.9f, 0.45f), 0.5f);
            }
            else
            {
                MessageFeed.Send("시스템", $"좋아요 ({_input.Count}/{requiredSequence.Length})", new Color(0.7f, 0.9f, 1f));
                FeedbackFX.Flash(new Color(0.35f, 0.85f, 0.5f), 0.16f);
            }
        }

        private void Update()
        {
            if (_cleared) return;

            int zonesOcc = 0;
            foreach (var z in _zones) if (z != null && z.IsOccupied) zonesOcc++;
            bool zonesDone = _zones.Length > 0 && zonesOcc == _zones.Length;

            string seqState = _sequenceDone
                ? "<color=#7FE39B>순서 완성 ✓</color>"
                : $"<color=#E0D0A0>순서 {_input.Count}/{requiredSequence.Length}</color>";
            ProgressLine = $"{seqState}   ·   도착존 {zonesOcc}/{_zones.Length}";

            if (_sequenceDone && zonesDone)
            {
                _cleared = true;
                ShowClearBanner();
                MessageFeed.Send("시스템", "스테이지 클리어! 함께 해냈습니다 🎉", new Color(0.6f, 0.95f, 0.7f));
            }
        }

        private void ShowClearBanner()
        {
            var canvas = PuzzleOverlay.GetCanvas();
            var go = new GameObject("ClearBanner", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(700f, 120f);
            go.AddComponent<Image>().color = new Color(0.08f, 0.16f, 0.11f, 0.95f);
            var txtGo = new GameObject("Text", typeof(RectTransform));
            var trt = (RectTransform)txtGo.transform; trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            _banner = txtGo.AddComponent<Text>();
            _banner.font = UIFontHelper.Load();
            _banner.text = "STAGE CLEAR\n말이 안 통해도, 함께 해냈다";
            _banner.fontSize = 30; _banner.alignment = TextAnchor.MiddleCenter;
            _banner.color = new Color(0.8f, 0.98f, 0.85f);
        }
    }
}
