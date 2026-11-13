using UnityEngine;

namespace Paradox.Game
{
    /// <summary>
    /// 4인 협동의 고정 역할. 슬롯 번호 == 역할 번호.
    /// 스테이지마다 이 중 하나가 "현지인"이 되어 표지판을 읽을 수 있고, 나머지 셋에게는 ???로 보인다.
    /// </summary>
    public enum GameRole
    {
        Operator = 0,    // 조작자 (한국인) — 스위치·레버·버튼 조작, Stage 1~3 채팅 자유
        Interpreter = 1, // 통역 (필리핀인) — 버튼 5개 소통, 표지판 읽기/글자 카드 전송
        Navigator = 2,   // 내비게이터 (베트남인) — 전체 맵 구조 파악 / 위치 안내
        Observer = 3     // 관측자 (남아공인) — 색상 기반 함정·패널 탐지
    }

    public static class Roles
    {
        public const int Count = 4;

        public static readonly string[] Names = { "조작자", "통역", "내비게이터", "관측자" };
        public static readonly string[] Shorts = { "조작", "통역", "내비", "관측" };
        public static readonly string[] Nations = { "한국인", "필리핀인", "베트남인", "남아공인" };
        public static readonly string[] Codes = { "KR", "PH", "VN", "ZA" };
        public static readonly string[] Jobs =
        {
            "스위치·레버·버튼 조작 담당. Stage 1~3에서는 채팅이 자유롭지만, 친구들에게는 외국어로 들린다.",
            "버튼 5개로 소통. 현지인일 때 표지판을 읽고 글자 카드를 보낸다.",
            "버튼 5개로 소통. 전체 맵(미니맵)을 보고 길을 안내한다.",
            "버튼 5개로 소통. 색상 고글로 위험·타이밍을 감지한다."
        };

        public static readonly Color[] Colors =
        {
            new Color(0.98f, 0.76f, 0.30f), // 조작자 - 노랑
            new Color(0.42f, 0.84f, 0.56f), // 통역 - 초록
            new Color(0.45f, 0.64f, 0.98f), // 내비게이터 - 파랑
            new Color(0.76f, 0.56f, 0.98f), // 관측자 - 보라
        };

        public static string Name(int slot) => Valid(slot) ? Names[slot] : "?";
        public static string Short(int slot) => Valid(slot) ? Shorts[slot] : "?";
        public static string Nation(int slot) => Valid(slot) ? Nations[slot] : "?";
        public static string Code(int slot) => Valid(slot) ? Codes[slot] : "?";
        public static string Job(int slot) => Valid(slot) ? Jobs[slot] : "";
        public static Color Color(int slot) => Valid(slot) ? Colors[slot] : UnityEngine.Color.white;
        public static string Hex(int slot) => ColorUtility.ToHtmlStringRGB(Color(slot));
        public static bool Valid(int slot) => slot >= 0 && slot < Count;

        /// <summary>"통역 (필리핀인)" 형태의 전체 표기.</summary>
        public static string Full(int slot) => $"{Name(slot)} ({Nation(slot)})";

        public static string ColoredName(int slot) => $"<color=#{Hex(slot)}>{Name(slot)}</color>";
    }
}
