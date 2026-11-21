using System;
using System.Collections.Generic;
using Paradox.Client;
using Paradox.Game;
using UnityEngine;

/// <summary>
/// 박물관 장면의 네트워크 약속 + AI 친구의 말(자기 나라 말 + 그림 신호) + 배운 말 수첩.
///  · 이벤트 종류 40: 퍼즐 조작(b = 퍼즐 번호 * 256 + 동작, s = 값 문자열, f = 행동한 사람 슬롯)
///  · 이벤트 종류 41: 미션 완료(b = 끝낸 미션 번호)
///  · 이벤트 종류 42: AI 친구의 말(f = 말한 AI 슬롯, s = 말)
/// 오프라인이면 Story1Net 이 같은 이벤트를 바로 되돌려 주므로 온라인·혼자 모두 같은 길로 처리한다.
/// AI 의 판단은 방장(혼자면 나) 한 곳에서만 하고, 결과를 이벤트로 보낸다 → 모든 화면이 같은 것을 본다.
/// </summary>
public static class MuseumNet
{
    public const int KindPuzzle = 40, KindMission = 41, KindBotSay = 42, KindSnap = 43;

    public static event Action<int, int, string, int> PuzzleEvent;   // 퍼즐, 동작, 값, 행동한 슬롯
    public static event Action<int> MissionDone;
    public static event Action<int, int, string> SnapReceived;      // 미션 차례, 퍼즐 번호, 상태(쉼표로 이은 정수)

    private static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { PuzzleEvent = null; MissionDone = null; SnapReceived = null; hooked = false; }

    /// <summary>방장 → 모두: 미션 차례와 지금 퍼즐의 전체 상태(온라인 동기화 보정).</summary>
    public static void Snap(int stage, int puzzleId, string data) { Story1Net.Send(KindSnap, stage * 256 + puzzleId, data ?? ""); }

    public static void Hook()
    {
        if (hooked) return;
        hooked = true;
        Story1Net.Received += OnEvent;
    }

    public static void Unhook() { if (!hooked) return; hooked = false; Story1Net.Received -= OnEvent; }

    /// <summary>AI 를 움직이는 쪽인가(혼자 할 때는 나, 온라인은 방장).</summary>
    public static bool Authority => !Story1Net.Online || Session.IsHost;

    public static void Puzzle(int puzzle, int op, string value = "", int actor = -1)
    {
        if (actor < 0) actor = Story1Role.Current;
        Story1Net.Send(KindPuzzle, puzzle * 256 + op, value ?? "", actor);
    }

    public static void Mission(int stage) { Story1Net.Send(KindMission, stage); }

    public static void BotSay(int slot, string text) { Story1Net.Send(KindBotSay, 0, text ?? "", slot); }

    // ---- 팀원 핑(이벤트 종류 44: s = "층|방|x|y", 보낸 사람 = e.slot)
    public const int KindPing = 44;
    /// <summary>핑을 받았다(보낸 슬롯, 층, 방, 지도 좌표).</summary>
    public static event Action<int, string, string, Vector2> PingReceived;

    public static void Ping(string floor, string room, Vector2 map)
    {
        Story1Net.Send(KindPing, 0, (floor ?? "") + "|" + (room ?? "") + "|" + Mathf.RoundToInt(map.x) + "|" + Mathf.RoundToInt(map.y));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPing() { PingReceived = null; }

    private static void OnPing(GameEvent e)
    {
        var p = (e.s ?? "").Split('|');
        if (p.Length < 4 || !Roles.Valid(e.slot)) return;
        int.TryParse(p[2], out int x); int.TryParse(p[3], out int y);
        PingReceived?.Invoke(e.slot, p[0], p[1], new Vector2(x, y));
    }

    private static void OnEvent(GameEvent e)
    {
        if (e == null) return;
        if (e.a == KindPing) { OnPing(e); return; }
        switch (e.a)
        {
            case KindPuzzle:
            {
                int actor = Mathf.RoundToInt(e.f);
                if (!Roles.Valid(actor)) actor = e.slot;
                PuzzleEvent?.Invoke(e.b / 256, e.b % 256, e.s ?? "", actor);
                break;
            }
            case KindMission: MissionDone?.Invoke(e.b); break;
            case KindSnap: SnapReceived?.Invoke(e.b / 256, e.b % 256, e.s ?? ""); break;
            case KindBotSay:
            {
                int slot = Mathf.RoundToInt(e.f);
                string shown = MuseumClues.Shape(slot, e.s);   // 난이도에 따라 배운 낱말의 신호를 뺀다(보는 사람마다 다르다)
                if (Roles.Valid(slot)) MuseumChat.Say(slot, shown);
                MuseumWords.LearnFrom(slot, shown);
                break;
            }
        }
    }
}

/// <summary>
/// 네 사람의 말. 서하린(한국어)·이안(필리핀어)·린(베트남어, 도트 글꼴에 없는 성조 기호는 뺀다)·카이(줄루어).
/// AI 친구는 한국어를 모른다. 숫자·화살표·색·카드처럼 누구나 읽는 신호만 알아듣는다.
/// </summary>
public static class MuseumVoice
{
    public static readonly string[] Language = { "한국어", "필리핀어", "베트남어", "줄루어" };

    // 숫자 1~4
    private static readonly string[][] Num =
    {
        new[] { "하나", "둘", "셋", "넷" },
        new[] { "isa", "dalawa", "tatlo", "apat" },
        new[] { "mot", "hai", "ba", "bon" },
        new[] { "kunye", "kubili", "kuthathu", "kune" },
    };

    // 순서(첫째~넷째)
    private static readonly string[][] Ord =
    {
        new[] { "첫째", "둘째", "셋째", "넷째" },
        new[] { "una", "ikalawa", "ikatlo", "ikaapat" },
        new[] { "thu nhat", "thu hai", "thu ba", "thu tu" },
        new[] { "okokuqala", "okwesibili", "okwesithathu", "okwesine" },
    };

    private static readonly string[] LeftW = { "왼쪽", "kaliwa", "trai", "kwesobunxele" };
    private static readonly string[] RightW = { "오른쪽", "kanan", "phai", "kwesokudla" };
    private static readonly string[] Yes = { "응", "Oo", "Vang", "Yebo" };
    private static readonly string[] No = { "아니", "Hindi", "Khong", "Cha" };
    private static readonly string[] What = { "뭐?", "Ano?", "Gi co?", "Uxolo?" };
    private static readonly string[] Thanks = { "고마워", "Salamat", "Cam on", "Ngiyabonga" };
    private static readonly string[] Great = { "잘했어", "Galing!", "Gioi qua!", "Kuhle!" };
    private static readonly string[] Here = { "여기", "Dito", "Day", "Lapha" };
    private static readonly string[] Go = { "가자", "Tara na", "Di thoi", "Sihambe" };
    private static readonly string[] Wait = { "잠깐", "Sandali", "Doi chut", "Linda" };
    private static readonly string[] Mine = { "내 거", "Akin", "Cua toi", "Okwami" };
    private static readonly string[] Yours = { "네 거", "Iyo", "Cua ban", "Okwakho" };
    private static readonly string[] Friend = { "친구", "kaibigan", "ban", "umngane" };

    // 방위(북 동 남 서)
    private static readonly string[][] Dir =
    {
        new[] { "북쪽", "동쪽", "남쪽", "서쪽" },
        new[] { "hilaga", "silangan", "timog", "kanluran" },
        new[] { "bac", "dong", "nam", "tay" },
        new[] { "enyakatho", "empumalanga", "eningizimu", "entshonalanga" },
    };
    public static readonly string[] DirArrow = { "↑", "→", "↓", "←" };
    public static string Direction(int lang, int d) => Dir[Clamp(lang)][Mathf.Clamp(d, 0, 3)];

    public static string Number(int role, int n) => n >= 1 && n <= 4 ? Num[Clamp(role)][n - 1] : n.ToString();
    public static string Order(int role, int n) => n >= 1 && n <= 4 ? Ord[Clamp(role)][n - 1] : n.ToString();
    public static string Left(int role) => LeftW[Clamp(role)];
    public static string Right(int role) => RightW[Clamp(role)];
    public static string YesW(int role) => Yes[Clamp(role)];
    public static string NoW(int role) => No[Clamp(role)];
    public static string WhatW(int role) => What[Clamp(role)];
    public static string ThanksW(int role) => Thanks[Clamp(role)];
    public static string GreatW(int role) => Great[Clamp(role)];
    public static string HereW(int role) => Here[Clamp(role)];
    public static string GoW(int role) => Go[Clamp(role)];
    public static string WaitW(int role) => Wait[Clamp(role)];
    public static string MineW(int role) => Mine[Clamp(role)];
    public static string YoursW(int role) => Yours[Clamp(role)];
    public static string FriendW(int role) => Friend[Clamp(role)];

    private static int Clamp(int r) => Mathf.Clamp(r, 0, 3);

    /// <summary>모든 나라 말 낱말 → (뜻) 표. 수첩에 들어가는 것.</summary>
    public static readonly Dictionary<string, (int lang, string meaning)> Glossary = BuildGlossary();

    private static Dictionary<string, (int, string)> BuildGlossary()
    {
        var d = new Dictionary<string, (int, string)>();
        for (int l = 0; l < 4; l++)
        {
            for (int n = 0; n < 4; n++) { d[Num[l][n].ToLowerInvariant()] = (l, (n + 1).ToString()); d[Ord[l][n].ToLowerInvariant()] = (l, (n + 1) + "번째"); }
            d[LeftW[l].ToLowerInvariant()] = (l, "왼쪽 ←");
            d[RightW[l].ToLowerInvariant()] = (l, "오른쪽 →");
            d[Yes[l].ToLowerInvariant()] = (l, "응 ○");
            d[No[l].ToLowerInvariant()] = (l, "아니 ×");
            d[Thanks[l].ToLowerInvariant()] = (l, "고마워");
            d[Great[l].ToLowerInvariant().TrimEnd('!')] = (l, "잘했어!");
            d[Here[l].ToLowerInvariant()] = (l, "여기");
            d[Go[l].ToLowerInvariant()] = (l, "가자");
            d[Wait[l].ToLowerInvariant()] = (l, "잠깐");
            d[Friend[l].ToLowerInvariant()] = (l, "친구");
            for (int k = 0; k < 4; k++) d[Dir[l][k]] = (l, Dir[0][k] + " " + DirArrow[k]);
        }
        d["kasama"] = (1, "함께하는 친구");
        d["kataga"] = (1, "낱말");
        d["talaga"] = (1, "정말");
        d["tanaga"] = (1, "필리핀 전통 시");
        // 사전 자물쇠(PuzzleWords) 낱말: 불 · 물 · 별 · 마음
        for (int l = 1; l <= 3; l++)
            for (int m = 0; m < 4; m++) d[PuzzleWords.WordOf[l][m]] = (l, PuzzleWords.PicName[m]);
        d["bagyo"] = (1, "태풍");
        d["tubig"] = (1, "물");
        d["ilaw"] = (1, "불빛");
        d["banga"] = (1, "항아리");
        d["bahay kubo"] = (1, "필리핀 전통 집");
        d["habi"] = (1, "손으로 짠 천");
        d["xin chao"] = (2, "안녕");
        d["sawubona"] = (3, "안녕");
        d["kumusta"] = (1, "안녕");
        return d;
    }
}

/// <summary>배운 말 수첩. 친구가 그림 신호와 함께 한 말, 전시물에서 읽은 말이 모인다.</summary>
public static class MuseumWords
{
    public struct Entry { public string word; public int lang; public string meaning; }

    public static readonly List<Entry> Learned = new List<Entry>();
    private static readonly HashSet<string> keys = new HashSet<string>();
    public static event Action<Entry> NewWord;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Learned.Clear(); keys.Clear(); NewWord = null; }

    public static void Reset() { Learned.Clear(); keys.Clear(); }

    public static void Learn(string word, int lang, string meaning)
    {
        if (string.IsNullOrEmpty(word)) return;
        string k = word.ToLowerInvariant();
        if (!keys.Add(k)) return;
        var e = new Entry { word = word, lang = lang, meaning = meaning };
        Learned.Add(e);
        NewWord?.Invoke(e);
    }

    /// <summary>AI 친구의 말에서 아는 낱말을 찾아 수첩에 넣는다(한국어 화자인 서하린 기준. 다른 역할은 자기 말이 아닌 것만).</summary>
    public static void LearnFrom(int speaker, string text)
    {
        if (string.IsNullOrEmpty(text) || speaker == Story1Role.Current) return;
        string low = text.ToLowerInvariant();
        bool shown = low.Contains("[");   // 손가락 숫자([2])를 같이 보여 준 말이면 '몇 번째' 낱말도 뜻을 알 수 있다
        bool arrows = low.IndexOfAny(new[] { '←', '→', '↑', '↓' }) >= 0;   // 화살표를 같이 보여 준 말이면 방향 낱말을 배운다
        bool digits = false; foreach (char ch in low) if (ch >= '0' && ch <= '9') { digits = true; break; }   // 숫자 낱말은 숫자를 같이 볼 때 배운다
        bool interp = MuseumGame.IHave(MuseumGame.Interpreter);           // 통역은 화살표 없이도 뜻을 안다
        foreach (var kv in MuseumVoice.Glossary)
        {
            if (kv.Value.lang == Story1Role.Current || kv.Value.lang == 0) continue;   // 한국어 낱말은 화면 말이라 배울 필요 없음
            if (kv.Value.meaning.Contains("번째") && !shown && !interp) continue;   // 서수는 숫자를 보여 줄 때 배운다(먼저 짐작해 보게)
            if (kv.Value.meaning.Contains("쪽") && !arrows && !interp) continue;    // 방향·방위는 화살표를 볼 때 배운다
            if (kv.Value.meaning.Length == 1 && char.IsDigit(kv.Value.meaning[0]) && !digits && !interp) continue;   // 숫자 신호를 뺀 말(보통·어려움)로는 숫자 낱말을 배우지 않는다
            int i = low.IndexOf(kv.Key, StringComparison.Ordinal);
            if (i < 0) continue;
            bool startOk = i == 0 || !char.IsLetter(low[i - 1]);
            int end = i + kv.Key.Length;
            bool endOk = end >= low.Length || !char.IsLetter(low[end]);
            if (!startOk || !endOk) continue;
            Learn(text.Substring(i, kv.Key.Length), kv.Value.lang, kv.Value.meaning);
        }
    }

    public static int CountOf(int lang) { int n = 0; foreach (var e in Learned) if (e.lang == lang) n++; return n; }
}

/// <summary>사람이 쓴 채팅에서 누구나 읽는 신호(숫자·화살표·기호)를 뽑는다. AI 는 이것만 알아듣는다.</summary>
public static class MuseumSignals
{
    public static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("<-", "←").Replace("->", "→").Replace("<", "←").Replace(">", "→").Replace("^", "↑")
                .Replace("①", "1").Replace("②", "2").Replace("③", "3").Replace("④", "4");
    }

    /// <summary>한글만 있고 숫자·화살표·기호가 하나도 없는 말인가(AI 가 못 알아듣는 말).</summary>
    public static bool OnlyWords(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        bool hangul = false;
        foreach (char c in s)
        {
            if (c >= '0' && c <= '9') return false;
            if ("←→↑↓○×?!★●■▲♥".IndexOf(c) >= 0) return false;
            if (MuseumEmoji.Is(c)) return false;
            if (c >= 0xAC00 && c <= 0xD7A3) hangul = true;
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) hangul = true;
        }
        return hangul;
    }

    public static List<int> Digits(string s, int min = 1, int max = 9)
    {
        var l = new List<int>();
        foreach (char c in Normalize(s)) if (c >= '0' + min && c <= '0' + max) l.Add(c - '0');
        return l;
    }
}

/// <summary>
/// 난이도별 AI 단서 모양. AI 는 "자기 나라 말 + 같은 뜻의 신호"로 말한다(예: "Balbula dalawa! kanan, tatlo   [2] →3").
///  · 쉬움: 신호를 늘 붙인다(지금까지 그대로).
///  · 보통: 내가 이미 배운 낱말에는 신호를 떼고 말로만 보여 준다(배운 말을 떠올려야 한다 · 수첩 N). 처음 듣는 낱말은 신호와 함께(그때 배운다).
///  · 어려움: 처음 들을 때는 말로만. 같은 낱말을 두 번째 들을 때(? 로 다시 물었을 때)만 신호를 붙인다. 배운 낱말은 늘 말로만.
/// 같은 뜻의 낱말이 말 안에 있는 신호만 뗀다 → "하린 = [2]" 처럼 숫자 자체가 정답인 신호는 그대로 둔다.
/// AI 말은 모든 PC 에 같은 글로 오고, 각 PC 가 자기 수첩 기준으로 모양을 정한다(통역은 모든 낱말을 아는 것으로 본다).
/// </summary>
public static class MuseumClues
{
    private static readonly Dictionary<string, int> heard = new Dictionary<string, int>();
    private static readonly System.Text.RegularExpressions.Regex Token =
        new System.Text.RegularExpressions.Regex(@"\[(\d)\]|([←→↑↓])\s*(\d?)");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { heard.Clear(); }
    public static void Reset() { heard.Clear(); }

    private struct Hit { public string word; public int lang; public string meaning; }

    /// <summary>말 속 외국어 낱말(수첩 표에 있는 것). 한국어 낱말은 화면 말이라 늘 아는 것으로 본다.</summary>
    private static List<Hit> Words(string text)
    {
        var list = new List<Hit>();
        string low = text.ToLowerInvariant();
        foreach (var kv in MuseumVoice.Glossary)
        {
            int i = low.IndexOf(kv.Key, StringComparison.Ordinal);
            while (i >= 0)
            {
                int end = i + kv.Key.Length;
                bool a = i == 0 || !char.IsLetter(low[i - 1]), b = end >= low.Length || !char.IsLetter(low[end]);
                if (a && b) { list.Add(new Hit { word = kv.Key, lang = kv.Value.lang, meaning = kv.Value.meaning }); break; }
                i = low.IndexOf(kv.Key, end, StringComparison.Ordinal);
            }
        }
        return list;
    }

    private static bool Known(Hit h, bool interp)
    {
        if (h.lang == 0 || h.lang == Story1Role.Current || interp) return true;
        foreach (var e in MuseumWords.Learned) if (string.Equals(e.word, h.word, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static string Shape(int speaker, string text)
    {
        if (string.IsNullOrEmpty(text) || speaker == Story1Role.Current) return text;
        int diff = Story1Net.Difficulty;
        if (diff == Story1Rules.Easy || !Token.IsMatch(text)) return text;
        var words = Words(text);
        if (words.Count == 0) return text;
        bool interp = MuseumGame.IHave(MuseumGame.Interpreter);

        // 이번 말에서 처음 듣는(아직 못 배운) 낱말을 센다
        var fresh = new HashSet<string>();
        foreach (var h in words)
        {
            if (Known(h, interp)) continue;
            heard.TryGetValue(h.word, out int n);
            heard[h.word] = n + 1;
            if (diff == Story1Rules.Normal || n >= 1) fresh.Add(h.word);   // 보통: 처음 들을 때 · 어려움: 두 번째 들을 때 신호를 붙인다
        }

        return Token.Replace(text, m =>
        {
            // 이 신호와 같은 뜻의 낱말들
            var same = new List<Hit>();
            if (m.Groups[1].Success)
            {
                string n = m.Groups[1].Value;
                foreach (var h in words) if (h.meaning == n || h.meaning == n + "번째") same.Add(h);
            }
            else
            {
                string arrow = m.Groups[2].Value, n = m.Groups[3].Value;
                bool dirOk = false, numOk = n.Length == 0;
                foreach (var h in words)
                {
                    if (h.meaning.EndsWith(arrow)) { dirOk = true; same.Add(h); }
                    else if (n.Length > 0 && h.meaning == n) { numOk = true; same.Add(h); }
                }
                if (!dirOk || !numOk) same.Clear();
            }
            if (same.Count == 0) return m.Value;                       // 말로는 알 수 없는 신호(정답 번호 등) → 그대로
            foreach (var h in same) if (fresh.Contains(h.word)) return m.Value;   // 지금 배울 낱말 → 신호를 붙여 준다
            return "";                                                  // 이미 아는(또는 아직 들려줄 때가 아닌) 낱말 → 말로만
        }).Replace("[]", "").Trim();
    }
}
