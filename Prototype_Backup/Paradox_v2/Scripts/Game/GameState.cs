using System;
using System.Collections.Generic;
using UnityEngine;

namespace Paradox.Game
{
    /// <summary>게임 전체 흐름의 단계. 유저 플로우 순서 그대로.</summary>
    public enum Phase
    {
        Title = 0,        // 타이틀 (접속 전, 로컬)
        Lobby,            // 4인 매칭 / 인원 확인
        CharacterSelect,  // 캐릭터 선택 + 이름 입력
        Prologue,         // 프롤로그 컷씬 (공용 화면)
        StageIntro,       // Stage N 도입 스토리
        StagePlay,        // Stage N 플레이
        StageClear,       // Stage N 클리어 연출/내레이션
        Ending,           // 엔딩 컷씬 (하이파이브)
        Result            // 결과 화면
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
        public int character = -1;
        public bool ready;
        public bool isBot;
    }

    /// <summary>전달 중인 글자 카드 한 장.</summary>
    [Serializable]
    public class LetterMsg
    {
        public string ch = "";
        public int from;
        public int to;
        public int hop;            // 0 = 중계자(내비게이터)를 향해 이동/대기 중, 1 = 최종 수신자를 향해 이동 중
        public float arriveClock;  // 이 클록 이후에 현재 목적지에 도착한 것으로 본다(전달 이펙트 시간)
    }

    [Serializable]
    public class Stats
    {
        public float playSeconds;
        public float[] stageSeconds = new float[5];
        public int[] retries = new int[5];
        public int signals;
        public int letters;
        public int chats;
        public int losses;
    }

    /// <summary>
    /// 현재 스테이지의 런타임 상태. 네 스테이지의 필드를 한 클래스에 모아(JsonUtility 제약)
    /// 서버가 통째로 브로드캐스트한다. 연속값(타이머·미터)은 별도 NetworkVariable로 흐른다.
    /// </summary>
    [Serializable]
    public class StageState
    {
        public int stage;
        public int seed;

        // ---- 시간 / 결과 ----
        public bool timed;
        public float timeLimit;
        public float startClock;
        public float penalty;
        public bool cleared;
        public float clearClock;
        public bool failed;
        public int retries;

        // ---- 이동 ----
        public int moveMode;                // 0 잠김, 1 자유, 2 레일(자동 이동)
        public Vec2[] rail = new Vec2[4];   // 레일 모드일 때 슬롯별 목표 지점
        public int step;
        public string status = "";

        // ---- 글자 카드 릴레이 (Stage 1, 2, 4) ----
        public string word = "";                                  // 이번에 전달해야 할 단어(현지인 UI에만 표시)
        public string picked = "";                                // 발신자가 지금까지 고른 글자
        public List<LetterMsg> inflight = new List<LetterMsg>();  // 중계 대기 중인 글자
        public string[] received = { "", "", "", "" };            // 슬롯별로 도착한 글자

        // ---- Stage 1: 릴레이 체인 ----
        public int fork;
        public int forkCount = 3;
        public float period = 8f;
        public float cycleStart;
        public int guardedCycle = -1;
        public int resolvedCycle = -1;
        public bool signalCut;
        public float cutUntil;
        public int railNode;                 // 0 시작, 1 F0, 2 F1, 3 장애물, 4 F2, 5 로비
        public bool obstacleCleared;
        public float[] lastMash = new float[4];
        public int[] leverSide = new int[3];
        public string[] forkWord = new string[3];
        public string[] forkDecoy = new string[3];

        // ---- Stage 2: 3자 동시 조작 ----
        public int consoleMode = -1;
        public int requiredMode;
        public bool holding;
        public float knockUntil;
        public float waveStart;
        public bool waveWarned;
        public float[] pullClock = { -99f, -99f, -99f, -99f };
        public bool gatesOpen;
        public bool exitUp;
        public int gateFails;
        public float[] lastHazardTick = new float[4];

        // ---- Stage 3: 정보 교차검증 ----
        public int[] gas = new int[12];        // 0 초록(안전) 1 노랑 2 빨강
        public bool[] blocked = new bool[12];  // 구조 붕괴(막다른 길)
        public int[] ppm = new int[12];        // 감지기 수치
        public int safeSector;
        public bool[] markObs = new bool[12];
        public bool[] markNav = new bool[12];
        public bool[] markInt = new bool[12];
        public bool confirmObs;
        public bool confirmNav;
        public int confirmObsSector = -1;
        public int confirmNavSector = -1;
        public int wrongConfirms;
        public float[] lastGasTick = new float[4];

        // ---- Stage 4: 우선순위 판단 + 멀티태스킹 ----
        public bool[] device = new bool[4];
        public bool devicesDone;
        public List<Vec2> fires = new List<Vec2>();
        public float lastSpread;
        public string[] keypad = { "", "", "", "" };
        public float[] codeClock = { -99f, -99f, -99f, -99f };
        public bool codeDone;
        public string broadcast = "";
        public float[] lastBurn = new float[4];
    }

    /// <summary>서버가 소유하고 모두에게 복제되는 세션 전체 상태.</summary>
    [Serializable]
    public class SessionState
    {
        public int version;
        public Phase phase = Phase.Lobby;
        public int stage;                       // 1~4 (스테이지 외 단계에서는 직전 값)
        public PlayerInfo[] players = new PlayerInfo[4];
        public int hostSlot;
        public string cutsceneId = "";
        public int cutsceneIndex;
        public StageState st = new StageState();
        public Stats stats = new Stats();
        public float gameStartClock;
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

    /// <summary>결정적 난수(시드 기반). 서버/클라이언트가 같은 시드로 같은 맵을 만든다.</summary>
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
