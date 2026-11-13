using System.Collections.Generic;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// 스테이지 플레이 화면 = 월드(StageWorld) + 개인 HUD(명세 8절, PDF s12 배치).
    /// 상단 바 · 좌상단 역할 배지 · 우상단 미니맵 · 좌하단 액션 열 · 우하단 역할 패널 · 중앙 팝업/도넛/로그/튜토리얼 · 집결 게이지 · 하단 토스트/상호작용 안내.
    /// 카메라는 WORLD 영역이 고정한다(여기서는 건드리지 않는다). 규칙 판정은 전부 서버 — 여기서는 명령을 보내고 상태를 그린다.
    /// </summary>
    public class StageScreen : ScreenBase
    {
        /// <summary>테스트 훅용 정적 인스턴스(현재 스테이지 화면).</summary>
        public static StageScreen Instance { get; private set; }

        private StageWorld _world;
        private TopBar _top;
        private RoleBadge _badge;
        private MinimapView _minimap;
        private ActionBar _actions;
        private RolePanel _panel;
        private GatherPanel _gather;
        private Popups _popups;
        private Donut _donut;
        private LogPanel _log;
        private TutorialOverlay _tutorial;

        private Text _prompt, _failText, _failHint;
        private RectTransform _failPanel, _clearPanel, _switchPanel;
        private readonly List<Button> _switchBtns = new List<Button>();
        private bool _chatLocked;

        // ---------------------------------------------------------------- 생명주기

        public override void Build(RectTransform parent)
        {
            base.Build(parent);
            Instance = this;
            Session.LocalSlotChanged += OnLocalSlot;   // Dispose(-=)와 항상 짝이 맞도록 Build 맨 앞에서 구독(Build 도중 화면이 교체돼도 해제된다)

            _world = new StageWorld();
            _world.Build();

            // 아래에서 위 순서 = 그리기 순서. 팝업 층은 역할 패널보다 아래라 팝업 중에도 채팅 입력이 된다.
            _top = new TopBar(); _top.Build(Root, ShowHelp);
            _badge = new RoleBadge(); _badge.Build(Root);
            _minimap = new MinimapView(); _minimap.Build(Root);
            BuildSwitchPanel();
            _actions = new ActionBar();
            _actions.Build(Root, OpenDonut, () => ShowLog(!_log.Visible), TryInteract, () => _minimap.ToggleEnlarged());
            _gather = new GatherPanel(); _gather.Build(Root);
            _prompt = UI.Label(Root, "", 17, HudKit.Amber, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(700f, 30f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0f));
            _popups = new Popups(); _popups.Build(Root);
            _popups.ChatDraft = SetChatDraft;
            _panel = new RolePanel(); _panel.Build(Root);
            _log = new LogPanel(); _log.Build(Root);
            _donut = new Donut(); _donut.Build(Root);
            BuildEndPanels();
            _tutorial = new TutorialOverlay(); _tutorial.Build(Root);

            // 알림은 하단 가운데(상호작용 안내 위)에 위로 쌓인다
            if (ScreenManager.Instance != null) ScreenManager.Instance.SetToastPlacement(new Vector2(0.5f, 0f), new Vector2(0f, 100f), true);

            OnState();
        }

        public override void Dispose()
        {
            Session.LocalSlotChanged -= OnLocalSlot;
            if (_chatLocked) { InteractionState.PopLock(); _chatLocked = false; }
            _tutorial?.Dispose();
            _popups?.Dispose();
            _donut?.Dispose();
            _log?.Dispose();
            _panel?.Dispose();
            _minimap?.Dispose();
            _world?.Dispose();
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private void BuildSwitchPanel()
        {
            _switchPanel = HudKit.Box(Root, "Switch", HudLayout.SwitchAnchor, HudLayout.SwitchPos, HudLayout.SwitchSize, HudKit.Panel, HudKit.Border, 1f);
            UI.HStack(_switchPanel, 6, 4, false, TextAnchor.MiddleLeft);
            var swLabel = UI.Text(_switchPanel, "조종 전환\n<size=9>1~4 키 · 봇 자리만</size>", 11, HudKit.Dim, TextAnchor.MiddleLeft, false);
            UI.LE(swLabel, 84, 32, 0f);
            _switchBtns.Clear();
            for (int i = 0; i < 4; i++)
            {
                int s = i;
                var b = UI.Button(_switchPanel, Roles.Short(i), HudKit.Slate, () => Session.SetLocalSlot(s), 12, 32, 56);
                UI.LE(b, 56, 32, 1f);
                _switchBtns.Add(b);
            }
            _switchPanel.gameObject.SetActive(false);
        }

        private void BuildEndPanels()
        {
            _failPanel = HudKit.Box(Root, "Fail", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(620f, 210f), new Color(0.20f, 0.07f, 0.09f, 0.97f), HudKit.Red, 2f);
            UI.VStack(_failPanel, 24, 12, false, TextAnchor.UpperCenter);
            _failText = UI.Text(_failPanel, "", 24, new Color(1f, 0.85f, 0.85f), TextAnchor.MiddleCenter, true, 70);
            // B5: 재시도는 버튼으로만(Esc 단축키 제거 — 실수로 눌러 친구들 화면이 튀지 않게). 문구는 호스트 이름·역할(StageGuide.HostRetryHint)
            if (Session.IsHost) HudKit.Btn(_failPanel, "다시 시도 — 마지막 관문 앞에서", HudKit.Amber, () => Session.Send("Retry"), 17, 48, 200, true);
            _failHint = UI.Text(_failPanel, StageGuide.HostRetryHint(), 14, HudKit.Dim, TextAnchor.MiddleCenter, true, 30);
            _failPanel.gameObject.SetActive(false);

            _clearPanel = HudKit.Box(Root, "Clear", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(680f, 150f), new Color(0.06f, 0.16f, 0.11f, 0.97f), HudKit.MonoGreen, 2f);
            UI.VStack(_clearPanel, 20, 8, false, TextAnchor.UpperCenter);
            UI.Text(_clearPanel, "✓  CLEAR", 38, HudKit.MonoGreen, TextAnchor.MiddleCenter, false, 52);
            UI.Text(_clearPanel, "셔터가 열린다 — 말이 안 통해도, 함께 해냈다", 18, HudKit.Ink, TextAnchor.MiddleCenter, false, 30);
            _clearPanel.gameObject.SetActive(false);
        }

        private void OnLocalSlot()
        {
            // 호스트가 다른 봇 자리를 조종하기 시작하면 내 것이던 팝업/도넛은 닫는다
            _popups?.Close();
            _donut?.Close();
            _panel?.OnState();
            _badge?.OnState();
            _minimap?.OnState();
            // 이전 슬롯 기준으로 만든 토스트·말풍선은 새 역할의 가시성 규칙을 어길 수 있어 모두 지운다
            if (ScreenManager.Instance != null) ScreenManager.Instance.ClearToasts();
            foreach (var kv in Session.Avatars) if (kv.Value != null && kv.Value.View != null) kv.Value.View.ClearSpeech();
        }

        // ---------------------------------------------------------------- 테스트 훅 / 외부 진입점(명세 8-2)

        public bool IsPopupOpen => _popups != null && _popups.IsOpen;
        /// <summary>팝업이 "완료 ✓"(담당 미션 끝, 3초 뒤 자동 닫힘) 상태인가 — 테스트 훅.</summary>
        public bool IsPopupDone => _popups != null && _popups.IsDone;
        public string CurrentPopup => _popups != null ? _popups.Current : "";
        public bool IsDonutOpen => _donut != null && _donut.IsOpen;
        public bool IsLogVisible => _log != null && _log.Visible;
        public bool ChatFocused => _panel != null && _panel.ChatFocused;
        /// <summary>테스트 훅: 우하단 역할 패널 루트(높이가 조종 전환 줄을 덮지 않는지 검사).</summary>
        public RectTransform RolePanelRect => _panel != null ? _panel.Root : null;

        /// <summary>담당 오브젝트 팝업을 연다(WORLD의 E/클릭 처리가 호출). 담당자 화면에만 존재(명세 5-3) — 비담당자는 열지 않는다.</summary>
        public void OpenPopup(string propId)
        {
            if (_popups == null) return;
            var st = Session.St;
            if (st.failed || st.cleared) return;
            if (Comms.PropSlot(propId) != Session.LocalSlot) return;
            _donut?.Close();
            _popups.Open(propId);
        }

        public void ClosePopup() => _popups?.Close();

        /// <summary>테스트 훅: 도넛이 열려 있으면 닫는다.</summary>
        public void CloseDonut()
        {
            if (_donut == null || !_donut.IsOpen) return;
            _donut.Close();
            _actions?.SetActive(0, false);
        }

        public void OpenDonut()
        {
            if (_donut == null || IsPopupOpen) return;
            if (_tutorial != null && _tutorial.Active) return;
            if (Session.LocalSlot == Comms.Op)
            {
                // D4: 조작자에게는 도넛이 없다(기획서 9절) — 고립이 이 스테이지의 메시지. 대신 채팅 입력칸으로 보낸다
                if (ScreenManager.Instance != null) ScreenManager.Instance.ShowToast("도넛은 없어요 — T로 말하면 통역만 알아들어요", GameLogic.ToneInfo);
                _panel?.FocusChat();
                return;
            }
            _donut.Open();
            _actions?.SetActive(0, _donut.IsOpen);
        }

        public void ShowLog(bool visible)
        {
            _log?.Show(visible);
            if (visible) _log?.BringToFront();   // A2: 팝업이 열려 있어도 수신 로그가 그 위에 보인다
            _actions?.SetActive(1, visible);
        }

        public void ShowHelp() => _tutorial?.ShowHelp();

        /// <summary>점자 칩 → 채팅 입력칸에 초안을 쓰고 포커스.</summary>
        public void SetChatDraft(string text) => _panel?.SetDraft(text);

        /// <summary>
        /// 액션 열의 [상호작용 E] 버튼. 아바타의 E 명령이 있으면 그대로 보내고,
        /// 없으면 내 담당 오브젝트가 발광 중이고 닿는 거리면 팝업을 연다(키보드 E와 같은 결과).
        /// </summary>
        public void TryInteract()
        {
            if (IsPopupOpen) { ClosePopup(); return; }
            var av = Session.ControlledAvatar;
            if (av == null) return;
            if (av.CurrentPrompt.cmd != null) { Session.Send(av.CurrentPrompt.cmd); return; }
            var map = Session.Map;
            if (map == null) return;
            int me = Session.LocalSlot;
            Vector2 pos = av.transform.position;
            var st = Session.St;
            foreach (var id in Comms.PropIds)
            {
                if (!Comms.IsGlowing(id, me, pos, st)) continue;
                var p = map.Prop(id);
                if (p == null) continue;
                if (Vector2.Distance(p.pos, pos) <= StageInteractions.Reach) { OpenPopup(id); return; }
            }
            if (ScreenManager.Instance != null) ScreenManager.Instance.ShowToast("가까이에 조사할 것이 없어요 — 빛나는 내 담당 오브젝트 앞에서 E", GameLogic.ToneInfo);
        }

        // ---------------------------------------------------------------- 상태 / 이벤트

        public override void OnState()
        {
            var st = Session.St;
            int me = Session.LocalSlot;

            _world.Refresh();
            _top.OnState();
            _badge.OnState();
            _minimap.OnState();
            _panel.OnState();
            _popups.OnState();
            _donut.OnState();

            _failPanel.gameObject.SetActive(st.failed);
            _clearPanel.gameObject.SetActive(st.cleared);
            if (st.failed)
            {
                _failText.text = $"물이 차올랐다…\n<size=15>재시도 {st.retries}회 · 체크포인트: {(st.checkpoint >= 1 ? "관문 2부터" : "처음부터")}</size>";
                if (_failHint != null) _failHint.text = StageGuide.HostRetryHint();
                _donut.Close();
            }
            if (st.cleared) { _donut.Close(); _log.Show(false); }

            _actions.SetOperatorMode(me == Comms.Op);   // D4: 조작자 첫 줄은 "말하기 T · 통역만 읽어요"
            _switchPanel.gameObject.SetActive(Session.IsHost && Session.HasBots);
            for (int i = 0; i < _switchBtns.Count; i++)
            {
                bool cur = i == me;
                UI.SetButtonColor(_switchBtns[i], cur ? Roles.Color(i) : HudKit.Slate);
                UI.ButtonLabel(_switchBtns[i]).text = cur ? $"★ {Roles.Short(i)}" : Roles.Short(i);
                UI.ButtonLabel(_switchBtns[i]).color = cur ? HudKit.InkDark : Color.white;
                _switchBtns[i].interactable = Session.CanControl(i);
            }
        }

        public override void OnEvent(GameEvent e)
        {
            if (e == null) return;
            _world.OnEvent(e);
            _popups.OnEvent(e);
            _log.OnEvent(e);
            _panel.OnEvent(e);
            _minimap.OnEvent(e);

            // 명세 6-3: 색 신고·카드는 수신자 화면에만 토스트(서버가 target=통역/조작자를 넣어 보낸다). 표시만 — 판정·로그는 서버/Session이 맡는다.
            // (서버 Toast 이벤트로 보내면 Session.LoggedTypes 에 Toast 가 있어 로그가 중복되므로 클라에서 띄운다)
            if (e.target == Session.LocalSlot && ScreenManager.Instance != null)
            {
                switch (e.type)
                {
                    case "Color":
                        ScreenManager.Instance.ShowToast($"관측자: <color=#{HudKit.H(Comms.ColorValue(e.a))}>■</color>{Comms.ColorName(e.a)}", GameLogic.ToneInfo);
                        break;
                    case "NumberCard":
                        ScreenManager.Instance.ShowToast($"통역이 보낸 {Comms.NumberCardText(e.a)} — 제어반에 입력!", GameLogic.ToneGood);
                        break;
                    case "DirCard":
                        ScreenManager.Instance.ShowToast($"통역이 보낸 {Comms.DirCardText(e.a)} — 레버를 그 방향으로!", GameLogic.ToneGood);
                        break;
                }
            }

            // 관문 1 클리어 직후의 전원 토스트("이렇게 서로 다른 정보를 …")는 서버(Stage1Logic)가 Toast 이벤트로 보낸다 — 여기서 중복으로 띄우지 않는다.
        }

        // ---------------------------------------------------------------- 매 프레임

        public override void Tick()
        {
            var st = Session.St;
            _world.Tick();
            _minimap.Tick();
            _top.Tick();
            _panel.Tick();
            _popups.Tick();
            _log.Tick();
            _gather.Tick();
            _tutorial.Tick();

            // S-05/B-01: 팝업이 열려 있는 동안은 미니맵을 숨긴다(팝업 밖 정보로 시선이 새지 않게 — 딤 알파는 팝업 쪽)
            _minimap.SetVisible(!IsPopupOpen);

            // 상호작용 안내(WORLD가 계산한 문구)
            var av = Session.ControlledAvatar;
            _prompt.text = (av != null && !IsPopupOpen && !st.failed && !st.cleared) ? av.CurrentPrompt.text : "";

            // 채팅 포커스 ↔ 입력 잠금
            bool chatFocused = _panel.ChatFocused;
            if (chatFocused != _chatLocked)
            {
                if (chatFocused) InteractionState.PushLock(); else InteractionState.PopLock();
                _chatLocked = chatFocused;
            }

            // 액션 열 강조 동기화
            _actions.SetActive(0, _donut.IsOpen);
            _actions.SetActive(1, _log.Visible);

            HandleKeys(st, chatFocused);
        }

        private void HandleKeys(StageState st, bool chatFocused)
        {
            var kb = Keyboard.current;
            if (kb == null || chatFocused) return;
            if (_tutorial.Active) return; // 튜토리얼이 Enter/Esc를 스스로 처리한다

            if (IsPopupOpen)
            {
                // 팝업이 잠금을 쥐고 있어도 닫기·채팅 포커스·수신 로그(A2: 벽보를 보면서 조작자 채팅을 다시 읽을 수 있게)는 된다
                if (kb.escapeKey.wasPressedThisFrame) ClosePopup();
                else if (kb.tKey.wasPressedThisFrame) _panel.FocusChat();
                else if (kb.cKey.wasPressedThisFrame) ShowLog(!_log.Visible);
                return;
            }
            if (InteractionState.InputLocked) return;

            if (_donut.IsOpen)
            {
                if (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) _donut.Close();
                return;
            }

            if (kb.qKey.wasPressedThisFrame) OpenDonut();
            if (kb.cKey.wasPressedThisFrame) ShowLog(!_log.Visible);
            if (kb.tabKey.wasPressedThisFrame) _minimap.ToggleEnlarged();
            if (kb.tKey.wasPressedThisFrame) _panel.FocusChat();
            // B5: 실패 중 Esc→Retry 단축키는 없앴다(버튼만) — 호스트가 무심코 눌러 네 명이 함께 되감기지 않게
            if (kb.escapeKey.wasPressedThisFrame && _log.Visible) ShowLog(false);
        }
    }
}
