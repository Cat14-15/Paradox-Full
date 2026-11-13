using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Paradox.Game
{
    /// <summary>
    /// Stage 1 — 필리핀 · 침수된 박물관. 서버(호스트)에서만 판정한다.
    ///  step 0: 관문 1 계산 자물쇠(안내데스크) — 조작자 제어반 / 통역 벽보 / 관측자 경고등
    ///  step 1: 이동 구간(침수 현황판, 내비게이터) + 관문 2 방향 자물쇠 4라운드(모자이크방) — 레버·벽화·색상등·배전함
    ///  step 2: 최종 집결 — 분수 주위를 도는 자기 색 원 안에 4명이 0.6초 동시에 머물면 클리어
    /// 정적 도우미(Parity/Table1/Table2/Row/BrailleDots/Flood/IsDanger/PathEnd/GatherCenter/InGather/…)는 클라이언트·봇·테스트가 같은 이름으로 쓴다.
    /// </summary>
    public static class Stage1Logic
    {
        private const int Op = (int)GameRole.Operator, In = (int)GameRole.Interpreter, Na = (int)GameRole.Navigator, Ob = (int)GameRole.Observer;

        // ---- 고정 상수 ----
        public const float TimeLimit = 720f;        // 침수까지 12분
        public const float BriefTimeout = 30f;      // 브리핑 게이트: 스테이지 시작 후 이 시간이 지나면 전원 Brief 가 없어도 타이머 시작
        public const float BotBriefDelay = 1.5f;    // 봇이 자동으로 Brief 를 보내는 지연
        public const float Gate1Penalty = 10f;      // 관문 1 오답(v2: 15 → 10)
        public const float Gate1Lock = 3f;          // 관문 1 오답 뒤 키패드 잠금(초)
        public const float Gate2Penalty = 15f;      // 관문 2 "안전" 오판(v2: 20 → 15)
        public const float NearReach = 2.6f;        // 서버 근접 조건(봇 동일). 클라 Reach 1.6 + 네트워크 위치 지연(≈0.65)을 흡수(LAN 감사 C-5)
        public const float GatherRadius = 0.95f;    // 집결 원 반지름
        public const float AllInHold = 0.6f;        // 4명 동시 유지 시간
        public const float GatherOmega = 0.11f;     // 회전 각속도(rad/s) — 약 57초에 한 바퀴
        public const float GatherR = 1.4f;          // 기본 반지름(메인 맵 그림의 홀 나침반 문양 둘레 — 홀 아래쪽 벽 안에 들어오는 최대)
        public const float GatherWobble = 0.25f;    // 반지름 출렁임
        public const float GatherWobbleFreq = 0.35f;
        public const float DangerFlood = 70f;       // 위험 구역 기준 침수율(%)
        public const float DangerToastEvery = 5f;   // 위험 구역 경고 토스트 간격
        public const float PingNudgeAfter = 40f;    // A3: 관문 1 타이머 시작 뒤 이 시간 동안 핑이 없으면 내비에게 한 번 안내
        /// <summary>C4: 남은 시간 경고 시점(분). 비트 1&lt;&lt;i 가 StageState.timeWarned 에 기록된다.</summary>
        public static readonly int[] TimeWarnMinutes = { 6, 3, 1 };
        public const int Rounds = 4;
        public const int Terminals = 8;
        public const int Zones = 8;

        // ---- 표 ----
        /// <summary>관문 1 매뉴얼: [홀짝 0 짝 1 홀][색 0 빨 1 노 2 초 3 파] → 숫자.</summary>
        public static readonly int[][] Table1 =
        {
            new[] { 5, 0, 9, 1 },
            new[] { 3, 7, 2, 0 }
        };

        /// <summary>관문 2 매뉴얼: [행 Row(점자 숫자)][색] → 방향 0▲ 1▼ 2◀ 3▶. 행 {1,4}, {2,7}, {5,3}, {6,9}.</summary>
        public static readonly int[][] Table2 =
        {
            new[] { 0, 1, 0, 1 },   // 1 · 4 = ▲▼▲▼
            new[] { 2, 3, 2, 3 },   // 2 · 7 = ◀▶◀▶
            new[] { 2, 0, 3, 0 },   // 5 · 3 = ◀▲▶▲
            new[] { 0, 3, 2, 1 }    // 6 · 9 = ▲▶◀▼
        };

        /// <summary>Table2의 각 행에 속한 점자 숫자 쌍(매뉴얼 카드 표시용).</summary>
        public static readonly int[][] RowDigits =
        {
            new[] { 1, 4 }, new[] { 2, 7 }, new[] { 5, 3 }, new[] { 6, 9 }
        };

        /// <summary>관문 2에서 쓰는 점자 숫자 후보.</summary>
        public static readonly int[] BrailleDigits = { 1, 2, 3, 4, 5, 6, 7, 9 };

        private static readonly string[] ColorNames = { "빨강", "노랑", "초록", "파랑" };
        private static readonly string[] DirGlyphs = { "▲", "▼", "◀", "▶" };
        private static readonly string[] DirNames = { "위", "아래", "왼쪽", "오른쪽" };
        private static readonly string[] ZoneNames =
        {
            "아트리움 통로", "안내데스크", "관리실", "모자이크방", "수족관방", "연못정원방", "지하 창고", "옥상 계단"
        };

        // 침수 기본값(시드로 ±10% / ±20% 흔든다)
        private static readonly float[] FloodBaseDefault = { 10f, 15f, 20f, 25f, 45f, 40f, 60f, 30f };
        private static readonly float[] FloodRateDefault = { 0.020f, 0.020f, 0.030f, 0.030f, 0.110f, 0.120f, 0.080f, 0.050f };

        // ==================================================================
        // 공용 정적 도우미
        // ==================================================================

        public static int Parity(int n) => (Mathf.Abs(n) & 1) == 0 ? 0 : 1;

        /// <summary>점자 숫자가 속한 Table2 행(0~3), 없으면 -1.</summary>
        public static int Row(int digit)
        {
            switch (digit)
            {
                case 1: case 4: return 0;
                case 2: case 7: return 1;
                case 5: case 3: return 2;
                case 6: case 9: return 3;
                default: return -1;
            }
        }

        /// <summary>표준 6점 점자(1·2·3 왼쪽 열 위→아래, 4·5·6 오른쪽 열)에서 켜진 점 번호. 모르는 숫자면 빈 배열.</summary>
        public static int[] BrailleDots(int digit)
        {
            switch (digit)
            {
                case 1: return new[] { 1 };
                case 2: return new[] { 1, 2 };
                case 3: return new[] { 1, 4 };
                case 4: return new[] { 1, 4, 5 };
                case 5: return new[] { 1, 5 };
                case 6: return new[] { 1, 2, 4 };
                case 7: return new[] { 1, 2, 4, 5 };
                case 8: return new[] { 1, 2, 5 };
                case 9: return new[] { 2, 4 };
                case 0: return new[] { 2, 4, 5 };
                default: return new int[0];
            }
        }

        /// <summary>점 번호 집합과 정확히 일치하는 점자 숫자(관문 2 후보 중), 없으면 -1.</summary>
        public static int DigitOfDots(ICollection<int> dots)
        {
            if (dots == null || dots.Count == 0) return -1;
            foreach (int d in BrailleDigits)
            {
                var pat = BrailleDots(d);
                if (pat.Length != dots.Count) continue;
                bool same = true;
                foreach (int p in pat) if (!dots.Contains(p)) { same = false; break; }
                if (same) return d;
            }
            return -1;
        }

        /// <summary>"점자 1,2,4" 형태의 채팅 문구(조작자 팝업의 점 번호 칩이 만든다).</summary>
        public static string DotsChat(int digit)
        {
            var dots = BrailleDots(digit);
            var sb = new StringBuilder("점자 ");
            for (int i = 0; i < dots.Length; i++) { if (i > 0) sb.Append(','); sb.Append(dots[i]); }
            return sb.ToString();
        }

        public static string ColorName(int c) => c >= 0 && c < 4 ? ColorNames[c] : "?";
        public static string DirGlyph(int d) => d >= 0 && d < 4 ? DirGlyphs[d] : "?";
        public static string DirName(int d) => d >= 0 && d < 4 ? DirNames[d] : "?";
        public static string ZoneName(int z) => z >= 0 && z < Zones ? ZoneNames[z] : "?";
        public static string RoomName(int room) => StageMaps.RoomName(room);
        public static Vector2 RoomDoor(int room) => StageMaps.RoomDoor(room);

        /// <summary>연산식 문자열("13 × 7 − 4").</summary>
        public static string CalcExpr(StageState st) => $"{st.calcA} × {st.calcB} {(st.calcOp == 0 ? "+" : "−")} {st.calcC}";

        /// <summary>구역 침수율(0~100). 시간의 함수라 서버·클라이언트가 각자 계산한다. 브리핑 게이트(timerRunning) 전에는 기본값에서 정지.</summary>
        public static float Flood(StageState st, int zone, float clock)
        {
            if (st == null || st.floodBase == null || zone < 0 || zone >= st.floodBase.Length || st.floodRate == null || zone >= st.floodRate.Length) return 0f;
            float t = st.timerRunning ? Mathf.Max(0f, clock - st.startClock) : 0f;
            return Mathf.Clamp(st.floodBase[zone] + st.floodRate[zone] * t, 0f, 100f);
        }

        /// <summary>침수율 색 단계: 0 초록(&lt;40) 1 노랑(&lt;70) 2 빨강(≥70).</summary>
        public static int FloodLevel(float pct) => pct >= DangerFlood ? 2 : (pct >= 40f ? 1 : 0);

        /// <summary>위험 구역인가: 수족관방(4)·연못정원방(5)의 침수율이 70 이상.</summary>
        public static bool IsDanger(StageState st, int room, float clock)
            => (room == StageMaps.RoomAquarium || room == StageMaps.RoomGarden) && Flood(st, room, clock) >= DangerFlood;

        /// <summary>
        /// 현황판 스냅샷(기획 6절 "가서 봐야 아는", D-A4): 내비가 마지막으로 현황판을 연 시각(boardClock)의 구역 침수율. 열기 전에는 0.
        /// 역할 패널·미니맵은 실시간 대신 이 값을 쓴다 — 현황판 팝업만 실시간(열어서 보는 것이 곧 확인). 봇의 침수 회피는 실제 값을 쓴다.
        /// </summary>
        public static float FloodSnapshot(StageState st, int zone)
            => st != null && st.boardOpened ? Flood(st, zone, st.boardClock) : 0f;

        /// <summary>스냅샷 기준 위험 구역(수족관방·연못정원방의 스냅샷 침수율 ≥ 70). 현황판을 열기 전에는 항상 false.</summary>
        public static bool IsDangerSnapshot(StageState st, int room)
            => (room == StageMaps.RoomAquarium || room == StageMaps.RoomGarden) && FloodSnapshot(st, room) >= DangerFlood;

        /// <summary>마지막으로 현황판을 확인한 뒤 지난 시간(초, 클라이언트는 Session.Clock 을 넘긴다). 열기 전이면 -1.</summary>
        public static float SecondsSinceBoard(StageState st, float clock)
            => st == null || !st.boardOpened ? -1f : Mathf.Max(0f, clock - st.boardClock);

        /// <summary>비상 전원 안전 라인 번호(1~8) = ladderSafe + 1. 침수 현황판이 알려주고, 배전함은 boardOpened 일 때만 표시한다.</summary>
        public static int SafeLine(StageState st) => st == null ? 0 : Mathf.Clamp(st.ladderSafe, 0, Terminals - 1) + 1;

        /// <summary>관문 1 키패드가 오답 잠금 중인가(서버 클록 기준, 클라이언트는 Session.Clock 을 넘긴다).</summary>
        public static bool KeypadLocked(StageState st, float clock) => st != null && clock < st.lockUntil1;

        public static int NextIndex(int round, int terminal, int dir) => round * 32 + terminal * 4 + dir;

        /// <summary>chosenDir을 따라 r라운드 뒤에 서 있는 단자(r = 0이면 시작 단자). 그 라운드가 아직 안 끝났으면 -1.</summary>
        public static int PathAt(StageState st, int r)
        {
            if (st == null || st.ladderNext == null || st.ladderNext.Length < 128 || st.chosenDir == null) return -1;
            int t = st.ladderStart;
            for (int i = 0; i < r; i++)
            {
                if (i >= st.chosenDir.Length) return -1;
                int d = st.chosenDir[i];
                if (d < 0 || d > 3) return -1;
                t = st.ladderNext[NextIndex(i, t, d)];
                if (t < 0 || t >= Terminals) return -1;
            }
            return t;
        }

        /// <summary>4라운드를 모두 마친 뒤 도착하는 단자. 미완이면 -1.</summary>
        public static int PathEnd(StageState st) => PathAt(st, Rounds);

        /// <summary>정답 경로의 r라운드 뒤 단자(검증용).</summary>
        public static int AnswerPathAt(StageState st, int r)
        {
            int t = st.ladderStart;
            for (int i = 0; i < r; i++) t = st.ladderNext[NextIndex(i, t, st.answerDir[i])];
            return t;
        }

        /// <summary>사다리 불변식 검증: "4개 전부 정답 ⇔ PathEnd == ladderSafe" 이 256가지 조합 전부에서 성립하는가.</summary>
        public static bool ValidateLadder(StageState st)
        {
            if (st == null || st.ladderNext == null || st.ladderNext.Length != 128 || st.answerDir == null || st.answerDir.Length != Rounds) return false;
            if (st.ladderStart < 0 || st.ladderStart >= Terminals || st.ladderSafe < 0 || st.ladderSafe >= Terminals) return false;
            foreach (int v in st.ladderNext) if (v < 0 || v >= Terminals) return false;
            for (int r = 0; r < Rounds; r++) if (st.answerDir[r] < 0 || st.answerDir[r] > 3) return false;
            if (st.terminalZone == null || st.terminalZone.Length != Terminals || st.terminalZone[st.ladderSafe] != 0) return false;
            var seen = new bool[Zones];
            foreach (int z in st.terminalZone) { if (z < 0 || z >= Zones || seen[z]) return false; seen[z] = true; }

            var dirs = new int[Rounds];
            for (int combo = 0; combo < 256; combo++)
            {
                bool allRight = true;
                for (int r = 0; r < Rounds; r++)
                {
                    dirs[r] = (combo >> (2 * r)) & 3;
                    if (dirs[r] != st.answerDir[r]) allRight = false;
                }
                int t = st.ladderStart;
                for (int r = 0; r < Rounds; r++) t = st.ladderNext[NextIndex(r, t, dirs[r])];
                if ((t == st.ladderSafe) != allRight) return false;
            }
            return true;
        }

        /// <summary>최종 집결 원의 중심(슬롯별, 시간의 함수). 분수 주위를 천천히 돌며 반지름이 출렁인다.</summary>
        public static Vector2 GatherCenter(int slot, StageState st, float clock)
        {
            float t = st != null ? Mathf.Max(0f, clock - st.gatherStart) : 0f;
            float theta = Mathf.Deg2Rad * (90f * slot + 45f) + GatherOmega * t;
            float r = GatherR + GatherWobble * Mathf.Sin(GatherWobbleFreq * t + slot);
            return StageMaps.GatherOrigin + new Vector2(r * Mathf.Cos(theta), r * Mathf.Sin(theta));
        }

        public static bool InGather(int slot, Vector2 pos, StageState st, float clock)
            => Vector2.Distance(pos, GatherCenter(slot, st, clock)) <= GatherRadius;

        /// <summary>단계별로 이 역할이 담당하는 오브젝트 id("" = 없음). 상단 팀 목록·봇 목표에 쓴다.</summary>
        public static string RolePropId(int slot, StageState st)
        {
            if (st == null) return "";
            if (st.step == 0)
            {
                switch (slot) { case Op: return "panel"; case In: return "poster"; case Ob: return "lamp"; case Na: return "board"; }
            }
            else if (st.step == 1)
            {
                switch (slot) { case Op: return "lever"; case In: return "mural"; case Ob: return "colorlamp"; case Na: return st.boardOpened ? "junction" : "board"; }
            }
            return "";
        }

        /// <summary>담당 미션이 끝났는가(발광을 끄는 기준).</summary>
        public static bool RoleMissionDone(int slot, string propId, StageState st)
        {
            if (st == null) return true;
            switch (propId)
            {
                case "panel": case "poster": case "lamp": return st.gate1Done;
                case "board": return false;
                case "lever": case "mural": case "colorlamp": case "junction": return st.gate2Done;
                default: return true;
            }
        }

        public static string PropName(string id)
        {
            switch (id)
            {
                case "panel": return "비상 제어반";
                case "poster": return "코팅된 벽보";
                case "lamp": return "경고등 패널";
                case "board": return "침수 현황판";
                case "lever": return "방향 레버반";
                case "mural": return "나침반 벽화 매뉴얼";
                case "colorlamp": return "색상 표시등";
                case "junction": return "배전함";
                case "fountain": return "분수";
                case "door3": return "모자이크방 문";
                default: return id ?? "";
            }
        }

        // ==================================================================
        // 생성
        // ==================================================================

        public static void Start(GameLogic g)
        {
            var st = g.S.st;
            var rng = new Rng(st.seed);

            // 관문 1
            st.calcA = rng.Range(11, 20);
            st.calcB = rng.Range(3, 10);
            st.calcC = rng.Range(1, 10);
            st.calcOp = rng.Range(0, 2);
            st.calcResult = st.calcA * st.calcB + (st.calcOp == 0 ? st.calcC : -st.calcC);
            st.lampColor = rng.Range(0, 4);
            st.calcAnswer = Table1[Parity(st.calcResult)][st.lampColor];
            st.calcFails = 0;
            st.calcInput = "";
            st.lastWrongDigit = -1;
            st.pingNudged = false;
            st.timeWarned = 0;

            // 침수
            st.floodBase = new float[Zones];
            st.floodRate = new float[Zones];
            for (int z = 0; z < Zones; z++)
            {
                st.floodBase[z] = FloodBaseDefault[z] * (0.9f + 0.2f * rng.Value());
                st.floodRate[z] = FloodRateDefault[z] * (0.8f + 0.4f * rng.Value());
            }
            // B-05: 침수 진행 순서를 시드로 섞는다 — 지름길 방 4·5 의 base/rate 를 50% 확률로 맞바꾸고, 관문 방 1·2·3 의 rate 를 순열로 섞는다
            if (rng.Chance(0.5f))
            {
                float tb = st.floodBase[StageMaps.RoomAquarium]; st.floodBase[StageMaps.RoomAquarium] = st.floodBase[StageMaps.RoomGarden]; st.floodBase[StageMaps.RoomGarden] = tb;
                float tr = st.floodRate[StageMaps.RoomAquarium]; st.floodRate[StageMaps.RoomAquarium] = st.floodRate[StageMaps.RoomGarden]; st.floodRate[StageMaps.RoomGarden] = tr;
            }
            int[] gateRooms = { StageMaps.RoomDesk, StageMaps.RoomOffice, StageMaps.RoomMosaic };
            for (int i = gateRooms.Length - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                float t = st.floodRate[gateRooms[i]]; st.floodRate[gateRooms[i]] = st.floodRate[gateRooms[j]]; st.floodRate[gateRooms[j]] = t;
            }
            // 아트리움은 항상 가장 안전
            float minBase = float.MaxValue, minRate = float.MaxValue;
            for (int z = 1; z < Zones; z++) { minBase = Mathf.Min(minBase, st.floodBase[z]); minRate = Mathf.Min(minRate, st.floodRate[z]); }
            st.floodBase[0] = Mathf.Min(st.floodBase[0], minBase - 1f);
            st.floodRate[0] = Mathf.Min(st.floodRate[0], minRate * 0.9f);

            // 관문 2
            st.gate2Attempts = 0;
            GenerateGate2(st, st.seed + 7);

            // 소통 상태
            st.colorReport = -1; st.colorReportClock = -99f;
            st.numberCard = -1; st.numberCardClock = -99f;
            st.dirCard = -1; st.dirCardClock = -99f;
            st.lastPingRoom = -1; st.lastPingClock = -99f;
            st.boardOpened = false;

            // 체크포인트 1이면 관문 1을 건너뛴다
            if (st.checkpoint >= 1)
            {
                st.gate1Done = true;
                st.step = 1;
            }
            else
            {
                st.gate1Done = false;
                st.step = 0;
            }
            st.gate2Done = false;
            st.gatherStart = 0f;
            st.allInSince = -1f;
            st.moveMode = 1;
            UpdateStatus(g);
        }

        /// <summary>라운드·관문이 바뀌면 이전 색 신고는 무효 — 통역 팝업이 "미확인"으로 돌아간다(관측자는 현재 라운드 색만 볼 수 있다).</summary>
        private static void ClearColorReport(StageState st) { st.colorReport = -1; st.colorReportClock = -99f; }

        /// <summary>관문 2의 점자 숫자·색·정답 방향·사다리를 시드로 만든다. 4개 전부 정답 ⇔ ladderSafe 도착.</summary>
        public static void GenerateGate2(StageState st, int seed)
        {
            var rng = new Rng(seed);
            st.brailleDigit = new int[Rounds];
            st.lampColor2 = new int[Rounds];
            st.answerDir = new int[Rounds];
            st.chosenDir = new[] { -1, -1, -1, -1 };
            for (int r = 0; r < Rounds; r++)
            {
                st.brailleDigit[r] = BrailleDigits[rng.Range(0, BrailleDigits.Length)];
                st.lampColor2[r] = rng.Range(0, 4);
                st.answerDir[r] = Table2[Row(st.brailleDigit[r])][st.lampColor2[r]];
            }

            // 정답 경로 p0..p4 (연속 라운드에서 같은 단자에 머물지 않게)
            var path = new int[Rounds + 1];
            path[0] = rng.Range(0, Terminals);
            for (int r = 1; r <= Rounds; r++)
            {
                int t = rng.Range(0, Terminals);
                if (t == path[r - 1]) t = (t + 1 + rng.Range(0, Terminals - 1)) % Terminals;
                path[r] = t;
            }
            st.ladderStart = path[0];
            st.ladderSafe = path[Rounds];

            st.ladderNext = new int[Rounds * 32];
            for (int r = 0; r < Rounds; r++)
                for (int t = 0; t < Terminals; t++)
                    for (int d = 0; d < 4; d++)
                    {
                        int next;
                        if (t == path[r] && d == st.answerDir[r]) next = path[r + 1];
                        else
                        {
                            // 정답 다음 단자(p_{r+1})만 아니면 된다 — 어디서 틀리든 정답 경로로 다시 합류할 수 없다
                            next = rng.Range(0, Terminals - 1);
                            if (next >= path[r + 1]) next++;
                        }
                        st.ladderNext[NextIndex(r, t, d)] = next;
                    }

            // 단자 라벨: 안전 단자 = 0 "아트리움 통로", 나머지 1~7을 섞어 배정
            st.terminalZone = new int[Terminals];
            var rest = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
            for (int i = rest.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                int tmp = rest[i]; rest[i] = rest[j]; rest[j] = tmp;
            }
            int k = 0;
            for (int t = 0; t < Terminals; t++) st.terminalZone[t] = t == st.ladderSafe ? 0 : rest[k++];

            st.round = 0;
            st.dirCard = -1; st.dirCardClock = -99f;
        }

        // ==================================================================
        // 명령
        // ==================================================================

        public static void Apply(GameLogic g, Cmd c)
        {
            var st = g.S.st;
            int slot = c.slot;
            switch (c.type)
            {
                case "Digit": Digit(g, slot, c.a); break;
                case "DigitClear": DigitClear(g, slot); break;
                case "DigitConfirm": DigitConfirm(g, slot); break;
                case "Color": ReportColor(g, slot, c.a); break;
                case "Card": Card(g, slot, c.a); break;
                case "DirCard": DirCard(g, slot, c.a); break;
                case "Lever": Lever(g, slot, c.a); break;
                case "Judge": Judge(g, slot, c.a); break;
                case "Ping": Ping(g, slot, c.a); break;
                case "OpenBoard": OpenBoard(g, slot); break;
            }
        }

        /// <summary>휠 신호(전원). 규칙에 영향을 주는 신호는 없다 — 통계·이벤트·봇 훅은 GameLogic이 처리한다.</summary>
        public static void OnSignal(GameLogic g, int slot, string id) { }

        // ---- 관문 1 ----

        private static void Digit(GameLogic g, int slot, int a)
        {
            var st = g.S.st;
            if (slot != Op || st.gate1Done || a < 0 || a > 9) return;
            if (!g.Near(slot, "panel", NearReach)) { g.Toast("제어반 앞에서 눌러야 해요", slot, GameLogic.ToneWarn); return; }
            if (KeypadLocked(st, g.Clock)) { g.Toast($"키패드 잠금 중 — {Mathf.CeilToInt(st.lockUntil1 - g.Clock)}초 뒤에 다시", slot, GameLogic.ToneWarn); return; }
            st.calcInput = a.ToString();
            g.Dirty = true;
        }

        private static void DigitClear(GameLogic g, int slot)
        {
            var st = g.S.st;
            if (slot != Op || st.gate1Done) return;
            if (st.calcInput.Length == 0) return;
            st.calcInput = "";
            g.Dirty = true;
        }

        private static void DigitConfirm(GameLogic g, int slot)
        {
            var st = g.S.st;
            if (slot != Op || st.gate1Done) return;
            if (!g.Near(slot, "panel", NearReach)) { g.Toast("제어반 앞에서 눌러야 해요", slot, GameLogic.ToneWarn); return; }
            if (KeypadLocked(st, g.Clock)) { g.Toast($"키패드 잠금 중 — {Mathf.CeilToInt(st.lockUntil1 - g.Clock)}초 뒤에 다시", slot, GameLogic.ToneWarn); return; }
            if (string.IsNullOrEmpty(st.calcInput)) { g.Toast("숫자를 먼저 누르세요", slot, GameLogic.ToneWarn); return; }
            int v;
            if (!int.TryParse(st.calcInput, out v)) { st.calcInput = ""; g.Dirty = true; return; }

            if (v == st.calcAnswer)
            {
                st.gate1Done = true;
                st.checkpoint = 1;
                st.step = 1;
                st.calcInput = "";
                st.lastWrongDigit = -1;
                st.lastPingRoom = -1;
                ClearColorReport(st);   // 경고등 신고는 관문 2 색상등에 쓸 수 없다
                g.Flash(GameLogic.ToneGood);
                g.Send(new GameEvent { type = "GateOpen", slot = Op, a = 1 });
                g.Toast("잠금 해제! 모자이크방 문이 열렸다", -1, GameLogic.ToneGood);
                g.Toast("이렇게 서로 다른 정보를 모아야 퍼즐이 풀려요. 이제부터는 스스로 해보세요!", -1, GameLogic.ToneInfo);
            }
            else
            {
                st.calcFails++;
                st.calcInput = "";
                st.lastWrongDigit = v;                 // B2: 통역이 "무엇을 넣었는지" 볼 수 있게(벽보 팝업 status)
                st.lockUntil1 = g.Clock + Gate1Lock;   // 무차별 입력 억제: 3초 잠금(팝업은 유지, 입력만 거부)
                g.S.stats.wrongInputs++;
                g.Penalty(Gate1Penalty, "오답! 경보가 울린다");
                g.Send(new GameEvent { type = "Alarm", slot = Op, s = "gate1", a = 1 });
                g.Toast(WrongDigitText(v), In, GameLogic.ToneWarn);
            }
            UpdateStatus(g);
            g.Dirty = true;
        }

        // ---- 소통: 색 신고 / 카드 ----

        private static void ReportColor(GameLogic g, int slot, int a)
        {
            var st = g.S.st;
            if (slot != Ob || a < 0 || a > 3) return;
            bool nearLamp = !st.gate1Done && g.Near(slot, "lamp", NearReach);
            bool nearColorLamp = st.gate1Done && !st.gate2Done && g.Near(slot, "colorlamp", NearReach);
            if (!nearLamp && !nearColorLamp) { g.Toast("램프 앞에서 신고해야 해요", slot, GameLogic.ToneWarn); return; }
            st.colorReport = a;
            st.colorReportClock = g.Clock;
            g.Send(new GameEvent { type = "Color", slot = Ob, a = a, target = In, s = ColorName(a) });
            g.Dirty = true;
        }

        private static void Card(GameLogic g, int slot, int a)
        {
            var st = g.S.st;
            if (slot != In || a < 0 || a > 9) return;
            if (st.gate1Done) { g.Toast("관문 1은 이미 열렸어요 — 이제 방향 카드 차례", slot, GameLogic.ToneInfo); return; }
            if (!g.Near(slot, "poster", NearReach)) { g.Toast("벽보 앞에서 보내야 해요", slot, GameLogic.ToneWarn); return; }
            st.numberCard = a;
            st.numberCardClock = g.Clock;
            st.lastWrongDigit = -1;   // 새 카드가 가면 이전 오답 안내는 지운다(B2)
            g.S.stats.cards++;
            g.Send(new GameEvent { type = "NumberCard", slot = In, a = a, target = Op, s = a.ToString() });
            g.Dirty = true;
        }

        private static void DirCard(GameLogic g, int slot, int a)
        {
            var st = g.S.st;
            if (slot != In || a < 0 || a > 3) return;
            if (!st.gate1Done || st.gate2Done) { g.Toast("지금은 방향 카드를 쓸 때가 아니에요", slot, GameLogic.ToneInfo); return; }
            if (!g.Near(slot, "mural", NearReach)) { g.Toast("벽화 앞에서 보내야 해요", slot, GameLogic.ToneWarn); return; }
            if (st.round >= Rounds) { g.Toast("4라운드가 끝났어요 — 내비게이터의 판독을 기다리세요", slot, GameLogic.ToneInfo); return; }
            st.dirCard = a;
            st.dirCardClock = g.Clock;
            g.S.stats.cards++;
            g.Send(new GameEvent { type = "DirCard", slot = In, a = a, target = Op, s = DirGlyph(a) });
            g.Dirty = true;
        }

        // ---- 관문 2 ----

        private static void Lever(GameLogic g, int slot, int a)
        {
            var st = g.S.st;
            if (slot != Op || a < 0 || a > 3) return;
            if (!st.gate1Done || st.gate2Done) return;
            if (!g.Near(slot, "lever", NearReach)) { g.Toast("레버반 앞에서 당겨야 해요", slot, GameLogic.ToneWarn); return; }
            if (st.round >= Rounds) { g.Toast("4라운드가 끝났어요 — 내비게이터의 판독을 기다리세요", slot, GameLogic.ToneInfo); return; }
            if (st.dirCard < 0) { g.Toast("통역의 방향 카드를 받아야 레버를 당길 수 있어요", slot, GameLogic.ToneWarn); return; }

            int from = PathAt(st, st.round);
            st.chosenDir[st.round] = a;
            int to = PathAt(st, st.round + 1);
            g.Send(new GameEvent { type = "Wire", slot = Op, a = st.round, s = $"{from}>{to}" });
            st.round++;
            st.dirCard = -1;
            st.dirCardClock = -99f;
            ClearColorReport(st);   // 색상등이 다음 라운드 색으로 바뀐다
            g.Toast(st.round >= Rounds ? "4라운드 완료 — 내비게이터가 배전함을 판독할 차례" : $"레버 {DirGlyph(a)} 확정 — 라운드 {st.round + 1}/4", -1, GameLogic.ToneInfo);
            // B1: 관측자는 색이 같아도 라운드마다 다시 신고해야 한다(서버가 신고를 지웠다) — 관측자에게만
            if (st.round < Rounds) g.Toast(RoundStartText(st.round + 1), Ob, GameLogic.ToneWarn);
            UpdateStatus(g);
            g.Dirty = true;
        }

        private static void Judge(GameLogic g, int slot, int a)
        {
            var st = g.S.st;
            if (slot != Na) return;
            if (!st.gate1Done || st.gate2Done) return;
            if (!g.Near(slot, "junction", NearReach)) { g.Toast("배전함 앞에서 판독해야 해요", slot, GameLogic.ToneWarn); return; }

            if (a == 1)
            {
                if (st.round < Rounds) { g.Toast("4라운드가 끝나야 판독할 수 있어요", slot, GameLogic.ToneWarn); return; }
                if (PathEnd(st) == st.ladderSafe)
                {
                    st.gate2Done = true;
                    st.step = 2;
                    st.gatherStart = g.Clock;
                    st.allInSince = -1f;
                    g.Flash(GameLogic.ToneGood);
                    g.Send(new GameEvent { type = "Judge", slot = Na, a = 1, s = "safe" });
                    g.Send(new GameEvent { type = "GateOpen", slot = Na, a = 2 });
                    g.Toast("셔터 개방! 모두 중앙 분수 아트리움으로 — 자기 색 원 안에 함께 서야 한다", -1, GameLogic.ToneGood);
                }
                else
                {
                    g.S.stats.wrongInputs++;
                    g.Penalty(Gate2Penalty, "위험한 통로였다! 경보가 울린다");
                    g.Send(new GameEvent { type = "Alarm", slot = Na, s = "gate2", a = 2 });
                    g.Send(new GameEvent { type = "Judge", slot = Na, a = 1, s = "wrong" });
                    ResetRounds(g);
                    g.Toast("방향 자물쇠가 초기화됐다 — 라운드 1부터 다시", -1, GameLogic.ToneWarn);
                }
            }
            else
            {
                if (st.round == 0) { g.Toast("아직 시작 전이에요", slot, GameLogic.ToneInfo); return; }
                // D-A5: "위험 — 처음부터"는 사람 내비가 눌러도 팀에게 몸짓 카드(위험 → 아니오)로 보인다. 서버가 대신 발행(통계·이벤트·봇 훅은 휠과 같은 경로)
                g.Signal(Na, "DANGER");
                g.Signal(Na, "NO");
                g.Send(new GameEvent { type = "Judge", slot = Na, a = 0, s = "reset" });
                ResetRounds(g);
                g.Toast("처음부터 다시 — 내비게이터가 재시도를 요청했다", -1, GameLogic.ToneWarn);
            }
            UpdateStatus(g);
            g.Dirty = true;
        }

        /// <summary>관문 2 라운드 리셋: 새 시드로 숫자·색·사다리를 다시 만든다.</summary>
        public static void ResetRounds(GameLogic g)
        {
            var st = g.S.st;
            st.gate2Attempts++;
            GenerateGate2(st, st.seed + 7 + 101 * st.gate2Attempts);
            st.round = 0;
            st.dirCard = -1;
            st.dirCardClock = -99f;
            ClearColorReport(st);
            g.Send(new GameEvent { type = "RoundReset", a = st.gate2Attempts });
            // B6: 새 시드 = 새 점자·새 색. 조작자는 점자를, 관측자는 색을 다시 보내야 한다(통역의 옛 정보는 무효)
            g.Toast(ResetOperatorText, Op, GameLogic.ToneWarn);
            g.Toast(ResetObserverText, Ob, GameLogic.ToneWarn);
            UpdateStatus(g);
            g.Dirty = true;
        }

        // ---- 안내 문구(팝업·테스트가 같은 문자열을 쓴다) ----
        public const string ResetOperatorText = "새 패턴이에요 — 점자를 다시 보내세요";
        public const string ResetObserverText = "색을 다시 신고";
        public const string PingNudgeNavText = "친구들이 기다려요 — [여기로!]로 안내데스크를 알려주세요";
        public const string PingNudgeOthersText = "안내데스크 쪽으로 가 보세요";
        public static string RoundStartText(int roundNo) => $"라운드 {roundNo} 시작 — 색이 같아도 다시 신고!";
        public static string WrongDigitText(int digit) => $"조작자가 {digit}을 넣었는데 틀렸어요 — 색·홀짝을 다시 확인";
        public static string TimeWarnText(int minutes) => $"남은 시간 {minutes}분";

        // ---- 내비게이터 ----

        private static void Ping(GameLogic g, int slot, int a)
        {
            var st = g.S.st;
            if (slot != Na || a < 0 || a >= StageMaps.RoomCount) return;
            st.lastPingRoom = a;
            st.lastPingClock = g.Clock;
            g.Send(new GameEvent { type = "Ping", slot = Na, a = a, pos = Vec2.Of(StageMaps.RoomDoor(a)), s = StageMaps.RoomName(a) });
            g.Dirty = true;
        }

        private static void OpenBoard(GameLogic g, int slot)
        {
            var st = g.S.st;
            if (slot != Na) return;
            if (!g.Near(slot, "board", NearReach)) { g.Toast("현황판 앞에서 열어야 해요", slot, GameLogic.ToneWarn); return; }
            bool first = !st.boardOpened;
            st.boardOpened = true;
            st.boardClock = g.Clock;   // 다시 열 때마다 스냅샷 시각 갱신
            if (first) g.Toast($"침수 현황판을 확인했다 — 비상 전원 안전 라인은 {SafeLine(st)}번. 배전함에서도 보인다", slot, GameLogic.ToneInfo);
            else g.Toast("침수 현황판을 다시 확인했다 — 패널의 침수율이 지금 값으로 바뀐다", slot, GameLogic.ToneInfo);
            UpdateStatus(g);
            g.Dirty = true;
        }

        // ==================================================================
        // 틱
        // ==================================================================

        public static void Tick(GameLogic g, float dt)
        {
            var st = g.S.st;
            TickDanger(g);
            TickTimeWarnings(g);

            if (st.step == 0)
            {
                // A3: 타이머가 돈 지 40초가 지나도록 내비의 핑이 없으면 한 번만 — 내비에게는 방법을, 나머지에게는 목적지를
                if (!st.pingNudged && st.timerRunning && st.lastPingRoom < 0 && g.Clock - st.startClock >= PingNudgeAfter)
                {
                    st.pingNudged = true;
                    g.Toast(PingNudgeNavText, Na, GameLogic.ToneWarn);
                    for (int i = 0; i < 4; i++) if (i != Na) g.Toast(PingNudgeOthersText, i, GameLogic.ToneInfo);
                    g.Dirty = true;
                }
            }
            else if (st.step == 1)
            {
                // 모자이크방에 누가 들어오면 진행 문구가 바뀐다
                string s = StatusText(g);
                if (s != st.status) { st.status = s; g.Dirty = true; }
            }
            else if (st.step == 2)
            {
                bool all = g.AllInGather();
                if (all)
                {
                    if (st.allInSince < 0f) { st.allInSince = g.Clock; g.Dirty = true; }
                    else if (g.Clock - st.allInSince >= AllInHold) g.Cleared();
                }
                else if (st.allInSince >= 0f)
                {
                    st.allInSince = -1f;
                    g.Dirty = true;
                }
            }
        }

        /// <summary>C4: 남은 시간이 6:00 / 3:00 / 1:00 아래로 내려가는 순간 전원 토스트 + 경보 플래시 1회씩(timeWarned 비트). 한 틱에 여럿을 지나치면 가장 작은 것만 알린다.</summary>
        private static void TickTimeWarnings(GameLogic g)
        {
            var st = g.S.st;
            if (!st.timed || !st.timerRunning) return;
            float left = g.TimeLeft;
            int say = -1;
            for (int i = 0; i < TimeWarnMinutes.Length; i++)
            {
                int bit = 1 << i;
                if ((st.timeWarned & bit) != 0 || left > TimeWarnMinutes[i] * 60f) continue;
                st.timeWarned |= bit;
                say = i;
            }
            if (say < 0) return;
            g.Toast(TimeWarnText(TimeWarnMinutes[say]), -1, GameLogic.ToneWarn);
            g.Flash(GameLogic.ToneWarn);
            g.Dirty = true;
        }

        /// <summary>위험 구역(침수 70% 이상인 수족관방·연못정원방)에 있는 사람에게 5초마다 경고. 시간 감점은 없다.</summary>
        private static void TickDanger(GameLogic g)
        {
            var st = g.S.st;
            for (int i = 0; i < 4; i++)
            {
                int room = g.RoomOf(i);
                if (room < 0 || !IsDanger(st, room, g.Clock)) continue;
                if (g.Clock - g.DangerToastClock[i] < DangerToastEvery) continue;
                g.DangerToastClock[i] = g.Clock;
                g.Toast("물이 허리까지 찼다 — 빠져나가!", i, GameLogic.ToneBad);
            }
        }

        // ==================================================================
        // 상태 문구
        // ==================================================================

        public static void UpdateStatus(GameLogic g)
        {
            g.S.st.status = StatusText(g);
        }

        private static string StatusText(GameLogic g)
        {
            var st = g.S.st;
            switch (st.step)
            {
                case 0: return "관문 1 — 계산 자물쇠 · 안내데스크";
                case 1:
                    if (st.round >= Rounds) return "배전함 판독 대기 — 내비게이터";
                    if (g.AnyInRoom(StageMaps.RoomMosaic)) return $"관문 2 — 방향 자물쇠 · 라운드 {st.round + 1}/4";
                    return "이동 구간 — 침수 현황판";
                case 2: return "최종 집결 — 중앙 분수 아트리움";
                default: return "";
            }
        }

        /// <summary>상단 진행 표시 ■□□.</summary>
        public static string ProgressGlyphs(StageState st)
        {
            if (st == null) return "□□□";
            if (st.cleared) return "■■■";
            switch (st.step) { case 0: return "■□□"; case 1: return "■■□"; default: return "■■■"; }
        }
    }
}
