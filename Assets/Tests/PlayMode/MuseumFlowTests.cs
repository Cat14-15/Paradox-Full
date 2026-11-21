using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Paradox.Tests
{
    /// <summary>
    /// MainStory1(박물관) 혼자 하기 끝까지: 이야기 건너뛰기 → 미션 1 밸브(사람 1 + AI 3 이 실제로 푼다) → 미션 2~4(시험용 완료)
    /// → 미션 5 함께 서기(사람이 발판에 서면 AI 셋이 걸어와 선다) → 엔딩 카드. 오류 로그·예외가 하나라도 나면 실패.
    /// 박물관 코드는 Assembly-CSharp 라 리플렉션으로 부른다.
    /// </summary>
    public class MuseumFlowTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private Assembly asm;
        private Type T(string n) => asm.GetType(n) ?? throw new Exception("타입 없음: " + n);
        private int Stage => (int)T("MuseumGame").GetProperty("Stage").GetValue(null);
        private bool Busy => (bool)T("MuseumStory").GetProperty("Busy").GetValue(null);
        private static object Get(object o, string f) => o.GetType().GetField(f, Any).GetValue(o);
        private static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, Any).Invoke(o, a);
        private int me;
        private void Puzzle(int id, int op, string v) => T("MuseumNet").GetMethod("Puzzle").Invoke(null, new object[] { id, op, v, me });

        private IEnumerator Load(int role)
        {
            me = role;
            Paradox.Client.Story1Role.Keep(role);   // 혼자 하기: 이 캐릭터로, 나머지 셋은 AI
            asm = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Assembly-CSharp");
            SceneManager.LoadScene("MainStory1");
            yield return null; yield return null;
        }

        [TearDown]
        public void Restore() { Time.timeScale = 1f; Paradox.Client.Story1Role.Reset(); }

        /// <summary>Enter 가 역할별로 (role-1.5)*24 만큼 옆으로 벌려 세우는 것을 되돌린 x.</summary>
        private float X(float x) => x - (me - 1.5f) * 24f;

        /// <summary>PARADOX_SHOTS 폴더가 있으면 화면을 찍는다(눈으로 겹침·깨짐 점검용).</summary>
        private IEnumerator Shot(string name)
        {
            string dir = Environment.GetEnvironmentVariable("PARADOX_SHOTS");
            if (string.IsNullOrEmpty(dir)) yield break;
            System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(0.6f);
            UiAudit.Report(dir, "museum_r" + me + "_" + name);   // 글자가 패널 밖으로 나가는 곳 목록
            // 배치모드는 화면을 그리지 않아 WaitForEndOfFrame·ScreenCapture 가 멈춘다 → 카메라를 직접 렌더(Story1Tour 와 같은 방식)
            var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(cam, "카메라 없음");
            // 주의: -batchmode 는 화면이 640x480 으로 고정이라 화면 크기에 맞춰 놓는 UI(퍼즐 창 크기·손전등 어둠)는 실제와 다르게 찍힌다. 레이아웃 점검은 에디터(창 있음)에서.
            const int W = 1920, H = 1080;
            var rt = new RenderTexture(W, H, 24);
            var prev = cam.targetTexture;
            var switched = new System.Collections.Generic.List<Canvas>();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingOrder += 20000; switched.Add(c); }   // UI 가 캐릭터 스프라이트(정렬 = y)보다 위에 오게
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            yield return null;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "museum_r" + me + "_" + name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = prev;
            foreach (var c in switched) { c.sortingOrder -= 20000; c.renderMode = RenderMode.ScreenSpaceOverlay; }
            UnityEngine.Object.Destroy(tex); rt.Release(); UnityEngine.Object.Destroy(rt);
        }

        /// <summary>퍼즐 창을 열어 찍고 닫는다.</summary>
        private IEnumerator PanelShot(object puzzle, string name)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PARADOX_SHOTS"))) yield break;
            Call(puzzle, "Open");
            yield return Shot(name);
            Call(puzzle, "Close");
        }

        private IEnumerator SkipStory(float limit = 20f)
        {
            float until = Time.realtimeSinceStartup + limit;
            while (Busy && Time.realtimeSinceStartup < until)
            {
                T("MuseumStory").GetMethod("DebugSkip").Invoke(null, null);
                yield return new WaitForSecondsRealtime(0.4f);
            }
        }

        private IEnumerator WaitStage(int want, float limit)
        {
            float until = Time.realtimeSinceStartup + limit;
            while (Stage < want && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(want, Stage, "미션 " + want + " 로 넘어가지 못함");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator SoloPlaysThroughWithBots([Values(0, 1, 2, 3)] int role)
        {
            yield return Load(role);
            Time.timeScale = 3f;
            var museum = T("StrangerMuseum").GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(museum, "StrangerMuseum 이 없음");
            var story = UnityEngine.Object.FindFirstObjectByType(T("MuseumStory"));
            var faces = (Sprite[])Get(story, "faces");
            Assert.IsTrue(faces.All(f => f != null), "이야기 초상이 비어 있음(StoryArt)");
            Assert.IsNotNull(Get(story, "bgCamp"), "캠프 배경이 비어 있음"); Assert.IsNotNull(Get(story, "bgClass"), "교실 배경이 비어 있음");
            yield return new WaitForSecondsRealtime(1.5f);
            int catCount = UnityEngine.Resources.FindObjectsOfTypeAll<SpriteRenderer>().Count(r => r.name == "Danpung" && r.sprite != null && r.gameObject.scene.IsValid());
            Assert.AreEqual(1, catCount, "숨바꼭질하는 단풍이(이스터 에그)");
            yield return Shot("00_story");
            yield return SkipStory();
            Assert.IsFalse(Busy, "첫 이야기가 끝나지 않음");
            Assert.AreEqual(0, Stage);

            // ---- 미션 1 · 밸브: 내가(0번) 하나 잡고, 내 단서를 알려 주고, AI 가 알려 준 정답으로 맞춘 뒤 고정
            Call(museum, "DebugTeleport", "B1", "maintenance", X(1280f), 900f);
            var valves = UnityEngine.Object.FindFirstObjectByType(T("PuzzleValves"));
            var knows = (int[])Get(valves, "knows"); var holder = (int[])Get(valves, "holder"); var ans = (int[])Get(valves, "ans");
            int mine = -1;
            for (int v = 0; v < 4 && mine < 0; v++) if (knows[me] != v && (bool)Call(valves, "Feasible", me, v)) mine = v;
            Assert.GreaterOrEqual(mine, 0, "잡을 수 있는 밸브가 없음");
            Puzzle(10, 1, mine.ToString());
            float until = Time.realtimeSinceStartup + 20f;
            while (holder.Any(h => h < 0) && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(holder.All(h => h >= 0), "AI 가 밸브를 다 잡지 않음: " + string.Join(",", holder));
            yield return PanelShot(valves, "01_valves");
            Call(valves, "Tell");
            Puzzle(10, 3, mine + ":" + ans[mine]);
            Puzzle(10, 4, mine.ToString());
            yield return WaitStage(1, 90f);
            yield return SkipStory();

            // ---- 미션 2 · 차단기: 내 칸을 알려 주고, 순서가 내 차단기면 올린다(AI 는 자기 차례에 스스로 올린다)
            Call(museum, "DebugTeleport", "B1", "power", X(1824f), 960f);
            var br = UnityEngine.Object.FindFirstObjectByType(T("PuzzleBreakers"));
            var order = (int[])Get(br, "order"); var owner = (int[])Get(br, "owner");
            yield return Shot("02a_power_room");
            yield return PanelShot(br, "02_breakers");
            Call(br, "Tell");
            until = Time.realtimeSinceStartup + 90f;
            float lastPress = 0f;
            while (Stage < 2 && Time.realtimeSinceStartup < until)
            {
                int pressed = (int)Get(br, "pressed");
                if (pressed < 4 && owner[order[pressed]] == me && Time.realtimeSinceStartup - lastPress > 1f)
                { Puzzle(11, 1, order[pressed].ToString()); lastPress = Time.realtimeSinceStartup; }
                yield return null;
            }
            yield return WaitStage(2, 1f);
            yield return SkipStory();

            // ---- 미션 3 · 나침반: 네 방위에 정답 모양을 맞추고 돌린다
            Call(museum, "DebugTeleport", "1F", "hall", X(950f), 1330f);
            var cp = UnityEngine.Object.FindFirstObjectByType(T("PuzzleCompass"));
            var answer = (int[])Get(cp, "answer");
            yield return PanelShot(cp, "03_compass");
            for (int d = 0; d < 4; d++) Puzzle(12, 2, d + ":" + answer[d]);
            Puzzle(12, 3, "");
            yield return WaitStage(3, 10f);
            yield return SkipStory();

            // ---- 미션 4 · 바이바이인: 틀린 세 글자 한 번 → 맞는 세 글자(KA SA MA)
            Call(museum, "DebugTeleport", "2F", "archive", X(583f), 290f);
            yield return PanelShot(UnityEngine.Object.FindFirstObjectByType(T("PuzzleScript")), "04_script");
            Puzzle(13, 1, "3"); Puzzle(13, 1, "3"); Puzzle(13, 1, "3");
            yield return null;
            Assert.AreEqual(3, Stage, "틀린 글자로 열리면 안 됨");
            var word = (int[])Get(UnityEngine.Object.FindFirstObjectByType(T("PuzzleScript")), "Word");
            foreach (int g in word) Puzzle(13, 1, g.ToString());
            yield return WaitStage(4, 10f);
            yield return SkipStory();

            // ---- 미션 5 · 네 나라 말 사전: 칸마다 뜻 그림을 넣고 연다(조작 이벤트는 조작자 슬롯 0 으로)
            Call(museum, "DebugTeleport", "2F", "reading", X(264f), 740f);
            var words = UnityEngine.Object.FindFirstObjectByType(T("PuzzleWords"));
            yield return PanelShot(words, "045_words");
            var meaning = (int[])Get(words, "meaning");
            for (int k = 0; k < 3; k++) Puzzle(15, 1, k + ":" + meaning[k]);
            Puzzle(15, 2, "");
            yield return WaitStage(5, 30f);
            yield return SkipStory();

            // ---- 미션 5 · 함께 서기: 발판 하나에 서면 AI 셋이 남은 발판으로 걸어와 선다
            // 내 발판(관측자가 알려 주는 자리)으로 간다
            var gather = UnityEngine.Object.FindFirstObjectByType(T("PuzzleGather"));
            int myPad = ((int[])Get(gather, "assign"))[me];
            float[] padX = { 896f, 1004f, 896f, 1004f }, padY = { 623f, 623f, 725f, 725f };
            Call(museum, "DebugTeleport", "2F", "atrium", X(padX[myPad]), padY[myPad]);
            yield return Shot("05_gather");
            yield return WaitStage(6, 60f);
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("06_final_story");   // 마지막 이야기 시작 → 엔딩 카드까지 오류 없는지
            yield return SkipStory(10f);
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot("07_end_card");
        }
    }
}
