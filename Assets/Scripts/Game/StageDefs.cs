using UnityEngine;

namespace Paradox.Game
{
    /// <summary>한 스테이지의 고정 설정(배경 국가·현지인·제한시간·색). Stage 2~4는 잠금(TOP SECRET)만 표시한다.</summary>
    public class StageDef
    {
        public int index;
        public string title;        // "STAGE 1"
        public string country;      // "필리핀"
        public string code;         // "PHL"
        public string disaster;     // "침수된 박물관"
        public string mechanic;     // "오브젝트-담당자 릴레이"
        public string blurb;        // 스테이지 선택 카드 설명
        public int localSlot;       // 이번 스테이지의 현지인(표지판을 읽는 역할)
        public float timeLimit;     // 0 = 시간 압박 없음
        public bool locked;         // TOP SECRET
        public Color floorA, floorB, rock, accent, flood;
        public string signLanguage; // 표지판 언어 이름(연출용)

        public string Headline => $"{title} · {country} · {disaster}";
    }

    public static class StageDefs
    {
        public const int Count = 4;

        private static readonly StageDef[] Defs =
        {
            new StageDef
            {
                index = 1, title = "STAGE 1", country = "필리핀", code = "PHL", disaster = "침수된 박물관", mechanic = "오브젝트-담당자 릴레이",
                blurb = "태풍으로 물이 차오르는 박물관. 표지판은 필리핀어로만 쓰여 있다.",
                localSlot = (int)GameRole.Interpreter, timeLimit = Stage1Logic.TimeLimit, locked = false,
                floorA = new Color(0.20f, 0.20f, 0.24f), floorB = new Color(0.16f, 0.16f, 0.20f),
                rock = new Color(0.04f, 0.05f, 0.07f), accent = new Color(0.949f, 0.773f, 0.447f), flood = new Color(0.22f, 0.45f, 0.85f),
                signLanguage = "필리핀어"
            },
            new StageDef
            {
                index = 2, title = "STAGE 2", country = "베트남", code = "VNM", disaster = "미정", mechanic = "TOP SECRET",
                blurb = "Stage 1을 클리어하면 다음 봉투가 열린다.",
                localSlot = (int)GameRole.Navigator, timeLimit = 0f, locked = true,
                floorA = new Color(0.20f, 0.30f, 0.34f), floorB = new Color(0.17f, 0.26f, 0.30f),
                rock = new Color(0.06f, 0.10f, 0.12f), accent = new Color(0.55f, 0.85f, 1f), flood = new Color(0.20f, 0.45f, 0.85f),
                signLanguage = "베트남어"
            },
            new StageDef
            {
                index = 3, title = "STAGE 3", country = "남아공", code = "ZAF", disaster = "미정", mechanic = "TOP SECRET",
                blurb = "Stage 1을 클리어하면 다음 봉투가 열린다.",
                localSlot = (int)GameRole.Observer, timeLimit = 0f, locked = true,
                floorA = new Color(0.30f, 0.30f, 0.27f), floorB = new Color(0.26f, 0.26f, 0.24f),
                rock = new Color(0.10f, 0.10f, 0.09f), accent = new Color(0.75f, 1f, 0.55f), flood = new Color(0.55f, 0.65f, 0.25f),
                signLanguage = "현지어"
            },
            new StageDef
            {
                index = 4, title = "STAGE 4", country = "한국", code = "KOR", disaster = "미정", mechanic = "TOP SECRET",
                blurb = "Stage 1을 클리어하면 다음 봉투가 열린다.",
                localSlot = (int)GameRole.Operator, timeLimit = 0f, locked = true,
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

        public static bool IsLocked(int stage) => Get(stage).locked;

        public static int LocalSlot(int stage) => Get(stage).localSlot;

        /// <summary>이 슬롯이 이번 스테이지의 현지인인가(표지판·벽보를 원래대로 읽는가).</summary>
        public static bool CanRead(int stage, int slot) => LocalSlot(stage) == slot;

        /// <summary>채팅 입력은 전원 허용(통역만 전부 읽는다 — 표시 규칙은 클라이언트 Comms.CanUnderstand).</summary>
        public static bool ChatAllowed(int stage, int slot) => Roles.Valid(slot);

        /// <summary>전체 미니맵(방 이름·미션 마커·위험 표시)을 보는 역할 = 내비게이터. 나머지는 흐린 미니맵만.</summary>
        public static bool HasMinimap(int stage, int slot) => slot == (int)GameRole.Navigator;

        /// <summary>시야 제한(안개) 반경. STRANGER는 한 층 전체가 항상 보인다 → 항상 0.</summary>
        public static float FogRadius(int stage, int slot) => 0f;
    }
}
