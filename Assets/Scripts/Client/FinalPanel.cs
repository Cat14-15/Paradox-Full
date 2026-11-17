using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 퍼즐 5 화면(씬에 놓인 UI): 메인 홀 4인 동시 입력. 원 4개(= 네 역할).
    ///  라운드 1~3 — 각자 화면에는 "내 원"이 켜지는 박만 보인다. 합쳐서 순서를 만들고 조작자가 입력한다.
    ///  마지막 — 카운트 뒤 "지금!" 에 네 명이 동시에 자기 원을 밟는다. (혼자 테스트: 키 1~4 = 네 역할의 원)
    /// 규칙 판정은 FinalPuzzle(순수 규칙).
    /// </summary>
    public sealed class FinalPanel : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public static bool Cleared { get; private set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsOpen = false; Cleared = false; }

        [Header("공통")]
        public CanvasGroup group;
        public RectTransform content;
        public PixelText title, roleText, roundText, hint, status;
        public Image flash;
        public RectTransform[] circleRects = new RectTransform[4];
        public Image[] circleRings = new Image[4];
        public Image[] circleLamps = new Image[4];
        public PixelText[] circleLabels = new PixelText[4];
        public Image[] ticks = new Image[5];
        public PixelText tickLabel;
        public Image[] seqDots = new Image[4];
        public PixelText seqLabel;

        private enum Phase { Rounds, Final, Done }

        private FinalFloor _owner;
        private FinalPuzzle _puzzle;
        private Phase _phase;
        private int _round;
        private readonly int[] _entered = new int[4];
        private int _enteredCount;
        private float _t0, _shake, _flashA, _closeAt = -1f, _goTime, _endAt;
        private readonly float[] _press = { -1f, -1f, -1f, -1f };
        private int _lastBeat = -1, _openedFrame;
        private Color _flashCol = Color.white;
        private static Sprite _circle;

        private static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int N = 64;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    px[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01((1f - d) * 12f));
                }
            t.SetPixels(px); t.Apply();
            return _circle = Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 64f);
        }

        private void Awake()
        {
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
            var s = Circle();
            for (int i = 0; i < 4; i++)
            {
                var inner = circleRects[i] != null ? circleRects[i].Find("Inner") : null;
                if (inner != null) inner.GetComponent<Image>().sprite = s;
            }
            for (int i = 0; i < 4; i++) { if (circleRings[i] != null) circleRings[i].sprite = s; if (circleLamps[i] != null) circleLamps[i].sprite = s; if (seqDots[i] != null) seqDots[i].sprite = s; }
        }

        public void Open(FinalFloor owner, FinalPuzzle puzzle)
        {
            _owner = owner; _puzzle = puzzle;
            _phase = Phase.Rounds; _round = Story1Net.Online ? Mathf.Clamp(Story1Net.FinalRound, 0, FinalPuzzle.Rounds - 1) : 0; _closeAt = -1f; _flashA = 0f;
            BeginRound();
            group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true;
            IsOpen = true; _openedFrame = Time.frameCount;
            Refresh();
            if (Story1Net.Online && Story1Net.FinalGoing) BeginFinal();   // 이미 마지막 단계가 시작됐다면 따라 들어간다
            Sfx.Play("ui_select", 0.8f);
        }

        private void OnEnable() { Story1Net.Received += OnNet; }
        private void OnDisable() { Story1Net.Received -= OnNet; }

        // 조작자(판정 권한)는 온라인에서 조작자 역할. 오프라인·혼자 테스트는 항상 본인.
        private static bool Authority => !Story1Net.Online || Story1Role.Current == (int)GameRole.Operator;

        private void OnNet(GameEvent e)
        {
            if (!IsOpen || Story1Net.IsMine(e)) return;
            switch (e.a)
            {
                case Story1Net.Round:
                    if (_phase != Phase.Rounds) break;
                    _round = Mathf.Clamp(e.b, 0, FinalPuzzle.Rounds - 1); BeginRound(); Refresh();
                    status.text = "맞았다! 다음 라운드."; status.color = new Color(0.45f, 0.95f, 0.6f);
                    break;
                case Story1Net.FinalGo:
                    if (_phase != Phase.Rounds) break;
                    BeginFinal(); status.text = "순서가 맞았다! 이제 마지막이다."; status.color = new Color(0.45f, 0.95f, 0.6f);
                    break;
                case Story1Net.FinalPress: RemotePress(e.b); break;
                case Story1Net.FinalResult:
                    if (e.b == 1) SucceedFinal(); else if (_phase == Phase.Final) FailFinal(e.s, false);
                    break;
            }
        }

        public void Close()
        {
            IsOpen = false;
            SoloBots.Hide();
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
            Sfx.Play("ui_back", 0.8f);
        }

        private void BeginRound()
        {
            _enteredCount = 0; _t0 = Time.unscaledTime; _lastBeat = -1;
            status.text = "";
        }

        private void BeginFinal()
        {
            _phase = Phase.Final;
            for (int i = 0; i < 4; i++) _press[i] = -1f;
            _t0 = Time.unscaledTime + 0.8f;   // 잠깐 숨 고르기
            _goTime = 4 * FinalPuzzle.BeatSeconds;
            _endAt = _goTime + FinalPuzzle.GoWindow;
            _lastBeat = -1;
            for (int i = 0; i < 4; i++) { _botDone[i] = false; _botAt[i] = _goTime + Random.Range(-0.12f, 0.12f); }
            status.text = "";
            Refresh();
        }

        private void Refresh()
        {
            int role = Story1Role.Current;
            roleText.text = "역할 · " + Roles.Name(role) + (Story1Role.Solo ? "    [Tab] 역할 바꾸기 (혼자 테스트)" : "");
            roleText.color = Roles.Color(role);

            bool rounds = _phase == Phase.Rounds, op = role == (int)GameRole.Operator;
            title.text = rounds ? "메인 홀 · 빛의 순서" : "메인 홀 · 동시에!";
            roundText.text = rounds ? "라운드 " + (_round + 1) + " / " + FinalPuzzle.Rounds : "마지막 — 카운트가 끝나면 모두 동시에";
            for (int i = 0; i < 4; i++)
            {
                circleLabels[i].text = Roles.Name(i) + (Story1Role.Solo || i == role ? "   [" + (i + 1) + "]" : "");
                circleLabels[i].color = Roles.Color(i);
                var rc = circleRings[i].color; rc = Roles.Color(i) * 0.35f; rc.a = (rounds && i != role) ? 0.25f : 1f; circleRings[i].color = rc;
            }
            tickLabel.text = rounds ? "박" : "카운트";
            seqLabel.gameObject.SetActive(rounds);
            for (int i = 0; i < 4; i++) seqDots[i].gameObject.SetActive(rounds);
            RefreshSeq();

            if (rounds)
            {
                hint.text = op || Story1Role.Solo
                    ? "내 원이 몇 번째 박에 켜지는지 팀원과 맞춰 보세요.     1~4  켜진 순서대로 원 입력     Backspace  지우기"
                    : "내 원이 몇 번째 박에 켜지는지 신호로 알려 주세요.";
            }
            else hint.text = Bots ? "카운트 뒤 \"지금!\" 에 1 또는 Space 를 누르세요. AI 동료는 알아서 밟아요."
                : Story1Role.Solo ? "카운트 뒤 \"지금!\" 에 1 2 3 4 를 한꺼번에 누르세요." : "카운트 뒤 \"지금!\" 에 자기 원을 밟으세요. 한 명이라도 늦으면 처음부터.";
            if (hint != null) { hint.maxWidth = 1420f; hint.text += "\nAI 말뜻  숫자 = 내 원이 켜지는 순서 · 응 = 지금 같이 밟아요"; }
            SoloBots.Sync(rounds ? BotLines.FinalRound(_puzzle, Mathf.Min(_round, FinalPuzzle.Rounds - 1)) : BotLines.FinalGo());
        }

        // 혼자 테스트 + 조작자 시점이면 나머지 세 역할은 AI 가 맡는다
        private static bool Bots => Story1Role.Helped;
        private readonly float[] _botAt = new float[4];
        private readonly bool[] _botDone = new bool[4];

        private void RefreshSeq()
        {
            for (int i = 0; i < 4; i++)
            {
                bool has = i < _enteredCount;
                seqDots[i].color = has ? Roles.Color(_entered[i]) : new Color(0.25f, 0.28f, 0.33f, 1f);
            }
        }

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

            if (_closeAt > 0f && Time.unscaledTime >= _closeAt) { Close(); _owner.OnSolved(); return; }

            if (_phase == Phase.Rounds) AnimateRounds();
            else if (_phase == Phase.Final) AnimateFinal();

            if (_phase == Phase.Done || Time.frameCount == _openedFrame) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame) { Close(); return; }   // 웹에서는 Esc 가 전체화면을 풀어 버려서 X·Q 도 쓴다
            if (kb.tabKey.wasPressedThisFrame && Story1Role.Solo) { Story1Role.Cycle(); Sfx.Play("ui_hover", 0.8f); Refresh(); return; }

            for (int i = 0; i < 4; i++)
            {
                bool pressed = (i == 0 && (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame))
                            || (i == 1 && (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame))
                            || (i == 2 && (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame))
                            || (i == 3 && (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame));
                if (!pressed) continue;
                if (_phase == Phase.Rounds) { if (Story1Role.Solo || Story1Role.Current == (int)GameRole.Operator) Enter(i); }
                else if ((Story1Role.Solo && !Bots) || Story1Role.Current == i) StepOn(i);
            }
            if (_phase == Phase.Final && Bots && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) StepOn(Story1Role.Current);
            if (_phase == Phase.Rounds && (kb.backspaceKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame) && _enteredCount > 0)
            { _enteredCount--; RefreshSeq(); Sfx.Play("ui_back", 0.6f); }
        }

        // ---- 라운드: 박마다 자기 원만 켜진다
        private void AnimateRounds()
        {
            float t = Time.unscaledTime - _t0;
            int tick = FinalPuzzle.TickAt(_round, t);
            int role = Story1Role.Current;
            for (int i = 0; i < 5; i++) ticks[i].color = i == tick ? (i == 4 ? new Color(0.5f, 0.5f, 0.55f) : new Color(1f, 0.9f, 0.55f)) : new Color(0.22f, 0.25f, 0.3f);
            for (int i = 0; i < 4; i++)
            {
                bool mine = i == role;
                bool lit = mine && _puzzle.Lit(_round, i, t);
                var c = Roles.Color(i); c.a = lit ? 1f : 0.06f;
                circleLamps[i].color = c;
                circleLamps[i].rectTransform.localScale = Vector3.one * (lit ? 1.08f : 1f);
            }
            if (tick != _lastBeat)
            {
                _lastBeat = tick;
                if (tick < 4 && _puzzle.Lit(_round, role, t)) Sfx.Play("ui_select", 0.7f);
                else if (tick < 4) Sfx.Play("ui_hover", 0.25f);
            }
        }

        private void Enter(int role)
        {
            if (_enteredCount >= 4) return;
            _entered[_enteredCount++] = role;
            Sfx.Play("ui_select", 0.7f);
            RefreshSeq();
            if (_enteredCount < 4) return;

            if (_puzzle.CheckOrder(_round, _entered))
            {
                _flashCol = new Color(0.5f, 1f, 0.6f); _flashA = 0.5f;
                Sfx.Play("power_on", 0.7f);
                _round++;
                if (_round >= FinalPuzzle.Rounds) { Story1Net.Send(Story1Net.FinalGo); BeginFinal(); status.text = "순서가 맞았다! 이제 마지막이다."; status.color = new Color(0.45f, 0.95f, 0.6f); }
                else { Story1Net.Send(Story1Net.Round, _round); status.text = "맞았다! 다음 라운드."; status.color = new Color(0.45f, 0.95f, 0.6f); _enteredCount = 0; _t0 = Time.unscaledTime; Refresh(); }
            }
            else
            {
                _flashCol = new Color(1f, 0.35f, 0.3f); _flashA = 0.5f; _shake = 10f;
                Sfx.Play("fuse_pop", 0.8f);
                status.text = "순서가 틀렸다. 다시!"; status.color = new Color(1f, 0.5f, 0.45f);
                _enteredCount = 0; RefreshSeq();
            }
        }

        // ---- 마지막: 카운트 → 지금!
        private void AnimateFinal()
        {
            float t = Time.unscaledTime - _t0;
            int beat = t < 0f ? -1 : Mathf.Min(4, (int)(t / FinalPuzzle.BeatSeconds));
            for (int i = 0; i < 5; i++)
            {
                bool on = i <= beat && beat >= 0;
                ticks[i].color = on ? (i == 4 ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.9f, 0.55f)) : new Color(0.22f, 0.25f, 0.3f);
            }
            for (int i = 0; i < 4; i++)
            {
                var c = Roles.Color(i); c.a = beat == 4 ? 1f : (_press[i] >= 0f ? 1f : 0.1f);
                circleLamps[i].color = c;
                circleLamps[i].rectTransform.localScale = Vector3.one * (_press[i] >= 0f ? 0.92f : 1f);
            }
            if (beat != _lastBeat)
            {
                _lastBeat = beat;
                if (beat >= 0 && beat < 4) { Sfx.Play("ui_hover", 0.8f); status.text = new[] { "하나", "둘", "셋", "넷" }[beat]; status.color = new Color(0.95f, 0.85f, 0.5f); }
                else if (beat == 4) { Sfx.Play("ui_select", 1f); status.text = "지금!"; status.color = new Color(0.45f, 0.95f, 0.6f); }
            }
            // AI 동료는 "지금!" 근처에서 자기 원을 밟는다
            if (Bots && Authority)
                for (int i = 0; i < 4; i++)
                    if (Story1Role.IsBot(i) && !_botDone[i] && t >= _botAt[i]) { _botDone[i] = true; StepOn(i); }
            if (_phase != Phase.Final) return;
            if (t >= _endAt && Authority) Judge();
        }

        private void StepOn(int role)
        {
            float t = Time.unscaledTime - _t0;
            bool mine = Story1Net.Online && role == Story1Role.Current;
            if (t < _goTime - FinalPuzzle.GoWindow)
            {
                if (Authority) FailFinal("너무 일찍 밟았다!", true);
                else if (mine) Story1Net.Send(Story1Net.FinalPress, role);   // 판정은 조작자가 한다
                return;
            }
            if (_press[role] >= 0f) return;
            _press[role] = t;
            Sfx.Play("ui_select", 0.8f);
            if (mine) Story1Net.Send(Story1Net.FinalPress, role);
            bool all = true; foreach (float p in _press) if (p < 0f) all = false;
            if (all && Authority) Judge();
        }

        // 다른 사람이 자기 원을 밟았다는 알림(도착한 시각을 그 사람의 밟은 시각으로 본다)
        private void RemotePress(int role)
        {
            if (_phase != Phase.Final || !Roles.Valid(role)) return;
            float t = Time.unscaledTime - _t0;
            if (t < _goTime - FinalPuzzle.GoWindow) { if (Authority) FailFinal("너무 일찍 밟았다!", true); return; }
            if (_press[role] < 0f) _press[role] = t;
            bool all = true; foreach (float p in _press) if (p < 0f) all = false;
            if (all && Authority) Judge();
        }

        private void SucceedFinal()
        {
            if (_phase == Phase.Done) return;
            _phase = Phase.Done;
            status.text = "셔터가 올라간다!"; status.color = new Color(0.45f, 0.95f, 0.6f);
            _flashCol = new Color(1f, 0.95f, 0.7f); _flashA = 0.8f;
            Sfx.Play("power_on", 1f);
            _closeAt = Time.unscaledTime + 1.8f;
        }

        private void Judge()
        {
            if (_phase != Phase.Final) return;
            if (FinalPuzzle.Simultaneous(_press, _goTime))
            {
                SucceedFinal();
                Story1Net.Send(Story1Net.FinalResult, 1);
                Story1Net.Send(Story1Net.Clear, Story1Net.PFinal);
            }
            else
            {
                bool missing = false; foreach (float p in _press) if (p < 0f) missing = true;
                FailFinal(missing ? "한 명이 늦었다! 처음부터." : "박자가 어긋났다! 처음부터.", true);
            }
        }

        private void FailFinal(string msg, bool announce)
        {
            if (announce) Story1Net.Send(Story1Net.FinalResult, 0, msg);
            _flashCol = new Color(1f, 0.35f, 0.3f); _flashA = 0.6f; _shake = 12f;
            Sfx.Play("fuse_pop", 1f);
            BeginFinal();
            status.text = msg; status.color = new Color(1f, 0.5f, 0.45f);
        }

        internal static void ResetState() { Cleared = false; IsOpen = false; }
        internal static void MarkCleared() { Cleared = true; }
    }
}
