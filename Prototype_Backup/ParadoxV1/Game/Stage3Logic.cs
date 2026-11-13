using UnityEngine;

namespace Paradox.Game
{
    /// <summary>
    /// Stage 3 — 국적 미정 · 유독가스 · 정보 교차검증 (시간 제한, 시야 흐림).
    ///  4 x 3 구역. 관측자(현지인)만 구역 색(초록/노랑/빨강)과 경고문을 읽고, 내비게이터는 미니맵으로 붕괴(막다른) 구역을,
    ///  통역은 감지기 수치(언어 무관)로 위험 구역을 안다. 각자 공용 보드에 표시하고, 세 표시가 겹치는 단 하나의 구역에
    ///  네 명이 모여 관측자·내비게이터가 확인 패널을 누르면 문이 열린다. 위험 구역에 있으면 시간이 깎인다.
    /// </summary>
    public static class Stage3Logic
    {
        private const int Op = (int)GameRole.Operator, In = (int)GameRole.Interpreter, Na = (int)GameRole.Navigator, Ob = (int)GameRole.Observer;
        public const int Sectors = 12;
        public const int PpmDanger = 50;

        public static void Start(GameLogic g)
        {
            var st = g.S.st;
            var rng = new Rng(st.seed);
            int start = StageMaps.Stage3StartSector;

            for (int i = 0; i < Sectors; i++)
            {
                int row = i / StageMaps.Stage3Cols;
                st.blocked[i] = row != 1 && i != start && rng.Chance(0.4f);
                float r = rng.Value();
                st.gas[i] = r < 0.36f ? 0 : (r < 0.66f ? 1 : 2);
                st.markObs[i] = st.markNav[i] = st.markInt[i] = false;
            }
            st.gas[start] = 0;
            st.blocked[start] = false;

            // 붕괴 구역이 너무 적으면 보강
            int blockedCount = 0;
            for (int i = 0; i < Sectors; i++) if (st.blocked[i]) blockedCount++;
            for (int tries = 0; blockedCount < 2 && tries < 20; tries++)
            {
                int i = rng.Range(0, Sectors);
                if (i / StageMaps.Stage3Cols == 1 || st.blocked[i]) continue;
                st.blocked[i] = true; blockedCount++;
            }

            for (int i = 0; i < Sectors; i++)
            {
                switch (st.gas[i])
                {
                    case 0: st.ppm[i] = rng.Chance(0.3f) ? rng.Range(55, 80) : rng.Range(8, 40); break;
                    case 1: st.ppm[i] = rng.Range(30, 75); break;
                    default: st.ppm[i] = rng.Range(70, 160); break;
                }
            }
            st.ppm[start] = rng.Range(10, 30);

            // 정답 구역: 출발 구역이 아닌, 세 조건을 모두 만족하는 곳 하나
            int safe = -1;
            for (int tries = 0; tries < 60 && safe < 0; tries++)
            {
                int i = rng.Range(0, Sectors);
                if (i == start) continue;
                if (st.gas[i] == 0 && !st.blocked[i] && st.ppm[i] < PpmDanger) safe = i;
            }
            if (safe < 0)
            {
                for (int tries = 0; tries < 60 && safe < 0; tries++)
                {
                    int i = rng.Range(0, Sectors);
                    if (i == start || st.blocked[i]) continue;
                    safe = i;
                }
                if (safe < 0) safe = (start + 1) % Sectors;
                st.gas[safe] = 0; st.blocked[safe] = false; st.ppm[safe] = rng.Range(10, 35);
            }
            st.safeSector = safe;

            // 다른 구역은 반드시 한 조건 이상 실패하게 만든다(정답 유일성)
            for (int i = 0; i < Sectors; i++)
            {
                if (i == safe || i == start) continue;
                if (st.gas[i] == 0 && !st.blocked[i] && st.ppm[i] < PpmDanger)
                {
                    if (i / StageMaps.Stage3Cols != 1 && rng.Chance(0.5f)) st.blocked[i] = true;
                    else st.ppm[i] = rng.Range(55, 85);
                }
            }

            st.confirmObs = st.confirmNav = false;
            st.confirmObsSector = st.confirmNavSector = -1;
            st.wrongConfirms = 0;
            for (int i = 0; i < 4; i++) st.lastGasTick[i] = -99f;
            st.word = "";
            st.step = 0;
            st.moveMode = 1;
            st.status = "세 표시가 겹치는 구역으로 모두 이동 → 관측자·내비게이터가 확인 패널";
        }

        public static void Apply(GameLogic g, Cmd c)
        {
            var st = g.S.st;
            int slot = c.slot;
            switch (c.type)
            {
                case "Mark":
                    {
                        int idx = c.a;
                        if (idx < 0 || idx >= Sectors || idx == StageMaps.Stage3StartSector) return;
                        bool[] layer = slot == Ob ? st.markObs : (slot == Na ? st.markNav : (slot == In ? st.markInt : null));
                        if (layer == null) { g.Toast("조작자는 표시할 수 없어요 — 친구들의 표시가 겹치는 곳으로 안내하세요", slot, GameLogic.ToneWarn); return; }
                        layer[idx] = !layer[idx];
                        g.Send(new GameEvent { type = "Mark", slot = slot, a = idx });
                        g.Dirty = true;
                    }
                    break;

                case "Confirm":
                    {
                        if (slot != Ob && slot != Na) return;
                        var p = g.Pos(slot);
                        if (p == null || g.Map == null) return;
                        int sector = g.Map.SectorAt(p.Value);
                        if (sector < 0) { g.Toast("구역 안에서 확인 패널을 누르세요", slot, GameLogic.ToneWarn); return; }
                        if (sector == StageMaps.Stage3StartSector) { g.Toast("출발 구역에는 탈출문이 없어요", slot, GameLogic.ToneWarn); return; }
                        string prop = (slot == Ob ? "confObs" : "confNav") + sector;
                        if (!g.Near(slot, prop, 1.8f)) { g.Toast("내 확인 패널 앞으로 가세요 (문 옆)", slot, GameLogic.ToneWarn); return; }
                        if (slot == Ob) { st.confirmObs = true; st.confirmObsSector = sector; }
                        else { st.confirmNav = true; st.confirmNavSector = sector; }
                        g.Send(new GameEvent { type = "Confirm", slot = slot, a = sector });
                        g.Toast($"{Roles.Name(slot)}이(가) {StageMaps.SectorName(sector)} 구역 확인 패널을 눌렀다", -1, GameLogic.ToneInfo);
                        g.Dirty = true;
                        TryOpen(g);
                    }
                    break;
            }
        }

        public static void OnSignal(GameLogic g, int slot, string id) { }

        public static void Tick(GameLogic g, float dt)
        {
            var st = g.S.st;
            if (g.Map == null) return;

            for (int i = 0; i < 4; i++)
            {
                var p = g.Pos(i);
                if (p == null) continue;
                int s = g.Map.SectorAt(p.Value);
                if (s < 0) continue;
                int level = st.gas[s];
                if (level > 0 && g.Clock - st.lastGasTick[i] > 3f)
                {
                    st.lastGasTick[i] = g.Clock;
                    float pen = level == 2 ? 4f : 1.5f;
                    st.penalty += pen;
                    g.Send(new GameEvent { type = "Cough", slot = i, a = level });
                    g.Toast(level == 2 ? $"콜록! 여긴 위험 구역이다 (-{pen:0}초)" : $"기침이 난다… 주의 구역 (-{pen:0}초)", i, GameLogic.ToneBad);
                    g.Dirty = true;
                }
            }

            // 확인한 사람이 그 구역을 떠나면 확인이 풀린다
            if (st.confirmObs) { var p = g.Pos(Ob); if (p != null && g.Map.SectorAt(p.Value) != st.confirmObsSector) { st.confirmObs = false; st.confirmObsSector = -1; g.Dirty = true; } }
            if (st.confirmNav) { var p = g.Pos(Na); if (p != null && g.Map.SectorAt(p.Value) != st.confirmNavSector) { st.confirmNav = false; st.confirmNavSector = -1; g.Dirty = true; } }

            if (st.confirmObs && st.confirmNav) TryOpen(g);
        }

        private static readonly Vector2[] TmpZones = new Vector2[4];

        private static void TryOpen(GameLogic g)
        {
            var st = g.S.st;
            if (!st.confirmObs || !st.confirmNav) return;
            if (st.confirmObsSector != st.confirmNavSector) return;
            int s = st.confirmObsSector;
            StageMaps.SectorZones(s, TmpZones);
            for (int i = 0; i < 4; i++)
            {
                var p = g.Pos(i);
                if (p == null || Vector2.Distance(p.Value, TmpZones[i]) > g.Map.zoneRadius) return;
            }

            if (s == st.safeSector)
            {
                g.Send(new GameEvent { type = "DoorOpen", a = s });
                g.Cleared();
            }
            else
            {
                st.wrongConfirms++;
                st.confirmObs = st.confirmNav = false;
                st.confirmObsSector = st.confirmNavSector = -1;
                g.Send(new GameEvent { type = "DoorLocked", a = s });
                g.Penalty(10f, "문이 열리지 않는다… 세 표시가 모두 겹치는 구역이 아니다");
            }
        }
    }
}
