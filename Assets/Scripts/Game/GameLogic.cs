using System;
using UnityEngine;

namespace Paradox.Game
{
    /// <summary>
    /// 권한(서버/호스트)에서만 도는 게임 규칙. 로비 → 이름 설정 → 준비 → 오프닝 → Stage 1 → 클리어 → 결과의 단계 전환,
    /// 명령 라우팅, 매 프레임 판정, 봇 구동을 맡는다. 네트워크 계층(SessionNet)은 명령을 넘기고, Dirty가 되면 상태를 통째로 브로드캐스트한다.
    /// 로비/이름/컷씬/Retry/Again/Control/BotsAuto는 여기서, Signal/Chat/Brief는 여기(통계·이벤트) → Stage1Logic.OnSignal/봇 훅, 나머지 스테이지 명령은 Stage1Logic.Apply.
    /// v2(D1) 브리핑 게이트: 스테이지 시작 후 timerRunning=false. 4슬롯 전원이 Brief 를 보내거나(봇은 BotDriver가 1.5초 뒤 자동) 30초가 지나면 타이머·침수가 시작된다.
    /// </summary>
    public class GameLogic
    {
        public const int ToneInfo = 0, ToneGood = 1, ToneBad = 2, ToneWarn = 3;

        public const float ReadyDelay = 0.4f;     // 준비 완료 화면 → 오프닝 자동 전환
        public const float ClearDelay = 3.2f;     // CLEAR 배너 → 클리어 컷씬

        /// <summary>휠 신호 id 6종.</summary>
        public static readonly string[] SignalIds = { "YES", "NO", "WHERE", "GO", "YOU", "DANGER" };

        public SessionState S = new SessionState();

        /// <summary>서버 기준 클록(초). 네트워크 계층이 매 틱 갱신한다.</summary>
        public float Clock;

        /// <summary>연속 미터(지금은 미사용, 네트워크 계층 호환용) — 상태 브로드캐스트 없이 매 틱 복제.</summary>
        public float MeterA;
        public float MeterB;

        public bool Dirty;
        public MapData Map;

        public Action<GameEvent> Emit;
        public Func<int, Vector2?> GetPos;
        public Action OnGameStarted;

        /// <summary>봇 슬롯의 자동 행동(혼자/부분 테스트).</summary>
        public BotDriver Bots;

        /// <summary>위험 구역 경고 토스트를 마지막으로 보낸 시각(슬롯별, 서버 런타임 전용).</summary>
        public readonly float[] DangerToastClock = { -99f, -99f, -99f, -99f };

        public bool HasBots
        {
            get { foreach (var p in S.players) if (p.present && p.isBot) return true; return false; }
        }

        // ======================================================================
        // 공용 헬퍼
        // ======================================================================

        public void MarkDirty() => Dirty = true;

        public void Send(GameEvent e) => Emit?.Invoke(e);

        public void Toast(string text, int target = -1, int tone = ToneInfo)
            => Send(new GameEvent { type = "Toast", s = text, target = target, a = tone });

        public void Flash(int tone) => Send(new GameEvent { type = "Flash", a = tone });

        public bool IsBot(int slot) => Roles.Valid(slot) && S.players[slot].present && S.players[slot].isBot;

        public static bool IsCutscene(Phase p) => p == Phase.StageIntro || p == Phase.StageClear;

        public static bool IsSignalId(string id) => !string.IsNullOrEmpty(id) && Array.IndexOf(SignalIds, id) >= 0;

        /// <summary>침수까지 남은 시간. 브리핑 게이트(timerRunning) 전에는 timeLimit 에서 멈춰 있다.</summary>
        public float TimeLeft
        {
            get
            {
                var st = S.st;
                if (!st.timed) return 0f;
                if (!st.timerRunning) return st.timeLimit;
                return st.timeLimit - (Clock - st.startClock) - st.penalty;
            }
        }

        /// <summary>브리핑을 마친 슬롯 수(상단 "준비 중 · 시작! n/4").</summary>
        public static int BriefCount(StageState st)
        {
            if (st == null || st.briefDone == null) return 0;
            int n = 0;
            foreach (bool b in st.briefDone) if (b) n++;
            return n;
        }

        public Vector2? Pos(int slot) => GetPos != null ? GetPos(slot) : null;

        /// <summary>슬롯의 아바타가 사물 반경 r 안에 있는가(맵·사물·위치가 없으면 false).</summary>
        public bool Near(int slot, string propId, float r)
        {
            if (Map == null) return false;
            var prop = Map.Prop(propId);
            var p = Pos(slot);
            if (prop == null || p == null) return false;
            return Vector2.Distance(p.Value, prop.pos) <= r;
        }

        /// <summary>슬롯의 아바타가 있는 방(-1 = 복도/문/위치 모름).</summary>
        public int RoomOf(int slot)
        {
            if (Map == null) return -1;
            var p = Pos(slot);
            return p == null ? -1 : Map.RoomAt(p.Value);
        }

        public bool AnyInRoom(int room)
        {
            for (int i = 0; i < 4; i++) if (RoomOf(i) == room) return true;
            return false;
        }

        /// <summary>최종 집결: 4명 모두 자기 원 안에 있는가(위치를 모르는 슬롯이 있으면 false).</summary>
        public bool AllInGather()
        {
            var st = S.st;
            for (int i = 0; i < 4; i++)
            {
                var p = Pos(i);
                if (p == null || !Stage1Logic.InGather(i, p.Value, st, Clock)) return false;
            }
            return true;
        }

        public int InGatherCount()
        {
            var st = S.st;
            int n = 0;
            for (int i = 0; i < 4; i++)
            {
                var p = Pos(i);
                if (p != null && Stage1Logic.InGather(i, p.Value, st, Clock)) n++;
            }
            return n;
        }

        /// <summary>시간 감점: 토스트 + 경보 플래시(팝업은 닫지 않는다).</summary>
        public void Penalty(float seconds, string reason, int target = -1)
        {
            S.st.penalty += seconds;
            Toast($"{reason} (−{seconds:0}초)", target, ToneBad);
            Flash(ToneBad);
            Dirty = true;
        }

        public void RebuildMap()
        {
            Map = S.phase == Phase.StagePlay ? StageMaps.Build(S.st) : null;
        }

        // ======================================================================
        // 로비 / 접속
        // ======================================================================

        public static string DefaultName(int slot) => Roles.DefaultName(slot);

        public int Join(long clientId, string name)
        {
            int existing = S.SlotOfClient(clientId);
            if (existing >= 0) return existing;
            // 진행 중인 판(준비·오프닝·플레이·클리어·결과)에는 새 참가자를 받지 않는다 — 결과 화면에 끼어들어 클리어가 기록되는 것 방지(LAN 감사 C-4)
            if (S.phase != Phase.Lobby && S.phase != Phase.NameSetup) return -2;
            for (int i = 0; i < 4; i++)
            {
                var p = S.players[i];
                if (p.present) continue;
                p.present = true; p.clientId = clientId; p.isBot = false; p.ready = false; p.character = i;
                p.name = string.IsNullOrWhiteSpace(name) ? DefaultName(i) : name.Trim();
                Dirty = true;
                Toast($"{p.name} 님이 들어왔습니다 → {Roles.Full(i)} 자리", -1, ToneInfo);
                return i;
            }
            return -1;
        }

        public void Leave(long clientId)
        {
            int s = S.SlotOfClient(clientId);
            if (s < 0) return;
            var p = S.players[s];
            if (S.phase == Phase.Lobby || S.phase == Phase.NameSetup || S.phase == Phase.Result)
            {
                Toast($"{p.name} 님이 나갔습니다", -1, ToneWarn);
                p.present = false; p.clientId = -1; p.ready = false; p.name = ""; p.character = -1; p.isBot = false;
            }
            else
            {
                // 게임 중 이탈 → 봇으로 전환(호스트가 대신 조작할 수 있음)
                Toast($"{p.name} 님의 연결이 끊겨 {Roles.Name(s)} 역할을 봇이 대신합니다", -1, ToneWarn);
                p.isBot = true;
                p.ready = true;
                p.clientId = S.players[S.hostSlot].clientId;
                p.name = DefaultName(s);
                p.character = s;
            }
            Dirty = true;
        }

        public void FillBots(long hostClientId)
        {
            for (int i = 0; i < 4; i++)
            {
                var p = S.players[i];
                if (p.present) continue;
                p.present = true; p.isBot = true; p.clientId = hostClientId; p.ready = true;
                p.name = DefaultName(i);
                p.character = i;
            }
            Dirty = true;
        }

        private void ChangeSlot(int from, int to)
        {
            if (!Roles.Valid(from) || !Roles.Valid(to) || from == to) return;
            if (S.phase != Phase.Lobby) return;
            var a = S.players[from];
            var b = S.players[to];
            if (!a.present || a.isBot) return;
            if (b.present && !b.isBot) return; // 사람이 있는 자리는 못 뺏는다(봇 자리는 교체 가능)
            if (b.present && b.isBot)
            {
                // 봇과 자리 교환
                S.players[to] = a; S.players[from] = b;
                b.character = from;
                b.name = DefaultName(from);
            }
            else
            {
                S.players[to] = a;
                S.players[from] = new PlayerInfo();
            }
            a.character = to;
            // 이전 역할의 기본 이름을 쓰고 있었다면 새 역할의 기본 이름으로
            if (Roles.SlotOfDefaultName(a.name) >= 0 || string.IsNullOrWhiteSpace(a.name)) a.name = DefaultName(to);
            if (S.hostSlot == from) S.hostSlot = to;
            else if (S.hostSlot == to) S.hostSlot = from;
            Dirty = true;
        }

        // ======================================================================
        // 명령 처리
        // ======================================================================

        public void Apply(Cmd c, bool fromHost)
        {
            if (c == null) return;
            int slot = c.slot;
            if (!Roles.Valid(slot)) return;
            var st = S.st;

            switch (c.type)
            {
                case "S1":   // Story1(월드·퍼즐) 중계: 권한자는 판정하지 않고 보낸 사람 슬롯을 붙여 모두에게 그대로 전달한다
                    if (c.a == 1 && c.b >= 0 && c.b < 8 && (S.s1Done & (1 << c.b)) == 0) { S.s1Done |= 1 << c.b; Dirty = true; }   // Clear: 푼 퍼즐을 상태에 기록
                    Send(new GameEvent { type = "S1", slot = slot, a = c.a, b = c.b, s = c.s ?? "", f = c.f, pos = c.pos });
                    break;
                case "Slot": ChangeSlot(slot, c.a); break;
                case "FillBots": if (fromHost && S.phase == Phase.Lobby) FillBots(S.players[S.hostSlot].clientId); break;
                case "StartGame":
                    if (fromHost && S.phase == Phase.Lobby)
                    {
                        if (S.PresentCount < 4) { Toast("4명이 모여야 시작할 수 있어요 — [빈 자리 봇으로 채우기]를 눌러도 돼요", slot, ToneWarn); break; }
                        BeginNameSetup();
                    }
                    break;
                case "Name":
                    // 새 로비(캐릭터 선택 + 닉네임)는 로비 단계에서도 이름을 받는다
                    if ((S.phase == Phase.NameSetup || S.phase == Phase.Lobby) && !S.players[slot].ready)
                    {
                        string n = (c.s ?? "").Trim();
                        if (n.Length > 10) n = n.Substring(0, 10);
                        S.players[slot].name = string.IsNullOrEmpty(n) ? DefaultName(slot) : n;
                        Dirty = true;
                    }
                    break;
                case "Ready":
                    if (S.phase == Phase.NameSetup)
                    {
                        var p = S.players[slot];
                        if (string.IsNullOrWhiteSpace(p.name)) p.name = DefaultName(slot);
                        p.ready = c.a != 0;
                        Dirty = true;
                        if (S.AllReady) BeginReady();
                    }
                    break;
                case "PrologueDone":
                    if ((int)S.phase >= (int)Phase.StageIntro && !S.players[slot].prologueDone)
                    {
                        S.players[slot].prologueDone = true;
                        Dirty = true;
                    }
                    break;
                case "CampDone":
                    if ((int)S.phase >= (int)Phase.StageIntro && !S.players[slot].campDone)
                    {
                        S.players[slot].campDone = true;
                        Dirty = true;
                    }
                    break;
                case "Back": if (fromHost && S.phase == Phase.NameSetup) BackToLobby(); break;
                case "Next": if (fromHost && IsCutscene(S.phase)) NextLine(); break;
                case "Skip": if (fromHost && IsCutscene(S.phase)) EndCutscene(); break;
                case "Retry": if (fromHost && S.phase == Phase.StagePlay && st.failed) StartStage(S.stage, true); break;
                case "ForceFail": if (fromHost && S.phase == Phase.StagePlay && !st.cleared) Fail("테스트 강제 실패"); break;   // 테스트 훅: 재시도 경로 검증용
                case "Again": if (fromHost && S.phase == Phase.Result) BackToLobby(); break;
                case "Signal":
                    // 조작자에게는 휠이 없다(기획서 9절·D4) — 클라 UI뿐 아니라 서버도 거부한다
                    if (slot == (int)GameRole.Operator) { Toast("조작자는 도넛을 쓸 수 없어요 — 채팅으로", slot, ToneInfo); break; }
                    if (S.phase == Phase.StagePlay && !st.failed && !st.cleared) Signal(slot, c.s);
                    break;
                case "Chat": if (S.phase == Phase.StagePlay) Chat(slot, c.s, c.a, c.b); break;
                case "Brief": if (S.phase == Phase.StagePlay && !st.failed && !st.cleared) Brief(slot); break;
                case "Control":
                    // 호스트가 직접 조종하는 봇 슬롯 — 그 봇의 자동 행동은 멈춘다
                    if (fromHost)
                    {
                        S.hostControlSlot = Roles.Valid(c.a) && IsBot(c.a) ? c.a : -1;
                        Dirty = true;
                    }
                    break;
                case "BotsAuto":
                    if (fromHost) { S.botsAuto = c.a != 0; Dirty = true; }
                    break;
                default:
                    if (S.phase == Phase.StagePlay && !st.failed && !st.cleared)
                    {
                        // 브리핑 게이트(D1): 타이머가 돌기 전에는 관문 명령을 받지 않는다 — 빠른 플레이어가 침수가 멈춘 채 관문을 푸는 것을 막는다.
                        // Chat/Signal/Ping/OpenBoard/Brief/이동은 허용.
                        if (!st.timerRunning && IsGateCommand(c.type)) { Toast("아직 준비 중 — 모두 [시작]을 누르면 시작해요", slot, ToneInfo); break; }
                        Stage1Logic.Apply(this, c);
                    }
                    break;
            }

            if (Dirty) RebuildMap();
        }

        /// <summary>브리핑 게이트 동안 거부하는 관문 명령(퍼즐 진행에 관여하는 것).</summary>
        public static bool IsGateCommand(string type)
        {
            switch (type)
            {
                case "Digit": case "DigitClear": case "DigitConfirm":
                case "Color": case "Card": case "DirCard":
                case "Lever": case "Judge":
                    return true;
                default: return false;
            }
        }

        /// <summary>
        /// 휠 신호(전원, 대상 없음). 몸짓은 만국 공통 — 모두에게 그대로 보인다.
        /// 서버 규칙이 대신 발행할 때도 이 경로를 쓴다(Stage1Logic.Judge a=0 의 DANGER·NO, D-A5) — 통계·이벤트·봇 훅이 사람의 휠과 같다.
        /// </summary>
        public void Signal(int slot, string id)
        {
            if (!IsSignalId(id)) return;
            S.stats.signals++;
            Send(new GameEvent { type = "Signal", slot = slot, s = id });
            Stage1Logic.OnSignal(this, slot, id);
            Bots?.OnSignal(slot, id);
            Dirty = true;
        }

        /// <summary>
        /// 채팅(전원 입력 가능, 60자). 통역만 전부 읽는 규칙은 클라이언트 표시(Comms.CanUnderstand)에서 적용한다.
        /// a/b 는 조작자 팝업의 빠른 문구 버튼이 붙이는 구조화 값(a=1 홀짝 b=1 홀/0 짝, a=2 점자 b=점 비트마스크) — 봇 해석 우선순위용이며 UI·판정에는 쓰지 않는다.
        /// </summary>
        private void Chat(int slot, string text, int a, int b)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            if (text.Length > 60) text = text.Substring(0, 60);
            S.stats.chats++;
            Send(new GameEvent { type = "Chat", slot = slot, s = text, a = a, b = b });
            Bots?.OnChat(slot, text, a, b);
            Dirty = true;
        }

        /// <summary>브리핑 완료(튜토리얼 [시작]/건너뛰기/? 시트 닫기, 봇은 자동). 4슬롯 전원이 마치면 타이머 시작.</summary>
        private void Brief(int slot)
        {
            var st = S.st;
            if (st.briefDone == null || st.briefDone.Length < 4) st.briefDone = new bool[4];
            if (st.briefDone[slot]) return;
            st.briefDone[slot] = true;
            Dirty = true;
            if (st.timerRunning) return;
            bool all = true;
            for (int i = 0; i < 4; i++) if (S.players[i].present && !st.briefDone[i]) all = false;
            if (all) StartTimer();
        }

        /// <summary>타이머·침수 시작(브리핑 게이트 해제). startClock 을 지금으로 찍고 TimerStart 이벤트.</summary>
        public void StartTimer(bool quiet = false)
        {
            var st = S.st;
            if (st.timerRunning) return;
            st.timerRunning = true;
            st.startClock = Clock;
            Send(new GameEvent { type = "TimerStart", a = S.stage });
            if (!quiet) Toast("시작! 물이 차오르기 시작한다 — 침수까지 12분", -1, ToneWarn);
            Dirty = true;
        }

        // ======================================================================
        // 흐름 전환
        // ======================================================================

        private void BeginNameSetup()
        {
            S.phase = Phase.NameSetup;
            foreach (var p in S.players)
            {
                if (!p.present) continue;
                p.ready = p.isBot;
            }
            for (int i = 0; i < 4; i++)
            {
                var p = S.players[i];
                if (!p.present) continue;
                p.character = i;
                if (string.IsNullOrWhiteSpace(p.name)) p.name = DefaultName(i);
            }
            Toast("이름을 정하고 [확인]을 눌러주세요 — 국적과 역할은 이미 정해져 있어요", -1, ToneInfo);
            Dirty = true;
        }

        private void BeginReady()
        {
            S.phase = Phase.Ready;
            S.readyClock = Clock;
            Dirty = true;
        }

        /// <summary>준비 완료 1.5초 뒤: 기록 초기화, 아바타 스폰, 오프닝 컷씬.</summary>
        private void StartGame()
        {
            S.stats = new Stats();
            S.gameStartClock = Clock;
            S.puzzleSeed = new System.Random().Next(1, 1000000);
            S.s1Done = 0;
            S.stage = 1;
            S.st = new StageState();
            foreach (var pl in S.players) pl.campDone = pl.prologueDone = pl.isBot || !pl.present;   // 사람은 프롤로그를 끝내야 true
            ShowCutscene(Phase.StageIntro, StoryData.IntroId(1));
            OnGameStarted?.Invoke();
        }

        private void ShowCutscene(Phase phase, string id)
        {
            S.phase = phase;
            S.cutsceneId = id;
            S.cutsceneIndex = 0;
            Map = null;
            Dirty = true;
        }

        private void NextLine()
        {
            S.cutsceneIndex++;
            if (S.cutsceneIndex >= StoryData.Length(S.cutsceneId)) EndCutscene();
            else Dirty = true;
        }

        private void EndCutscene()
        {
            switch (S.phase)
            {
                case Phase.StageIntro: StartStage(1, false); break;
                case Phase.StageClear: ShowResult(); break;
            }
        }

        private void ShowResult()
        {
            S.phase = Phase.Result;
            Map = null;
            Dirty = true;
        }

        private void BackToLobby()
        {
            S.phase = Phase.Lobby;
            S.stage = 0;
            S.st = new StageState();
            S.cutsceneId = "";
            S.cutsceneIndex = 0;
            S.hostControlSlot = -1;   // 호스트가 봇 자리를 보던 채로 판이 끝나도 다음 판에서 그 봇이 멈춰 있지 않게
            foreach (var p in S.players) if (p.present && !p.isBot) p.ready = false;
            Map = null;
            Dirty = true;
        }

        /// <summary>
        /// 스테이지 시작. retry = 시간 초과 후 재시도: 체크포인트(0 처음 / 1 관문 2부터)를 유지하고 시간은 다시 12분, 퍼즐 값은 새 시드.
        /// 순서 고정: Stage1Logic.Start(상태 채움) → RebuildMap → Teleport(스폰) → StageStart.
        /// </summary>
        public void StartStage(int n, bool retry)
        {
            var def = StageDefs.Get(n);
            var prev = S.st;
            int prevSeed = prev != null ? prev.seed : 0;
            int prevRetries = prev != null ? prev.retries : 0;
            int checkpoint = retry && prev != null ? prev.checkpoint : 0;

            S.stage = n;
            S.phase = Phase.StagePlay;
            var st = new StageState
            {
                stage = n,
                seed = retry ? prevSeed + 17 : UnityEngine.Random.Range(1, 900000),
                retries = retry ? prevRetries + 1 : 0,
                checkpoint = checkpoint,
                startClock = Clock,
                timed = def.timeLimit > 0f,
                timeLimit = def.timeLimit > 0f ? def.timeLimit : Stage1Logic.TimeLimit
            };
            S.st = st;
            if (retry) S.stats.retries++;
            MeterA = 0f; MeterB = 0f;
            for (int i = 0; i < 4; i++) DangerToastClock[i] = -99f;

            Stage1Logic.Start(this);
            // 브리핑 게이트: 처음 시작은 전원 Brief(또는 30초)까지 정지. 재시도는 튜토리얼이 없으므로 바로 시작
            st.briefDone = new bool[4];
            st.timerRunning = false;
            if (retry) StartTimer(true);

            RebuildMap();
            for (int i = 0; i < 4; i++)
                Send(new GameEvent { type = "Teleport", slot = i, pos = Vec2.Of(Map.spawns[i]) });
            Send(new GameEvent { type = "StageStart", a = n });
            if (retry)
                Toast(checkpoint >= 1 ? $"다시 도전! 관문 2부터 — 침수까지 12분 (재시도 {st.retries}회)" : $"다시 도전! 처음부터 — 침수까지 12분 (재시도 {st.retries}회)", -1, ToneInfo);
            else
                Toast($"{def.title} · {def.disaster} — 침수까지 12분. 내비게이터의 안내를 기다리세요", -1, ToneInfo);
            Dirty = true;
        }

        /// <summary>클리어: 이동 정지, 기록, CLEAR 이벤트 → 3.2초 뒤 클리어 컷씬.</summary>
        public void Cleared()
        {
            var st = S.st;
            if (st.cleared) return;
            st.cleared = true;
            st.clearClock = Clock;
            st.moveMode = 0;
            S.stats.playSeconds += Mathf.Max(0f, Clock - st.startClock);
            Flash(ToneGood);
            Send(new GameEvent { type = "Clear", a = S.stage });
            Dirty = true;
        }

        private void Fail(string reason)
        {
            var st = S.st;
            if (st.failed) return;
            st.failed = true;
            st.moveMode = 0;
            S.stats.playSeconds += Mathf.Max(0f, Clock - st.startClock);
            Flash(ToneBad);
            Send(new GameEvent { type = "Fail", s = reason, a = S.stage });
            Dirty = true;
        }

        private void AfterClear()
        {
            ShowCutscene(Phase.StageClear, StoryData.ClearId(S.stage));
        }

        // ======================================================================
        // 틱
        // ======================================================================

        public void Tick(float dt)
        {
            // 봇(게임 중 나간 사람이 봇으로 바뀐 경우 포함)과 빈 자리는 프롤로그를 읽지 않으므로 서버가 완료로 처리한다
            if ((int)S.phase >= (int)Phase.StageIntro)
                foreach (var pl in S.players)
                    if (!pl.prologueDone && (pl.isBot || !pl.present)) { pl.prologueDone = true; Dirty = true; }
                    else if (!pl.campDone && (pl.isBot || !pl.present)) { pl.campDone = true; Dirty = true; }

            if (S.phase == Phase.Ready)
            {
                if (Clock - S.readyClock >= ReadyDelay) StartGame();
            }
            else if (S.phase == Phase.StagePlay)
            {
                var st = S.st;
                if (Map == null) RebuildMap();
                if (!st.failed && !st.cleared)
                {
                    if (!st.timerRunning && Clock - st.startClock >= Stage1Logic.BriefTimeout) StartTimer();
                    if (st.timed && TimeLeft <= 0f)
                    {
                        Fail("물이 차올랐다… 침수까지 시간이 다 됐다");
                    }
                    else
                    {
                        Stage1Logic.Tick(this, dt);
                        if (S.botsAuto && HasBots && !st.failed && !st.cleared)
                        {
                            if (Bots == null) Bots = new BotDriver(this);
                            Bots.Tick(dt);
                        }
                    }
                }
                else if (st.cleared && Clock - st.clearClock >= ClearDelay)
                {
                    AfterClear();
                }
            }
            if (Dirty) RebuildMap();
        }
    }
}
