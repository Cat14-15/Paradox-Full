using UnityEngine;

namespace Paradox.Game
{
    /// <summary>
    /// 4인 협동의 고정 역할. 슬롯 번호 == 역할 번호. 캐릭터 선택은 없다(슬롯마다 캐릭터·국적·기본 이름이 정해져 있다).
    /// Stage 1에서는 통역(필리핀인)만 이 나라의 글자를 읽을 수 있다.
    /// </summary>
    public enum GameRole
    {
        Operator = 0,    // 조작자 (한국인 · 도윤) — 스위치·레버 조작, 자유 채팅
        Interpreter = 1, // 통역 (필리핀인 · 마리아) — 표지판·벽보 해독, 모두의 채팅 해독, 숫자·방향 카드
        Navigator = 2,   // 내비게이터 (베트남인 · 린) — 맵·침수 현황, 이동 지시
        Observer = 3     // 관측자 (남아공인 · 아이샤) — 색 구분
    }

    public static class Roles
    {
        public const int Count = 4;

        public static readonly string[] Names = { "조작자", "통역", "내비게이터", "관측자" };
        public static readonly string[] Shorts = { "조작", "통역", "내비", "관측" };
        public static readonly string[] Nations = { "한국인", "필리핀인", "베트남인", "남아공인" };
        public static readonly string[] Countries = { "한국", "필리핀", "베트남", "남아공" };
        public static readonly string[] Codes = { "KOR", "PHL", "VNM", "ZAF" };
        public static readonly string[] DefaultNames = { "도윤", "마리아", "린", "아이샤" };

        public static readonly string[] Jobs =
        {
            "스위치와 레버를 조작할 수 있는 유일한 사람. 자유롭게 채팅하지만, 이 나라의 글자는 읽지 못한다.",
            "이 스테이지의 표지판·벽보를 읽을 수 있는 유일한 사람. 모두의 채팅을 읽고 쓸 수 있고, 숫자·방향 카드를 보낸다.",
            "맵과 침수 상황을 볼 수 있다. 팀이 어디로 가야 하는지는 너만 안다.",
            "램프와 패널의 색을 구분할 수 있다. 색은 너만 볼 수 있다."
        };

        /// <summary>역할 색(HTML F2C572 / 5FD3C4 / E98A8A / 9F8FE0).</summary>
        public static readonly Color[] Colors =
        {
            new Color(0.949f, 0.773f, 0.447f), // 조작자 - 앰버
            new Color(0.373f, 0.827f, 0.769f), // 통역 - 청록
            new Color(0.914f, 0.541f, 0.541f), // 내비게이터 - 연빨강
            new Color(0.624f, 0.561f, 0.878f), // 관측자 - 보라
        };

        public static string Name(int slot) => Valid(slot) ? Names[slot] : "?";
        public static string Short(int slot) => Valid(slot) ? Shorts[slot] : "?";
        public static string Nation(int slot) => Valid(slot) ? Nations[slot] : "?";
        public static string Country(int slot) => Valid(slot) ? Countries[slot] : "?";
        public static string Code(int slot) => Valid(slot) ? Codes[slot] : "?";
        public static string DefaultName(int slot) => Valid(slot) ? DefaultNames[slot] : "플레이어";
        public static string Job(int slot) => Valid(slot) ? Jobs[slot] : "";
        public static Color Color(int slot) => Valid(slot) ? Colors[slot] : UnityEngine.Color.white;
        public static string Hex(int slot) => ColorUtility.ToHtmlStringRGB(Color(slot));
        public static bool Valid(int slot) => slot >= 0 && slot < Count;

        /// <summary>이 이름이 어떤 슬롯의 기본 이름인가(-1 = 아님). 자리를 옮긴 사람의 이름을 새 역할 기본 이름으로 바꿀 때 쓴다.</summary>
        public static int SlotOfDefaultName(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            for (int i = 0; i < Count; i++) if (DefaultNames[i] == name) return i;
            return -1;
        }

        /// <summary>"통역 (필리핀인)" 형태의 전체 표기.</summary>
        public static string Full(int slot) => $"{Name(slot)} ({Nation(slot)})";

        public static string ColoredName(int slot) => $"<color=#{Hex(slot)}>{Name(slot)}</color>";
    }
}
