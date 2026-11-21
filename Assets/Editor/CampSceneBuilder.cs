using System.Collections.Generic;
using Paradox.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Paradox.Editor.PrologueSceneBuilder;

namespace Paradox.Editor
{
    /// <summary>
    /// Camp.unity(Stage 1 오프닝 씬 1 — 캠프 첫 만남)를 만든다. 메뉴: Paradox ▸ Build Camp Scene.
    /// PrologueScene 컴포넌트를 그대로 쓰고(isCamp), 프롤로그 다음 · Paradox 앞에 놓인다.
    /// </summary>
    public static class CampSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Camp.unity";
        private static readonly Color Ink = new Color(0.9f, 0.95f, 0.96f);
        private static readonly Color Amber = new Color(0.949f, 0.773f, 0.447f);

        private static PrologueLine Op(PrologueLine l) { l.operatorOnly = true; return l; }
        private static PrologueLine D(float dark, PrologueLine l) { l.dark = dark; return l; }
        private static PrologueLine M(string bgm, PrologueLine l) { l.bgm = bgm; return l; }

        // 가운데에 부드러운 구멍이 뚫린 어둠(손전등). 처음 한 번만 PNG 로 만든다.
        private static Sprite Spotlight()
        {
            const string path = "Assets/Art/UI/Camp/spotlight.png";
            if (!System.IO.File.Exists(path))
            {
                const int N = 256;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(N / 2f, N / 2f));
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 23f, d)) * 0.96f;
                        t.SetPixel(x, y, new Color(0.01f, 0.012f, 0.02f, a));
                    }
                System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Sprite; imp.filterMode = FilterMode.Bilinear; imp.wrapMode = TextureWrapMode.Clamp; imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [MenuItem("Paradox/Build Camp Scene")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중에는 만들 수 없어요"); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.orthographic = true;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();

            var cv = new GameObject("Canvas");
            var canvas = cv.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cv.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            cv.AddComponent<GraphicRaycaster>();
            var root = cv.GetComponent<RectTransform>();
            var ctl = new GameObject("CampScene").AddComponent<PrologueScene>();
            ctl.isCamp = true; ctl.bgmTrack = "camp"; ctl.startAmbience = "ambience_camp"; ctl.startAmbVol = 0.15f;

            // 배경: BAYANI CAMP 그림(3:2) — 화면 폭에 맞춰 키우고 위아래를 조금 잘라낸다(1920x1280)
            var bg = Node("Background", root); Stretch(bg);
            var pic = Node("CampYard", bg); At(pic, 0, 0, 1920, 1280);
            var picImg = Img(pic, Spr("Assets/Art/UI/Camp/bg_camp.png"), Color.white);

            var vig = Node("Vignette", root); Stretch(vig);
            Img(vig, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/vignette.png"), new Color(1, 1, 1, 0.55f));

            var dk = Node("Blackout", root); Stretch(dk);
            var dkg = dk.gameObject.AddComponent<CanvasGroup>(); dkg.alpha = 0f; dkg.blocksRaycasts = false; dkg.interactable = false;
            var hole = Node("Spotlight", dk); At(hole, 0, 60, 6000, 6000);
            Img(hole, Spotlight(), Color.white);
            var fillR = Node("Fill", dk); Stretch(fillR);
            var darkFill = Img(fillR, null, new Color(0.01f, 0.012f, 0.02f, 0f));

            var dlg = Node("Dialogue", root); Stretch(dlg);
            var dg = dlg.gameObject.AddComponent<CanvasGroup>(); dg.blocksRaycasts = false; dg.interactable = false;
            var pr = Node("Portrait", dlg); pr.pivot = new Vector2(0.5f, 0f);
            At(pr, -680, -232, 470, 514);
            var prImg = Img(pr, null, new Color(1, 1, 1, 0)); prImg.preserveAspect = true;

            var box = Node("Box", dlg); At(box, 0, -365, 1760, 330);
            var fill = Img(box, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Lobby/panel_fill.png"), new Color(0.05f, 0.06f, 0.09f, 0.95f));
            fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = 1f / 3f;
            var bd = Node("Border", box); Stretch(bd);
            var bdi = Img(bd, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Lobby/panel_border.png"), new Color(0.62f, 0.5f, 0.3f));
            bdi.type = Image.Type.Sliced; bdi.pixelsPerUnitMultiplier = 1f / 3f;

            var name = Txt(dlg, "Name", "", 3, Ink, -150, -268, 500, 56, TextAnchor.MiddleLeft);
            var tag = Txt(dlg, "Tag", "", 2, new Color(0.55f, 0.6f, 0.66f), 0, -272, 400, 36, TextAnchor.MiddleLeft);
            var body = Txt(dlg, "Body", "", 3, Ink, 0, -415, 1600, 200, TextAnchor.UpperLeft);
            var next = Txt(dlg, "Next", "▼", 2, Amber, 810, -466, 40, 36, TextAnchor.MiddleCenter);
            var skipPill = TopRight(Node("SkipBg", root), 32, 32, 240, 56);
            { var pill = Img(skipPill, null, new Color(0.04f, 0.05f, 0.08f, 0.72f)); pill.raycastTarget = false; }
            Txt(skipPill, "SkipHint", "X  건너뛰기", 2, new Color(0.97f, 0.97f, 0.94f, 1f), 0, 0, 220, 36, TextAnchor.MiddleCenter);

            var bl = Node("Black", root); Stretch(bl); Img(bl, null, Color.black);
            var wait = Txt(bl, "WaitText", "", 3, Ink, 0, 0, 1500, 70, TextAnchor.MiddleCenter);
            var title = Txt(bl, "Title", "", 6, Amber, 0, 40, 1700, 100, TextAnchor.MiddleCenter);
            var sub = Txt(bl, "Sub", "", 3, new Color(0.6f, 0.65f, 0.7f), 0, -50, 1500, 60, TextAnchor.MiddleCenter);
            var bg2 = bl.gameObject.AddComponent<CanvasGroup>(); bg2.blocksRaycasts = false; bg2.interactable = false;

            ctl.darkGroup = dkg; ctl.darkFill = darkFill; ctl.black = bg2; ctl.sils = new RectTransform[0]; ctl.dialogue = dlg; ctl.dialogueGroup = dg;
            ctl.portrait = prImg; ctl.portraitRect = pr;
            ctl.portraitSprites = new[]
            {
                Spr("Assets/Art/Portraits/Dialogue/01_Korean.png"),
                Spr("Assets/Art/Portraits/Dialogue/02_Filipino.png"),
                Spr("Assets/Art/Portraits/Dialogue/04_Vietnamese.png"),
                Spr("Assets/Art/Portraits/Dialogue/03_South_African.png"),
                Spr("Assets/Art/UI/Prologue/portrait_teacher.png")   // 4 스태프(실루엣)
            };
            ctl.nameText = name; ctl.tagText = tag; ctl.bodyText = body; ctl.nextMark = next; ctl.nameRect = name.rectTransform;
            ctl.waitText = wait;
            ctl.bgImage = picImg; ctl.bgSprites = new[] { picImg.sprite, Spr("Assets/Art/UI/Camp/hall.png") };
            ctl.titleText = title; ctl.subText = sub;
            var ca = new GameObject("CampAmbience").AddComponent<AudioSource>();
            ca.playOnAwake = false; ca.loop = true; ca.spatialBlend = 0f; ca.volume = 0f;
            ctl.crowd = ca;
            ctl.nextScene = "MainStory1";

            // 초상 번호: 0 서하린, 1 이안, 2 린, 3 카이
            ctl.lines = new[]
            {
                C("Stage 1 · BAYANI CAMP", "필리핀 · 국제 청소년 교류 캠프, 첫째 날", 0,
                    L("", "필리핀. 해마다 태풍이 스무 개 가까이 지나가는 '태풍의 길목'이다. 우기가 오면 이 나라는 같은 피해를 되풀이해서 겪는다.", 1, -1, -1, 0f)),
                L("", "그리고 오늘은 국제 청소년 교류 캠프의 첫날. 서하린을 태운 버스가 BAYANI CAMP 앞에 천천히 멈춰 선다.", 1, -1, -1, 0f),
                S("bus_arrive", 0.5f, L("", "치이익~ 버스 문이 열린다. 후끈한 공기가 훅 끼쳐 온다.", 1, -1, -1, 0f)),
                L("서하린", "와… 진짜 외국이다. 공기부터 다르네. 덥고, 습하고, 나무는 엄청 크고.", 2, 0, -1, 0f),
                L("서하린", "내가 대표로 뽑힌 거니까 처음부터 잘해야지. 어깨 펴고, 웃으면서.", 2, 0, -1, 0f),
                L("", "서하린은 캐리어를 끌고 버스에서 내린다. 본관 입구의 현수막에는 'Together for a Brighter Tomorrow' 라고 적혀 있다. 그 아래, 세 명이 서하린을 향해 손을 흔들고 있다.", 1, -1, -1, 0f),
                L("이안", "Kumusta! Maligayang pagdating!", 0, 1, -1, 0f, "환하게 손을 흔들며"),
                L("서하린", "…무슨 말이지? 영어는 아닌데. 일단 웃자.", 2, 0, -1, 0f),
                L("서하린", "아, 안녕! 나는 서하린이야. 한국에서 왔어!", 0, 0, -1, 0f, "자기 가슴을 톡톡 치며, 또박또박"),
                L("", "이안이 고개를 갸웃한다. 서하린은 한 번 더, 더 천천히, 더 크게 말한다.", 1, -1, -1, 0f),
                L("서하린", "서-하-린. 서하린!", 0, 0, -1, 0f, "큰 소리로"),
                L("이안", "Seoharin? …Seoharin!", 0, 1, -1, 0f, "눈이 커지며"),
                L("서하린", "맞아, 서하린! 너는?", 0, 0, -1, 0f, "이안을 가리키며"),
                L("이안", "Ako si Ian. Ian!", 0, 1, -1, 0f, "자기를 가리키며"),
                L("린", "Xin chao! Toi la Lin.", 0, 2, -1, 0f, "조용히 한 걸음 다가와"),
                L("서하린", "이름은 알아듣겠는데… 뒤는 하나도 모르겠다.", 2, 0, -1, 0f),
                L("카이", "Sawubona! Kai! I am Kai!", 0, 3, -1, 0f, "두 팔을 크게 흔들며"),
                L("", "카이가 손을 번쩍 들어 하이파이브를 청한다. 서하린이 마주 치자, 짝! 하고 경쾌한 소리가 난다. 린은 두 손을 모으고 가볍게 고개를 숙인다.", 1, -1, -1, 0f),
                L("서하린", "손짓이랑 표정으로는 얼추 통하네. 이 정도면 할 만한데?", 2, 0, -1, 0f),
                L("이안", "Tulungan na kita!", 0, 1, -1, 0f, "서하린의 캐리어 손잡이를 가리키며"),
                L("서하린", "아, 괜찮아 괜찮아! 내가 들 수 있어!", 0, 0, -1, 0f, "손을 휘휘 저으며"),
                L("", "이안은 멈칫하다가 웃으며 물러선다. 이번에는 린이 서하린의 물병을 가리키고, 카이가 본관을 가리킨다. 세 사람이 자기 말로 한꺼번에 말을 걸어 오지만, 서하린에게는 어느 것도 말이 되지 않는다.", 1, -1, -1, 0f),
                L("서하린", "정신없다… 그래도 다들 반겨 주는 건 알겠어.", 2, 0, -1, 0f),
                L("서하린", "모두 말이 안 통해서 불편할 텐데. 내가 먼저 나서서 잘 챙겨 줘야겠다.", 2, 0, -1, 0f),
                A("ambience_camp", 0.2f, L("", "그때, 야자수 잎이 크게 흔들린다. 하늘 한쪽이 어느새 잿빛이다. 이안이 그쪽을 한참 올려다본다.", 1, -1, -1, 0f)),
                L("서하린", "이안? 왜 그래? 하늘에 뭐 있어?", 0, 0, -1, 0f, "같이 올려다보며"),
                L("이안", "…Ayos lang. Halika na.", 0, 1, -1, 0f, "고개를 젓고는 다시 웃으며"),
                L("", "이안이 손짓으로 본관 쪽을 가리킨다. 마침 본관 문이 열리고, 스태프가 손뼉을 치며 모두를 불러 모은다.", 1, -1, -1, 0f),
                L("서하린", "오늘부터 진짜 시작이구나. 좋아, 가 보자!", 2, 0, -1, 0f),

                // ---------------- 씬 2: 미션 안내 (조작자 화면에만 정상 자막, 나머지는 ???)
                C("미션 안내", "BAYANI CAMP 본관 앞", -1,
                    L("", "잠시 뒤, 본관 앞 공터. 스태프가 네 사람을 한자리에 불러 세운다. 서하린, 이안, 린, 카이. 오늘부터 이 넷이 한 팀이다.", 1, -1, -1, 0f)),
                Op(L("스태프", "모두 모였죠? 만나서 반갑습니다. 저는 이번 캠프 진행을 맡은 스태프예요.", 0, 4, -1, 0f)),
                Op(L("스태프", "오늘부터는 팀별로 미션을 수행합니다. 네 명이 한 팀이에요.", 0, 4, -1, 0f)),
                L("서하린", "다들 표정이 굳었네. 설명을 못 알아듣는 눈치야. 이럴 땐 내가 나서야지.", 2, 0, -1, 0f),
                Op(L("스태프", "첫 목적지는 박물관입니다. 서로 도와 가면서, 반드시 팀원끼리 힘을 합치세요.", 0, 4, -1, 0f)),
                Op(L("스태프", "박물관 안은 복잡하니까 절대 혼자 떨어지지 마세요. 하늘이 심상치 않아서, 일정은 서두르겠습니다.", 0, 4, -1, 0f)),
                L("서하린", "첫 목적지가 박물관이래. 얘네는 못 알아들은 것 같은데… 그냥 내가 알아서 이끌면 되겠지.", 2, 0, -1, 0f),
                L("서하린", "박물관! 뮤지엄! 우리, 같이, 가자!", 0, 0, -1, 0f, "손짓에 서툰 영어를 섞어 크게"),
                L("린", "…Museum?", 0, 2, -1, 0f, "고개를 갸웃하며"),
                L("이안", "Oo, museo. Tara na!", 0, 1, -1, 0f, "고개를 끄덕이며 앞장서서"),
                L("", "이안이 알겠다는 듯 앞장서 걷기 시작한다. 카이가 손을 번쩍 들고 뒤를 따르고, 린도 말없이 걸음을 옮긴다.", 1, -1, -1, 0f),
                L("서하린", "봐, 통했잖아. 역시 내가 잘 이끌고 있어.", 2, 0, -1, 0f),
                L("서하린", "길은 쟤가 아는 것 같으니까 맡기고, 나는 전체를 챙기면 되겠지.", 2, 0, -1, 0f),

                // ---------------- 씬 3: 박물관 · 사이렌, 잠긴 문, 정전
                M("none", C("재난 발생", "박물관 중앙 홀", 1,
                    A("storm_wind", 0.16f, L("", "박물관 중앙 홀. 무거운 문을 밀고 들어서자 바람 소리가 한 겹 멀어진다. 높은 천장 아래, 조각상 하나가 네 사람을 내려다보고 있다.", 1, -1, -1, 0f)))),
                L("서하린", "우와, 크다… 박물관이 이렇게 넓을 줄은 몰랐네.", 2, 0, -1, 0f),
                L("", "카이는 조각상 앞에서 사진 찍는 시늉을 하고, 린은 벽의 안내문을 살핀다. 이안만 자꾸 창밖을 올려다본다.", 1, -1, -1, 0f),
                A("storm_wind", 0.34f, S("siren_far", 0.5f, L("", "그때, 멀리서 사이렌이 길게 울린다. 창밖의 야자수가 미친 듯이 흔들리고, 유리창이 덜덜 떨린다.", 1, -1, -1, 0f))),
                L("서하린", "뭐야, 무슨 소리야? 훈련인가?", 0, 0, -1, 0f, "두리번거리며"),
                L("이안", "Bagyo! May paparating na bagyo!", 0, 1, -1, 0f, "창밖을 가리키며 다급하게"),
                L("서하린", "다들 표정이 심각해… 무슨 일이 난 거지?", 2, 0, -1, 0f),
                L("", "린이 입구 쪽을 가리킨다. 카이가 제일 먼저 뛰어가 문손잡이를 힘껏 당긴다.", 1, -1, -1, 0f),
                S("door_locked", 0.8f, L("", "철컹, 철컹. 문이 열리지 않는다. 문이 잠겨 있다.", 1, -1, -1, 0f)),
                L("서하린", "잠겼다고? 방금 우리가 들어온 문인데!", 0, 0, -1, 0f, "손잡이를 흔들어 보며"),
                L("", "세 사람이 번갈아 문을 밀어 보지만 소용없다. 거세지는 바람이 문틈으로 비명처럼 새어 든다.", 1, -1, -1, 0f),
                S("museum_thud", 0.9f, L("", "그 순간, 쿵~ 건물 어딘가에서 둔탁한 소리가 울린다.", 1, -1, -1, 0f)),
                S("power_cut", 0.8f, D(1f, L("", "천장의 불빛이 두어 번 깜빡이다가, 툭, 꺼진다. 정전이다.", 1, -1, -1, 0f))),
                L("", "완전한 어둠. 바람 소리와 서로의 숨소리만 들린다.", 1, -1, -1, 0f),
                S("flashlight_click", 0.9f, D(2f, L("", "딸깍. 누군가 손전등을 켠다. 좁은 불빛 하나가 어둠 속에서 얼굴들을 하나씩 비춘다.", 1, -1, -1, 0f))),
                L("서하린", "어? 나… 아무것도 안 보이는데.", 2, 0, -1, 0f),
                L("서하린", "문은 잠겼고, 불은 나갔고… 내가 챙겨 줘야 하는 줄 알았는데, 얘네가 날 도와줘야 하는 거였어?", 2, 0, -1, 0f),

                // ---------------- 씬 4: 첫 소통 시도 (어둠 속, 각자 다른 것을 가리킨다) → 미션 안내 → Story1
                L("", "손전등 불빛이 흔들리며 벽에 붙은 안내문을 비춘다. 이안이 한 걸음 다가가 소리 내어 읽기 시작한다.", 1, -1, -1, 0f),
                L("이안", "Nasa likod ng reception ang fuse box! Kailangan nating ibalik ang kuryente!", 0, 1, -1, 0f, "안내문을 짚으며 다급하게"),
                L("서하린", "뭐라고 하는 거지? 표정이 엄청 급해 보이는데…", 2, 0, -1, 0f),
                L("서하린", "잠깐, 잠깐만! 일단 진정하고! 문은 잠겼으니까 반대쪽으로 가 보자!", 0, 0, -1, 0f, "손을 휘저으며 복도 쪽을 가리키고"),
                L("이안", "Hindi! Doon! Sa reception!", 0, 1, -1, 0f, "서하린의 팔을 붙잡아 막으며"),
                L("서하린", "내가 이끌어야 하는데… 왜 안 따라오지?", 2, 0, -1, 0f),
                L("", "이안이 안내실 쪽을 가리키며 무언가를 강하게 말한다. 서하린에게는 그저 알 수 없는 소리다. 그때 린이 손전등을 돌려 벽 한쪽을 비추고, 서하린의 어깨를 톡톡 두드린다.", 1, -1, -1, 0f),
                L("린", "Nhin nay! Nhin nay!", 0, 2, -1, 0f, "벽의 그림을 가리키며"),
                L("", "배선도처럼 보이는 그림이다. 이번에는 카이가 그 위의 색색 선을 짚으며 손가락으로 하나씩 센다.", 1, -1, -1, 0f),
                L("카이", "Bheka! Imibala! Obomvu, luhlaza... phuzi!", 0, 3, -1, 0f, "빨강, 파랑, 노랑을 차례로 짚으며"),
                L("서하린", "이안은 저쪽 방을, 린은 벽의 그림을, 카이는 색을… 다들 서로 다른 걸 가리키고 있어.", 2, 0, -1, 0f),
                L("서하린", "…얘네가 다 뭔가 알고 있어. 나만 아무것도 모르네.", 2, 0, -1, 0f),
                L("", "네 사람이 서로를 바라본다. 같은 말을 쓰는 사람은 아무도 없다. 하지만 이 어둠을 벗어나려면, 각자가 본 것을 모아야 한다는 건 모두가 안다.", 1, -1, -1, 0f),
                S("ui_select", 0.8f, L("", "[ 미션 1 ]  물길 막기 : 박물관 지하로 물이 들어오고 있다. 지하 관리 통로의 급수 밸브부터 잠근다.", 1, -1, -1, 0f)),
                S("ui_select", 0.8f, L("", "[ 미션 2 ]  불 켜기 : 그다음 지하 전기실의 차단기를 올려 박물관의 전원을 되살린다.", 1, -1, -1, 0f)),
                A("storm_wind", 0.4f, S("museum_water", 0.7f, L("", "바닥 아래 어딘가에서, 물이 차오르는 소리가 들려온다.", 1, -1, -1, 0f))),
            };

            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == ScenePath);
            int at = list.FindIndex(s => s.path == "Assets/Scenes/Prologue.unity");
            list.Insert(at >= 0 ? at + 1 : list.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[CampSceneBuilder] " + ScenePath + " 저장 완료");
        }
    }
}
