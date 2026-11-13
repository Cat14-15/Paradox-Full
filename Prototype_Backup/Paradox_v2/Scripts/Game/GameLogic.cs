using System;
using UnityEngine;

namespace Paradox.Game
{
    /// <summary>
    /// 권한(서버/호스트)에서만 도는 게임 규칙. 유저 플로우의 단계 전환과 명령 처리, 매 프레임 판정을 담당한다.
    /// 네트워크 계층(SessionNet)은 명령을 넘기고, 상태가 Dirty가 되면 통째로 브로드캐스트한다.
    /// 연속값(타이머 기준 클록·미터)은 별도 변수로 매 틱 복제된다.
    /// </summary>
    public class GameLogic
    {
        public const int ToneInfo = 0, ToneGood = 1, ToneBad = 2, ToneWarn = 3;

        public SessionState S = new SessionState();

        /// <summary>서버 기준 스테이지 클록(초). 네트워크 계층이 매 틱 갱신한다.</summary>
        public float Clock;

        /// <summary>연속 미터(방벽·연타 진행도 등) — 상태 브로드캐스트 없이 매 틱 복제.</summary>
        public float MeterA;
        public float MeterB;

        public bool Dirty;
        public MapData Map;

        public Action<GameEvent> Emit;
        public Func<int, Vector2?> GetPos;
        public Action OnGameStarted;

        /// <summary>봇 슬롯의 자동 행동(혼자/부분 테스트).</summary>
        public BotDriver Bots;

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

        public static bool IsCutscene(Phase p)
            => p == Phase.Prologue || p == Phase.StageIntro || p == Phase.StageClear || p == Phase.Ending;

        public float TimeLeft
        {
            get
            {
                var st = S.st;
                if (!st.timed) return 0f;
                return st.timeLimit - (Clock - st.startClock) - st.penalty;
            }
        }

        public Vector2? Pos(int slot) => GetPos != null ? GetPos(slot) : null;

        public bool Near(int slot, string propId, float r)
        {
            if (Map == null) return false;
            var prop = Map.Prop(propId);
            var p = Pos(slot);
            if (prop == null || p == null) return false;
            return Vector2.Distance(p.Value, prop.pos) <= r;
        }

        public bool AllInZones()
        {
            if (Map == null) return false;
            for (int i = 0; i < 4; i++)
            {
                var p = Pos(i);
                if (p == null || !Map.InZone(i, p.Value)) return false;
            }
            return true;
        }

        public int InZoneCount()
        {
            if (Map == null) return 0;
            int n = 0;
            for (int i = 0; i < 4; i++)
            {
                var p = Pos(i);
                if (p != null && Map.InZone(i, p.Value)) n++;
            }
            return n;
        }

        public void Penalty(float seconds, string reason, int target = -1)
        {
            S.st.penalty += seconds;
            Toast($"{reason} (-{seconds:0}초)", target, ToneBad);
            Flash(ToneBad);
            Dirty = true;
        }

        public void RebuildMap()
        {
            Map = S.phase == Phase.StagePlay ? StageMaps.Build(S.st) : null;
        }

        public static bool ValidSyllable(string ch) => !string.IsNullOrEmpty(ch) && Array.IndexOf(StageDefs.Syllables, ch) >= 0;

        // ======================================================================
        // 로비 / 접속
        // ======================================================================

        public static string DefaultName(int slot) => $"플레이어 {slot + 1}";

        public int Join(long clientId, string name)
        {
            int existing = S.SlotOfClient(clientId);
            if (existing >= 0) return existing;
            for (int i = 0; i < 4; i++)
            {
                var p = S.players[i];
                if (p.present) continue;
                p.present = true; p.clientId = clientId; p.isBot = false; p.ready = false; p.character = -1;
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
            if (S.phase == Phase.Lobby || S.phase == Phase.CharacterSelect || S.phase == Phase.Result)
            {
                Toast($"{p.name} 님이 나갔습니다", -1, ToneWarn);
                p.present = false; p.clientId = -1; p.ready = false; p.name = ""; p.character = -1; p.isBot = false;
            }
            else
            {
                // 게임 중 이탈 → 봇으로 전환(호스트가 대신 조작할 수 있음)
                Toast($"{p.name} 님의 연결이 끊겨 {Roles.Name(s)} 역할을 봇이 대신합니다", -1, ToneWarn);
                p.isBot = true;
                p.clientId = S.players[S.hostSlot].clientId;
                p.name = "봇";
                if (p.character < 0) p.character = PickUnusedCharacter(new Rng(s + 7));
            }
            Dirty = true;
        }

        public void FillBots(long hostClientId)
        {
            var rng = new Rng(Environment.TickCount);
            for (int i = 0; i < 4; i++)
            {
                var p = S.players[i];
                if (p.present) continue;
                p.present = true; p.isBot = true; p.clientId = hostClientId; p.ready = true;
                p.name = "봇";
                p.character = PickUnusedCharacter(rng);
            }
            Dirty = true;
        }

        private int PickUnusedCharacter(Rng rng)
        {
            for (int tries = 0; tries < 30; tries++)
            {
                int c = rng.Range(0, 9);
                bool used = false;
                foreach (var p in S.players) if (p.present && p.character == c) used = true;
                if (!used) return c;
            }
            return rng.Range(0, 9);
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
                b.name = "봇";
            }
            else
            {
                S.players[to] = a;
                S.players[from] = new PlayerInfo();
            }
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
                case "Slot": ChangeSlot(slot, c.a); break;
                case "FillBots": if (fromHost && S.phase == Phase.Lobby) FillBots(S.players[S.hostSlot].clientId); break;
                case "StartGame":
                    if (fromHost && S.phase == Phase.Lobby)
                    {
                        if (S.PresentCount < 4) { Toast("4명이 모여야 시작할 수 있어요 (빈 자리는 봇으로 채울 수 있음)", slot, ToneWarn); break; }
                        BeginCharacterSelect();
                    }
                    break;
                case "Name":
                    if (S.phase == Phase.CharacterSelect && !S.players[slot].ready)
                    {
                        string n = (c.s ?? "").Trim();
                        if (n.Length > 10) n = n.Substring(0, 10);
                        S.players[slot].name = string.IsNullOrEmpty(n) ? DefaultName(slot) : n;
                        Dirty = true;
                    }
                    break;
                case "Char":
                    if (S.phase == Phase.CharacterSelect && !S.players[slot].ready && c.a >= 0 && c.a < 9)
                    {
                        S.players[slot].character = c.a;
                        Dirty = true;
                    }
                    break;
                case "Ready":
                    if (S.phase == Phase.CharacterSelect)
                    {
                        var p = S.players[slot];
                        if (c.a != 0 && p.character < 0) { Toast("캐릭터를 먼저 골라주세요", slot, ToneWarn); break; }
                        if (string.IsNullOrWhiteSpace(p.name)) p.name = DefaultName(slot);
                        p.ready = c.a != 0;
                        Dirty = true;
                        if (S.AllReady) StartGame();
                    }
                    break;
                case "Next": if (fromHost && IsCutscene(S.phase)) NextLine(); break;
                case "Skip": if (fromHost && IsCutscene(S.phase)) EndCutscene(); break;
                case "Retry": if (fromHost && S.phase == Phase.StagePlay && st.failed) StartStage(S.stage, true); break;
                case "Again": if (fromHost && S.phase == Phase.Result) BackToLobby(); break;
                case "Signal": if (S.phase == Phase.StagePlay && !st.failed) Signal(slot, c.s, c.b); break;
                case "Chat": if (S.phase == Phase.StagePlay) Chat(slot, c.s); break;
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
                    if (S.phase == Phase.StagePlay && !st.failed && !st.cleared) StageApply(c);
                    break;
            }

            if (Dirty) RebuildMap();
        }

        private void Signal(int slot, string id, int target)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!Roles.Valid(target)) target = -1;
            S.stats.signals++;
            Send(new GameEvent { type = "Signal", slot = slot, s = id, target = target });
            StageOnSignal(slot, id);
            Bots?.OnSignal(slot, id, target);
            Dirty = true;
        }

        private void Chat(int slot, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!StageDefs.ChatAllowed(S.stage, slot))
            {
                Toast("이번 스테이지에서는 채팅을 쓸 수 없어요 — 버튼과 글자 카드로만!", slot, ToneWarn);
                return;
            }
            text = text.Trim();
            if (text.Length > 60) text = text.Substring(0, 60);
            S.stats.chats++;
            Send(new GameEvent { type = "Chat", slot = slot, s = text });
            Dirty = true;
        }

        // ======================================================================
        // 흐름 전환
        // ======================================================================

        private void BeginCharacterSelect()
        {
            S.phase = Phase.CharacterSelect;
            var rng = new Rng(Environment.TickCount);
            foreach (var p in S.players)
            {
                if (!p.present) continue;
                if (p.isBot) { p.ready = true; if (p.character < 0) p.character = PickUnusedCharacter(rng); }
                else p.ready = false;
            }
            Toast("캐릭터를 고르고 이름을 입력한 뒤 '준비 완료'를 눌러주세요", -1, ToneInfo);
            Dirty = true;
        }

        private void StartGame()
        {
            S.stats = new Stats();
            S.gameStartClock = Clock;
            S.stage = 0;
            S.st = new StageState();
            ShowCutscene(Phase.Prologue, "prologue");
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
                case Phase.Prologue: IntroStage(1); break;
                case Phase.StageIntro: StartStage(S.stage, false); break;
                case Phase.StageClear: IntroStage(S.stage + 1); break;
                case Phase.Ending: S.phase = Phase.Result; Dirty = true; break;
            }
        }

        private void IntroStage(int n)
        {
            S.stage = n;
            ShowCutscene(Phase.StageIntro, StoryData.IntroId(n));
        }

        private void BackToLobby()
        {
            S.phase = Phase.Lobby;
            S.stage = 0;
            S.st = new StageState();
            foreach (var p in S.players) if (p.present && !p.isBot) p.ready = false;
            Map = null;
            Dirty = true;
        }

        public void StartStage(int n, bool retry)
        {
            var def = StageDefs.Get(n);
            int prevSeed = S.st != null ? S.st.seed : 0;
            int prevRetries = S.st != null ? S.st.retries : 0;

            S.stage = n;
            S.phase = Phase.StagePlay;
            var st = new StageState
            {
                stage = n,
                seed = retry ? prevSeed + 17 : UnityEngine.Random.Range(1, 900000),
                retries = retry ? prevRetries + 1 : 0,
                startClock = Clock,
                timed = def.timeLimit > 0f,
                timeLimit = def.timeLimit
            };
            S.st = st;
            if (retry) S.stats.retries[n]++;
            MeterA = 0f; MeterB = 0f;

            switch (n)
            {
                case 1: Stage1Logic.Start(this); break;
                case 2: Stage2Logic.Start(this); break;
                case 3: Stage3Logic.Start(this); break;
                case 4: Stage4Logic.Start(this); break;
            }

            RebuildMap();
            for (int i = 0; i < 4; i++)
                Send(new GameEvent { type = "Teleport", slot = i, pos = Vec2.Of(Map.spawns[i]) });
            Send(new GameEvent { type = "StageStart", a = n });
            Toast(retry ? $"{def.title} 다시 도전! ({st.retries}번째 재시도)" : $"{def.Headline} — 이번 현지인: {Roles.Full(def.localSlot)}", -1, ToneInfo);
            Dirty = true;
        }

        public void Cleared()
        {
            var st = S.st;
            if (st.cleared) return;
            st.cleared = true;
            st.clearClock = Clock;
            st.moveMode = 0;
            if (S.stage >= 1 && S.stage < S.stats.stageSeconds.Length) S.stats.stageSeconds[S.stage] = Clock - st.startClock;
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
            st.holding = false;
            Flash(ToneBad);
            Send(new GameEvent { type = "Fail", s = reason, a = S.stage });
            Dirty = true;
        }

        private void AfterClear()
        {
            int n = S.stage;
            if (n >= 4) ShowCutscene(Phase.Ending, StoryData.ClearId(n));
            else ShowCutscene(Phase.StageClear, StoryData.ClearId(n));
        }

        // ======================================================================
        // 틱
        // ======================================================================

        public void Tick(float dt)
        {
            if (S.phase == Phase.StagePlay)
            {
                var st = S.st;
                if (Map == null) RebuildMap();
                if (!st.failed && !st.cleared)
                {
                    if (st.timed && TimeLeft <= 0f)
                    {
                        Fail(FailReason(S.stage));
                    }
                    else
                    {
                        StageTick(dt);
                        if (S.botsAuto && HasBots && !st.failed && !st.cleared)
                        {
                            if (Bots == null) Bots = new BotDriver(this);
                            Bots.Tick(dt);
                        }
                    }
                }
                else if (st.cleared && Clock - st.clearClock >= 3.2f)
                {
                    AfterClear();
                }
            }
            if (Dirty) RebuildMap();
        }

        private static string FailReason(int stage)
        {
            switch (stage)
            {
                case 2: return "물에 잠겼다…";
                case 3: return "가스에 휩싸였다…";
                case 4: return "연기에 갇혔다…";
                default: return "실패…";
            }
        }

        private void StageApply(Cmd c)
        {
            switch (S.stage)
            {
                case 1: Stage1Logic.Apply(this, c); break;
                case 2: Stage2Logic.Apply(this, c); break;
                case 3: Stage3Logic.Apply(this, c); break;
                case 4: Stage4Logic.Apply(this, c); break;
            }
        }

        private void StageTick(float dt)
        {
            switch (S.stage)
            {
                case 1: Stage1Logic.Tick(this, dt); break;
                case 2: Stage2Logic.Tick(this, dt); break;
                case 3: Stage3Logic.Tick(this, dt); break;
                case 4: Stage4Logic.Tick(this, dt); break;
            }
        }

        private void StageOnSignal(int slot, string id)
        {
            if (S.st.cleared || S.st.failed) return;
            switch (S.stage)
            {
                case 1: Stage1Logic.OnSignal(this, slot, id); break;
                case 2: Stage2Logic.OnSignal(this, slot, id); break;
                case 3: Stage3Logic.OnSignal(this, slot, id); break;
                case 4: Stage4Logic.OnSignal(this, slot, id); break;
            }
        }

        // ======================================================================
        // 글자 카드 공용
        // ======================================================================

        public static void ResetLetters(StageState st, bool keepWord = true)
        {
            st.picked = "";
            st.inflight.Clear();
            for (int i = 0; i < st.received.Length; i++) st.received[i] = "";
        }

        public const int TrayMax = 4;

        /// <summary>글자 카드를 골라 보내기 전 트레이(picked)에 담는다.</summary>
        public bool PickToTray(int slot, string ch)
        {
            var st = S.st;
            if (!ValidSyllable(ch)) return false;
            if (st.picked.Length >= TrayMax) { Toast($"한 번에 {TrayMax}글자까지만 — 먼저 보내거나 지우세요", slot, ToneWarn); return false; }
            st.picked += ch;
            Dirty = true;
            return true;
        }

        /// <summary>
        /// 트레이의 글자를 모두 날려 보낸다. hop 0 = 중계자(내비게이터)를 거침(Stage 1), hop 1 = 수신자에게 직접.
        /// 글자는 순서대로 조금씩 시간차를 두고 도착한다(전달 이펙트).
        /// </summary>
        public void SendTray(int from, int hop, int[] targets, float transit)
        {
            var st = S.st;
            if (st.picked.Length == 0) { Toast("보낼 글자를 먼저 고르세요", from, ToneWarn); return; }
            int i = 0;
            foreach (char ch in st.picked)
            {
                foreach (int to in targets)
                    st.inflight.Add(new LetterMsg { ch = ch.ToString(), from = from, to = to, hop = hop, arriveClock = Clock + transit + i * 0.45f });
                i++;
            }
            S.stats.letters += st.picked.Length;
            Send(new GameEvent { type = "LetterSent", slot = from, s = st.picked, target = targets.Length == 1 ? targets[0] : -1 });
            st.picked = "";
            Dirty = true;
        }

        /// <summary>직접 전달(hop 1) 중인 글자가 도착 시각을 지나면 수신자에게 넣는다.</summary>
        public void DeliverLetters()
        {
            var st = S.st;
            for (int i = 0; i < st.inflight.Count; i++)
            {
                var m = st.inflight[i];
                if (m.hop != 1 || Clock < m.arriveClock) continue;
                st.inflight.RemoveAt(i);
                i--;
                if (Roles.Valid(m.to)) st.received[m.to] += m.ch;
                Send(new GameEvent { type = "LetterArrive", slot = m.from, s = m.ch, target = m.to });
                Dirty = true;
            }
        }
    }
}
