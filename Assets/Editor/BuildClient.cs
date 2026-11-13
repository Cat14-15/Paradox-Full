using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Paradox.Editor
{
    /// <summary>
    /// 에디터 전용 배치 빌드(클라이언트 exe). 메뉴 항목 없이 커맨드라인에서만 부른다:
    /// <c>Unity.exe -batchmode -nographics -quit -projectPath &lt;프로젝트&gt; -executeMethod Paradox.Editor.BuildClient.Build -logFile &lt;로그&gt;</c>
    /// 출력: 환경변수 <c>PARADOX_BUILD_DIR</c>(폴더) 또는 프로젝트 옆 <c>Build/Client/Paradox.exe</c>.
    /// </summary>
    public static class BuildClient
    {
        public const string ScenePath = "Assets/Scenes/Paradox.unity";
        public const string BuildDirEnv = "PARADOX_BUILD_DIR";

        public static string OutputDir()
        {
            string dir = Environment.GetEnvironmentVariable(BuildDirEnv);
            if (string.IsNullOrWhiteSpace(dir))
                dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "Client");
            return Path.GetFullPath(dir);
        }

        /// <summary>Build Settings에 켜진 씬 순서 그대로(Intro가 첫 씬).</summary>
        public static string[] BuildScenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) list.Add(s.path);
            return list.Count > 0 ? list.ToArray() : new[] { ScenePath };
        }

        /// <summary>배치용: 씬에 놓는 UI 를 다시 만든다(Story1 흐름·로비).</summary>
        public static void RebuildUi()
        {
            Story1FlowBuilder.Build();
            LobbySceneBuilder.Build();
            FixStageSelectHint();
            AssetDatabase.SaveAssets();
            Debug.Log("[BuildClient] UI 재생성 완료");
        }

        /// <summary>스테이지 선택 하단의 그림 속 "Esc 뒤로" 안내를 글자(X 뒤로)로 바꾼다. 여러 번 실행해도 안전.</summary>
        public static void FixStageSelectHint()
        {
            var sc = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/StageSelect.unity");
            foreach (var im in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (im.sprite == null || im.sprite.name != "hint" || !im.enabled) continue;
                var r = im.rectTransform;
                var go = new GameObject("HintText", typeof(RectTransform));
                var nr = (RectTransform)go.transform;
                nr.SetParent(r.parent, false);
                nr.anchorMin = r.anchorMin; nr.anchorMax = r.anchorMax; nr.pivot = r.pivot;
                nr.anchoredPosition = r.anchoredPosition;
                nr.sizeDelta = new Vector2(Mathf.Max(r.sizeDelta.x, 700f), Mathf.Max(r.sizeDelta.y, 30f));
                nr.localScale = Vector3.one;
                var t = go.AddComponent<Paradox.Client.PixelText>();
                t.scale = 2; t.align = TextAnchor.MiddleCenter; t.color = new Color(0.85f, 0.88f, 0.95f, 0.9f);
                t.text = "A / D 선택     Enter 시작     X 뒤로"; t.raycastTarget = false;
                im.enabled = false;
                Debug.Log("[BuildClient] StageSelect 힌트 교체: " + r.anchoredPosition + " " + r.sizeDelta);
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        }

        public static void Build()
        {
            string dir = OutputDir();
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "Paradox.exe");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };

            Debug.Log($"[BuildClient] 빌드 시작 → {exe}");
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;
            Debug.Log($"[BuildClient] 결과 {s.result} · {s.totalSize / (1024 * 1024)} MB · 오류 {s.totalErrors} · 경고 {s.totalWarnings} · {s.totalTime.TotalSeconds:0}초 → {exe}");

            if (s.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw new Exception("[BuildClient] 빌드 실패: " + s.result);
            }
        }

        /// <summary>
        /// 배포용 릴리스 빌드(개발 옵션 없음). 제품 이름 STRANGER, 창 크기 조절 가능.
        /// 출력: 환경변수 PARADOX_BUILD_DIR(폴더) 또는 프로젝트 옆 Build/Release/STRANGER/STRANGER.exe.
        /// <c>-executeMethod Paradox.Editor.BuildClient.BuildRelease</c>
        /// </summary>
        /// <summary>WebGL 빌드(압축 없음, 어느 정적 서버에서나 열림). PARADOX_BUILD_DIR 또는 Build/Web.</summary>
        public static void BuildWebFresh() { RebuildUi(); BuildWeb(); }

        public static void BuildWeb()
        {
            string dir = Environment.GetEnvironmentVariable(BuildDirEnv);
            if (string.IsNullOrWhiteSpace(dir))
                dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "Web");
            dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(dir);

            PlayerSettings.productName = "STRANGER";
            PlayerSettings.companyName = "PARALLAX";
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;

            var opts = new BuildPlayerOptions
            {
                scenes = BuildScenes(),
                locationPathName = dir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;
            Debug.Log($"[BuildClient] 웹 결과 {s.result} · 오류 {s.totalErrors} · {s.totalTime.TotalSeconds:0}초 → {dir}");
            if (s.result == BuildResult.Succeeded) PatchWebIndex(dir);
            if (s.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw new Exception("[BuildClient] 웹 빌드 실패: " + s.result);
            }
        }

        /// <summary>index.html 을 창 크기에 꽉 차게 하고, 클릭하면 캔버스가 키보드 포커스를 받게 한다(itch.io 의 iframe 안에서 키가 안 먹는 문제).</summary>
        private static void PatchWebIndex(string dir)
        {
            string p = Path.Combine(dir, "index.html");
            if (!File.Exists(p)) return;
            string s = File.ReadAllText(p);
            const string css = @"    <style>
      html, body { margin: 0; width: 100%; height: 100%; overflow: hidden; background: #000; }
      #unity-container, #unity-container.unity-desktop { position: fixed !important; left: 0 !important; top: 0 !important; transform: none !important; width: 100% !important; height: 100% !important; }
      #unity-canvas { width: 100% !important; height: 100% !important; display: block; background: #000; outline: none; }
      #unity-footer { display: none !important; }
    </style>
  </head>";
            if (!s.Contains("#unity-footer { display: none")) s = s.Replace("  </head>", css);
            s = s.Replace("canvas.style.width = \"960px\";", "canvas.style.width = \"100%\";").Replace("canvas.style.height = \"600px\";", "canvas.style.height = \"100%\";");
            const string focus = @"
      // 키보드 포커스: 클릭·터치하면 캔버스로 (iframe 안에서도 방향키/WASD 가 먹도록)
      function focusGame() { try { window.focus(); canvas.focus(); } catch (e) {} }
      window.addEventListener('mousedown', focusGame);
      window.addEventListener('touchstart', focusGame);
      window.addEventListener('keydown', function (e) { if (['ArrowUp','ArrowDown','ArrowLeft','ArrowRight',' '].indexOf(e.key) >= 0) e.preventDefault(); }, { passive: false });
      setTimeout(focusGame, 500);
";
            if (!s.Contains("function focusGame")) s = s.Replace("      document.body.appendChild(script);", focus + "      document.body.appendChild(script);");
            File.WriteAllText(p, s);
            Debug.Log("[BuildClient] index.html 보정 완료");
        }

        public static void BuildRelease()
        {
            string dir = Environment.GetEnvironmentVariable(BuildDirEnv);
            if (string.IsNullOrWhiteSpace(dir))
                dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build", "Release", "STRANGER");
            dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "STRANGER.exe");

            PlayerSettings.productName = "STRANGER";
            PlayerSettings.companyName = "PARALLAX";
            PlayerSettings.bundleVersion = "0.1";
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;   // 여러 창을 띄워도 멈추지 않게(LAN 게임)

            var opts = new BuildPlayerOptions
            {
                scenes = BuildScenes(),
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            Debug.Log($"[BuildClient] 릴리스 빌드 시작 → {exe}");
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary s = report.summary;
            Debug.Log($"[BuildClient] 릴리스 결과 {s.result} · {s.totalSize / (1024 * 1024)} MB · 오류 {s.totalErrors} · 경고 {s.totalWarnings} · {s.totalTime.TotalSeconds:0}초 → {exe}");

            if (s.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw new Exception("[BuildClient] 릴리스 빌드 실패: " + s.result);
            }
        }
    }
}
