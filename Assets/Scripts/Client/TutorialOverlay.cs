using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 스테이지 시작 튜토리얼(명세 8-1): 역할 팝업 "당신은 ○○입니다" → 코치마크 5단계 → [시작].
    /// 상단 ? 버튼으로 조작 안내 시트를 언제든 다시 볼 수 있다. "건너뛰기"는 항상.
    /// 떠 있는 동안 InteractionState.PushLock(이동 불가). 테스트 훅: AutoSkip = true 면 아무것도 띄우지 않는다.
    /// 재시도 스테이지(retries&gt;0)는 표시하지 않는다(명세 14-1 D1(2): 재시도는 튜토리얼 없이 타이머 즉시 시작).
    /// 브리핑 게이트(v2 D1): 마지막 [시작]·[건너뛰기]·? 시트 닫기에서 Brief 명령을 보낸다(AutoSkip·재시도면 첫 Tick 에서 — Build 도중 Send 하면 호스트 루프백이 화면을 다시 만든다). 전원이 보내야 타이머가 돈다.
    /// </summary>
    public class TutorialOverlay
    {
        /// <summary>테스트 훅 — true면 튜토리얼을 표시하지 않는다. 떠 있는 도중에 true 로 바뀌면 즉시 닫힌다.</summary>
        public static bool AutoSkip
        {
            get => _autoSkip;
            set
            {
                _autoSkip = value;
                if (value && Instance != null && Instance.Active) Instance.Skip();
            }
        }
        private static bool _autoSkip;

        /// <summary>지금 살아 있는 오버레이(스테이지 화면당 하나). 테스트 훅 ShowCoachmark 용.</summary>
        public static TutorialOverlay Instance { get; private set; }

        private RectTransform _layer;
        private int _mode;      // 0 없음, 1 역할 팝업, 2 코치마크, 3 ? 시트
        private int _step;
        private bool _locked;
        private bool _done;
        private bool _briefPending;   // Build 에서 미룬 Brief 전송(첫 Tick 에서 보낸다)

        public bool Active => _layer != null && _layer.gameObject.activeSelf;

        private struct Coach { public string title, body; public Vector2 anchor, pos, size; public Vector2 cardAnchor, cardPos; }

        private static readonly Coach[] Steps =
        {
            new Coach { title = "이동과 조사", body = "WASD로 걷고, 오브젝트 앞에서 <b>E</b>를 누르면 조사한다. 팝업이 열려 있는 동안에는 이동할 수 없다.",
                anchor = HudLayout.ActionAnchor, pos = HudLayout.ActionPos, size = HudLayout.ActionSize, cardAnchor = new Vector2(0f, 0f), cardPos = new Vector2(292f, 40f) },
            new Coach { title = "역할은 고정이다", body = "왼쪽 위에 내 역할이 표시된다. 역할마다 쓸 수 있는 도구와 읽을 수 있는 정보가 정해져 있고, 게임 중에 바꿀 수 없다.",
                anchor = HudLayout.BadgeAnchor, pos = HudLayout.BadgePos, size = HudLayout.BadgeSize, cardAnchor = new Vector2(0f, 1f), cardPos = new Vector2(226f, -80f) },
            new Coach { title = "네 화면은 서로 다르다", body = "같은 오브젝트를 봐도 역할에 따라 보이는 정보가 다르다. 관문은 네 명의 정보를 합쳐야 풀린다.",
                anchor = HudLayout.TeamAnchor, pos = HudLayout.TeamPos, size = HudLayout.TeamSize, cardAnchor = new Vector2(0.5f, 1f), cardPos = new Vector2(0f, -150f) },
            new Coach { title = "로그 도넛으로 알린다", body = "로그 아이콘을 누르거나 <b>Q</b>를 누르면 여섯 칸 도넛이 열리고, 고른 칸이 내 캐릭터 머리 위 말풍선으로 뜬다. <b>C</b>는 지난 신호. (조작자는 도넛 대신 <b>T</b> 채팅 — 통역만 알아듣는다)",
                anchor = HudLayout.ActionAnchor, pos = HudLayout.LogIconPos, size = HudLayout.LogIconSize, cardAnchor = new Vector2(0f, 0f), cardPos = new Vector2(292f, 130f) },
            new Coach { title = "침수까지 남은 시간", body = "네 명 모두 [시작]을 누르면(늦어도 30초 뒤에는 저절로) 물이 차오르기 시작한다. 시간이 0이 되면 실패하고 마지막으로 통과한 관문 앞에서 다시 시작한다. 관문 오답에는 시간 감소가 붙는다.",
                anchor = HudLayout.TimerAnchor, pos = HudLayout.TimerPos, size = HudLayout.TimerSize, cardAnchor = new Vector2(1f, 1f), cardPos = new Vector2(-420f, -80f) },
        };

        private static readonly string[][] Can =
        {
            new[] { "제어반·레버 조작", "자유 채팅으로 팀에 설명", "자물쇠 정답 입력" },
            new[] { "표지판·벽보 해독", "모두의 채팅 해독 — 다른 사람 채팅은 나에게만 원문", "숫자 카드 0~9 전송", "방향 카드 ▲▼◀▶ 전송" },
            new[] { "구역별 침수율 확인", "안전 경로 판단", "6칸 도넛으로 이동 지시" },
            new[] { "램프·패널 색 식별", "색상 신고 버튼 4개", "6칸 도넛으로 경고" },
        };

        private static readonly string[][] Cannot =
        {
            new[] { "표지판 읽기", "램프 색 확인", "침수율 확인" },
            new[] { "제어반 조작", "램프 색 확인", "침수율 확인" },
            new[] { "표지판 읽기", "연산 결과 확인", "램프 색 확인" },
            new[] { "표지판 읽기", "숫자 정보 전송", "침수율 확인" },
        };

        public void Build(RectTransform parent)
        {
            _layer = UI.Stretch(parent, "Tutorial");
            _layer.gameObject.SetActive(false);
            Instance = this;
            // 재시도 스테이지(명세 14-1 D1(2))는 튜토리얼 없이 바로 시작한다 — 서버는 이미 StartTimer(quiet) 상태
            var st = Session.St;
            bool retry = st != null && st.retries > 0;
            if (AutoSkip || retry) { _done = true; _briefPending = true; return; }   // Brief 는 timerRunning 뒤엔 서버가 무시(GameLogic.Brief)하므로 무해
            ShowRolePopup();
        }

        /// <summary>브리핑 완료를 서버에 알린다(중복은 서버가 무시).</summary>
        private static void SendBrief()
        {
            if (Session.Connected && Roles.Valid(Session.LocalSlot)) Session.Send("Brief");
        }

        public void Dispose()
        {
            Unlock();
            if (_layer != null) Object.Destroy(_layer.gameObject);
            _layer = null;
            if (Instance == this) Instance = null;
        }

        /// <summary>테스트 훅: 코치마크 n단계(0~4)를 바로 띄운다(스크린샷 투어용).</summary>
        public void ShowCoachmark(int step)
        {
            if (_layer == null) return;
            _done = false;
            _mode = 2;
            _step = Mathf.Clamp(step, 0, Steps.Length - 1);
            ShowStep();
        }

        private void Lock() { if (!_locked) { InteractionState.PushLock(); _locked = true; } }
        private void Unlock() { if (_locked) { InteractionState.PopLock(); _locked = false; } }

        private void Show()
        {
            _layer.gameObject.SetActive(true);
            _layer.SetAsLastSibling();
            Lock();
        }

        private void Hide()
        {
            HudKit.Clear(_layer);
            _layer.gameObject.SetActive(false);
            _mode = 0;
            Unlock();
        }

        /// <summary>건너뛰기(언제든).</summary>
        public void Skip() => Finish();

        private void Finish()
        {
            _done = true;
            Hide();
            SendBrief();
        }

        /// <summary>상단 ? — 조작 안내 시트(PDF 06-1). 튜토리얼이 진행 중이면 무시.</summary>
        public void ShowHelp()
        {
            if (_layer == null) return;
            if (Active && _mode != 3) return;
            if (Active && _mode == 3) { CloseHelp(); return; }
            HudKit.Clear(_layer);
            _mode = 3;
            Show();
            UI.Bg(_layer, new Color(0f, 0f, 0f, 0.62f), true);
            var card = HudKit.Box(_layer, "Help", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(920f, 600f), HudKit.PanelSolid, HudKit.Border, 2f);
            UI.VStack(card, 22, 8, false, TextAnchor.UpperLeft);
            var head = HudKit.Row(card, 34f, 10f, false);
            var tag = HudKit.Block(head, "Tag", HudKit.Amber, 26f);
            UI.LE(tag, 90, 26, 0f, -1, 90, 26);
            HudKit.Fill(tag, "<b>TUTORIAL</b>", 12, HudKit.InkDark, TextAnchor.MiddleCenter, 0f);
            var t = UI.Text(head, "<b>조작 안내</b>", 22, HudKit.Ink, TextAnchor.MiddleLeft, false);
            UI.LE(t, 200, 34, 1f);
            var r = UI.Text(head, "상단 ? 아이콘으로 언제든 다시 볼 수 있다", 12, HudKit.Dim, TextAnchor.MiddleRight, false);
            UI.LE(r, 200, 34, 0f);
            for (int i = 0; i < Steps.Length; i++)
            {
                var line = UI.Rect(card, "Line"); UI.Bg(line, HudKit.Border, false); UI.LE(line, -1, 1);
                var row = HudKit.Row(card, 70f, 14f, false, TextAnchor.MiddleLeft);
                var num = HudKit.Block(row, "Num", HudKit.Slate, 44f, HudKit.Border);
                UI.LE(num, 44, 44, 0f, -1, 44, 44);
                HudKit.Fill(num, $"<b>0{i + 1}</b>", 15, HudKit.Amber, TextAnchor.MiddleCenter, 0f);
                var body = UI.Text(row, $"<size=17><b>{Steps[i].title}</b></size>\n<color=#8A96A8>{Steps[i].body}</color>", 13, HudKit.Ink, TextAnchor.MiddleLeft, true);
                UI.LE(body, 300, 70, 1f);
            }
            var foot = HudKit.Row(card, 46f, 10f, false);
            var note = UI.Text(foot, "이 안내는 상단 ? 아이콘으로 다시 볼 수 있다", 12, HudKit.Dim, TextAnchor.MiddleLeft, false);
            UI.LE(note, 200, 46, 1f);
            var ok = HudKit.Btn(foot, "확인", HudKit.Amber, CloseHelp, 17, 44, 120, true);
            UI.LE(ok, 120, 44, 0f);
        }

        private void CloseHelp()
        {
            Hide();
            // 튜토리얼 도중이 아니었으므로 그대로 끝. ? 시트를 닫는 것도 브리핑 완료로 친다
            if (!_done) _done = true;
            SendBrief();
        }

        // ---------------------------------------------------------------- 1. 역할 팝업

        private void ShowRolePopup()
        {
            HudKit.Clear(_layer);
            _mode = 1;
            Show();
            UI.Bg(_layer, new Color(0f, 0f, 0f, 0.62f), true);
            int me = Session.LocalSlot;
            if (!Roles.Valid(me)) me = 0;
            var rc = Roles.Color(me);
            var card = HudKit.Box(_layer, "Role", new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(640f, 400f), HudKit.PanelSolid, HudKit.Border, 2f);
            var head = UI.Rect(card, "Head");
            head.anchorMin = new Vector2(0f, 1f); head.anchorMax = new Vector2(1f, 1f); head.pivot = new Vector2(0.5f, 1f); head.sizeDelta = new Vector2(0f, 84f);
            UI.Bg(head, new Color(0.04f, 0.06f, 0.10f, 1f), false);
            var badge = HudKit.CodeBadge(head, me, 44f, 12);
            badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f); badge.pivot = new Vector2(0f, 0.5f); badge.anchoredPosition = new Vector2(22f, 0f);
            UI.Label(head, "역할 안내", 12, HudKit.Dim, new Vector2(0f, 1f), new Vector2(80f, -18f), new Vector2(400f, 18f), TextAnchor.MiddleLeft, new Vector2(0f, 1f));
            var title = UI.Label(head, $"당신은 <color=#{Roles.Hex(me)}>{Roles.Name(me)}</color>입니다", 26, HudKit.Ink, new Vector2(0f, 1f), new Vector2(80f, -38f), new Vector2(520f, 36f), TextAnchor.MiddleLeft, new Vector2(0f, 1f));
            title.fontStyle = FontStyle.Bold;
            var hl = UI.Rect(head, "Line"); hl.anchorMin = new Vector2(0f, 0f); hl.anchorMax = new Vector2(1f, 0f); hl.offsetMin = Vector2.zero; hl.offsetMax = new Vector2(0f, 1f); UI.Bg(hl, HudKit.Border, false);

            UI.Label(card, Roles.Job(me), 15, HudKit.Ink, new Vector2(0f, 1f), new Vector2(24f, -100f), new Vector2(592f, 48f), TextAnchor.UpperLeft, new Vector2(0f, 1f));
            BuildList(card, "할 수 있다", HudKit.Green, Can[me], new Vector2(24f, -156f));
            BuildList(card, "할 수 없다", HudKit.Red, Cannot[me], new Vector2(326f, -156f));
            // I-02: 통역에게는 채팅의 양면(모두 읽음 / 내 글은 모두가 읽음)을 한 줄 더
            if (me == Comms.In)
                UI.Label(card, "<color=#F2C572>!</color> 내가 쓴 채팅은 모두가 읽어요 — 다른 사람끼리는 서로 못 읽어요", 12, HudKit.Ink, new Vector2(0f, 0f), new Vector2(24f, 44f), new Vector2(480f, 20f), TextAnchor.MiddleLeft, new Vector2(0f, 0f));
            UI.Label(card, "4명 전원이 동시에 집결 지점(중앙 분수)에 도착해야 클리어된다", 12, HudKit.Dim, new Vector2(0f, 0f), new Vector2(24f, 22f), new Vector2(380f, 20f), TextAnchor.MiddleLeft, new Vector2(0f, 0f));
            var ok = HudKit.Btn(card, "확인", rc, StartCoach, 17, 44, 110, true);
            var okrt = (RectTransform)ok.transform; okrt.anchorMin = okrt.anchorMax = new Vector2(1f, 0f); okrt.pivot = new Vector2(1f, 0f); okrt.anchoredPosition = new Vector2(-24f, 18f); okrt.sizeDelta = new Vector2(110f, 44f);
            var skip = HudKit.Btn(card, "건너뛰기", HudKit.Slate, Skip, 13, 32, 90);
            var skrt = (RectTransform)skip.transform; skrt.anchorMin = skrt.anchorMax = new Vector2(1f, 0f); skrt.pivot = new Vector2(1f, 0f); skrt.anchoredPosition = new Vector2(-146f, 24f); skrt.sizeDelta = new Vector2(90f, 32f);
        }

        private static void BuildList(RectTransform card, string title, Color color, string[] items, Vector2 pos)
        {
            // 높이 140: 통역의 4줄(긴 줄 하나는 두 줄로 접힘)까지 잘리지 않는다(I-02)
            var box = HudKit.Box(card, "List", new Vector2(0f, 1f), pos, new Vector2(290f, 140f), HudKit.Slate, HudKit.Border, 1f);
            var sb = new System.Text.StringBuilder();
            sb.Append($"<size=12><color=#{HudKit.H(color)}>{title}</color></size>\n");
            foreach (var it in items) sb.Append("· ").Append(it).Append('\n');
            HudKit.Fill(box, sb.ToString().TrimEnd(), 14, HudKit.Ink, TextAnchor.UpperLeft, 12f);
        }

        // ---------------------------------------------------------------- 2. 코치마크

        private void StartCoach()
        {
            _step = 0;
            ShowStep();
        }

        private void ShowStep()
        {
            if (_step >= Steps.Length) { Finish(); return; }
            HudKit.Clear(_layer);
            _mode = 2;
            Show();
            var img = _layer.GetComponent<Image>();
            if (img != null) img.color = new Color(0f, 0f, 0f, 0f);
            var s = Steps[_step];
            var canvas = HudLayout.CanvasSize();
            var target = HudLayout.ToCanvasRect(s.anchor, s.pos, s.size, canvas);
            const float pad = 8f;
            target = new Rect(target.xMin - pad, target.yMin - pad, target.width + pad * 2f, target.height + pad * 2f);
            var dim = new Color(0f, 0f, 0f, 0.72f);
            // 대상 주변 네 조각만 어둡게(대상 영역은 밝게 남는다)
            Quad(new Rect(-canvas.x / 2f, target.yMax, canvas.x, canvas.y / 2f - target.yMax), dim);
            Quad(new Rect(-canvas.x / 2f, -canvas.y / 2f, canvas.x, target.yMin + canvas.y / 2f), dim);
            Quad(new Rect(-canvas.x / 2f, target.yMin, target.xMin + canvas.x / 2f, target.height), dim);
            Quad(new Rect(target.xMax, target.yMin, canvas.x / 2f - target.xMax, target.height), dim);
            // 앰버 테두리
            var frame = UI.Fixed(_layer, "Frame", new Vector2(0.5f, 0.5f), target.center, target.size, new Vector2(0.5f, 0.5f));
            HudKit.Frame(frame, HudKit.Amber, 3f);

            // 설명 카드
            var card = HudKit.Box(_layer, "Card", s.cardAnchor, s.cardPos, new Vector2(540f, 168f), HudKit.PanelSolid, HudKit.Amber, 2f);
            var num = HudKit.Box(card, "Num", new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(44f, 44f), HudKit.Slate, HudKit.Border, 1f);
            HudKit.Fill(num, $"<b>0{_step + 1}</b>", 16, HudKit.Amber, TextAnchor.MiddleCenter, 0f);
            UI.Label(card, $"<b>{s.title}</b>", 19, HudKit.Ink, new Vector2(0f, 1f), new Vector2(74f, -16f), new Vector2(440f, 28f), TextAnchor.MiddleLeft, new Vector2(0f, 1f));
            UI.Label(card, s.body, 14, HudKit.Ink, new Vector2(0f, 1f), new Vector2(74f, -46f), new Vector2(448f, 60f), TextAnchor.UpperLeft, new Vector2(0f, 1f));
            UI.Label(card, $"{_step + 1} / {Steps.Length}", 12, HudKit.Dim, new Vector2(0f, 0f), new Vector2(18f, 20f), new Vector2(100f, 20f), TextAnchor.MiddleLeft, new Vector2(0f, 0f));
            bool last = _step == Steps.Length - 1;
            var next = HudKit.Btn(card, last ? "시작" : "다음 ▶", HudKit.Amber, () => { _step++; ShowStep(); }, 15, 40, 110, true);
            var nrt = (RectTransform)next.transform; nrt.anchorMin = nrt.anchorMax = new Vector2(1f, 0f); nrt.pivot = new Vector2(1f, 0f); nrt.anchoredPosition = new Vector2(-16f, 14f); nrt.sizeDelta = new Vector2(110f, 40f);
            var skip = HudKit.Btn(card, "건너뛰기", HudKit.Slate, Skip, 13, 32, 90);
            var srt = (RectTransform)skip.transform; srt.anchorMin = srt.anchorMax = new Vector2(1f, 0f); srt.pivot = new Vector2(1f, 0f); srt.anchoredPosition = new Vector2(-136f, 18f); srt.sizeDelta = new Vector2(90f, 32f);
        }

        private void Quad(Rect r, Color c)
        {
            if (r.width <= 0f || r.height <= 0f) return;
            var q = UI.Fixed(_layer, "Dim", new Vector2(0.5f, 0.5f), r.center, r.size, new Vector2(0.5f, 0.5f));
            UI.Bg(q, c, true);
        }

        // ---------------------------------------------------------------- 키

        public void Tick()
        {
            if (_briefPending) { _briefPending = false; SendBrief(); }
            if (!Active) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            bool advance = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (_mode == 3) CloseHelp(); else Skip();
                return;
            }
            if (!advance) return;
            switch (_mode)
            {
                case 1: StartCoach(); break;
                case 2: _step++; ShowStep(); break;
                case 3: CloseHelp(); break;
            }
        }
    }
}
