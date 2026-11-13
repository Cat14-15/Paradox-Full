using UnityEngine;

namespace Paradox.Game
{
    /// <summary>한 스테이지의 고정 설정(배경 국가·현지인·제한시간·색).</summary>
    public class StageDef
    {
        public int index;
        public string title;        // "Stage 1"
        public string country;      // "필리핀"
        public string code;         // "PH"
        public string disaster;     // "태풍·침수"
        public string mechanic;     // "릴레이 체인"
        public int localSlot;       // 이번 스테이지의 현지인(표지판을 읽는 역할)
        public float timeLimit;     // 0 = 시간 압박 없음
        public Color floorA, floorB, rock, accent, flood;
        public string signLanguage; // 표지판 언어 이름(연출용)

        public string Headline => $"{title} · {country} · {disaster}";
    }

    public static class StageDefs
    {
        public const int Count = 4;

        /// <summary>글자 카드 패널의 음절(6 x 5). 모든 스테이지 공통.</summary>
        public static readonly string[] Syllables =
        {
            "계", "단", "창", "고", "로", "비",
            "옥", "상", "출", "구", "지", "하",
            "배", "수", "차", "순", "환", "안",
            "전", "희", "망", "친", "물", "불",
            "문", "길", "위", "험", "동", "서"
        };

        /// <summary>Stage 1 갈림길: [정답 단어, 미끼 단어]</summary>
        public static readonly string[][] ForkPairs =
        {
            new[] { "계단", "창고" },
            new[] { "로비", "옥상" },
            new[] { "출구", "지하" }
        };

        /// <summary>Stage 2 펌프 콘솔 모드(표지판이 알려주는 정답 중 하나).</summary>
        public static readonly string[] ConsoleModes = { "배수", "차단", "순환" };

        /// <summary>Stage 4 탈출 암호 후보.</summary>
        public static readonly string[] CodeWords = { "안전", "희망", "친구" };

        private static readonly StageDef[] Defs =
        {
            new StageDef
            {
                index = 1, title = "Stage 1", country = "필리핀", code = "PH", disaster = "태풍·침수", mechanic = "릴레이 체인",
                localSlot = (int)GameRole.Interpreter, timeLimit = 0f,
                floorA = new Color(0.17f, 0.18f, 0.24f), floorB = new Color(0.14f, 0.15f, 0.21f),
                rock = new Color(0.07f, 0.07f, 0.10f), accent = new Color(1f, 0.85f, 0.45f), flood = new Color(0.22f, 0.42f, 0.85f),
                signLanguage = "필리핀어"
            },
            new StageDef
            {
                index = 2, title = "Stage 2", country = "베트남", code = "VN", disaster = "터널 침수", mechanic = "3자 동시 조작",
                localSlot = (int)GameRole.Navigator, timeLimit = 170f,
                floorA = new Color(0.20f, 0.30f, 0.34f), floorB = new Color(0.17f, 0.26f, 0.30f),
                rock = new Color(0.06f, 0.10f, 0.12f), accent = new Color(0.55f, 0.85f, 1f), flood = new Color(0.20f, 0.45f, 0.85f),
                signLanguage = "베트남어"
            },
            new StageDef
            {
                index = 3, title = "Stage 3", country = "국적 미정", code = "??", disaster = "유독가스 누출", mechanic = "정보 교차검증",
                localSlot = (int)GameRole.Observer, timeLimit = 200f,
                floorA = new Color(0.30f, 0.30f, 0.27f), floorB = new Color(0.26f, 0.26f, 0.24f),
                rock = new Color(0.10f, 0.10f, 0.09f), accent = new Color(0.75f, 1f, 0.55f), flood = new Color(0.55f, 0.65f, 0.25f),
                signLanguage = "현지어"
            },
            new StageDef
            {
                index = 4, title = "Stage 4", country = "한국", code = "KR", disaster = "지하 화재", mechanic = "우선순위 판단 + 멀티태스킹",
                localSlot = (int)GameRole.Operator, timeLimit = 240f,
                floorA = new Color(0.32f, 0.27f, 0.25f), floorB = new Color(0.28f, 0.23f, 0.21f),
                rock = new Color(0.10f, 0.08f, 0.07f), accent = new Color(1f, 0.6f, 0.3f), flood = new Color(0.85f, 0.35f, 0.15f),
                signLanguage = "한국어"
            }
        };

        public static StageDef Get(int stage)
        {
            int i = Mathf.Clamp(stage, 1, Count) - 1;
            return Defs[i];
        }

        public static int LocalSlot(int stage) => Get(stage).localSlot;

        /// <summary>이 슬롯이 이번 스테이지의 현지인인가(표지판을 원래대로 읽는가).</summary>
        public static bool CanRead(int stage, int slot) => LocalSlot(stage) == slot;

        /// <summary>Stage 4에서는 조작자도 버튼 5개로만 소통(완전 반전).</summary>
        public static bool ChatAllowed(int stage, int slot) => slot == (int)GameRole.Operator && stage < 4;

        /// <summary>글자 카드를 보내는 역할(현지인). Stage 1: 통역, Stage 2: 내비, Stage 4: 조작자.</summary>
        public static bool IsLetterSender(int stage, int slot) => (stage == 1 || stage == 2 || stage == 4) && CanRead(stage, slot);

        /// <summary>방향 신호(← ↑ ↓ →)를 쓰는 안내 역할.</summary>
        public static bool HasDirectionPad(int stage, int slot)
        {
            if (slot == (int)GameRole.Navigator) return true;
            if (stage == 3 && slot == (int)GameRole.Observer) return true;
            if (stage == 4 && slot == (int)GameRole.Operator) return true;
            return false;
        }

        /// <summary>미니맵을 보는 역할. 내비게이터(1~3), Stage 4는 조작자만.</summary>
        public static bool HasMinimap(int stage, int slot)
        {
            if (stage == 4) return slot == (int)GameRole.Operator;
            return slot == (int)GameRole.Navigator;
        }

        /// <summary>관측자 고글(색상 감지). Stage 4에서는 무력화.</summary>
        public static bool HasGoggles(int stage, int slot) => slot == (int)GameRole.Observer && stage != 4;

        /// <summary>시야 제한(안개) 반경. 0이면 없음.</summary>
        public static float FogRadius(int stage, int slot, bool devicesDone)
        {
            switch (stage)
            {
                case 1: return 7.5f;   // 정전 — 손전등 느낌
                case 3: return 4.6f;   // 유독가스 — 시야 흐림
                case 4: return slot == (int)GameRole.Operator ? 0f : (devicesDone ? 7f : 3.8f);
                default: return 0f;
            }
        }
    }
}
