using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 채팅 이모티콘. 누구나 알아보는 그림이라 나라가 달라도 읽힌다(채팅의 모자이크에서 제외).
/// 글 안에서는 한 글자(개인 사용 영역 문자)로 다니고, 그릴 때 그림으로 바뀐다.
/// 그림은 Resources/Emoji/{이름}.png (정사각 도트 그림, 12x12 권장)를 넣으면 그걸 쓰고, 없으면 코드에 든 기본 도트 그림을 쓴다.
/// </summary>
public static class MuseumEmoji
{
    public const char Base = '';
    public static readonly string[] Keys = { "fire", "water", "bolt", "key", "door", "danger", "eye", "star", "heart", "smile", "sad", "ok",
                                             "bb_ka", "bb_sa", "bb_ma", "bb_ba", "bb_la", "bb_ta", "bb_na", "bb_ga" };
    public static readonly string[] Labels = { "불", "물", "전기", "열쇠", "문", "위험", "눈", "별", "하트", "웃음", "슬픔", "좋아",
                                               "KA", "SA", "MA", "BA", "LA", "TA", "NA", "GA" };
    public static int Count => Keys.Length;
    /// <summary>채팅 이모티콘 선택창에 보이는 개수(뒤쪽은 바이바이인 글자 그림 — AI 친구가 보여 줄 때만 쓴다).</summary>
    public const int PickCount = 12;
    /// <summary>바이바이인 글자 그림의 첫 번호(KA SA MA BA LA TA NA GA 차례).</summary>
    public const int Baybayin = 12;

    public static bool Is(char c) => c >= Base && c < Base + Keys.Length;
    public static char Char(int index) => (char)(Base + index);

    private static readonly Sprite[] cache = new Sprite[20];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { System.Array.Clear(cache, 0, cache.Length); }

    public static Sprite Get(int index)
    {
        if (index < 0 || index >= Keys.Length) return null;
        if (cache[index] != null) return cache[index];
        Texture2D tex = Resources.Load<Texture2D>("Emoji/" + Keys[index]);
        if (tex == null) tex = Draw(Pattern[index]);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        cache[index] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        return cache[index];
    }

    private static readonly Dictionary<char, Color32> Palette = new Dictionary<char, Color32>
    {
        ['k'] = new Color32(36, 26, 22, 255), ['r'] = new Color32(226, 70, 50, 255), ['o'] = new Color32(250, 150, 40, 255),
        ['y'] = new Color32(255, 224, 80, 255), ['w'] = new Color32(255, 255, 255, 255), ['b'] = new Color32(40, 110, 220, 255),
        ['c'] = new Color32(130, 205, 255, 255), ['g'] = new Color32(80, 180, 90, 255), ['p'] = new Color32(245, 140, 170, 255),
        ['n'] = new Color32(150, 100, 60, 255), ['d'] = new Color32(90, 70, 60, 255), ['m'] = new Color32(214, 170, 90, 255),
    };

    // 한 줄이 한 행, '|' 로 행을 나눈다(위에서 아래로)
    private static Texture2D Draw(string pattern)
    {
        string[] rows = pattern.Split('|');
        int h = rows.Length, w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char ch = x < rows[y].Length ? rows[y][x] : '.';
                tex.SetPixel(x, h - 1 - y, Palette.TryGetValue(ch, out var c) ? c : new Color32(0, 0, 0, 0));
            }
        tex.Apply();
        return tex;
    }

    private static readonly string[] Pattern =
    {
        "......r.....|.....rr.....|....rrr..r..|...rrorr.rr.|...roooorrr.|..rrooyoorr.|..rooyyyoor.|..rooyyyoor.|..rooyywyor.|...rooyyoor.|....rroorr..|.....rrrr...",
        ".....bb.....|.....bb.....|....bccb....|....bccb....|...bcccwb...|...bccwcb...|..bcccwccb..|..bccccccb..|..bccccccb..|...bccccb...|....bbbb....|............",
        "......yyyy..|.....yyyy...|....yyyy....|...yyyyyyyy.|.....yyyyy..|....yyyy....|....yyy.....|...yyy......|...yy.......|..yy........|..y.........|............",
        "............|..mmmm......|.mmoomm.....|.mo..om.....|.mo..ommmmmm|.mmoomm..m.m|..mmmm...m.m|............|............|............|............|............",
        "...nnnnnn...|..nddddddn..|..ndnnnndn..|..ndnnnndn..|..ndnnnndn..|..ndnnnmmdn.|..ndnnnnndn.|..ndnnnnndn.|..ndnnnnndn.|..ndnnnnndn.|..nddddddddn|............",
        ".....kk.....|....kyyk....|...kyyyyk...|...kyddyk...|..kyyddyyk..|..kyyddyyk..|.kyyyddyyyk.|.kyyyyyyyyk.|kyyyyddyyyyk|kyyyyddyyyyk|kkkkkkkkkkkk|............",
        "............|............|...kkkkkk...|..kwwwwwwk..|.kwwbbbbwwk.|kwwbbddbbwwk|kwwbbddbbwwk|.kwwbbbbwwk.|..kwwwwwwk..|...kkkkkk...|............|............",
        ".....yy.....|.....yy.....|....yyyy....|yyyyyyyyyyyy|.yyyyyyyyyy.|..yyyyyyyy..|...yyyyyy...|...yyyyyy...|..yyy..yyy..|.yyy....yyy.|.yy......yy.|............",
        "............|..rrr..rrr..|.rrprrrrrrr.|rrprrrrrrrrr|rrrrrrrrrrrr|rrrrrrrrrrrr|.rrrrrrrrrr.|..rrrrrrrr..|...rrrrrr...|....rrrr....|.....rr.....|............",
        "....kkkk....|..kkyyyykk..|.kyyyyyyyyk.|.kyyyyyyyyk.|kyyykyykyyyk|kyyyyyyyyyyk|kyyyyyyyyyyk|kyykyyyykyyk|.kyykkkkyyk.|.kyyyyyyyyk.|..kkyyyykk..|....kkkk....",
        "....kkkk....|..kkyyyykk..|.kyyyyyyyyk.|.kyyyyyyyyk.|kyyykyykyyyk|kyyycyyyyyyk|kyyyyyyyyyyk|kyyyykkyyyyk|.kyykyykyyk.|.kyyyyyyyyk.|..kkyyyykk..|....kkkk....",
        "....kkkk....|..kkggggkk..|.kggggggwgk.|.kgggggwwgk.|kggwgggwwggk|kgggwgwwgggk|kggggwwggggk|kgggggwggggk|.kggggggggk.|.kggggggggk.|..kkggggkk..|....kkkk....",
        // 바이바이인(필리핀 옛 글자) — 흰 선으로 그려 두고 쓰는 곳에서 색을 입힌다
        "................|................|................|...........wwww.|........www...w.|.ww...www.......|..wwwww.w.......|........w.......|........w.......|........w.wwwww.|........www...w.|.ww...www.......|..wwwww.........|................|................|................",   // KA
        "................|................|.wwww.....wwww..|....w....ww...w.|....w...ww....w.|....w...w.....w.|....w..w.....ww.|....w.ww...www..|....w.w.......w.|....www.......w.|....ww........w.|....ww.......ww.|....w....wwwww..|................|................|................",   // SA
        "................|................|................|............ww..|...........w..w.|.www......w.....|...w......w.....|...wwwwwww......|...w....w.......|...w....w.......|...w...w........|...w..ww........|....ww..........|................|................|................",   // MA
        "................|................|......wwww......|....ww....ww....|...ww......ww...|...w........w...|..w..........w..|..w..........w..|.w...........w..|.w...........w..|.w...........w..|.w.....w....ww..|.ww...www...w...|..wwww..wwww....|................|................",   // BA
        "................|................|.........wwwwww.|.w....wwww......|..wwwwww........|.......w........|.......ww.......|........ww......|.......ww.......|........ww......|........ww......|.......ww.......|.......w........|................|................|................",   // LA
        "................|................|................|.........wwwwww.|........ww......|......ww........|.w...ww.........|.w..ww..........|.wwww...........|...ww...........|...w............|..ww............|..w.............|................|................|................",   // TA
        "................|.....wwwww......|...ww..w..ww....|...w...ww..w....|..w.....ww..w...|.ww....ww...ww..|.w....www....w..|.w......ww...w..|.w.....ww....w..|.w....ww.....w..|.w.....www...w..|.w.....ww....w..|.ww...ww....ww..|......w.........|................|................",   // NA
        "................|................|.....ww.........|...wwwwwwwww....|........w..ww...|........w...w...|.......ww...w...|....wwww....w...|.......ww...w...|........w...w...|........w...w...|........w...w...|.......ww...ww..|.wwwwww......ww.|................|................",   // GA
    };
}
