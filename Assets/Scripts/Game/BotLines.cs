using System;
using System.Collections.Generic;

namespace Paradox.Game
{
    /// <summary>
    /// AI 동료(통역·내비게이터·관측자)가 조작자에게 보내는 신호. 사람 동료와 똑같이 "신호 카드"(숫자·방향·색 단어·응/아니/여기)로만 말한다 —
    /// 문장을 만들지 않는다. 반환: [0]=통역, [1]=내비게이터, [2]=관측자. 유니티 비의존.
    /// 카드 뜻: 색 단어(불·물·별·풀) = 빨강·파랑·노랑·초록, 화살표(← ↑ →) = 왼쪽·가운데·오른쪽(또는 "A → B" 는 A 다음 B / A 가 B 로).
    /// </summary>
    public static class BotLines
    {
        public static readonly int[] RoleOrder = { (int)GameRole.Interpreter, (int)GameRole.Navigator, (int)GameRole.Observer };

        /// <summary>신호 카드의 색 단어(퍼즐 색 번호 0~3 = 빨강·파랑·노랑·초록).</summary>
        public static readonly string[] ColorWord = { "불", "물", "별", "풀" };
        public static readonly string[] LaneArrow = { "←", "↑", "→" };
        public const string Yes = "응", No = "아니";

        private const string Gap = "    ";

        public static string[] Fuse(FusePuzzle p)
        {
            // 통역: "A → B" = A 퓨즈가 B 퓨즈보다 왼쪽
            var tr = ColorWord[p.order[0].left] + " → " + ColorWord[p.order[0].right] + Gap + ColorWord[p.order[1].left] + " → " + ColorWord[p.order[1].right];
            // 내비게이터: "색 번호" = 그 색은 그 슬롯
            var nv = ColorWord[p.wired.color] + " " + (p.wired.slot + 1);
            // 관측자: "번호 아니 색" = 그 슬롯에는 그 색이 안 됨
            var ob = (p.banned[0].slot + 1) + " " + No + " " + ColorWord[p.banned[0].color] + Gap + (p.banned[1].slot + 1) + " " + No + " " + ColorWord[p.banned[1].color];
            return new[] { tr, nv, ob };
        }

        /// <summary>stage: 지금 건너려는 구간(0~2). sparkOn: 지금 불꽃이 튀는가.</summary>
        public static string[] Gallery(GalleryPuzzle p, int stage, bool sparkOn)
        {
            stage = Math.Max(0, Math.Min(GalleryPuzzle.Stages - 1, stage));
            var tr = (stage + 1) + " " + No + " " + LaneArrow[p.decoy[stage]];       // 막힌 통로
            var open = new List<string>();
            for (int l = 0; l < GalleryPuzzle.Lanes; l++) if (p.Connected(stage, l)) open.Add(LaneArrow[l]);
            var nv = (stage + 1) + " " + string.Join(" ", open.ToArray());            // 이어진 통로들
            var ob = sparkOn ? No : Yes;                                              // 불꽃이 꺼졌으면 응(건너도 돼)
            return new[] { tr, nv, ob };
        }

        public static string[] Water(WaterPuzzle p)
        {
            // 통역: "위치 → 이름표 번째"
            var tags = new List<string>();
            for (int i = 0; i < WaterPuzzle.Valves; i++) tags.Add((i + 1) + " → " + (p.tagOf[i] + 1));
            var tr = string.Join(Gap, tags.ToArray());
            // 내비게이터: 열려야 할 이름표 번째들
            var nums = new List<string>();
            foreach (int n in p.TargetNumbers()) nums.Add(n.ToString());
            var nv = string.Join(" ", nums.ToArray()) + " " + Yes;
            // 관측자: "위치 → 같이 도는 위치"
            var links = new List<string>();
            for (int i = 0; i < WaterPuzzle.Valves; i++) links.Add((i + 1) + " → " + (p.link[i] + 1));
            var ob = string.Join(Gap, links.ToArray());
            return new[] { tr, nv, ob };
        }

        public static string[] Relic(RelicPuzzle p)
        {
            // 통역: 이름표 시대의 오래된 순서(첫째~넷째) — 카드로는 숫자만
            var tr = "1 → 2 → 3 → 4";
            // 내비게이터: "받침대 → 올려야 할 번째"
            var ped = new List<string>();
            for (int i = 0; i < RelicPuzzle.Pedestals; i++) ped.Add((i + 1) + " → " + (p.pedestalEra[i] + 1));
            var nv = string.Join(Gap, ped.ToArray());
            // 관측자: "번호 아니" = 그 칸은 복제품
            var ob = (p.fake + 1) + " " + No;
            return new[] { tr, nv, ob };
        }

        /// <summary>라운드 1~3: 각 AI 는 자기 원이 몇 번째 박에 켜지는지 숫자 카드로 말한다.</summary>
        public static string[] FinalRound(FinalPuzzle p, int round)
        {
            var r = new string[3];
            for (int i = 0; i < 3; i++) r[i] = (p.RankOf(round, RoleOrder[i]) + 1).ToString();
            return r;
        }

        public static string[] FinalGo()
        {
            return new[] { Yes, Yes, Yes };
        }

        // ================================================================ 읽기 쉬운 한국어 버전(화면에 보이는 AI 의 말)
        // 위 카드 버전은 규칙·테스트용으로 남겨 두고, 같은 정보를 문장으로 풀어 쓴다. 정답은 같은 퍼즐 데이터에서 그대로 뽑는다.

        private static readonly string[] Lane = { "왼쪽", "가운데", "오른쪽" };

        public static string[] FuseSay(FusePuzzle p)
        {
            string C(int c) => FusePuzzle.ColorKo[c];
            var tr = C(p.order[0].left) + "은 " + C(p.order[0].right) + "보다 왼쪽,  " + C(p.order[1].left) + "은 " + C(p.order[1].right) + "보다 왼쪽";
            var nv = C(p.wired.color) + "은 " + (p.wired.slot + 1) + "번 칸에 꽂아요";
            var ob = (p.banned[0].slot + 1) + "번 칸에 " + C(p.banned[0].color) + " 안 돼요,  " + (p.banned[1].slot + 1) + "번 칸에 " + C(p.banned[1].color) + " 안 돼요";
            return new[] { tr, nv, ob };
        }

        public static string[] GallerySay(GalleryPuzzle p, int stage, bool sparkOn)
        {
            stage = Math.Max(0, Math.Min(GalleryPuzzle.Stages - 1, stage));
            string n = (stage + 1) + "번째 구간  ";
            var tr = n + Lane[p.decoy[stage]] + " 길은 막혔어요";
            var open = new List<string>();
            for (int l = 0; l < GalleryPuzzle.Lanes; l++) if (p.Connected(stage, l)) open.Add(Lane[l]);
            var nv = n + string.Join(", ", open.ToArray()) + " 길이 이어져 있어요";
            var ob = sparkOn ? "불꽃이 튀어요. 기다려요" : "불꽃이 꺼졌어요. 지금 건너요";
            return new[] { tr, nv, ob };
        }

        public static string[] WaterSay(WaterPuzzle p)
        {
            var tags = new List<string>(); var links = new List<string>();
            for (int i = 0; i < WaterPuzzle.Valves; i++) { tags.Add((i + 1) + "→" + (p.tagOf[i] + 1)); links.Add((i + 1) + "→" + (p.link[i] + 1)); }
            var tr = "밸브→이름표 번호   " + string.Join("  ", tags.ToArray());
            var nums = new List<string>();
            foreach (int n in p.TargetNumbers()) nums.Add(n.ToString());
            var nv = "열어야 할 이름표   " + string.Join(", ", nums.ToArray()) + "번";
            var ob = "돌리면 같이 도는 밸브   " + string.Join("  ", links.ToArray());
            return new[] { tr, nv, ob };
        }

        public static string[] RelicSay(RelicPuzzle p)
        {
            var eras = new List<string>(); var ped = new List<string>();
            for (int i = 0; i < RelicPuzzle.Slots; i++) eras.Add((i + 1) + "→" + (p.eraOf[i] + 1));
            for (int i = 0; i < RelicPuzzle.Pedestals; i++) ped.Add((i + 1) + "→" + (p.pedestalEra[i] + 1));
            var tr = "칸→오래된 순서   " + string.Join("  ", eras.ToArray());
            var nv = "받침대→오래된 순서   " + string.Join("  ", ped.ToArray());
            var ob = (p.fake + 1) + "번 칸은 복제품이에요";
            return new[] { tr, nv, ob };
        }

        public static string[] FinalRoundSay(FinalPuzzle p, int round)
        {
            var r = new string[3];
            for (int i = 0; i < 3; i++) r[i] = "내 원은 " + (p.RankOf(round, RoleOrder[i]) + 1) + "번째로 켜져요";
            return r;
        }

        public static string[] FinalGoSay() => new[] { "지금이에요. 같이 밟아요", "지금이에요. 같이 밟아요", "지금이에요. 같이 밟아요" };
    }
}
