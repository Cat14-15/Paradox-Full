using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 퍼즐 2 화면(씬에 놓인 UI): 어두운 전시실 건너기. 역할마다 다른 면.
    ///  조작자 — 손전등 반경만 보이는 통로, 직접 걷는다 / 통역 — 필리핀어 안내문 / 내비게이터 — 전시실 안내도 / 관측자 — 스파크 신호등.
    /// 규칙 판정은 GalleryPuzzle(순수 규칙).
    /// </summary>
    public sealed class GalleryPanel : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static bool Cleared { get; private set; }
        public static event System.Action Solved;

        [UnityEngine.RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsOpen = false; Cleared = false; Solved = null; }

        [Header("공통")]
        public CanvasGroup group;
        public RectTransform content;
        public PixelText title, roleText, hint, status;
        public Image flash;
        public RectTransform[] cellRects = new RectTransform[9];   // [구간*3 + 통로], 구간 0 이 맨 아래
        public Image[] cellImages = new Image[9];
        public PixelText[] cellMarks = new PixelText[9];

        [Header("조작자 면")]
        public GameObject viewOperator;
        public RectTransform marker;
        public Image markerImage;

        [Header("통역 면")]
        public GameObject viewInterpreter;
        public PixelText[] signTl = new PixelText[3];
        public PixelText[] signKo = new PixelText[3];

        [Header("관측자 면")]
        public GameObject viewObserver;
        public Image[] lamps = new Image[3];
        public Image[] lampRows = new Image[3];
        public PixelText[] lampLabels = new PixelText[3];

        [Header("내비게이터 면")]
        public GameObject viewNavigator;

        private static readonly Color Dark = new Color(0.04f, 0.05f, 0.07f, 1f);
        private GalleryFloor _floor;
        private GalleryPuzzle _puzzle;
        private int _stage, _lane = 1;
        private int _openedFrame;
        private float _shake, _flashA, _stunUntil, _closeAt = -1f;
        private Color _flashCol = Color.white;
        private bool _solved;
        private int _sayVer = -1;

        private void Awake() { if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; } }

        public void Open(GalleryFloor floor, GalleryPuzzle puzzle)
        {
            _floor = floor; _puzzle = puzzle;
            _stage = 0; _lane = 1; _solved = false; _closeAt = -1f; _flashA = 0f; _stunUntil = 0f;
            group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true;
            IsOpen = true; Story1BotOperator.Opened(Story1Net.PGallery); _openedFrame = Time.frameCount; _sayVer = -1; _shocks = 0; _breakReadyAt = 0f; _seenBreak = Story1Net.BreakUntil; Story1Net.ResetSent(Story1Net.PGallery);
            status.text = "";
            Refresh();
            Sfx.Play("ui_select", 0.8f);
        }

        /// <summary>조작자가 AI 일 때: 동료의 신호를 받아 AI 조작자가 푼다.</summary>
        private void BotSolve()
        {
            if (_solved) return;
            _solved = true;
            status.text = "AI 조작자가 신호대로 풀었어요!";
            status.color = new Color(0.45f, 0.95f, 0.6f);
            _flashCol = new Color(0.5f, 1f, 0.6f); _flashA = 0.5f;
            Sfx.Play("power_on", 0.9f);
            _closeAt = Time.unscaledTime + 1.5f;
            Story1Net.Send(Story1Net.Clear, Story1Net.PGallery);
        }

        private float _botNote;

        private void BotOperatorTick()
        {
            if (_solved) return;
            string waiting; int st = Story1BotOperator.Tick(Story1Net.PGallery, out waiting);
            if (st == 1 && Time.unscaledTime > _botNote) { _botNote = Time.unscaledTime + 1f; status.text = waiting.Length == 0 ? "AI 조작자가 신호대로 푸는 중이에요" : "AI 조작자가 신호를 기다려요: " + waiting + " 의 신호 카드가 필요해요 (C)"; status.color = new Color(0.95f, 0.85f, 0.55f); }
            else if (st == 2) BotSolve();
        }

        public void Close()
        {
            IsOpen = false;
            SoloBots.Hide();
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
            Sfx.Play("ui_back", 0.8f);
        }

        private void Refresh()
        {
            int role = Story1Role.Current;
            bool op = role == (int)GameRole.Operator, tr = role == (int)GameRole.Interpreter, ob = role == (int)GameRole.Observer, nv = role == (int)GameRole.Navigator;
            viewOperator.SetActive(op); viewInterpreter.SetActive(tr); viewObserver.SetActive(ob); viewNavigator.SetActive(nv);

            roleText.text = "역할 · " + Roles.Name(role) + (Story1Role.Solo ? "    [Tab] 역할 바꾸기 (혼자 테스트)" : "");
            roleText.color = Roles.Color(role);

            bool grid = op || nv;
            for (int i = 0; i < 9; i++) cellRects[i].gameObject.SetActive(grid);
            marker.gameObject.SetActive(op && !_solved);

            if (op)
            {
                title.text = "어두운 전시실" + Story1Net.RoundLabel(Story1Net.PGallery);
                hint.text = "← →  통로 고르기     ↑ / Space  건너기     손전등 안만 보여요";
                for (int s = 0; s < 3; s++)
                    for (int l = 0; l < 3; l++)
                    {
                        int i = s * 3 + l;
                        bool passed = s < _stage || _solved, here = s == _stage && !_solved;
                        cellImages[i].color = passed ? new Color(0.14f, 0.3f, 0.22f) : here ? (l == _lane ? new Color(0.36f, 0.34f, 0.26f) : new Color(0.2f, 0.19f, 0.16f)) : Dark;
                        cellMarks[i].text = passed && _puzzle.answer[s] == l ? "OK" : "";
                        cellMarks[i].color = new Color(0.5f, 1f, 0.65f);
                    }
                if (!_solved) marker.anchoredPosition = cellRects[_stage * 3 + _lane].anchoredPosition;
            }
            if (nv)
            {
                title.text = "전시실 안내도";
                hint.text = "이어진 통로는 밝게, 막다른 곳은 벽으로 보입니다. 팀원에게 알려 주세요.";
                for (int s = 0; s < 3; s++)
                    for (int l = 0; l < 3; l++)
                    {
                        int i = s * 3 + l;
                        bool c = _puzzle.Connected(s, l);
                        cellImages[i].color = c ? new Color(0.17f, 0.38f, 0.4f) : new Color(0.12f, 0.12f, 0.14f);
                        cellMarks[i].text = c ? "" : "X";
                        cellMarks[i].color = new Color(0.5f, 0.5f, 0.55f);
                    }
            }
            if (tr)
            {
                title.text = "벽의 안내문 (필리핀어)";
                for (int s = 0; s < 3; s++)
                {
                    signTl[s].text = _puzzle.SignTagalog(s);
                    signKo[s].text = "→ " + _puzzle.SignKorean(s);
                }
                hint.text = "이 글은 당신만 읽을 수 있어요. 팀원에게 신호로 알려 주세요.";
            }
            if (ob)
            {
                title.text = "끊어진 전선 (스파크)";
                hint.text = "불꽃이 꺼진 동안만 건너요     Space  전원 끊기 (2초, 6초마다)";
                for (int s = 0; s < 3; s++) lampLabels[s].text = "구간 " + (s + 1);
            }
            _lastSpark = Spark(_stage);
            if (hint != null) { hint.maxWidth = 1420f; hint.text += "\nAI 말뜻  2 아니 ← 2구간 왼쪽 막힘 · 2 ← ↑ 이어진 길 · 응 건너도 돼"; }
            SoloBots.Sync(BotLines.Gallery(_puzzle, _stage, _lastSpark));
            if (op && !_solved) Story1Net.Tell(Story1Net.PGallery, "조작자: " + (_stage + 1) + "구간을 건너는 중");
        }

        private bool _lastSpark;
        private int _shocks;
        private float _breakReadyAt, _seenBreak;

        // 관측자가 전원을 잠깐 끊으면(Space) 모두의 스파크가 2초쯤 꺼진다
        private bool Spark(int stage) => Story1Net.Now >= Story1Net.BreakUntil && _puzzle.SparkOn(stage, Story1Net.Now);

        private void Update()
        {
            if (content != null)
            {
                _shake = Mathf.MoveTowards(_shake, 0f, Time.unscaledDeltaTime * 30f);
                content.anchoredPosition = _shake > 0.01f ? new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * _shake + new Vector2(0f, 60f) : new Vector2(0f, 60f);
            }
            if (flash != null)
            {
                _flashA = Mathf.MoveTowards(_flashA, 0f, Time.unscaledDeltaTime * 2.2f);
                var c = _flashCol; c.a = _flashA; flash.color = c;
            }
            if (!IsOpen) return;
            Story1Net.ShowSay(Story1Net.PGallery, status, ref _sayVer);
            if (Story1BotOperator.Active) BotOperatorTick();

            // 관측자의 스파크 신호등 (현재 구간 강조)
            if (Story1Role.Current == (int)GameRole.Observer)
            {
                for (int s = 0; s < 3; s++)
                {
                    bool on = Spark(s);
                    float flick = on ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 40f) : 1f;
                    lamps[s].color = on ? new Color(1f, 0.3f, 0.2f) * flick : new Color(0.3f, 0.9f, 0.5f);
                    var rc = lampRows[s].color; rc.a = s == _stage ? 1f : 0.35f; lampRows[s].color = rc;
                }
            }

            // 혼자 테스트: 관측자 AI 의 "지금 건너요" 말이 불꽃 상태에 맞게 바뀐다
            if (!_solved && Story1Role.IsBot((int)GameRole.Observer) && Spark(_stage) != _lastSpark) Refresh();

            if (_closeAt > 0f && Time.unscaledTime >= _closeAt) { Close(); _floor.OnSolved(); return; }
            if (_solved || Time.frameCount == _openedFrame) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame) { Close(); return; }   // 웹에서는 Esc 가 전체화면을 풀어 버려서 X·Q 도 쓴다
            if (kb.tabKey.wasPressedThisFrame && Story1Role.Solo) { Story1Role.Cycle(); Sfx.Play("ui_hover", 0.8f); Refresh(); return; }
            if (Story1Net.BreakUntil != _seenBreak)
            {
                _seenBreak = Story1Net.BreakUntil;
                status.text = "전원이 끊겼다! 지금 건너자!"; status.color = new Color(0.6f, 0.95f, 0.7f);
                Sfx.Play("ui_select", 0.8f);
            }
            if (Story1Role.Current == (int)GameRole.Observer && Story1Net.Now >= _breakReadyAt && kb.spaceKey.wasPressedThisFrame)
            {
                _breakReadyAt = Story1Net.Now + 6f;
                Story1Net.Send(Story1Net.Break);
            }
            if (Story1Role.Current != (int)GameRole.Operator) return;
            if (Time.unscaledTime < _stunUntil) return;

            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) { _lane = Mathf.Max(0, _lane - 1); Sfx.Play("ui_hover", 0.7f); Refresh(); }
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) { _lane = Mathf.Min(2, _lane + 1); Sfx.Play("ui_hover", 0.7f); Refresh(); }
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Cross();
        }

        private void Cross()
        {
            if (_puzzle.Closed(_stage, _lane) || !_puzzle.Connected(_stage, _lane))
            {
                status.text = _puzzle.Closed(_stage, _lane) ? "쓰러진 진열장이 길을 막고 있다." : "막다른 벽이다. 길이 이어져 있지 않다.";
                status.color = new Color(0.95f, 0.8f, 0.45f);
                _stunUntil = Time.unscaledTime + 0.6f; _shake = 5f;
                Sfx.Play("ui_back", 0.8f);
                return;
            }
            if (Spark(_stage))
            {
                status.text = "감전! 앞 구간으로 밀려났다.";
                status.color = new Color(1f, 0.5f, 0.45f);
                _flashCol = new Color(1f, 0.9f, 0.4f); _flashA = 0.65f; _shake = 14f;
                _stunUntil = Time.unscaledTime + 1.2f;
                _stage = Mathf.Max(0, _stage - 1);
                Sfx.Play("fuse_pop", 1f);
                _shocks++;
                Refresh();
                if (_shocks >= System.Math.Max(1, Story1Rules.HintAfter(Story1Net.Difficulty) - 1)) { status.text = "힌트: " + (_stage + 1) + "구간의 길은 " + GalleryPuzzle.LaneKo[_puzzle.answer[_stage]] + "이다"; status.color = new Color(0.95f, 0.85f, 0.45f); }
                Story1Net.Tell(Story1Net.PGallery, "조작자: 감전! " + (_stage + 1) + "구간으로 밀려났다");
                return;
            }
            _stage++;
            Sfx.Play("ui_select", 0.8f);
            if (_stage >= 3)
            {
                _solved = true;
                status.text = "출구 쪽 통로를 확보했다!";
                status.color = new Color(0.45f, 0.95f, 0.6f);
                _flashCol = new Color(0.5f, 1f, 0.6f); _flashA = 0.5f;
                Sfx.Play("power_on", 0.8f);
                _closeAt = Time.unscaledTime + 1.5f;
                Story1Net.Send(Story1Net.Clear, Story1Net.PGallery);
            }
            else { status.text = "건넜다. 다음 구간!"; status.color = new Color(0.6f, 0.95f, 0.7f); }
            Refresh();
        }

        internal static void ResetState() { Cleared = false; IsOpen = false; }
        internal static void MarkCleared() { Cleared = true; Solved?.Invoke(); }
    }
}
