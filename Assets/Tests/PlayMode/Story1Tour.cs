using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Paradox.Client;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Paradox.Tests
{
    /// <summary>Story1 화면 검수: 퍼즐 화면을 네 역할 시점으로 열어 PNG 로 저장한다. PARADOX_SHOTS 폴더가 있을 때만(그래픽 있는 배치 실행).</summary>
    public class Story1Tour
    {
        private static string Dir => Environment.GetEnvironmentVariable("PARADOX_SHOTS");

        private static IEnumerator Shot(string name)
        {
            yield return null;
            var cam = Camera.main;
            Assert.IsNotNull(cam, "카메라 없음");
            const int W = 1600, H = 900;
            var rt = new RenderTexture(W, H, 24);
            var prev = cam.targetTexture;
            var switched = new System.Collections.Generic.List<Canvas>();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; switched.Add(c); }
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            yield return null;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = prev;
            foreach (var c in switched) { c.renderMode = RenderMode.ScreenSpaceOverlay; }
            UnityEngine.Object.Destroy(tex); rt.Release(); UnityEngine.Object.Destroy(rt);
        }

        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Story1_Puzzles_AllRoles()
        {
            if (string.IsNullOrEmpty(Dir)) Assert.Ignore("PARADOX_SHOTS 환경변수가 없어 건너뜀");
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(Dir);
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Story1.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return Frames(30);
            var tut = UnityEngine.Object.FindAnyObjectByType<Story1Tutorial>();
            if (tut != null) typeof(Story1Tutorial).GetMethod("Close", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(tut, new object[] { false });
            yield return Frames(10);
            yield return Shot("00_world");

            Story1Blackout.RestorePower();
            yield return Frames(5);
            var all = new System.Collections.Generic.List<Story1Interactable>(Story1Interactable.All);
            foreach (var it in all)
            {
                string tag = it.GetType().Name;
                for (int r = 0; r < 4; r++)
                {
                    int guard = 0; while (Story1Role.Current != r && guard++ < 4) Story1Role.Cycle();
                    it.Activate();
                    yield return Frames(20);
                    yield return Shot(tag + "_role" + r);
                    CloseAll();
                    yield return Frames(5);
                }
            }
            Story1Role.Reset();
            while (Story1Role.Current != 0) Story1Role.Cycle();

            // 월드 위 창들: 기록(여러 줄)·신호 휠·지도·나가기·도움말
            for (int i = 0; i < 6; i++) Story1Net.BotSay(1 + i % 3, "테스트 " + i, "t" + i);
            yield return Frames(15); yield return Shot("W_chat");
            var bar = UnityEngine.Object.FindAnyObjectByType<SignalBar>(); bar.Toggle(); yield return Frames(25); yield return Shot("W_signal"); bar.Toggle(); yield return Frames(15);
            var map = UnityEngine.Object.FindAnyObjectByType<Story1Map>(); map.Toggle(); yield return Frames(25); yield return Shot("W_map"); map.Toggle(); yield return Frames(15);
            var pause = UnityEngine.Object.FindAnyObjectByType<Story1Pause>(); pause.OpenFromButton(); yield return Frames(25); yield return Shot("W_pause"); pause.Stay(); yield return Frames(15);
            var tut2 = UnityEngine.Object.FindAnyObjectByType<Story1Tutorial>(); tut2.Reopen(); yield return Frames(25); yield return Shot("W_help");
        }

        private static void CloseAll()
        {
            UnityEngine.Object.FindAnyObjectByType<FusePanel>()?.Close();
            UnityEngine.Object.FindAnyObjectByType<GalleryPanel>()?.Close();
            UnityEngine.Object.FindAnyObjectByType<WaterPanel>()?.Close();
            UnityEngine.Object.FindAnyObjectByType<RelicPanel>()?.Close();
            UnityEngine.Object.FindAnyObjectByType<FinalPanel>()?.Close();
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Scenes_Static()
        {
            if (string.IsNullOrEmpty(Dir)) Assert.Ignore("PARADOX_SHOTS 환경변수가 없어 건너뜀");
            LogAssert.ignoreFailingMessages = true;
            Directory.CreateDirectory(Dir);
            foreach (var n in new[] { "Intro", "Lobby", "StageSelect", "Camp", "Prologue" })
            {
#if UNITY_EDITOR
                yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + n + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Shot("S_" + n);
            }
        }
    }
}
