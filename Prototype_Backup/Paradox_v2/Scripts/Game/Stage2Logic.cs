using UnityEngine;

namespace Paradox.Game
{
    /// <summary>
    /// Stage 2 — 베트남 · 침수 터널 · 3자 동시 조작 (시간 제한).
    ///  step 0: 수문 방 — 내비게이터(현지인)가 표지판의 펌프 모드를 글자 카드로 조작자에게 전달.
    ///          조작자는 콘솔을 그 모드로 맞추고 붙잡아(방벽 사수) 미터를 60 이상 유지, 큰 물살이 오면 손을 뗀다(회피).
    ///          통역은 물살 미터가 약할 때 GO 신호, 관측자는 그에 맞춰 수문을 당긴다. 둘의 당김이 1.5초 안에 겹치고
    ///          모드·방벽·물살 조건이 맞으면 수문이 열린다.
    ///  step 1: 갈림길 — 내비게이터만 표지판/미니맵으로 침수되는 갈래를 안다. 상부 통로의 원 4개에 동시 도착하면 클리어.
    /// </summary>
    public static class Stage2Logic
    {
        private const int Op = (int)GameRole.Operator, In = (int)GameRole.Interpreter, Na = (int)GameRole.Navigator, Ob = (int)GameRole.Observer;

        public const float SyncWindow = 1.5f;
        public const float CurrentPeriod = 7f;
        public const float CurrentSafe = 40f;
        public const float BarrierNeeded = 60f;
        public const float WavePeriod = 10f;
        public const float WaveWarnAt = 7.5f;

        public static float Current(float clock) => 50f + 50f * Mathf.Sin(2f * Mathf.PI * clock / CurrentPeriod);

        public static float WavePhase(StageState st, float clock) => clock - st.waveStart;

        public static void Start(GameLogic g)
        {
            var st = g.S.st;
            var rng = new Rng(st.seed);
            st.requiredMode = rng.Range(0, 3);
            st.exitUp = rng.Chance(0.5f);
            st.consoleMode = -1;
            st.holding = false;
            st.gatesOpen = false;
            st.waveStart = g.Clock;
            st.waveWarned = false;
            st.knockUntil = -99f;
            for (int i = 0; i < 4; i++) { st.pullClock[i] = -99f; st.lastHazardTick[i] = -99f; }
            st.word = StageDefs.ConsoleModes[st.requiredMode];
            GameLogic.ResetLetters(st);
            st.step = 0;
            st.moveMode = 1;
            g.MeterA = 0f;
            UpdateStatus(g);
        }

        public static void Apply(GameLogic g, Cmd c)
        {
            var st = g.S.st;
            int slot = c.slot;
            switch (c.type)
            {
                case "Pick":
                    if (slot != Na || st.step != 0) return;
                    g.PickToTray(slot, c.s);
                    break;

                case "SendLetters":
                    if (slot != Na || st.step != 0) return;
                    g.SendTray(Na, 1, new[] { Op }, 1.3f);
                    break;

                case "ClearLetters":
                    if (slot != Op && slot != Na) return;
                    GameLogic.ResetLetters(st);
                    g.Send(new GameEvent { type = "LettersCleared", slot = slot });
                    g.Dirty = true;
                    break;

                case "Console":
                    if (slot != Op || st.step != 0) return;
                    if (!g.Near(Op, "console", 2.0f)) { g.Toast("펌프 콘솔 앞으로 가세요", Op, GameLogic.ToneWarn); return; }
                    if (c.a < 0 || c.a > 2) return;
                    st.consoleMode = c.a;
                    g.Send(new GameEvent { type = "ConsoleSet", slot = Op, a = c.a });
                    g.Dirty = true;
                    break;

                case "Hold":
                    if (slot != Op || st.step != 0) return;
                    if (c.a != 0)
                    {
                        if (!g.Near(Op, "console", 2.0f)) { g.Toast("펌프 콘솔 앞으로 가세요", Op, GameLogic.ToneWarn); return; }
                        if (g.Clock < st.knockUntil) { g.Toast("물살에 밀려 아직 잡을 수 없어요", Op, GameLogic.ToneWarn); return; }
                        st.holding = true;
                    }
                    else st.holding = false;
                    g.Send(new GameEvent { type = "Hold", slot = Op, a = st.holding ? 1 : 0 });
                    g.Dirty = true;
                    break;

                case "Pull":
                    if (st.step != 0) return;
                    if (slot != Ob && slot != In) return;
                    {
                        string wheel = slot == Ob ? "wheelA" : "wheelB";
                        if (!g.Near(slot, wheel, 2.0f)) { g.Toast("수문 핸들 앞으로 가세요", slot, GameLogic.ToneWarn); return; }
                        st.pullClock[slot] = g.Clock;
                        g.Send(new GameEvent { type = "Pull", slot = slot });
                        Evaluate(g);
                        g.Dirty = true;
                    }
                    break;
            }
        }

        public static void OnSignal(GameLogic g, int slot, string id) { }

        private static void Evaluate(GameLogic g)
        {
            var st = g.S.st;
            float a = st.pullClock[Ob], b = st.pullClock[In];
            bool bothRecent = g.Clock - a <= SyncWindow && g.Clock - b <= SyncWindow;
            if (!bothRecent) return;

            float later = Mathf.Max(a, b);
            float cur = Current(later);
            string fail = null;
            if (st.consoleMode != st.requiredMode) fail = "펌프 모드가 다르다 — 표지판이 알려주는 모드로 콘솔을 맞춰야 한다";
            else if (!st.holding || g.MeterA < BarrierNeeded) fail = "방벽이 버티지 못했다 — 조작자가 콘솔을 붙잡아 방벽을 60 이상 유지해야 한다";
            else if (cur >= CurrentSafe) fail = "물살이 너무 셌다 — 통역의 물살 미터가 낮을 때 당겨야 한다";

            st.pullClock[Ob] = -99f;
            st.pullClock[In] = -99f;
            if (fail == null) Success(g);
            else Failure(g, fail);
        }

        private static void Success(GameLogic g)
        {
            var st = g.S.st;
            st.gatesOpen = true;
            st.step = 1;
            st.holding = false;
            st.penalty -= 15f;
            g.Flash(GameLogic.ToneGood);
            g.Send(new GameEvent { type = "GatesOpen" });
            g.Toast("수문이 열렸다! (+15초) 통로가 열린다 — 내비게이터의 안내로 물이 안 차는 쪽으로!", -1, GameLogic.ToneGood);
            UpdateStatus(g);
            g.Dirty = true;
        }

        private static void Failure(GameLogic g, string reason)
        {
            var st = g.S.st;
            st.gateFails++;
            g.Send(new GameEvent { type = "GateFail" });
            g.Penalty(6f, reason);
        }

        public static void Tick(GameLogic g, float dt)
        {
            var st = g.S.st;
            g.DeliverLetters();
            if (st.step == 0)
            {
                // 방벽 미터
                if (st.holding) g.MeterA = Mathf.Min(100f, g.MeterA + 30f * dt);
                else g.MeterA = Mathf.Max(0f, g.MeterA - 18f * dt);
                g.MeterB = Current(g.Clock);

                // 큰 물살(파도): 예고 → 타격
                float w = WavePhase(st, g.Clock);
                if (w >= WaveWarnAt && !st.waveWarned)
                {
                    st.waveWarned = true;
                    g.Send(new GameEvent { type = "WaveWarn" });
                    g.Toast("큰 물살이 온다! 콘솔에서 손을 떼고 피하세요 (E)", Op, GameLogic.ToneWarn);
                    g.Dirty = true;
                }
                if (w >= WavePeriod)
                {
                    if (st.holding)
                    {
                        st.holding = false;
                        g.MeterA = 0f;
                        st.knockUntil = g.Clock + 1.5f;
                        g.Send(new GameEvent { type = "WaveHit", slot = Op });
                        g.Toast("물살에 밀렸다! 방벽이 무너졌다 — 다시 잡으세요", -1, GameLogic.ToneBad);
                    }
                    else g.Send(new GameEvent { type = "WavePass" });
                    st.waveStart = g.Clock;
                    st.waveWarned = false;
                    g.Dirty = true;
                }

                // 혼자만 당긴 경우 — 동기화 실패
                CheckLonePull(g, Ob, In);
                CheckLonePull(g, In, Ob);
            }
            else if (st.step == 1)
            {
                if (g.Map != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var p = g.Pos(i);
                        if (p == null) continue;
                        foreach (var h in g.Map.hazards)
                        {
                            if (!h.Contains(p.Value)) continue;
                            if (g.Clock - st.lastHazardTick[i] < 2f) break;
                            st.lastHazardTick[i] = g.Clock;
                            st.penalty += 4f;
                            Vector2 dir = st.exitUp ? Vector2.up : Vector2.down;
                            g.Send(new GameEvent { type = "Knock", slot = i, pos = Vec2.Of(dir) });
                            g.Toast("여긴 침수 중! 되돌아가 (-4초)", i, GameLogic.ToneBad);
                            g.Dirty = true;
                            break;
                        }
                    }
                }
                if (g.AllInZones()) g.Cleared();
            }
        }

        private static void CheckLonePull(GameLogic g, int me, int other)
        {
            var st = g.S.st;
            float t = st.pullClock[me];
            if (t < -90f) return;
            if (g.Clock - t <= SyncWindow) return;
            // 내 당김이 창을 넘겼는데 상대는 안 당겼다
            st.pullClock[me] = -99f;
            Failure(g, $"{Roles.Name(me)} 혼자 당겼다 — 두 사람이 {SyncWindow:0.#}초 안에 함께 당겨야 한다");
        }

        public static void UpdateStatus(GameLogic g)
        {
            var st = g.S.st;
            st.status = st.step == 0
                ? "수문 방 · 콘솔 모드 + 방벽 유지 + 수문 2개 동시 당기기"
                : "갈림길 · 물이 안 차는 통로로 상부 통로 도착";
        }
    }
}
