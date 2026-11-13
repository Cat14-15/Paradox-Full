using System.Text;
using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 소통 규칙(명세 6절)의 "표시" 쪽 정리.
    /// 누가 누구의 채팅을 읽을 수 있는지, 휠 신호 id가 어떤 카드(기호·단어·색)로 보이는지,
    /// 색 이름·카드 문구·오브젝트 담당 표를 한곳에 둔다. 월드(말풍선)·HUD·로그가 같이 쓴다.
    /// </summary>
    public static class Comms
    {
        public const int Op = 0, In = 1, Na = 2, Ob = 3;

        /// <summary>6칸 휠 신호 id(명령 Signal s). 순서 = 도넛 칸 순서(상좌, 상우, 좌, 우, 하좌, 하우).</summary>
        public static readonly string[] SignalIds = { "YES", "NO", "WHERE", "GO", "YOU", "DANGER" };

        // ---------------------------------------------------------------- 채팅 가시성

        /// <summary>viewer가 sender의 채팅을 읽을 수 있는가. 내 글, 통역이 보는 글, 통역이 쓴 글만 읽힌다.</summary>
        public static bool CanUnderstand(int viewer, int sender)
            => viewer == sender || viewer == In || sender == In;

        /// <summary>외국어 채팅은 어디서나 같은 방식(??? 덩어리)으로 보인다 — 말풍선·로그·컷씬 통일.</summary>
        public static string Garble(string text)
        {
            if (string.IsNullOrEmpty(text)) return "???";
            var sb = new StringBuilder(text.Length + 8);
            int run = 0;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) { if (run > 0) { sb.Append(' '); run = 0; } continue; }
                int n = 1 + ((c * 13 + 7) % 3);
                if (run == 0) sb.Append('?', n); else if (run % 2 == 0) sb.Append('?');
                run++;
                if (run >= 4) { sb.Append(' '); run = 0; }
            }
            string s = sb.ToString().Trim();
            return s.Length == 0 ? "???" : s;
        }

        /// <summary>채팅 이벤트를 viewer 시점의 문장으로. 못 읽으면 ??? + (외국어).</summary>
        public static string ChatText(int viewer, int sender, string text)
            => CanUnderstand(viewer, sender) ? text : Garble(text) + " (외국어)";

        // ---------------------------------------------------------------- 휠 신호 카드

        public static bool IsSignal(string id)
        {
            foreach (var s in SignalIds) if (s == id) return true;
            return false;
        }

        /// <summary>카드 기호(폰트가 확실히 지원하는 문자만 쓴다).</summary>
        public static string SignalGlyph(string id)
        {
            switch (id)
            {
                case "YES": return "○";
                case "NO": return "×";
                case "WHERE": return "?";
                case "GO": return "→";
                case "YOU": return "★";
                case "DANGER": return "!";
                default: return id ?? "";
            }
        }

        /// <summary>글리프 대신 픽셀 아이콘(SpriteLib.Icon)으로 그리는 신호의 아이콘 키(D-A7/P-A8): DANGER → "danger", YOU → "you". 나머지는 null(글리프 유지).</summary>
        public static string SignalIconKind(string id)
        {
            switch (id)
            {
                case "DANGER": return "danger";
                case "YOU": return "you";
                default: return null;
            }
        }

        /// <summary>카드 아래에 적히는 단어(영문 소문자, 위험만 한국어 — 도넛·패널·카드·로그 모두 이 표 하나를 쓴다).</summary>
        public static string SignalWord(string id)
        {
            switch (id)
            {
                case "YES": return "yes";
                case "NO": return "no";
                case "WHERE": return "where";
                case "GO": return "go";
                case "YOU": return "you";
                case "DANGER": return "위험";
                default: return (id ?? "").ToLowerInvariant();
            }
        }

        /// <summary>도넛 칸 설명(한국어). 빈 문자열이면 부제를 생략한다(위험은 단어 자체가 한국어).</summary>
        public static string SignalKorean(string id)
        {
            switch (id)
            {
                case "YES": return "응 · 좋아";
                case "NO": return "아니 · 안 돼";
                case "WHERE": return "어디로?";
                case "GO": return "가자!";
                case "YOU": return "너!";
                case "DANGER": return "";
                default: return "";
            }
        }

        /// <summary>"go  가자!" 형태의 버튼·칸 캡션. 부제가 비면 단어만.</summary>
        public static string SignalCaption(string id, int subSize, bool bold)
        {
            string word = bold ? $"<b>{SignalWord(id)}</b>" : SignalWord(id);
            string sub = SignalKorean(id);
            return string.IsNullOrEmpty(sub) ? word : $"{word}  <size={subSize}>{sub}</size>";
        }

        /// <summary>카드 색. yes/no 흰색, where 분홍, go 초록, you 노랑, 위험 빨강.</summary>
        public static Color SignalColor(string id)
        {
            switch (id)
            {
                case "WHERE": return new Color(0.93f, 0.62f, 0.78f);
                case "GO": return new Color(0.55f, 0.85f, 0.60f);
                case "YOU": return new Color(0.95f, 0.85f, 0.45f);
                case "DANGER": return new Color(0.93f, 0.45f, 0.42f);
                default: return new Color(0.95f, 0.94f, 0.90f);
            }
        }

        /// <summary>"→ go" 형태의 짧은 라벨(말풍선·로그 공용).</summary>
        public static string SignalLabel(string id) => $"{SignalGlyph(id)} {SignalWord(id)}";

        // ---------------------------------------------------------------- 색 · 방향 · 카드 문구

        public static string ColorName(int a)
        {
            switch (a)
            {
                case 0: return "빨강";
                case 1: return "노랑";
                case 2: return "초록";
                case 3: return "파랑";
                default: return "?";
            }
        }

        public static Color ColorValue(int a)
        {
            switch (a)
            {
                case 0: return new Color(0.88f, 0.27f, 0.25f);
                case 1: return new Color(0.91f, 0.73f, 0.23f);
                case 2: return new Color(0.31f, 0.69f, 0.37f);
                case 3: return new Color(0.31f, 0.48f, 0.79f);
                default: return new Color(0.25f, 0.27f, 0.32f);
            }
        }

        public static string DirGlyph(int d)
        {
            switch (d)
            {
                case 0: return "▲";
                case 1: return "▼";
                case 2: return "◀";
                case 3: return "▶";
                default: return "?";
            }
        }

        public static string DirName(int d)
        {
            switch (d)
            {
                case 0: return "위";
                case 1: return "아래";
                case 2: return "왼쪽";
                case 3: return "오른쪽";
                default: return "?";
            }
        }

        public static string NumberCardText(int a) => $"숫자 카드 {a}";
        public static string DirCardText(int d) => $"방향 카드 {DirGlyph(d)}";

        /// <summary>국적을 짧게("필리핀인" → "필리핀").</summary>
        public static string NationShort(int slot)
        {
            string n = Roles.Nation(slot);
            if (n.EndsWith("인") && n.Length > 1) n = n.Substring(0, n.Length - 1);
            return n;
        }

        // ---------------------------------------------------------------- 매뉴얼 표(표시용 — 서버 Stage1Logic.Table1/Table2 와 같은 내용)

        /// <summary>계산 모듈: [홀짝][색] → 숫자. 0 짝수, 1 홀수 / 열 = 빨강·노랑·초록·파랑.</summary>
        public static readonly int[,] Table1 = { { 5, 0, 9, 1 }, { 3, 7, 2, 0 } };

        /// <summary>방향 모듈: [행][색] → 방향(0▲ 1▼ 2◀ 3▶). 행 = {1,4} {2,7} {5,3} {6,9}.</summary>
        public static readonly int[,] Table2 = { { 0, 1, 0, 1 }, { 2, 3, 2, 3 }, { 2, 0, 3, 0 }, { 0, 3, 2, 1 } };

        /// <summary>방향 모듈 각 행의 점자 숫자 두 개.</summary>
        public static readonly int[][] Table2Digits = { new[] { 1, 4 }, new[] { 2, 7 }, new[] { 5, 3 }, new[] { 6, 9 } };

        /// <summary>표준 6점 점자에서 켜지는 점 번호(1·2·3 왼쪽 열 위→아래, 4·5·6 오른쪽 열). 표시용 사본.</summary>
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
                case 9: return new[] { 2, 4 };
                default: return new int[0];
            }
        }

        /// <summary>"점자 1,2,4" — 조작자가 채팅으로 모양을 전할 때 쓰는 문구.</summary>
        public static string BrailleChatText(int digit)
        {
            var dots = BrailleDots(digit);
            if (dots.Length == 0) return "점자 ?";
            var sb = new StringBuilder("점자 ");
            for (int i = 0; i < dots.Length; i++) { if (i > 0) sb.Append(','); sb.Append(dots[i]); }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 오브젝트 표(명세 4절)

        public static readonly string[] PropIds = { "panel", "poster", "lamp", "board", "lever", "mural", "colorlamp", "junction" };

        public static bool IsProp(string id)
        {
            foreach (var p in PropIds) if (p == id) return true;
            return false;
        }

        /// <summary>짧은 이름(상단 팀 칸·라벨용).</summary>
        public static string PropName(string id)
        {
            switch (id)
            {
                case "panel": return "제어반";
                case "poster": return "벽보";
                case "lamp": return "경고등";
                case "board": return "현황판";
                case "lever": return "레버반";
                case "mural": return "벽화";
                case "colorlamp": return "색상등";
                case "junction": return "배전함";
                default: return id ?? "";
            }
        }

        /// <summary>팝업 제목.</summary>
        public static string PropTitle(string id)
        {
            switch (id)
            {
                case "panel": return "제어반 — 계산 자물쇠";
                case "poster": return "벽보 — 계산 모듈 매뉴얼";
                case "lamp": return "경고등 #1";
                case "board": return "침수 현황판";
                case "lever": return "레버반 — 방향 자물쇠";
                case "mural": return "벽화 매뉴얼 — 방향 모듈";
                case "colorlamp": return "색상등";
                case "junction": return "배전함 — 경로 누적";
                default: return PropName(id);
            }
        }

        public static int PropSlot(string id)
        {
            switch (id)
            {
                case "panel": case "lever": return Op;
                case "poster": case "mural": return In;
                case "board": case "junction": return Na;
                case "lamp": case "colorlamp": return Ob;
                default: return -1;
            }
        }

        public static int PropRoom(string id)
        {
            switch (id)
            {
                case "panel": case "poster": case "lamp": return 1;
                case "board": return 2;
                case "lever": case "mural": case "colorlamp": case "junction": return 3;
                default: return -1;
            }
        }

        /// <summary>그 오브젝트의 미션이 끝났는가(끝나면 발광이 꺼진다).</summary>
        public static bool MissionDone(string id, StageState st)
        {
            if (st == null) return false;
            switch (id)
            {
                case "panel": case "poster": case "lamp": return st.gate1Done;
                case "board": return st.gate2Done;
                case "lever": case "mural": case "colorlamp": case "junction": return st.gate2Done;
                default: return false;
            }
        }

        /// <summary>내 담당 오브젝트가 지금 발광 중인가(담당 + 같은 방 + 미완료).</summary>
        public static bool IsGlowing(string id, int slot, Vector2? myPos, StageState st)
        {
            if (PropSlot(id) != slot || myPos == null || MissionDone(id, st)) return false;
            var map = Session.Map;
            if (map == null) return false;
            return map.RoomAt(myPos.Value) == PropRoom(id);
        }

        /// <summary>역할이 지금 단계에서 맡은 오브젝트 이름(상단 팀 칸).</summary>
        public static string PropForRole(int slot, int step)
        {
            if (step >= 2) return "분수";
            if (step == 0)
            {
                switch (slot) { case Op: return "제어반"; case In: return "벽보"; case Na: return "현황판"; case Ob: return "경고등"; }
            }
            else
            {
                switch (slot) { case Op: return "레버반"; case In: return "벽화"; case Na: return "배전함"; case Ob: return "색상등"; }
            }
            return "";
        }

        // ---------------------------------------------------------------- 로그 한 줄

        /// <summary>"n초 전" 표기.</summary>
        public static string Ago(float seconds)
        {
            if (seconds < 3f) return "방금";
            if (seconds < 60f) return $"{(int)seconds}초 전";
            return $"{(int)(seconds / 60f)}분 전";
        }

        /// <summary>
        /// 로그(수신 목록)에 적을 본문. 가시성 규칙:
        /// 채팅은 CanUnderstand, 색 신고의 색 이름은 통역(과 보낸 사람)만, 카드 값은 조작자(와 보낸 사람)만.
        /// </summary>
        public static string LogText(GameEvent e, int viewer, bool withGlyph = true)
        {
            if (e == null) return "";
            switch (e.type)
            {
                case "Signal":
                    {
                        var c = SignalColor(e.s);
                        string sub = SignalKorean(e.s);
                        string tail = string.IsNullOrEmpty(sub) ? "" : $"  <color=#8A96A8><size=11>{sub}</size></color>";
                        // withGlyph=false: 로그 패널이 픽셀 아이콘(danger/you)을 따로 그릴 때 — 색 네모·글리프 없이 단어만
                        string label = withGlyph ? $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>■</color> <b>{SignalLabel(e.s)}</b>" : $"<b>{SignalWord(e.s)}</b>";
                        return $"{label}{tail}";
                    }
                case "Chat":
                    return CanUnderstand(viewer, e.slot)
                        ? e.s
                        : $"<color=#C08890>{Garble(e.s)}</color> <size=11><color=#8A7A80>(외국어)</color></size>";
                case "Color":
                    {
                        bool see = viewer == In || viewer == e.slot;
                        return see
                            ? $"→ 통역  <color=#{ColorUtility.ToHtmlStringRGB(ColorValue(e.a))}>■</color> {ColorName(e.a)}"
                            : "→ 통역  [색 신고]";
                    }
                case "NumberCard":
                    {
                        bool see = viewer == Op || viewer == e.slot;
                        return see ? $"→ 조작자  <color=#F2C572>{NumberCardText(e.a)}</color>" : "→ 조작자  [카드]";
                    }
                case "DirCard":
                    {
                        bool see = viewer == Op || viewer == e.slot;
                        return see ? $"→ 조작자  <color=#F2C572>{DirCardText(e.a)}</color>" : "→ 조작자  [카드]";
                    }
                case "Ping":
                    // D5: 방 이름은 내비게이터 자신의 로그에만(표지판 ??? 원칙)
                    return viewer == Na
                        ? $"<color=#F2C572>여기로! ▼</color> {Stage1Logic.ZoneName(e.a)}"
                        : "<color=#F2C572>여기로! ▼</color> 방 입구의 표시를 따라가요";
                default:
                    return e.s ?? "";
            }
        }

        /// <summary>로그에 남는 이벤트 타입인가(명세 7-2).</summary>
        public static bool IsLogged(string type)
            => type == "Signal" || type == "Chat" || type == "Color" || type == "NumberCard" || type == "DirCard" || type == "Ping";
    }
}
