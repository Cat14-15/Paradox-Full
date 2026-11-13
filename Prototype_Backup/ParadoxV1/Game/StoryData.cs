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
        public bool system;           // 시스템 안내(스테이지 규칙 요약)
        public string sceneTag = "";  // 장면 전환 표기("교실 점심시간" 등)
        public bool dark;             // 암전 연출
    }

    public class Cutscene
    {
        public string id = "";
        public string title = "";
        public Color top = new Color(0.16f, 0.18f, 0.24f);
        public Color bottom = new Color(0.07f, 0.08f, 0.11f);
        public readonly List<StoryLine> lines = new List<StoryLine>();
        public int Count => lines.Count;
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

    /// <summary>기획서의 프롤로그·스테이지 도입/클리어·엔딩 스토리를 데이터로 담는다.</summary>
    public static class StoryData
    {
        private static readonly Dictionary<string, Cutscene> Cache = new Dictionary<string, Cutscene>();

        private const int Op = 0, In = 1, Na = 2, Ob = 3;
        private const int MaskAllButOp = (1 << In) | (1 << Na) | (1 << Ob);
        private const int MaskAllButNa = (1 << Op) | (1 << In) | (1 << Ob);
        private const int MaskAllButOb = (1 << Op) | (1 << In) | (1 << Na);

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

        /// <summary>스테이지 클리어 후 이어지는 컷씬 id.</summary>
        public static string ClearId(int stage) => stage >= 4 ? "ending" : $"s{stage}_clear";
        public static string IntroId(int stage) => $"s{stage}_intro";

        private static Cutscene Build(string id)
        {
            switch (id)
            {
                case "prologue": return Prologue();
                case "s1_intro": return S1Intro();
                case "s1_clear": return S1Clear();
                case "s2_intro": return S2Intro();
                case "s2_clear": return S2Clear();
                case "s3_intro": return S3Intro();
                case "s3_clear": return S3Clear();
                case "s4_intro": return S4Intro();
                case "ending": return Ending();
                default: return null;
            }
        }

        private static StoryLine N(string text, string tag = "", bool dark = false)
            => new StoryLine { text = text, sceneTag = tag, dark = dark };

        private static StoryLine L(string speaker, string text, int portrait = -1, int mask = 0)
            => new StoryLine { speaker = speaker, text = text, portrait = portrait, barrierMask = mask };

        private static StoryLine Sys(string text) => new StoryLine { text = text, system = true };

        private static StoryLine Inner(string text) => new StoryLine { speaker = "한국인 (속마음)", text = text, portrait = Op };

        // ------------------------------------------------------------------
        private static Cutscene Prologue()
        {
            var c = new Cutscene { id = "prologue", title = "프롤로그 — 한국, 학교", top = new Color(0.93f, 0.86f, 0.70f), bottom = new Color(0.72f, 0.62f, 0.48f) };
            c.lines.Add(N("한국의 어느 중학교, 점심시간. 주인공은 친구들과 왁자지껄 점심을 먹고 있다. 몇 자리 떨어진 곳에 지호가 혼자 앉아 있다.", "교실 점심시간"));
            c.lines.Add(L("친구 1", "야, 지호 너 근데 진짜 한국말 잘한다ㅋㅋ 태어날 때부터 한국 살았다며?", Npc.Friend1));
            c.lines.Add(L("지호", "(어색하게 웃으며) 어… 나 한국에서 태어났는데.", Npc.Jiho));
            c.lines.Add(L("친구 2", "근데 너희 엄마는 어느 나라 사람이야? 베트남?", Npc.Friend2));
            c.lines.Add(N("주인공은 이 대화를 옆에서 듣고 있지만, 딱히 문제라고 느끼지 않고 웃으며 자연스럽게 대화에 섞인다."));
            c.lines.Add(Inner("다들 그냥 궁금해서 물어보는 거잖아. 지호도 웃고 있고, 딱히 기분 나빠 보이지 않는데."));
            c.lines.Add(N("조별 과제를 정하는 시간. 주인공의 조에 지호가 배정된다.", "조별 과제"));
            c.lines.Add(L("한국인", "지호야, 너는 그냥… 자료만 정리해줄래? 발표는 내가 할게.", Op));
            c.lines.Add(N("지호는 별말 없이 고개를 끄덕이지만, 표정에 옅은 실망이 스친다. 주인공은 이를 눈치채지 못한다."));
            c.lines.Add(Inner("발표는 내가 하는 게 더 낫지, 뭐. 지호도 딱히 하고 싶어 하는 눈치는 아니었고."));
            c.lines.Add(N("집으로 가는 길, 담임 선생님이 주인공을 불러 세운다.", "하굣길"));
            c.lines.Add(L("담임 선생님", "이번에 국제 청소년 교류 캠프 대표로 네가 뽑혔어. 여러 나라 친구들이랑 한 팀이 될 거야.", Npc.Teacher));
            c.lines.Add(Inner("외국인 친구들이랑 팀이 된다고? 뭐, 말은 안 통해도 내가 잘 챙겨주면 되겠지."));
            return c;
        }

        private static Cutscene S1Intro()
        {
            var c = new Cutscene { id = "s1_intro", title = "Stage 1 — 필리핀, 태풍·침수", top = new Color(0.35f, 0.62f, 0.78f), bottom = new Color(0.18f, 0.30f, 0.40f) };
            c.lines.Add(N("필리핀 — 연간 20개 안팎의 태풍이 지나가는 '태풍의 길목'. 우기(6~11월)마다 태풍 피해가 반복되는 나라.", "필리핀 캠프장"));
            c.lines.Add(N("캠프장에 도착한 주인공이 버스에서 내린다. 필리핀인, 베트남인, 남아공인이 먼저 도착해 손을 흔든다. 서로 말이 통하지 않아 몸짓으로만 인사가 오간다."));
            c.lines.Add(Inner("말이 안 통하니까… 내가 잘 챙겨줘야겠다. 얘네는 여기서 나보다 훨씬 불편할 테니까."));
            c.lines.Add(L("캠프 스태프", "오늘부터 팀별로 미션을 수행합니다. 첫 목적지는 견학 건물입니다. 팀원끼리 서로 도우세요.", Npc.Staff, MaskAllButOp));
            c.lines.Add(N("한국인은 친구들의 화면에 물음표만 떠 있다는 걸 모른 채, 특별히 이상하게 여기지 않고 넘어간다."));
            c.lines.Add(Inner("그냥 내가 알아서 이끌면 되겠지."));
            c.lines.Add(N("설명이 끝나자마자 팀은 첫 목적지 건물로 들어선다. 창밖에서 강풍 소리가 몰아치더니 — 태풍 경보 사이렌이 울린다.", "견학 건물"));
            c.lines.Add(N("'쿵' — 건물 전체가 정전된다. 화면이 암전되었다가, 손전등 불빛만 켜진 어두운 복도가 드러난다.", "", true));
            c.lines.Add(Inner("어? 나… 아무것도 안 보이는데. 얘네가 날 도와줘야 하는 거였어?"));
            c.lines.Add(N("당황한 한국인이 주변을 둘러보는 사이, 필리핀인이 벽에 붙은 안내문 앞에 서서 무언가 읽는 듯한 표정을 짓는다.", "", true));
            c.lines.Add(Inner("쟤가 뭘 읽고 있는 것 같은데… 근데 나한테 뭘 전달하려는 거지?"));
            c.lines.Add(Sys("릴레이 체인 — 이번 현지인은 통역(필리핀인). 통역만 표지판을 읽을 수 있다.\n통역 → 내비게이터 → 조작자 순서로 글자 카드를 그대로 전달하고, 조작자는 도착한 단어와 같은 레버를 고른다.\n관측자는 정전 경고등이 최고조일 때 ⚠ 버튼을 눌러 신호를 지켜라."));
            return c;
        }

        private static Cutscene S1Clear()
        {
            var c = new Cutscene { id = "s1_clear", title = "Stage 1 클리어", top = new Color(0.20f, 0.28f, 0.36f), bottom = new Color(0.08f, 0.10f, 0.14f) };
            c.lines.Add(N("셔터가 열리고 네 사람이 로비에 모여 안도의 숨을 내쉰다. 서로의 얼굴을 보며 어색하게 웃는다."));
            c.lines.Add(Inner("생각해보니까… 나 처음으로 뭘 몰라서 답답한 게 뭔지 알 것 같아. 얘네는 맨날 이런 기분이었나?"));
            return c;
        }

        private static Cutscene S2Intro()
        {
            var c = new Cutscene { id = "s2_intro", title = "Stage 2 — 베트남, 터널 침수", top = new Color(0.22f, 0.48f, 0.52f), bottom = new Color(0.08f, 0.18f, 0.22f) };
            c.lines.Add(N("베트남 — 긴 해안선과 메콩강 삼각주를 가진 나라. 우기의 폭우와 홍수는 도시의 지하 공간을 위협한다.", "베트남 지하 터널"));
            c.lines.Add(N("캠프의 다음 목적지는 지하 터널 견학. 그런데 폭우로 터널이 침수되기 시작한다. 이번엔 시간이 없다."));
            c.lines.Add(L("베트남인", "이 표지판… 펌프 모드를 먼저 맞추고 수문을 동시에 열라고 써 있어! 내가 읽을 수 있어!", Na, MaskAllButNa));
            c.lines.Add(Inner("이번엔 필리핀 친구도 표지판을 못 읽는 눈치다. 베트남 친구만 뭔가 아는 것 같은데… 정보를 가진 사람이 매번 다르구나."));
            c.lines.Add(Sys("3자 동시 조작 — 이번 현지인은 내비게이터(베트남인).\n내비게이터는 표지판의 펌프 모드를 글자 카드로 조작자에게 알리고, 조작자는 콘솔을 그 모드로 맞춘 뒤 붙잡는다(방벽 사수).\n통역은 물살이 약할 때 GO 신호를, 관측자는 GO에 맞춰 수문을 동시에 당겨라. 그 뒤 내비게이터가 미니맵으로 물이 안 차는 통로를 안내한다."));
            return c;
        }

        private static Cutscene S2Clear()
        {
            var c = new Cutscene { id = "s2_clear", title = "Stage 2 클리어", top = new Color(0.16f, 0.30f, 0.34f), bottom = new Color(0.06f, 0.10f, 0.12f) };
            c.lines.Add(N("상부 통로에 올라선 네 사람. 발밑에서 물소리가 멀어진다."));
            c.lines.Add(Inner("필리핀 친구가 이번엔 나처럼 아무것도 못 읽는 얼굴이었어. 그런데도 물살 타이밍을 알려줬지. 아는 게 달라도, 할 수 있는 일은 있구나."));
            return c;
        }

        private static Cutscene S3Intro()
        {
            var c = new Cutscene { id = "s3_intro", title = "Stage 3 — 유독가스 누출", top = new Color(0.45f, 0.52f, 0.30f), bottom = new Color(0.16f, 0.18f, 0.10f) };
            c.lines.Add(N("캠프의 세 번째 목적지(국적 미정). 화학 공장 견학 중, 갑작스러운 유독가스 누출 경보가 울린다.", "화학 공장"));
            c.lines.Add(N("시야가 뿌옇게 흐려진다. 위험 구역의 색상 표시와 경고문은 남아공인(관측자)만 읽을 수 있다."));
            c.lines.Add(L("남아공인", "초록 구역만 안전하대! 노란 구역도 위험하다고 써 있어!", Ob, MaskAllButOb));
            c.lines.Add(Inner("표지판은 관측자가 읽고, 지도는 내비게이터가 보고, 통역은 감지기를 들고 있다. 다들 아는 게 다르다. 겹치는 곳을 찾아야 해."));
            c.lines.Add(Sys("정보 교차검증 — 이번 현지인은 관측자(남아공인).\n관측자는 색상 고글로 안전 구역을, 내비게이터는 미니맵으로 구조 안전 구역을, 통역은 감지기로 위험 구역을 공용 보드에 표시하라.\n세 표시가 겹치는 단 하나의 구역으로 전원이 이동하고, 관측자와 내비게이터가 확인 패널을 누르면 문이 열린다."));
            return c;
        }

        private static Cutscene S3Clear()
        {
            var c = new Cutscene { id = "s3_clear", title = "Stage 3 클리어", top = new Color(0.30f, 0.36f, 0.22f), bottom = new Color(0.10f, 0.12f, 0.08f) };
            c.lines.Add(N("문이 열리고 맑은 공기가 밀려온다. 네 사람이 기침을 하며 서로를 부축한다."));
            c.lines.Add(Inner("말은 몰라도 도울 방법은 있었다. 다음은… 우리 나라다."));
            return c;
        }

        private static Cutscene S4Intro()
        {
            var c = new Cutscene { id = "s4_intro", title = "Stage 4 — 한국, 지하 화재 (역할 반전)", top = new Color(0.62f, 0.32f, 0.18f), bottom = new Color(0.18f, 0.08f, 0.05f) };
            c.lines.Add(N("한국 — 캠프의 마지막 목적지, 주인공의 나라. 지하상가 견학 중 화재가 발생한다.", "한국 지하상가"));
            c.lines.Add(N("연기가 자욱하다. 비상 안내 방송과 표지판은 모두 한국어. 이번엔 조작자만 읽을 수 있다."));
            c.lines.Add(L("세 친구", "…?! (당황한 목소리로 무언가 묻지만, 알아들을 수 없다)", In, 1 << Op));
            c.lines.Add(Inner("이번엔 내가 유일하게 읽을 수 있다. 그리고… 말이 아니라 버튼으로만 전해야 한다. 얘네가 나한테 해줬던 것처럼."));
            c.lines.Add(Sys("역할 반전 — 이번 현지인은 조작자(한국인). 채팅 없이 버튼 5개와 글자 카드로만 소통한다.\n관측자의 고글과 내비게이터의 미니맵은 연기로 무력화. 조작자만 전체 지도와 방송 자막을 본다.\n세 친구를 각자의 장치(소화기·비상벨·셔터 스위치)까지 안내하고, 불길을 ⚠로 경고하고, 방송의 탈출 암호를 글자 카드로 전달하라. 세 친구가 암호를 동시에 입력하면 셔터가 열린다."));
            return c;
        }

        private static Cutscene Ending()
        {
            var c = new Cutscene { id = "ending", title = "엔딩", top = new Color(0.95f, 0.78f, 0.55f), bottom = new Color(0.55f, 0.35f, 0.30f) };
            c.lines.Add(N("셔터가 열리고, 네 사람이 지상으로 뛰쳐나온다. 햇빛 아래에서 서로를 보며 웃는다.", "지상"));
            c.lines.Add(N("말은 여전히 통하지 않는다. 그래도 네 사람은 동시에 손을 들어 — 하이파이브."));
            c.lines.Add(Inner("나도 다른 나라에 가면 이방인이 되고, 우리 나라에서는 내가 누군가를 도와줄 수 있는 사람이 된다."));
            c.lines.Add(Inner("돌아가면 지호한테… 발표, 같이 하자고 말해야겠다."));
            return c;
        }
    }
}
