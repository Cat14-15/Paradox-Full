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
    /// 박물관 새 기능 점검(2026-11-21): 단풍이 숨바꼭질과 보상 · 난이도별 AI 단서 모양 · 실수 벌점 · 스피드런.
    /// 박물관 코드는 Assembly-CSharp 라 리플렉션으로 부른다. PARADOX_SHOTS 폴더가 있으면 화면도 찍는다.
    /// </summary>
    public class MuseumFeatureTests
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private Assembly asm;
        private Type T(string n) => asm.GetType(n) ?? throw new Exception("타입 없음: " + n);
        private bool Busy => (bool)T("MuseumStory").GetProperty("Busy").GetValue(null);
        private int prevDiff; private bool prevSpeed;

        [SetUp]
        public void Remember() { prevDiff = Paradox.Client.Story1Net.Difficulty; prevSpeed = Paradox.Client.Story1Net.Speedrun; }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            Paradox.Client.Story1Net.SetDifficulty(prevDiff);
            Paradox.Client.Story1Net.SetSpeedrun(prevSpeed);
            Paradox.Client.Story1Role.Reset();
        }

        private IEnumerator Load(int role)
        {
            Paradox.Client.Story1Role.Keep(role);
            asm = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Assembly-CSharp");
            SceneManager.LoadScene("MainStory1");
            yield return null; yield return null;
        }

        private IEnumerator SkipStory(float limit = 20f)
        {
            float until = Time.realtimeSinceStartup + limit;
            while (Busy && Time.realtimeSinceStartup < until)
            {
                T("MuseumStory").GetMethod("DebugSkip").Invoke(null, null);
                yield return new WaitForSecondsRealtime(0.4f);
            }
            Assert.IsFalse(Busy, "이야기가 끝나지 않음");
        }

        private IEnumerator Shot(string name)
        {
            string dir = Environment.GetEnvironmentVariable("PARADOX_SHOTS");
            if (string.IsNullOrEmpty(dir)) yield break;
            System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(0.5f);
            UiAudit.Report(dir, "feature_" + name);   // 글자가 패널 밖으로 나가는 곳 목록
            var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
            const int W = 1920, H = 1080;
            var rt = new RenderTexture(W, H, 24);
            var prev = cam.targetTexture;
            var switched = new System.Collections.Generic.List<Canvas>();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingOrder += 20000; switched.Add(c); }
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            yield return null;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "feature_" + name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = prev;
            foreach (var c in switched) { c.sortingOrder -= 20000; c.renderMode = RenderMode.ScreenSpaceOverlay; }
            UnityEngine.Object.Destroy(tex); rt.Release(); UnityEngine.Object.Destroy(rt);
        }

        // ---------------------------------------------------------------- 화면 점검(찍기만: 로비 단추 겹침 · 랭킹 창)
        [UnityTest, Timeout(120000)]
        public IEnumerator UiShots()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PARADOX_SHOTS"))) yield break;
            // 검사 자체 확인: 일부러 넘치는 패널을 만들면 잡아내야 한다
            var cvGo = new GameObject("AuditSelfTest", typeof(Canvas)); cvGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var pnl = new GameObject("Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image)); pnl.transform.SetParent(cvGo.transform, false);
            ((RectTransform)pnl.transform).sizeDelta = new Vector2(120, 40);
            var txt = new GameObject("Long", typeof(RectTransform)); txt.transform.SetParent(pnl.transform, false);
            var pt = txt.AddComponent<Paradox.Client.PixelText>(); pt.scale = 2; pt.text = "아주 아주 긴 글자가 패널 밖으로 나간다";
            yield return null;
            Assert.Greater(UiAudit.Check().Count, 0, "넘치는 글자를 검사가 찾아야 함");
            UnityEngine.Object.Destroy(cvGo);
            yield return null;
            asm = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Assembly-CSharp");
            // 타이틀: 단풍이 영구 보상이 글자에 가려지지 않는지 · 설정 → 만든 사람들
            int hadFriend = PlayerPrefs.GetInt("stranger.danpung.friend", 0);
            PlayerPrefs.SetInt("stranger.danpung.friend", 1);
            Paradox.Client.IntroSequence.SkipToMenu = true;
            SceneManager.LoadScene("Intro");
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("title_danpung");
            Paradox.Client.CreditsPanel.Open();
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("credits");
            UnityEngine.Object.Destroy(GameObject.Find("CreditsPanel"));
            if (hadFriend == 0) PlayerPrefs.DeleteKey("stranger.danpung.friend");
            yield return null;
            SceneManager.LoadScene("Lobby");
            yield return new WaitForSecondsRealtime(1.5f);
            var lobby = UnityEngine.Object.FindFirstObjectByType<Paradox.Client.LobbyScene>();
            Assert.IsNotNull(lobby, "로비");
            typeof(Paradox.Client.LobbyScene).GetMethod("SoloTest", Any).Invoke(lobby, null);
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("lobby_character");
            Paradox.Client.RankPanel.Open(0);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("rank_panel");
            Paradox.Client.RankPanel.CloseAll();
            var hub = Paradox.Net.NetHub.Instance; if (hub != null) hub.Shutdown();

            // 박물관 안의 창들: 수첩 · 지도 · 일시 정지 · 말하기(카드 줄) · 스피드런 시계
            Paradox.Client.Story1Net.SetSpeedrun(true);
            yield return Load(1);   // 통역(이안)
            yield return new WaitForSecondsRealtime(2f);
            yield return SkipStory();
            var hudT = T("MuseumHud");
            hudT.GetMethod("ToggleBook").Invoke(null, null); yield return Shot("ui_book"); hudT.GetMethod("CloseModals").Invoke(null, null);
            hudT.GetMethod("ToggleMap").Invoke(null, null); yield return Shot("ui_map"); hudT.GetMethod("CloseModals").Invoke(null, null);
            hudT.GetMethod("TogglePause").Invoke(null, null); yield return Shot("ui_pause"); hudT.GetMethod("CloseModals").Invoke(null, null);
            var chat = T("MuseumChat").GetField("instance", Any).GetValue(null);
            chat.GetType().GetMethod("SetFocus", Any).Invoke(chat, new object[] { true });
            for (int i = 0; i < 6; i++) T("MuseumChat").GetMethod("Say").Invoke(null, new object[] { i % 4, "Balbula dalawa! kanan, tatlo   [2] →3  긴 문장 시험 · 줄바꿈이 잘 되는지" });
            yield return Shot("ui_chat_cards");
            chat.GetType().GetMethod("SetFocus", Any).Invoke(chat, new object[] { false });
            T("MuseumGame").GetMethod("Mistake").Invoke(null, null);
            for (int k = 0; k < 4; k++) { T("MuseumGame").GetMethod("DebugFinishCurrent").Invoke(null, null); yield return new WaitForSecondsRealtime(1.2f); }
            yield return Shot("ui_speedrun_long");
        }

        // ---------------------------------------------------------------- 단풍이
        [UnityTest, Timeout(300000)]
        public IEnumerator DanpungHideAndSeekGivesRewards()
        {
            Paradox.Client.Story1Net.SetDifficulty(0);
            Paradox.Client.Story1Net.SetSpeedrun(false);
            yield return Load(0);
            yield return new WaitForSecondsRealtime(1f);
            yield return SkipStory();
            var museum = T("StrangerMuseum").GetProperty("Instance").GetValue(null);
            var cats = T("MuseumCats");
            int Found() => (int)cats.GetProperty("FoundCount").GetValue(null);
            int total = (int)cats.GetProperty("Total").GetValue(null);
            Assert.AreEqual(5, total, "단풍이 숨는 자리 수");
            var spotAt = cats.GetMethod("SpotAt");
            var danpung = GameObject.Find("Danpung");
            Assert.IsNotNull(danpung, "단풍이 오브젝트(처음엔 1층에 보여야 함)");
            for (int k = 0; k < total; k++)
            {
                var s = spotAt.Invoke(null, new object[] { k });
                string floor = (string)s.GetType().GetField("Item1").GetValue(s);
                Vector2 at = (Vector2)s.GetType().GetField("Item2").GetValue(s);
                string room = (string)s.GetType().GetField("Item3").GetValue(s);
                // 단풍이 바로 옆(왼쪽 30px)으로 옮긴다 · Enter 는 역할 0 을 -36px 옆으로 벌려 세우므로 되돌린다
                museum.GetType().GetMethod("DebugTeleport").Invoke(museum, new object[] { floor, room, at.x - 30f + 36f, at.y });
                float until = Time.realtimeSinceStartup + 6f;
                while (Found() < k + 1 && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(k + 1, Found(), "단풍이 " + (k + 1) + "번째(" + floor + "/" + room + ")를 찾지 못함");
                if (k == 0) yield return Shot("danpung_found1");
                if (k < total - 1) yield return new WaitForSecondsRealtime(1.4f);   // 달아나는 연출이 끝나길
            }
            Assert.IsTrue((bool)cats.GetProperty("AllFound").GetValue(null), "다 찾음 표시");
            Assert.IsTrue((bool)T("MuseumHud").GetProperty("ModalOpen").GetValue(null), "다 찾으면 보상 창이 떠야 함");
            Assert.AreEqual(1, PlayerPrefs.GetInt("stranger.danpung.friend", 0), "타이틀 화면 보상(영구 저장)");
            Assert.IsTrue(T("MuseumWords").GetField("Learned").GetValue(null) is System.Collections.IList learned
                          && learned.Cast<object>().Any(e => (string)e.GetType().GetField("word").GetValue(e) == "magkakasama"), "낱말 magkakasama 를 배워야 함");
            yield return Shot("danpung_reward");
            T("MuseumHud").GetMethod("CloseModals").Invoke(null, null);
            // 동행: 내가 걸어가면 따라온다
            var pm = (Vector2)museum.GetType().GetProperty("PlayerMap").GetValue(museum);
            yield return new WaitForSecondsRealtime(1.5f);
            Vector2 cat = new Vector2(danpung.transform.position.x * 100f, -danpung.transform.position.y * 100f);
            Assert.IsTrue(danpung.activeSelf, "다 찾은 뒤 단풍이가 보여야 함(동행)");
            Assert.Less((cat - pm).magnitude, 80f, "단풍이가 내 곁에 있어야 함");
            // 엔딩 이야기에 단풍이 장면이 들어간다
            var beat = (Array)T("MuseumStory").GetMethod("BeatLines", Any).Invoke(null, new object[] { 5 });
            bool has = beat.Cast<object>().Any(l => ((string)l.GetType().GetField("text").GetValue(l)).Contains("단풍"));
            Assert.IsTrue(has, "엔딩 이야기에 단풍이 장면");
            PlayerPrefs.DeleteKey("stranger.danpung.friend");   // 시험 기록이 실제 타이틀에 남지 않게
        }

        // ---------------------------------------------------------------- 난이도별 AI 단서
        [UnityTest, Timeout(120000)]
        public IEnumerator CluesFollowDifficulty()
        {
            yield return Load(0);   // 서하린(한국어) 기준 · 말하는 사람은 이안(필리핀어)
            yield return new WaitForSecondsRealtime(0.5f);
            var clues = T("MuseumClues"); var words = T("MuseumWords");
            string Shape(string s) => (string)clues.GetMethod("Shape").Invoke(null, new object[] { 1, s });
            void Learn(string s) => words.GetMethod("LearnFrom").Invoke(null, new object[] { 1, s });
            void Reset() { words.GetMethod("Reset").Invoke(null, null); clues.GetMethod("Reset").Invoke(null, null); }
            const string Valve = "Balbula dalawa! kanan, tatlo   [2] →3";
            const string Pad = "Dito!  서하린 = [2]";

            Paradox.Client.Story1Net.SetDifficulty(0); Reset();
            Assert.AreEqual(Valve, Shape(Valve), "쉬움: 그대로");

            Paradox.Client.Story1Net.SetDifficulty(1); Reset();
            string a = Shape(Valve); Learn(a);
            Assert.IsTrue(a.Contains("[2]") && a.Contains("→3"), "보통 · 처음 듣는 낱말: 신호를 붙여 준다 · " + a);
            string b = Shape(Valve);
            Assert.IsFalse(b.Contains("[2]") || b.Contains("→"), "보통 · 배운 낱말: 말로만 · " + b);
            Assert.IsTrue(b.Contains("dalawa") && b.Contains("tatlo"), "낱말은 남아야 함 · " + b);
            Assert.IsTrue(Shape(Pad).Contains("[2]"), "숫자 자체가 정답인 신호는 그대로");

            Paradox.Client.Story1Net.SetDifficulty(2); Reset();
            string h1 = Shape(Valve); Learn(h1);
            Assert.IsFalse(h1.Contains("[2]") || h1.Contains("→"), "어려움 · 처음: 말로만 · " + h1);
            string h2 = Shape(Valve); Learn(h2);
            Assert.IsTrue(h2.Contains("[2]") && h2.Contains("→3"), "어려움 · 다시 물었을 때: 신호 · " + h2);
            Assert.IsTrue(Shape(Pad).Contains("[2]"), "어려움에서도 정답 번호는 그대로");
            Reset();
        }

        // ---------------------------------------------------------------- 벌점 · 스피드런
        [UnityTest, Timeout(120000)]
        public IEnumerator HardMistakeAddsPenalty()
        {
            Paradox.Client.Story1Net.SetDifficulty(2);
            yield return Load(1);
            yield return new WaitForSecondsRealtime(0.5f);
            var g = T("MuseumGame");
            float before = (float)g.GetProperty("Elapsed").GetValue(null);
            g.GetMethod("Mistake").Invoke(null, null);
            Assert.AreEqual(15f, (float)g.GetProperty("Penalty").GetValue(null), 0.01f, "어려움 실수 +15초");
            Assert.GreaterOrEqual((float)g.GetProperty("Elapsed").GetValue(null), before + 15f, "기록에 벌점이 더해짐");
            Paradox.Client.Story1Net.SetDifficulty(1);
            g.GetMethod("Mistake").Invoke(null, null);
            Assert.AreEqual(20f, (float)g.GetProperty("Penalty").GetValue(null), 0.01f, "보통 실수 +5초");
            yield return Shot("penalty_clock");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SpeedrunSkipsStoryAndShowsSplits()
        {
            Paradox.Client.Story1Net.SetDifficulty(0);
            Paradox.Client.Story1Net.SetSpeedrun(true);
            yield return Load(2);
            float until = Time.realtimeSinceStartup + 4f;
            while (Busy && Time.realtimeSinceStartup < until) yield return null;   // 건너뛰기 없이 스스로 끝나야 함
            Assert.IsFalse(Busy, "스피드런: 오프닝 이야기를 바로 건너뛰어야 함");
            T("MuseumGame").GetMethod("DebugFinishCurrent").Invoke(null, null);
            until = Time.realtimeSinceStartup + 4f;
            while ((int)T("MuseumGame").GetProperty("Stage").GetValue(null) < 1 && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsFalse(Busy, "스피드런: 미션 뒤 이야기도 건너뜀");
            var splits = (float[])T("MuseumGame").GetField("Splits").GetValue(null);
            Assert.Greater(splits[0], 0f, "미션 1 구간 기록");
            var split = GameObject.Find("Splits");
            Assert.IsNotNull(split, "구간 기록 글");
            Assert.IsTrue(split.GetComponent<Paradox.Client.PixelText>().text.Contains("물길 막기"), "구간 기록에 미션 이름");
            Assert.AreEqual(4, Paradox.Client.Ranking.Board(1, true), "스피드런 보통 = 기록표 4");
            yield return Shot("speedrun_splits");
        }
    }
}
