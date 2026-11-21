using System;
using UnityEngine;

namespace Paradox.Game
{
    /// <summary>
    /// 서버(호스트) 단계. 접속 전 로컬 화면(로고·타이틀·프롤로그·스테이지 선택·매칭)은 Paradox.Client.LocalNav가 맡는다.
    /// Lobby → NameSetup → Ready(1.5초 자동) → StageIntro(s1_intro) → StagePlay → StageClear(s1_clear) → Result → (Again) Lobby
    /// </summary>
    public enum Phase
    {
        Title = 0,     // 접속 전(로컬) — 서버 상태로는 쓰이지 않는다
        Lobby,         // 4인 매칭
        NameSetup,     // 이름 설정(캐릭터·역할은 고정)
        Ready,         // 네 명 모두 준비 완료 — 서버가 1.5초 뒤 자동으로 오프닝으로
        StageIntro,    // Stage 1 오프닝 컷씬(네트워크, 호스트 진행)
        StagePlay,     // Stage 1 플레이
        StageClear,    // 클리어 컷씬
        Result         // 결과 화면
    }

    [Serializable]
    public struct Vec2
    {
        public float x, y;
        public Vec2(float x, float y) { this.x = x; this.y = y; }
        public Vector2 V => new Vector2(x, y);
        public static Vec2 Of(Vector2 v) => new Vec2(v.x, v.y);
    }

    [Serializable]
    public class PlayerInfo
    {
        public bool present;
        public long clientId = -1;
        public string name = "";
        public int character = -1;   // 항상 슬롯 번호(캐릭터 선택 없음). UI는 UIPortrait.MakeForSlot만 쓴다.
        public bool ready;
        public bool isBot;
        public bool campDone;         // 캠프 첫 만남(Camp 씬)을 끝까지 봤다. 규칙은 prologueDone 과 같다
        public bool prologueDone;     // 프롤로그를 끝까지 봤다(전원이 끝나야 게임 장면으로 넘어간다). 봇은 처음부터 true
    }

    /// <summary>결과 화면 기록.</summary>
    [Serializable]
    public class Stats
    {
        public float playSeconds;   // 스테이지 플레이 총 시간(재시도 포함, 초)
        public int wrongInputs;     // 관문 1 오답 + 관문 2 "안전" 오판 횟수
        public int cards;           // 통역이 보낸 숫자·방향 카드 수
        public int chats;           // 채팅 수
        public int signals;         // 휠 신호 수
        public int retries;         // 시간 초과 후 재시도 횟수
    }

    /// <summary>
    /// Stage 1(침수된 박물관)의 런타임 상태. JsonUtility로 통째로 직렬화되므로 public 평면 필드만 둔다.
    /// 규칙 판정은 서버(Stage1Logic)만 하고, 클라이언트는 표시만 한다.
    /// </summary>
    [Serializable]
    public class StageState
    {
        public int stage = 1;
        public int seed;
        public int retries;
        public int checkpoint;          // 0 = 처음부터, 1 = 관문 2부터(관문 1을 통과한 뒤 실패하면 여기서 다시 시작)

        // ---- 시간 / 결과 ----
        public bool timed = true;
        public float timeLimit = 720f;
        public float startClock;            // 타이머 시작 시각(timerRunning 이 되는 순간 다시 찍는다)
        public float penalty;
        public bool[] briefDone = new bool[4];   // 슬롯별 브리핑(튜토리얼) 완료 — 명령 Brief
        public bool timerRunning;                // 전원 브리핑 완료 또는 30초 경과 → true. 그 전에는 타이머·침수 정지
        public float lockUntil1;                 // 관문 1 오답 뒤 키패드 잠금 해제 시각(서버 클록)
        public bool cleared;
        public bool failed;
        public float clearClock;

        // ---- 진행 ----
        public int step;                // 0 관문 1, 1 이동 구간 + 관문 2, 2 최종 집결
        public int moveMode = 1;        // 0 잠김, 1 자유
        public string status = "";      // 상단 진행 문구(서버가 갱신)

        // ---- 관문 1: 계산 자물쇠 ----
        public int calcA, calcB, calcC, calcOp, calcResult;   // calcResult = A×B ± C (calcOp 0 = +, 1 = −)
        public int lampColor;           // 0 빨강 1 노랑 2 초록 3 파랑
        public int calcAnswer;          // Table1[홀짝][lampColor]
        public int calcFails;
        public string calcInput = "";   // 조작자가 누른 한 자리
        public int lastWrongDigit = -1; // 관문 1 마지막 오답 입력값(B2: 통역 벽보 팝업 status 표시). 정답·숫자 카드 갱신 시 −1
        public bool gate1Done;

        // ---- 소통 ----
        public int colorReport = -1;    public float colorReportClock = -99f;   // 관측자 → 통역 색 신고
        public int numberCard = -1;     public float numberCardClock = -99f;    // 통역 → 조작자 숫자 카드
        public int dirCard = -1;        public float dirCardClock = -99f;       // 통역 → 조작자 방향 카드(이번 라운드)
        public int lastPingRoom = -1;   public float lastPingClock = -99f;      // 내비게이터 "여기로!" 방 핑
        public bool pingNudged;         // A3: 관문 1 타이머 시작 40초 뒤에도 핑이 없어 내비에게 안내 토스트를 보냈다(한 번만)
        public int timeWarned;          // C4: 남은 시간 경고 비트 — 1 = 6:00, 2 = 3:00, 4 = 1:00 (전원 토스트 + Flash 1회씩)
        public bool boardOpened;        // 내비게이터가 침수 현황판을 한 번이라도 열었다
        public float boardClock = -99f; // 마지막으로 현황판을 연 시각(Clock) — 역할 패널·미니맵은 이 시점의 침수율 스냅샷만 보여준다(기획 6절 "가서 봐야 아는")

        // ---- 관문 2: 방향 자물쇠(4라운드) ----
        public int round;                                   // 0~4 (4 = 판독 대기)
        public int[] brailleDigit = new int[4];             // 라운드별 점자 숫자 {1,2,3,4,5,6,7,9}
        public int[] lampColor2 = new int[4];               // 라운드별 색상등
        public int[] answerDir = new int[4];                // 정답 방향 0▲ 1▼ 2◀ 3▶
        public int[] chosenDir = { -1, -1, -1, -1 };        // 조작자가 실제로 당긴 방향
        public int ladderStart;                             // 시작 단자(0~7)
        public int[] ladderNext = new int[128];             // [r*32 + t*4 + d] → 다음 단자
        public int ladderSafe;                              // 4개 전부 정답일 때 도착하는 단자
        public int[] terminalZone = new int[8];             // 단자 i의 구역 라벨(0~7), terminalZone[ladderSafe] == 0
        public int gate2Attempts;
        public bool gate2Done;

        // ---- 침수(구역 8개 = 방 0~5 + 6 지하 창고 + 7 옥상 계단) ----
        public float[] floodBase = new float[8];
        public float[] floodRate = new float[8];

        // ---- 최종 집결 ----
        public float gatherStart;
        public float allInSince = -1f;
    }

    /// <summary>서버가 소유하고 모두에게 복제되는 세션 전체 상태.</summary>
    [Serializable]
    public class SessionState
    {
        public int version;
        public Phase phase = Phase.Lobby;
        public int stage;                       // 1 (스테이지 밖에서는 직전 값)
        public PlayerInfo[] players = new PlayerInfo[4];
        public int hostSlot;
        public string cutsceneId = "";
        public int cutsceneIndex;
        public StageState st = new StageState();
        public Stats stats = new Stats();
        public float gameStartClock;
        public int s1Done;                 // Story1 에서 푼 퍼즐(비트 = 퍼즐 id). 늦게 들어온 PC·씬 전환 중이던 PC 도 같은 상태가 되도록 상태에 둔다
        public int difficulty = 0;        // 0 쉬움 · 1 보통 · 2 어려움 (방장이 로비에서 고른다)
        public bool speedrun;             // 스피드런: 이야기를 건너뛰고 기록만 잰다(방장이 로비에서 고른다)
        public int s1Rounds;               // 퍼즐별로 푼 횟수(3비트씩, Story1Rules.RoundsOf)
        public int puzzleSeed;           // 게임 시작 때 서버가 한 번 정하는 정수 시드(모든 PC 의 퍼즐이 같아지도록. float 는 JSON 에서 반올림돼 쓰면 안 된다)
        public float readyClock;          // Ready 단계에 들어온 시각(1.5초 뒤 자동 전환)
        public int hostControlSlot = -1;   // 호스트가 지금 직접 조종 중인 봇 슬롯(그 봇의 자동 행동은 멈춘다)
        public bool botsAuto = true;       // 봇이 자기 역할을 자동 수행하는가(혼자/부분 테스트)

        public SessionState()
        {
            for (int i = 0; i < players.Length; i++) players[i] = new PlayerInfo();
        }

        public int PresentCount
        {
            get { int n = 0; foreach (var p in players) if (p.present) n++; return n; }
        }

        public bool AllReady
        {
            get { foreach (var p in players) if (!p.present || !p.ready) return false; return true; }
        }

        /// <summary>들어온 사람(봇 제외)이 1명 이상이고 모두 준비했는가. 빈 자리는 시작할 때 AI 가 채운다(2~3명 플레이).</summary>
        public bool HumansReady
        {
            get
            {
                int humans = 0;
                foreach (var p in players)
                {
                    if (!p.present || p.isBot) continue;
                    humans++;
                    if (!p.ready) return false;
                }
                return humans > 0;
            }
        }

        public int SlotOfClient(long clientId)
        {
            for (int i = 0; i < players.Length; i++)
                if (players[i].present && players[i].clientId == clientId && !players[i].isBot) return i;
            return -1;
        }

        public bool InStage => phase == Phase.StagePlay;
    }

    /// <summary>클라이언트 → 서버 명령. 슬롯은 서버가 접속자 기준으로 덮어쓴다(봇 슬롯은 호스트만 대신 조작).</summary>
    [Serializable]
    public class Cmd
    {
        public string type = "";
        public int slot = -1;
        public int a;
        public int b;
        public float f;
        public string s = "";
        public Vec2 pos;

        public static Cmd Make(string type, int a = 0, int b = 0, string s = "", float f = 0f)
            => new Cmd { type = type, a = a, b = b, s = s ?? "", f = f };
    }

    /// <summary>서버 → 모든 클라이언트 이벤트(연출·피드·신호). 상태와 달리 일회성.</summary>
    [Serializable]
    public class GameEvent
    {
        public string type = "";
        public int slot = -1;      // 관련 슬롯(보낸 사람 등)
        public int target = -1;    // 대상 슬롯(-1 = 전체)
        public int a;
        public int b;              // 부가 값(Chat: 구조화 문구의 값 — 봇 해석용)
        public string s = "";
        public Vec2 pos;
        public float f;

        public static GameEvent Make(string type, int slot = -1, string s = "", int target = -1, int a = 0)
            => new GameEvent { type = type, slot = slot, s = s ?? "", target = target, a = a };
    }

    public static class Json
    {
        public static string To(object o) => JsonUtility.ToJson(o);
        public static T From<T>(string s) => JsonUtility.FromJson<T>(s);
    }

    /// <summary>결정적 난수(시드 기반). 같은 시드면 서버와 테스트가 같은 값을 얻는다.</summary>
    public class Rng
    {
        private uint _s;
        public Rng(int seed) { _s = (uint)seed * 2654435761u + 0x9E3779B9u; if (_s == 0) _s = 1; }
        public uint Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(Next() % (uint)(maxExclusive - minInclusive));
        }
        public float Value() => (Next() & 0xFFFFFF) / (float)0x1000000;
        public bool Chance(float p) => Value() < p;
    }
}
