using System.Collections.Generic;
using Paradox.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Editor
{
    /// <summary>
    /// Prologue.unity(씬 1 점심시간)를 씬 오브젝트로 만든다. 메뉴: Paradox ▸ Build Prologue Scene.
    /// 좌표·대사를 고치고 다시 실행하면 씬이 통째로 다시 만들어진다.
    /// </summary>
    public static class PrologueSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Prologue.unity";
        private const string Art = "Assets/Art/UI/Prologue/";
        private static readonly Color Ink = new Color(0.9f, 0.95f, 0.96f);
        private static readonly Color Amber = new Color(0.949f, 0.773f, 0.447f);

        internal static Sprite Spr(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            var filter = (path.Contains("bg_") && !path.Contains("bg_street")) || path.Contains("Portraits") ? FilterMode.Bilinear : FilterMode.Point;   // 그림은 부드럽게, 도트는 또렷하게
            if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single ||imp.filterMode != filter || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.mipmapEnabled))
            {
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.filterMode = filter;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        internal static RectTransform Node(string name, Transform parent)
        {
            var g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            return r;
        }

        internal static RectTransform At(RectTransform r, float x, float y, float w, float h) { r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); return r; }
        internal static RectTransform Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; return r; }

        internal static Image Img(RectTransform r, Sprite s, Color c)
        {
            var i = r.gameObject.AddComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = false; return i;
        }

        internal static PixelText Txt(Transform parent, string name, string text, int scale, Color color, float x, float y, float w, float h, TextAnchor a)
        {
            var r = Node(name, parent); At(r, x, y, w, h);
            var t = r.gameObject.AddComponent<PixelText>();
            t.scale = scale; t.align = a; t.color = color; t.text = text; t.raycastTarget = false;
            return t;
        }

        internal static PrologueLine C(string chapter, string sub, int bg, PrologueLine l) { l.chapter = chapter; l.chapterSub = sub; l.bg = bg; return l; }

        internal static PrologueLine A(string ambience, float vol, PrologueLine l) { l.ambience = ambience; l.ambVol = vol; return l; }
        internal static PrologueLine S(string sfx, float vol, PrologueLine l) { l.sfx = sfx; l.sfxVol = vol; return l; }

        internal static PrologueLine L(string speaker, string text, int kind, int portrait, int focus, float chatter, string note = "")
            => new PrologueLine { speaker = speaker, text = text, kind = kind, portrait = portrait, focus = focus, chatter = chatter, note = note };

        [MenuItem("Paradox/Build Prologue Scene")]
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
            var ctl = new GameObject("PrologueScene").AddComponent<PrologueScene>();

            // ---------------- 배경: 점심시간 급식실 그림 (1672x941 = 16:9, 화면에 꽉 차게)
            var bg = Node("Background", root); Stretch(bg);
            var pic = Node("LunchRoom", bg); Stretch(pic);
            var picImg = Img(pic, Spr(Art + "bg_lunch.png"), Color.white);

            // ---------------- 말하는 사람 위치(0 친구1, 1 친구2, 2 도윤, 3 말없는 친구, 4 지호) — 그림 위 머리 위치
            var cast = Node("Cast", root); Stretch(cast);
            float[] sx = { -397, -1, -196, 200, 786 };
            var sils = new RectTransform[5];
            for (int i = 0; i < 5; i++) { var r = Node("Spot" + i, cast); At(r, sx[i], 140, 40, 40); sils[i] = r; }

            var vig = Node("Vignette", root); Stretch(vig);
            Img(vig, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/vignette.png"), new Color(1, 1, 1, 0.55f));


            // ---------------- 대화창
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

            // ---------------- 페이드 + 건너뛰기 안내
            { var pill = Img(At(Node("SkipBg", root), 700, 440, 220, 52), null, new Color(0.04f, 0.05f, 0.08f, 0.72f)); pill.raycastTarget = false; }
            var hint = Txt(root, "SkipHint", "X  건너뛰기", 2, new Color(0.97f, 0.97f, 0.94f, 1f), 700, 440, 200, 36, TextAnchor.MiddleCenter);
            var bl = Node("Black", root); Stretch(bl); Img(bl, null, Color.black);
            var wait = Txt(bl, "WaitText", "", 3, Ink, 0, 0, 1500, 70, TextAnchor.MiddleCenter);
            var title = Txt(bl, "Title", "", 6, Amber, 0, 40, 1700, 100, TextAnchor.MiddleCenter);
            var sub = Txt(bl, "Sub", "", 3, new Color(0.6f, 0.65f, 0.7f), 0, -50, 1500, 60, TextAnchor.MiddleCenter);
            var bg2 = bl.gameObject.AddComponent<CanvasGroup>(); bg2.blocksRaycasts = false; bg2.interactable = false;

            // ---------------- 연결 + 대사(점심시간)
            ctl.black = bg2; ctl.sils = sils; ctl.dialogue = dlg; ctl.dialogueGroup = dg;
            ctl.portrait = prImg; ctl.portraitRect = pr;
            ctl.portraitSprites = new[]
            {
                Spr("Assets/Art/Portraits/Dialogue/01_Korean.png"),
                Spr(Art + "portrait_friend1.png"), Spr(Art + "portrait_friend2.png"),
                Spr("Assets/Art/Portraits/Dialogue/05_Jiho.png"),
                Spr(Art + "portrait_teacher.png")
            };
            ctl.nameText = name; ctl.tagText = tag; ctl.bodyText = body; ctl.nextMark = next; ctl.nameRect = name.rectTransform;
            ctl.waitText = wait;
            ctl.bgImage = picImg; ctl.bgSprites = new[] { picImg.sprite, Spr(Art + "bg_class.png"), Spr(Art + "bg_street.png") };
            ctl.titleText = title; ctl.subText = sub;
            var ca = new GameObject("CafeteriaCrowd").AddComponent<AudioSource>();
            ca.clip = Resources.Load<AudioClip>("Sfx/cafeteria_crowd"); ca.playOnAwake = false; ca.loop = true; ca.spatialBlend = 0f; ca.volume = 0f;
            ctl.crowd = ca;
            ctl.nextScene = "Camp";
            ctl.lines = new[]
            {
                L("", "한국의 어느 중학교, 점심시간. 도윤은 친구들과 왁자지껄 점심을 먹고 있다. 몇 자리 떨어진 곳에는 지호가 혼자 앉아 있다.", 1, -1, -1, 1f),
                L("친구 1", "야, 지호 너 한국말 진짜 잘한다ㅋㅋ 외국인 맞아?", 0, 1, 0, 0.8f),
                L("지호", "어… 나 한국에서 태어났는데.", 0, 3, 4, 0.15f, "어색하게 웃으며"),
                L("친구 2", "근데 너희 엄마는 어느 나라 사람이야? 베트남?", 0, 2, 1, 0.7f),
                L("지호", "어… 응. 베트남 분이셔.", 0, 3, 4, 0.15f, "머뭇거리다가"),
                L("친구 1", "오, 진짜? 그럼 베트남어도 할 줄 알아? 아무 말이나 해봐!", 0, 1, 0, 0.9f),
                L("지호", "아… 집에서는 조금 쓰는데, 지금은 좀…", 0, 3, 4, 0.1f, "난처하게 웃으며"),
                L("도윤", "에이, 한마디만! 나도 궁금하다. 지호야, 베트남어로 '안녕' 이 뭐야?", 0, 0, 2, 1f),
                L("지호", "…씬 짜오.", 0, 3, 4, 0.1f, "아주 작은 목소리로"),
                L("친구 2", "오~ 신기하다ㅋㅋ 또 해봐, 또!", 0, 2, 1, 1f),
                L("", "도윤은 문제라고 느끼지 않고, 웃으며 자연스럽게 대화에 섞인다.", 1, -1, 2, 1f),
                L("도윤", "에이, 그냥 장난이지. 지호도 웃고 있잖아.", 2, 0, 2, 0.5f),

                // ---------------- 씬 2: 조별 과제 (교실)
                C("다음 날 · 조별 과제", "사회 시간", 1, A("ambience_classroom", 0.3f, L("", "다음 날, 사회 시간. 칠판에는 '다문화 사회, 우리 주변의 이야기'라고 적혀 있다. 선생님이 조별 과제 조 편성표를 붙이고 나간다.", 1, -1, -1, 0f))),
                L("친구 1", "야야, 우리 조 누구야? 나 도윤이랑 같은 조 맞지?", 0, 1, 0, 0f),
                L("도윤", "어디 보자… 나, 너, 쟤, 그리고… 어, 지호도 우리 조네.", 0, 0, 2, 0f, "명단을 훑어보다가"),
                L("친구 2", "오, 지호 우리 조야? 잘됐다. 이번 주제 완전 너 얘기네ㅋㅋ", 0, 2, 1, 0f),
                L("지호", "어… 잘 부탁해.", 0, 3, 4, 0f, "조심스럽게"),
                L("도윤", "어, 어. 잘 부탁해.", 0, 0, 2, 0f, "잠깐 머뭇거리다가"),
                L("친구 1", "발표는 누가 해? 지호 네가 해도 되겠다. 베트남 얘기 하면 딱이잖아.", 0, 1, 0, 0f),
                L("지호", "나는… 사실 베트남에 가 본 적도 별로 없어서. 잘 모르는데…", 0, 3, 4, 0f, "머쓱하게"),
                L("도윤", "아, 그럼 발표는 내가 할게. 내가 말하는 건 좀 잘하잖아.", 0, 0, 2, 0f, "웃으며"),
                L("친구 2", "맞네, 도윤아 너가 하면 되겠네.", 0, 2, 1, 0f),
                L("지호", "저기… 나도 하고 싶은 부분이 있는데…", 0, 3, 4, 0f, "아주 작은 목소리로"),
                L("도윤", "아, 괜찮아 괜찮아. 넌 그냥… 자료만 정리해 줄래? 발표는 내가 할게.", 0, 0, 2, 0f, "이미 노트를 펴며"),
                L("지호", "…응. 알겠어.", 0, 3, 4, 0f, "잠시 말이 없다가"),
                L("", "지호는 고개를 끄덕인다. 아주 잠깐, 표정에 옅은 실망이 스친다.", 1, -1, -1, 0f),
                L("도윤", "발표는 내가 하는 게 낫지. 괜히 맡겼다가 지호가 부담스러워할 수도 있고.", 2, 0, 2, 0f),
                L("친구 1", "도윤아, 끝나고 PC방 갈 거지?", 0, 1, 0, 0f),
                L("도윤", "당연하지! 먼저 가 있어, 금방 갈게.", 0, 0, 2, 0f),
                S("school_bell", 0.7f, L("", "종이 울리자 아이들은 우르르 교실을 빠져나간다. 지호는 혼자 남아, 조용히 자료를 정리하기 시작한다.", 1, -1, -1, 0f)),

                // ---------------- 씬 3: 하굣길 (학교 앞 횡단보도)
                C("며칠 후 · 하굣길", "학교 앞 횡단보도", 2, A("ambience_street", 0.3f, L("", "며칠 뒤, 하굣길. 도윤은 친구들과 헤어져 혼자 교문을 나선다. 횡단보도 앞에서 신호가 바뀌기를 기다리는데, 뒤에서 누군가 도윤을 부른다.", 1, -1, -1, 0f))),
                L("담임 선생님", "도윤아! 잠깐만 기다려 봐.", 0, 4, -1, 0f),
                L("도윤", "어, 선생님. 무슨 일이세요?", 0, 0, 2, 0f),
                L("담임 선생님", "좋은 소식이 있어서. 이번에 국제 청소년 교류 캠프 대표로 네가 뽑혔어.", 0, 4, -1, 0f),
                L("도윤", "네? 제가요?", 0, 0, 2, 0f, "놀라서"),
                L("담임 선생님", "그래. 선생님들 사이에서 네가 친구들을 잘 이끈다고 말이 많더라. 이번 조별 과제도 네가 잘 맡았고.", 0, 4, -1, 0f),
                L("담임 선생님", "여러 나라 친구들이랑 한 팀이 될 거야. 말도 문화도 다르니까 쉽지는 않겠지만, 네가 잘 해낼 거라고 믿는다.", 0, 4, -1, 0f),
                L("도윤", "우와, 외국 친구들이랑요? 재밌겠다! 열심히 해 볼게요, 선생님.", 0, 0, 2, 0f, "활짝 웃으며"),
                L("담임 선생님", "그래, 준비 잘하고. 출발하기 전에 다시 안내해 줄게. 조심히 들어가렴.", 0, 4, -1, 0f),
                L("", "선생님이 돌아가고, 신호등이 초록색으로 바뀐다. 도윤은 신이 나서 가방끈을 고쳐 메고 횡단보도를 건넌다.", 1, -1, -1, 0f),
                L("도윤", "외국인 친구들이랑 팀이 된다고? 뭐, 말은 안 통해도 내가 잘 챙겨주면 되겠지.", 2, 0, 2, 0f),
                L("", "노을이 천천히 내려앉는다. 도윤의 발걸음은, 가볍다.", 1, -1, -1, 0f),
            };

            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == ScenePath);
            int at = list.FindIndex(s => s.path == "Assets/Scenes/Lobby.unity");
            list.Insert(at >= 0 ? at + 1 : list.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log("[PrologueSceneBuilder] " + ScenePath + " 저장 완료");
        }
    }
}
