using UnityEngine;

namespace Paradox.Game
{
    /// <summary>
    /// Stage 1 — 필리핀 · 정전된 복도 · 릴레이 체인.
    ///  step 0: 릴레이 — 통역이 표지판 단어의 글자를 고르면 글자가 '날아가' 내비게이터에게 도착 → "그대로 전달" → 다시 날아가 조작자에게 도착.
    ///          관측자는 정전 경고등이 최고조일 때 ⚠를 눌러 파동을 막는다(못 막으면 전달 중이던 글자 소실 → 통역의 선택부터 재시작).
    ///          조작자는 도착한 단어(2글자)와 같은 라벨의 레버(좌/우)를 고른다. 갈림길에서는 자유롭게 걸어 다닐 수 있다.
    ///  step 2: 레일 이동 — 네 명이 다음 지점까지 자동 이동.
    ///  step 3: 장애물 — 전원 SPACE 연타로 쓰러진 집기 제거.
    ///  step 4: 로비 — 자유 이동, 네 명이 각자 원 안에 동시에 들어오면 클리어.
    /// </summary>
    public static class Stage1Logic
    {
        private const int Op = (int)GameRole.Operator, In = (int)GameRole.Interpreter, Na = (int)GameRole.Navigator, Ob = (int)GameRole.Observer;

        public const float GuardWindow = 2.0f;   // 경고등 최고조 전, ⚠가 유효한 구간(초)
        public const float SurgeLead = 0.6f;     // 파동 판정 시점(주기 끝에서 이만큼 전)
        public const float CutDuration = 2.2f;   // 신호 차단 배너 시간
        public const float TransitTime = 1.3f;   // 글자 카드가 다음 사람에게 날아가는 시간
        public const int WordLength = 2;

        public static void Start(GameLogic g)
        {
            var st = g.S.st;
            var rng = new Rng(st.seed);
            for (int f = 0; f < 3; f++)
            {
                st.forkWord[f] = StageDefs.ForkPairs[f][0];
                st.forkDecoy[f] = StageDefs.ForkPairs[f][1];
                st.leverSide[f] = rng.Range(0, 2);
            }
            st.fork = 0;
            st.period = 8f;
            st.cycleStart = g.Clock;
            st.guardedCycle = -1;
            st.resolvedCycle = -1;
            st.signalCut = false;
            st.obstacleCleared = false;
            st.word = st.forkWord[0];
            GameLogic.ResetLetters(st);
            SetRailTo(g, 1);
            st.step = 2;
            UpdateStatus(g);
        }

        /// <summary>갈림길 f의 레버 라벨. 정답 쪽에 표지판 단어, 반대쪽에 미끼 단어.</summary>
        public static string LeverLabel(StageState st, int fork, int side)
        {
            if (fork < 0 || fork > 2) return "";
            return st.leverSide[fork] == side ? st.forkWord[fork] : st.forkDecoy[fork];
        }

        public static float Phase01(StageState st, float clock)
        {
            if (st.period <= 0f) return 0f;
            return Mathf.Repeat(clock - st.cycleStart, st.period) / st.period;
        }

        public static int Cycle(StageState st, float clock) => Mathf.FloorToInt(Mathf.Max(0f, clock - st.cycleStart) / Mathf.Max(0.01f, st.period));

        public static bool InGuardWindow(StageState st, float clock)
        {
            float ph = Mathf.Repeat(clock - st.cycleStart, st.period);
            return ph >= st.period - GuardWindow;
        }

        /// <summary>전달 중인 글자가 있는가(파동에 소실될 수 있는 상태).</summary>
        public static bool LettersInTransit(StageState st) => st.inflight.Count > 0;

        /// <summary>관측자의 ⚠ 버튼이 붉게 강조되어야 하는 순간: 최고조 창 + 전달 중인 글자.</summary>
        public static bool GuardGlow(StageState st, float clock)
            => st.step == 0 && !st.signalCut && InGuardWindow(st, clock) && LettersInTransit(st) && st.guardedCycle != Cycle(st, clock);

        /// <summary>내비게이터가 지금 전달할 수 있는(도착한) 글자가 있는가.</summary>
        public static bool RelayReady(StageState st, float clock)
        {
            foreach (var m in st.inflight) if (m.hop == 0 && clock >= m.arriveClock) return true;
            return false;
        }

        /// <summary>조작자가 레버를 고를 수 있는가(단어가 다 도착했는가).</summary>
        public static bool CanPullLever(StageState st) => st.step == 0 && !st.signalCut && st.received[Op].Length >= WordLength;

        public static void Apply(GameLogic g, Cmd c)
        {
            var st = g.S.st;
            int slot = c.slot;
            switch (c.type)
            {
                case "Pick":
                    if (slot != In || st.step != 0) return;
                    g.PickToTray(slot, c.s);
                    break;

                case "SendLetters":
                    if (slot != In || st.step != 0) return;
                    if (st.signalCut) { g.Toast("신호가 끊긴 동안은 보낼 수 없어요", slot, GameLogic.ToneWarn); return; }
                    g.SendTray(In, 0, new[] { Op }, TransitTime);
                    break;

                case "Relay":
                    if (slot != Na || st.step != 0) return;
                    if (st.signalCut) { g.Toast("신호가 끊긴 동안은 전달할 수 없어요", slot, GameLogic.ToneWarn); return; }
                    {
                        LetterMsg head = null;
                        foreach (var m in st.inflight) if (m.hop == 0 && g.Clock >= m.arriveClock) { head = m; break; }
                        if (head == null) { g.Toast("전달할 글자가 아직 없어요 — 통역의 글자를 기다리세요", slot, GameLogic.ToneWarn); return; }
                        head.hop = 1;
                        head.from = Na;
                        head.arriveClock = g.Clock + TransitTime;
                        g.Send(new GameEvent { type = "LetterSent", slot = Na, s = head.ch, target = Op });
                        g.Dirty = true;
                    }
                    break;

                case "ClearLetters":
                    if (slot != Op && slot != In) return;
                    if (st.step != 0) return;
                    GameLogic.ResetLetters(st);
                    g.Send(new GameEvent { type = "LettersCleared", slot = slot });
                    g.Toast(slot == Op ? "조작자가 다시 요청했어요 — 통역은 처음부터 다시 보내주세요" : "통역이 글자를 지웠어요 — 처음부터 다시", -1, GameLogic.ToneWarn);
                    g.Dirty = true;
                    break;

                case "Lever":
                    if (slot != Op || st.step != 0) return;
                    if (st.signalCut) { g.Toast("신호가 끊긴 동안은 레버를 쓸 수 없어요", slot, GameLogic.ToneWarn); return; }
                    if (!CanPullLever(st)) { g.Toast("글자가 아직 다 오지 않았어요 — 2글자가 모이면 레버를 고를 수 있어요", slot, GameLogic.ToneWarn); return; }
                    if (c.a == st.leverSide[st.fork])
                    {
                        g.Flash(GameLogic.ToneGood);
                        g.Toast($"올바른 길! [{LeverLabel(st, st.fork, c.a)}] 방향으로 모두 이동합니다", -1, GameLogic.ToneGood);
                        g.Send(new GameEvent { type = "LeverOk", slot = Op, a = c.a });
                        SetRailTo(g, NextNodeAfterFork(st.fork));
                        st.step = 2;
                    }
                    else
                    {
                        g.Flash(GameLogic.ToneBad);
                        g.Send(new GameEvent { type = "LeverBad", slot = Op, a = c.a });
                        g.Toast($"잘못된 길! [{LeverLabel(st, st.fork, c.a)}] 쪽에서 물이 차오른다… 단어를 다시 확인하고 고르세요", -1, GameLogic.ToneBad);
                        g.S.stats.losses++;
                    }
                    UpdateStatus(g);
                    g.Dirty = true;
                    break;

                case "Mash":
                    if (st.step != 3) return;
                    Mash(g, slot);
                    break;
            }
        }

        private static void Mash(GameLogic g, int slot)
        {
            var st = g.S.st;
            st.lastMash[slot] = g.Clock;
            g.MeterA = Mathf.Min(100f, g.MeterA + 3.5f);
            if (g.MeterA >= 100f) ClearObstacle(g);
        }

        public static void OnSignal(GameLogic g, int slot, string id)
        {
            var st = g.S.st;
            if (st.step != 0) return;
            if (slot == Ob && id == "WARN") Guard(g);
            else if (slot == Op && id == "ASK")
            {
                if (st.picked.Length == 0 && st.received[Op].Length == 0 && st.inflight.Count == 0) return;
                GameLogic.ResetLetters(st);
                g.Send(new GameEvent { type = "LettersCleared", slot = Op });
                g.Toast("조작자가 다시 요청(?)했어요 — 통역은 처음부터 다시 보내주세요", -1, GameLogic.ToneWarn);
                g.Dirty = true;
            }
        }

        private static void Guard(GameLogic g)
        {
            var st = g.S.st;
            if (InGuardWindow(st, g.Clock))
            {
                int cycle = Cycle(st, g.Clock);
                if (st.guardedCycle == cycle) return;
                st.guardedCycle = cycle;
                g.Send(new GameEvent { type = "GuardOk", slot = Ob });
                g.Toast(LettersInTransit(st) ? "관측자의 경고 접수! 전달 중인 글자를 지킵니다" : "관측자의 경고 접수! 이번 파동은 막았어요", -1, GameLogic.ToneGood);
                g.Dirty = true;
            }
            else
            {
                g.Toast("아직 아니에요 — 경고등이 붉게 최고조로 깜빡일 때 누르세요", Ob, GameLogic.ToneWarn);
            }
        }

        public static void Tick(GameLogic g, float dt)
        {
            var st = g.S.st;
            switch (st.step)
            {
                case 0: TickLetters(g); TickSurge(g); break;
                case 2: if (AllAtRail(g)) Arrived(g); break;
                case 3: TickMash(g, dt); break;
                case 4: if (g.AllInZones()) g.Cleared(); break;
            }
            if (st.signalCut && g.Clock >= st.cutUntil)
            {
                st.signalCut = false;
                g.Dirty = true;
            }
        }

        /// <summary>내비게이터가 전달한 글자가 조작자에게 도착하는 순간을 처리한다(신호 차단 중에는 멈춤).</summary>
        private static void TickLetters(GameLogic g)
        {
            if (g.S.st.signalCut) return;
            g.DeliverLetters();
        }

        private static void TickSurge(GameLogic g)
        {
            var st = g.S.st;
            float t = g.Clock - st.cycleStart;
            int cycle = Mathf.FloorToInt(t / st.period);
            float ph = t - cycle * st.period;
            if (ph >= st.period - SurgeLead && st.resolvedCycle != cycle)
            {
                st.resolvedCycle = cycle;
                if (st.guardedCycle == cycle)
                {
                    st.signalCut = true;
                    st.cutUntil = g.Clock + CutDuration;
                    // 차단 중에는 글자가 멈춘다(도착 시각을 그만큼 미룬다)
                    foreach (var m in st.inflight) m.arriveClock += CutDuration;
                    g.Send(new GameEvent { type = "SignalCut" });
                }
                else if (LettersInTransit(st))
                {
                    GameLogic.ResetLetters(st);
                    g.S.stats.losses++;
                    g.Flash(GameLogic.ToneBad);
                    g.Send(new GameEvent { type = "Loss" });
                    g.Toast("정전 파동! 전달 중이던 글자가 소실됐다 — 통역의 글자 선택부터 다시", -1, GameLogic.ToneBad);
                }
                else
                {
                    g.Send(new GameEvent { type = "Flicker" });
                }
                g.Dirty = true;
            }
        }

        private static bool AllAtRail(GameLogic g)
        {
            var st = g.S.st;
            for (int i = 0; i < 4; i++)
            {
                var p = g.Pos(i);
                if (p == null) continue;
                if (Vector2.Distance(p.Value, st.rail[i].V) > 0.6f) return false;
            }
            return true;
        }

        private static void Arrived(GameLogic g)
        {
            var st = g.S.st;
            int node = st.railNode;
            if (node == StageMaps.Stage1ObstacleNode && !st.obstacleCleared)
            {
                st.step = 3;
                st.moveMode = 0;
                g.MeterA = 0f;
                for (int i = 0; i < 4; i++) st.lastMash[i] = -99f;
                g.Send(new GameEvent { type = "Obstacle" });
                g.Toast("쓰러진 집기가 길을 막았다! 네 명 모두 SPACE 연타로 함께 치우자", -1, GameLogic.ToneWarn);
            }
            else if (node == StageMaps.Stage1LobbyNode)
            {
                st.step = 4;
                st.moveMode = 1;
                g.Send(new GameEvent { type = "Lobby" });
                g.Toast("로비 도착 — 네 사람 모두 자기 색 원 안으로 들어가야 셔터가 열립니다", -1, GameLogic.ToneInfo);
            }
            else
            {
                int fork = StageMaps.Stage1ForkOfNode(node);
                st.fork = Mathf.Max(0, fork);
                st.step = 0;
                st.moveMode = 1;
                st.period = Mathf.Max(4.5f, 8f - st.fork * 1.5f);
                st.cycleStart = g.Clock;
                st.guardedCycle = -1;
                st.resolvedCycle = -1;
                st.signalCut = false;
                GameLogic.ResetLetters(st);
                st.word = st.forkWord[st.fork];
                g.Send(new GameEvent { type = "Fork", a = st.fork });
                g.Toast($"갈림길 {st.fork + 1}/3 — 표지판을 읽을 수 있는 건 통역뿐. 글자를 릴레이로 전달하세요", -1, GameLogic.ToneInfo);
            }
            UpdateStatus(g);
            g.Dirty = true;
        }

        private static void TickMash(GameLogic g, float dt)
        {
            var st = g.S.st;
            for (int i = 0; i < 4; i++)
                if (g.IsBot(i) && g.Clock - st.lastMash[i] > 0.4f) Mash(g, i);

            bool all = true;
            for (int i = 0; i < 4; i++) if (g.Clock - st.lastMash[i] > 2f) { all = false; break; }
            g.MeterA = Mathf.Max(0f, g.MeterA - dt * (all ? 3f : 16f));
        }

        private static void ClearObstacle(GameLogic g)
        {
            var st = g.S.st;
            if (st.obstacleCleared) return;
            st.obstacleCleared = true;
            g.MeterA = 0f;
            g.Flash(GameLogic.ToneGood);
            g.Send(new GameEvent { type = "ObstacleCleared" });
            g.Toast("집기를 치웠다! 함께 하니까 되네 — 계속 이동", -1, GameLogic.ToneGood);
            SetRailTo(g, StageMaps.Stage1ObstacleNode + 1);
            st.step = 2;
            UpdateStatus(g);
            g.Dirty = true;
        }

        private static int NextNodeAfterFork(int fork)
        {
            switch (fork)
            {
                case 0: return 2;
                case 1: return StageMaps.Stage1ObstacleNode;
                default: return StageMaps.Stage1LobbyNode;
            }
        }

        private static void SetRailTo(GameLogic g, int node)
        {
            var st = g.S.st;
            st.railNode = node;
            var p = new Vector2(StageMaps.Stage1NodeX[node], 0f);
            for (int i = 0; i < 4; i++) st.rail[i] = Vec2.Of(p + StageMaps.SlotOffsets[i]);
            st.moveMode = 2;
        }

        public static void UpdateStatus(GameLogic g)
        {
            var st = g.S.st;
            switch (st.step)
            {
                case 0: st.status = $"갈림길 {st.fork + 1}/3 · 통역 → 내비게이터 → 조작자 글자 릴레이 → 레버"; break;
                case 2: st.status = "이동 중…"; break;
                case 3: st.status = "장애물! 전원 SPACE 연타"; break;
                case 4: st.status = "로비 · 네 명 모두 자기 원 안으로"; break;
                default: st.status = ""; break;
            }
        }
    }
}
