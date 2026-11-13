using System.Collections.Generic;
using UnityEngine;

namespace Paradox.Game
{
    /// <summary>컷씬 한 줄.</summary>
    public class StoryLine
    {
        public string speaker = "";   // 빈 문자열 = 내레이션
        public string text = "";
        public int portrait = -1;     // 0~3 = 해당 역할의 캐릭터, 10+ = NPC, -1 = 없음
        public int barrierMask;       // 비트가 켜진 슬롯에게는 ???로 보인다(언어 장벽)
        public bool system;           // 시스템 안내
        public string sceneTag = "";  // 장면 전환 표기("교실 점심시간" 등)
        public bool dark;             // 암전 연출
        public string bg = "";        // 배경 키: classroom / school / camp / museum_dark / museum_relief ("" = 이전 줄의 배경 유지)
        public bool group;            // 네 사람 초상을 나란히(클리어 컷씬·캠프 첫 만남)
        public bool blackout;         // 정전 연출: 이 줄에서 1.1초 완전 암전(자막 없음) 뒤 bg 가 드러난다 + 흔들림 (QA D-A2)
        public string sfx = "";       // 이 줄에 들어설 때 재생할 효과음 id(Sfx). "wind,siren" 처럼 쉼표로 차례 재생 (QA D-A1/D-A2)
        public int portraitRight = -1; // 우측 초상 슬롯(0~3 팀원, 10+ NPC, -1 없음). 좌측 portrait 와 같이 쓸 수 있다 (QA D-A3/P-A11)
        public string icon = "";      // 화면에 얹는 오브젝트 아이콘(SpriteLib.Icon 키, 예: poster) (QA D-A3)
        public string direction = ""; // 지문 — 이름표 아래 작은 회색 글씨("어색하게 웃으며") (QA D-A10)
    }

    public class Cutscene
    {
        public string id = "";
        public string title = "";
        public Color top = new Color(0.16f, 0.18f, 0.24f);
        public Color bottom = new Color(0.07f, 0.08f, 0.11f);
        public readonly List<StoryLine> lines = new List<StoryLine>();
        public int Count => lines.Count;

        /// <summary>index 줄의 배경 키(빈 줄이면 앞쪽에서 가장 가까운 값).</summary>
        public string BackgroundAt(int index)
        {
            for (int i = Mathf.Min(index, lines.Count - 1); i >= 0; i--)
                if (!string.IsNullOrEmpty(lines[i].bg)) return lines[i].bg;
            return "";
        }
    }

    /// <summary>NPC 초상 번호.</summary>
    public static class Npc
    {
        public const int Jiho = 10;      // 지호 (다문화가정 급우)
        public const int Friend1 = 11;
        public const int Friend2 = 12;
        public const int Teacher = 13;   // 담임 선생님
        public const int Staff = 14;     // 캠프 스태프

        public static string Name(int id)
        {
            switch (id)
            {
                case Jiho: return "지호";
                case Friend1: return "친구 1";
                case Friend2: return "친구 2";
                case Teacher: return "담임 선생님";
                case Staff: return "캠프 스태프";
                default: return "";
            }
        }
    }

    /// <summary>기획서의 프롤로그(로컬)·Stage 1 오프닝(네트워크)·클리어 대사를 데이터로 담는다. 컷씬 id가 GameLogic과의 계약이다.</summary>
    public static class StoryData
    {
        public const string PrologueId = "prologue";

        private static readonly Dictionary<string, Cutscene> Cache = new Dictionary<string, Cutscene>();

        private const int Op = 0, In = 1, Na = 2, Ob = 3;
        private const int MaskAllButOp = (1 << In) | (1 << Na) | (1 << Ob);

        public static Cutscene Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Cache.TryGetValue(id, out var c)) return c;
            c = Build(id);
            if (c != null) Cache[id] = c;
            return c;
        }

        public static int Length(string id)
        {
            var c = Get(id);
            return c != null ? c.Count : 0;
        }

        public static string ClearId(int stage) => $"s{stage}_clear";
        public static string IntroId(int stage) => $"s{stage}_intro";

        /// <summary>컷씬 안에서 조건에 맞는 첫 줄의 index(없으면 -1). 결과 화면·투어가 특정 줄을 찾을 때 쓴다.</summary>
        public static int FindLine(string id, System.Predicate<StoryLine> pred)
        {
            var c = Get(id);
            if (c == null || pred == null) return -1;
            for (int i = 0; i < c.lines.Count; i++) if (pred(c.lines[i])) return i;
            return -1;
        }

        /// <summary>클리어 컷씬의 주인공 속마음 한 줄(결과 화면 내레이션의 단일 출처, QA D-A10).</summary>
        public static string ClearInnerText(int stage)
        {
            string id = ClearId(stage);
            int i = FindLine(id, l => l.portrait == Op && !string.IsNullOrEmpty(l.speaker) && l.speaker.Contains("속마음"));
            return i >= 0 ? Get(id).lines[i].text : "";
        }

        private static Cutscene Build(string id)
        {
            switch (id)
            {
                case "prologue": return Prologue();
                case "s1_intro": return S1Intro();
                case "s1_clear": return S1Clear();
                default: return null;
            }
        }

        private static StoryLine N(string text, string tag = "", string bg = "", bool dark = false)
            => new StoryLine { text = text, sceneTag = tag, bg = bg, dark = dark };

        private static StoryLine L(string speaker, string text, int portrait = -1, int mask = 0, string bg = "", bool dark = false)
            => new StoryLine { speaker = speaker, text = text, portrait = portrait, barrierMask = mask, bg = bg, dark = dark };

        private static StoryLine Sys(string text) => new StoryLine { text = text, system = true };

        private static StoryLine Inner(string text, bool dark = false) => new StoryLine { speaker = "도윤 (속마음)", text = text, portrait = Op, dark = dark };

        // ------------------------------------------------------------------
        // 프롤로그 — 한국, 학교 (로컬 재생, 3씬)
        private static Cutscene Prologue()
        {
            var c = new Cutscene { id = "prologue", title = "프롤로그 — 한국, 학교", top = new Color(0.93f, 0.86f, 0.70f), bottom = new Color(0.72f, 0.62f, 0.48f) };

            // 씬 1 — 교실 점심시간
            c.lines.Add(N("한국의 어느 중학교, 점심시간. 도윤은 친구들과 왁자지껄 점심을 먹고 있다. 몇 자리 떨어진 곳에 지호가 혼자 앉아 있다.", "교실 점심시간", "classroom"));
            c.lines.Add(L("친구 1", "야, 지호 너 근데 진짜 한국말 잘한다ㅋㅋ 태어날 때부터 한국 살았다며?", Npc.Friend1));
            var jiho = L("지호", "어... 나 한국에서 태어났는데.", Npc.Jiho);
            jiho.direction = "어색하게 웃으며";   // QA D-A10: 지문은 본문 밖 이름표 아래
            c.lines.Add(jiho);
            c.lines.Add(L("친구 2", "근데 너희 엄마는 어느 나라 사람이야? 베트남?", Npc.Friend2));
            c.lines.Add(N("도윤은 옆에서 듣고 있지만 딱히 문제라고 느끼지 않고, 웃으며 자연스럽게 대화에 섞인다."));
            c.lines.Add(Inner("다들 그냥 궁금해서 물어보는 거잖아. 지호도 웃고 있고, 딱히 기분 나빠 보이지 않는데."));

            // 씬 2 — 조별 과제
            c.lines.Add(N("조별 과제를 정하는 시간. 도윤의 조에 지호가 배정된다.", "다음 날 · 조별 과제", "classroom"));
            c.lines.Add(L("도윤", "지호야, 너는 그냥... 자료만 정리해줄래? 발표는 내가 할게.", Op));
            c.lines.Add(N("지호는 별말 없이 고개를 끄덕이지만, 표정에 옅은 실망이 스친다. 도윤은 눈치채지 못한다."));
            c.lines.Add(Inner("발표는 내가 하는 게 더 낫지, 뭐. 지호도 딱히 하고 싶어 하는 눈치는 아니었고."));

            // 씬 3 — 며칠 후 · 하굣길
            c.lines.Add(N("집으로 가는 길, 담임 선생님이 도윤을 불러 세운다.", "며칠 후 · 하굣길", "school"));
            c.lines.Add(L("담임 선생님", "이번에 국제 청소년 교류 캠프 대표로 네가 뽑혔어. 여러 나라 친구들이랑 한 팀이 될 거야.", Npc.Teacher));
            c.lines.Add(N("도윤은 별생각 없이 좋아하며 집으로 향한다."));
            c.lines.Add(Inner("외국인 친구들이랑 팀이 된다고? 뭐, 말은 안 통해도 내가 잘 챙겨주면 되겠지."));
            return c;
        }

        // ------------------------------------------------------------------
        // Stage 1 오프닝 — 필리핀 캠프 → 박물관 (네트워크, 호스트 진행, 4씬)
        private static Cutscene S1Intro()
        {
            var c = new Cutscene { id = "s1_intro", title = "Stage 1 — 필리핀 · 침수된 박물관", top = new Color(0.35f, 0.62f, 0.78f), bottom = new Color(0.18f, 0.30f, 0.40f) };

            // 씬 1 — 캠프 첫 만남
            c.lines.Add(N("필리핀 — 해마다 20개 안팎의 태풍이 지나가는 '태풍의 길목'. 우기(6~11월)마다 태풍 피해가 되풀이되는 나라.", "캠프 첫 만남", "camp"));
            var meet = N("BAYANI CAMP. 캠프장에 도착한 도윤이 버스에서 내린다. 마리아, 린, 아이샤가 먼저 도착해 있다가 손을 흔들며 인사한다. 서로 말이 통하지 않아 몸짓으로만 인사가 오간다.");
            meet.group = true;   // QA D-A3: 네 사람이 나란히(SetTeamRow)
            c.lines.Add(meet);
            c.lines.Add(Inner("말이 안 통하니까... 내가 잘 챙겨줘야겠다. 얘네는 여기서 나보다 훨씬 불편할 테니까."));

            // 씬 2 — 미션 안내 (언어 장벽의 첫 시각화: 조작자만 정상 자막)
            c.lines.Add(N("캠프 스태프가 앞에 서서 팀별 미션을 설명한다.", "미션 안내", "camp"));
            c.lines.Add(L("캠프 스태프", "오늘부터 팀별로 미션을 수행합니다.", Npc.Staff, MaskAllButOp));   // QA D-A6: 기획서 원문 그대로
            c.lines.Add(L("캠프 스태프", "첫 목적지는 박물관입니다. 팀원끼리 서로 도우세요.", Npc.Staff, MaskAllButOp));
            c.lines.Add(Inner("그냥 내가 알아서 이끌면 되겠지."));

            // 씬 3 — 재난 발생 (QA D-A2: 정전 전에는 밝은 배경(camp 유지), '쿵' 줄에서 암전 뒤 museum_dark 가 드러난다)
            var storm = N("설명이 끝나자마자 팀은 박물관으로 들어선다. 들어선 직후, 창밖에서 강풍 소리가 몰아치더니 태풍 경보 사이렌이 울린다.", "재난 발생", "camp");
            storm.sfx = "wind,siren";
            c.lines.Add(storm);
            var thud = N("몇 초 뒤 '쿵' 하는 소리와 함께 건물 전체가 정전된다. 화면이 암전되었다가, 손전등 불빛만 켜진 어두운 로비가 드러난다.", "", "museum_dark");
            thud.blackout = true;
            thud.sfx = "thud";
            c.lines.Add(thud);
            c.lines.Add(Inner("어? 나... 아무것도 안 보이는데. 얘네가 날 도와줘야 하는 거였어?"));   // dark 오버레이 대신 museum_dark 유지

            // 씬 4 — 첫 소통 시도 (QA D-A3: 마리아(통역) 우측 초상 + 벽보 아이콘, 배경은 정전 로비 그대로)
            var poster = N("당황한 도윤이 주변을 둘러보는 사이, 마리아가 벽에 붙은 안내문 앞에 서서 무언가 읽는 듯한 표정을 짓는다.", "첫 소통 시도", "museum_dark");
            poster.portraitRight = In; poster.icon = "poster";
            c.lines.Add(poster);
            var wonder = Inner("쟤가 뭘 읽고 있는 것 같은데... 근데 나한테 뭘 전달하려는 거지?");
            wonder.portraitRight = In; wonder.icon = "poster";
            c.lines.Add(wonder);
            c.lines.Add(Sys("조작을 시작합니다. 박물관에 물이 차오르고 있어요 — 4명 전원이 동시에 집결 지점(중앙 분수)에 도착해야 클리어됩니다."));
            return c;
        }

        // ------------------------------------------------------------------
        // Stage 1 클리어 — 네 사람의 안도 + 내레이션
        private static Cutscene S1Clear()
        {
            var c = new Cutscene { id = "s1_clear", title = "Stage 1 클리어", top = new Color(0.20f, 0.28f, 0.36f), bottom = new Color(0.08f, 0.10f, 0.14f) };
            var relief = N("셔터가 열리고 네 사람이 분수 앞에 모여 안도의 숨을 내쉰다. 서로의 얼굴을 보며 어색하게, 그래도 함께 웃는다.", "클리어", "museum_relief");
            relief.group = true;
            c.lines.Add(relief);
            c.lines.Add(Inner("생각해보니까... 나 처음으로 뭘 몰라서 답답한 게 뭔지 알 것 같아. 얘네는 맨날 이런 기분이었나?"));
            return c;
        }
    }
}
