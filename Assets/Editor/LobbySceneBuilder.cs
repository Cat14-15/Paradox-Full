using System.Collections.Generic;
using Paradox.Client;
using Paradox.Game;
using Paradox.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Editor
{
    /// <summary>
    /// Lobby.unity(방 만들기/참가 → 캐릭터 선택)를 씬 오브젝트로 만들어 저장한다. 메뉴: Paradox ▸ Build Lobby Scene.
    /// 디자인을 고칠 땐 이 파일의 좌표·크기를 바꾸고 다시 실행하면 된다(씬이 통째로 다시 만들어진다).
    /// </summary>
    public static class LobbySceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Lobby.unity";
        private static readonly Color Mint = new Color(0.376f, 0.984f, 0.796f);
        private static readonly Color Amber = new Color(0.949f, 0.773f, 0.447f);
        private static readonly Color Warn = new Color(1f, 0.55f, 0.5f);
        private static readonly Color Dim = new Color(0.52f, 0.58f, 0.62f);
        private static readonly Color Ink = new Color(0.9f, 0.95f, 0.96f);

        private static Sprite Spr(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
        private static Sprite Lobby(string n) => Spr("Assets/Art/UI/Lobby/" + n + ".png");

        private static RectTransform Node(string name, Transform parent)
        {
            var g = new GameObject(name, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            return r;
        }

        private static RectTransform At(RectTransform r, float x, float y, float w, float h)
        {
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        private static RectTransform Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; return r;
        }

        private static Image Img(RectTransform r, Sprite s, Color c, bool raycast = false)
        {
            var i = r.gameObject.AddComponent<Image>();
            i.sprite = s; i.color = c; i.raycastTarget = raycast;
            return i;
        }

        private static CanvasGroup Group(RectTransform r, bool blocks = false)
        {
            var g = r.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = blocks; g.interactable = blocks;
            return g;
        }

        private static PixelText Txt(Transform parent, string name, string text, int scale, Color color, float x, float y, float w, float h, TextAnchor a = TextAnchor.MiddleCenter)
        {
            var r = Node(name, parent);
            At(r, x, y, w, h);
            var t = r.gameObject.AddComponent<PixelText>();
            t.scale = scale; t.align = a; t.color = color; t.text = text; t.raycastTarget = false;
            return t;
        }

        /// <summary>도트 테두리 패널. fill(어두운 판) + border(색이 바뀌는 테두리) + 선택 시 호버/클릭을 받는 PointerRelay.</summary>
        private static LobbyScene.Btn Panel(Transform parent, string name, float x, float y, float w, float h, LobbyScene owner, int group, int id)
        {
            var r = Node(name, parent);
            At(r, x, y, w, h);
            var fill = Img(r, Lobby("panel_fill"), new Color(0.055f, 0.065f, 0.085f, 0.94f), true);
            fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = 1f / 3f;
            var br = Node("Border", r); Stretch(br);
            var border = Img(br, Lobby("panel_border"), new Color(0.3f, 0.33f, 0.38f));
            border.type = Image.Type.Sliced; border.pixelsPerUnitMultiplier = 1f / 3f;
            var relay = r.gameObject.AddComponent<PointerRelay>();
            relay.owner = owner; relay.group = group; relay.id = id;
            return new LobbyScene.Btn { rect = r, border = border, fill = fill };
        }

        [MenuItem("Paradox/Build Lobby Scene")]
        public static void Build()
        {
            if (Application.isPlaying) { Debug.LogWarning("플레이 중에는 만들 수 없어요"); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.orthographic = true;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();

            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();

            new GameObject("NetHub").AddComponent<NetHub>();

            var cv = new GameObject("Canvas");
            var canvas = cv.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cv.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            cv.AddComponent<GraphicRaycaster>();
            var root = cv.GetComponent<RectTransform>();

            var mus = new GameObject("Music").AddComponent<SceneMusic>(); mus.track = "lobby"; mus.volume = 0.3f;
            var ctl = new GameObject("LobbyScene").AddComponent<LobbyScene>();

            // ---------------- 배경 (스테이지 선택과 같은 분위기)
            var bg = Node("Background", root); Stretch(bg);
            var hall = Node("HallArt", bg); At(hall, 0, 0, 2100, 1575);
            Img(hall, Spr("Assets/Art/Museum/00_Central_Hall.png"), new Color(0.2f, 0.22f, 0.27f));
            var vig = Node("Vignette", bg); Stretch(vig);
            Img(vig, Spr("Assets/Art/UI/vignette.png"), Color.white);
            var dust = Node("Dust", root); Stretch(dust);
            var rnd = new System.Random(21);
            for (int i = 0; i < 24; i++)
            {
                var d = Node("Dust" + i, dust);
                float sz = 6f + (float)rnd.NextDouble() * 10f;
                At(d, (float)(rnd.NextDouble() * 1900 - 950), (float)(rnd.NextDouble() * 1100 - 550), sz, sz);
                Img(d, Spr("Assets/Art/UI/dust.png"), new Color(1f, 0.9f, 0.65f, 0.4f));
                var dd = d.gameObject.AddComponent<DustDrift>();
                dd.speed = 8f + (float)rnd.NextDouble() * 16f; dd.sway = 6f + (float)rnd.NextDouble() * 18f;
                dd.phase = (float)(rnd.NextDouble() * 6.28); dd.alpha = 0.25f + (float)rnd.NextDouble() * 0.4f;
            }

            // ================================================================ ① 방 패널 (글은 꼭 필요한 것만)
            var roomR = Node("RoomPanel", root); Stretch(roomR);
            var roomG = Group(roomR, true);
            Txt(roomR, "Title", "팀 모으기", 5, Ink, 0, 390, 900, 90);

            var createCard = Panel(roomR, "CreateCard", 310, 0, 460, 480, ctl, 0, 0);   // 참가가 앞(왼쪽), 만들기가 뒤(오른쪽)
            var ci = Node("Icon", createCard.rect); At(ci, 0, 50, 160, 160); Img(ci, Lobby("icon_create"), Mint);
            Txt(createCard.rect, "Label", "방 만들기", 4, Ink, 0, -100, 420, 70);

            var joinCard = Panel(roomR, "JoinCard", -310, 0, 460, 480, ctl, 0, 1);
            var ji = Node("Icon", joinCard.rect); At(ji, 0, 110, 160, 160); Img(ji, Lobby("icon_join"), Mint);
            Txt(joinCard.rect, "Label", "방 참가하기", 4, Ink, 0, -20, 420, 70);
            var codeBorders = new Image[6]; var codeChars = new PixelText[6];
            for (int i = 0; i < 6; i++)
            {
                float cx = -(2.5f * 66f) + i * 66f;
                var box = Node("CodeBox" + i, joinCard.rect); At(box, cx, -150, 56, 80);
                var f = Img(box, Lobby("panel_fill"), new Color(0.03f, 0.035f, 0.05f, 1f)); f.type = Image.Type.Sliced; f.pixelsPerUnitMultiplier = 1f / 3f;
                var bb = Node("Border", box); Stretch(bb);
                var b = Img(bb, Lobby("panel_border"), new Color(0.3f, 0.33f, 0.38f)); b.type = Image.Type.Sliced; b.pixelsPerUnitMultiplier = 1f / 3f;
                codeBorders[i] = b;
                codeChars[i] = Txt(box, "Char", "", 4, Mint, 0, 0, 56, 80);
            }

            var soloBtn = Panel(roomR, "SoloButton", -700, -460, 360, 64, ctl, 1, 0);
            var si = Node("Icon", soloBtn.rect); At(si, -140, 0, 44, 44); Img(si, Lobby("icon_bot"), Amber);
            Txt(soloBtn.rect, "Label", "혼자 하기", 2, Amber, 24, 0, 260, 36);

            var backBtn = Panel(roomR, "BackButton", -760, 440, 200, 76, ctl, 7, 0);
            backBtn.label = Txt(backBtn.rect, "Label", "뒤로", 2, Warn, 0, 0, 190, 40);
            var roomStatus = Txt(roomR, "Status", "", 2, Warn, 0, -330, 1500, 40);

            // ================================================================ ② 캐릭터 선택 패널
            var charR = Node("CharacterPanel", root); Stretch(charR);
            var charG = Group(charR, false);
            var charTitle = Txt(charR, "Title", "캐릭터 선택", 4, Ink, 0, 440, 900, 70);
            var charCode = Txt(charR, "RoomCode", "", 2, Mint, 0, 382, 1300, 40);

            var cards = new LobbyScene.Card[4];
            float[] cxs = { -540, -180, 180, 540 };
            for (int i = 0; i < 4; i++)
            {
                var b = Panel(charR, "Card" + i, cxs[i], 40, 320, 560, ctl, 2, i);
                var card = new LobbyScene.Card { btn = b };
                card.tag = Txt(b.rect, "Tag", "", 2, Mint, 74, 234, 110, 36, TextAnchor.MiddleRight);
                var pr = Node("Portrait", b.rect); At(pr, 0, 60, 160, 320);
                card.portrait = Img(pr, Lobby("portrait_" + i), Color.white);
                card.portrait.preserveAspect = true;
                card.name = Txt(b.rect, "Name", Roles.DefaultName(i), 3, Ink, 0, -128, 300, 56);
                card.role = Txt(b.rect, "Role", Roles.Name(i), 2, Roles.Color(i), 0, -190, 300, 36);
                card.owner = Txt(b.rect, "Owner", "", 2, Ink, 0, -240, 300, 36);
                cards[i] = card;
            }

            // 아래 한 줄: 나가기 | 닉네임 | 게임 시작 (| 에디터 전용 봇 채우기)
            var leave = Panel(charR, "LeaveButton", -760, -420, 200, 76, ctl, 4, 0);
            leave.label = Txt(leave.rect, "Label", "나가기", 2, Warn, 0, 0, 190, 40);
            var nick = Panel(charR, "NickPanel", -330, -420, 520, 76, ctl, 3, 0);
            Txt(nick.rect, "Label", "닉네임", 2, Dim, -170, 0, 140, 36, TextAnchor.MiddleLeft);
            var nickValue = Txt(nick.rect, "Value", "", 3, Ink, 60, 0, 300, 56, TextAnchor.MiddleLeft);
            nick.label = nickValue;
            var start = Panel(charR, "StartButton", 330, -420, 620, 76, ctl, 5, 0);
            start.label = Txt(start.rect, "Label", "", 3, Mint, 0, 0, 600, 56);
            var bots = Panel(charR, "BotsButton", 790, -420, 160, 76, ctl, 6, 0);
            var bi = Node("Icon", bots.rect); At(bi, -40, 0, 44, 44); Img(bi, Lobby("icon_bot"), Amber);
            bots.label = Txt(bots.rect, "Label", "+봇", 2, Amber, 36, 0, 70, 36);

            // 난이도(방장이 누르면 바뀌고, 다른 사람 화면에도 같은 값이 보인다)
            var diff = Panel(charR, "DifficultyButton", 450, 440, 380, 76, ctl, 8, 0);
            diff.label = Txt(diff.rect, "Label", "난이도  보통", 2, Amber, 0, 0, 360, 40);

            // 스테이지(방 안에서 방장이 고른다)
            var stg = Panel(charR, "StageButton", -450, 440, 380, 76, ctl, 9, 0);
            stg.label = Txt(stg.rect, "Label", "Stage 1", 2, Mint, 0, 0, 360, 40);

            var charStatus = Txt(charR, "Status", "", 2, Warn, 0, 330, 1500, 36);
            charStatus.rectTransform.anchoredPosition = new Vector2(0, -345);

            // ---------------- 진행 중 오버레이 / 페이드
            var busyR = Node("Busy", root); Stretch(busyR);
            var busyImg = Img(busyR, null, new Color(0.02f, 0.025f, 0.035f, 0.97f), true);
            var busyG = Group(busyR, false);
            var busyText = Txt(busyR, "Text", "", 5, Ink, 0, 30, 1700, 90);
            var busySub = Txt(busyR, "Sub", "", 2, new Color(0.75f, 0.8f, 0.85f), 0, -60, 1500, 40);
            _ = busyImg;

            var blackR = Node("Black", root); Stretch(blackR);
            Img(blackR, null, Color.black);
            var blackG = Group(blackR, false);

            // ---------------- 연결
            ctl.black = blackG; ctl.roomPanel = roomG; ctl.charPanel = charG; ctl.busy = busyG;
            ctl.busyText = busyText; ctl.busySub = busySub;
            ctl.roomCards = new[] { createCard, joinCard };
            ctl.solo = soloBtn; ctl.backBtn = backBtn;
            ctl.codeBorders = codeBorders; ctl.codeChars = codeChars; ctl.roomStatus = roomStatus;
            ctl.cards = cards;
            ctl.charTitle = charTitle; ctl.charCodeLine = charCode; ctl.charStatus = charStatus;
            ctl.nickBtn = nick; ctl.nickValue = nickValue; ctl.leaveBtn = leave; ctl.startBtn = start; ctl.botsBtn = bots; ctl.diffBtn = diff; ctl.stageBtn = stg;

            SettingsPanelBuilder.Add(root);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuild(ScenePath, 2);
            Debug.Log("[LobbySceneBuilder] " + ScenePath + " 저장 완료");
        }

        private static void AddToBuild(string path, int index)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == path);
            list.Insert(Mathf.Min(index, list.Count), new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
