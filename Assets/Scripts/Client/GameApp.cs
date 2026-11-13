using System;
using System.Collections;
using System.IO;
using Paradox.Core;
using Paradox.Game;
using Paradox.Net;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Paradox.Client
{
    /// <summary>
    /// 씬에 하나 놓는 부트스트랩. 캐릭터 프리팹 참조를 들고 있고, 카메라/조명/네트워크/화면 관리자를 코드로 준비한다.
    /// Paradox.unity 씬을 Play 하면 타이틀 화면부터 시작한다.
    /// 환경변수 <c>PARADOX_AUTOJOIN=&lt;방 코드&gt;</c> 가 있으면 자동 참가 드라이버(온라인 검증용)가 켜진다 — 없으면 완전히 비활성.
    /// </summary>
    public class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }

        [Tooltip("Jovial Games 캐릭터 프리팹 9종 (Body/Head/Left_Foot/Right_Foot 파츠 구조)")]
        public GameObject[] characterPrefabs = new GameObject[0];

        [Tooltip("역할별 전용 픽셀 캐릭터 시트(160x256: 4행 = 앞/뒤/왼/오, 5열 = 걷기 프레임, 칸 32x64). 비어 있는 역할은 종이인형 캐릭터를 고른다")]
        public Texture2D[] roleSheets = new Texture2D[4];

        public Texture2D RoleSheet(int slot) => roleSheets != null && slot >= 0 && slot < roleSheets.Length ? roleSheets[slot] : null;
        public bool HasRoleArt(int slot) => RoleSheet(slot) != null;

        public static readonly string[] CharacterNames = { "궁수", "원시인", "광대", "수도승", "닌자", "해적", "군인", "전사", "마법사" };

        public CameraFollow Follow { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 씬을 다시 불러온 경우(테스트 등): 이미 살아 있는 카메라/조명을 쓰고, 새 씬의 중복 오브젝트는 지운다
                foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (cam.gameObject.scene == gameObject.scene) Destroy(cam.gameObject);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Application.runInBackground = true;

            UIRoot.EnsureEventSystem();
            EnsureCamera();
            EnsureLight();

            if (NetHub.Instance == null) new GameObject("NetHub").AddComponent<NetHub>();
            if (ScreenManager.Instance == null) new GameObject("ScreenManager").AddComponent<ScreenManager>();
        }

        private void Start()
        {
            if (Instance != this) return;
            string code = Environment.GetEnvironmentVariable(AutoJoinEnv);
            if (!string.IsNullOrWhiteSpace(code)) StartCoroutine(AutoJoin(code.Trim()));
        }

        private void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                go.AddComponent<UniversalAdditionalCameraData>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 5.2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            Follow = cam.GetComponent<CameraFollow>() ?? cam.gameObject.AddComponent<CameraFollow>();
            Follow.pixelsPerUnit = 0f;
            DontDestroyOnLoad(cam.gameObject);
        }

        private void EnsureLight()
        {
            // 조명은 씬에 남겨 둔다(씬을 다시 불러와도 전역 조명이 둘이 되지 않게)
            var existing = FindAnyObjectByType<Light2D>();
            if (existing != null) return;
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;
        }

        public GameObject CharacterPrefab(int index)
        {
            if (characterPrefabs == null || characterPrefabs.Length == 0) return null;
            if (index < 0) index = 0;
            return characterPrefabs[index % characterPrefabs.Length];
        }

        public int CharacterCount => characterPrefabs != null ? characterPrefabs.Length : 0;

        public static string CharacterName(int index)
        {
            if (index < 0) return "미선택";
            return CharacterNames[index % CharacterNames.Length];
        }

        /// <summary>프리팹의 파츠 스프라이트(UI 초상용).</summary>
        public Sprite PartSprite(int character, string part)
        {
            var prefab = CharacterPrefab(character);
            if (prefab == null) return null;
            var t = prefab.transform.Find(part);
            if (t == null) return null;
            var sr = t.GetComponent<SpriteRenderer>();
            return sr != null ? sr.sprite : null;
        }

        // ================================================================== 자동 참가 드라이버(온라인 검증)

        /// <summary>참가할 방 코드. 있으면 시작 직후 매칭 화면으로 가서 StartClient 한다.</summary>
        public const string AutoJoinEnv = "PARADOX_AUTOJOIN";
        /// <summary>진단 로그 파일 경로(5초마다 한 줄 append). 없으면 Debug.Log 만.</summary>
        public const string AutoJoinLogEnv = "PARADOX_AUTOJOIN_LOG";
        public const string AutoJoinName = "클라";
        public const float AutoMoveInterval = 3f;
        public const float AutoMoveStep = 0.7f;
        public const float AutoLogInterval = 5f;
        public const float AutoRetryAfter = 4f;   // 접속 실패(또는 아직 호스트 없음) 뒤 다시 StartClient 까지

        /// <summary>자동 참가 드라이버가 켜져 있는가(테스트/디버그 표시용).</summary>
        public bool AutoJoinActive { get; private set; }

        private string _autoLogPath;

        private IEnumerator AutoJoin(string code)
        {
            AutoJoinActive = true;
            _autoLogPath = Environment.GetEnvironmentVariable(AutoJoinLogEnv);
            AutoLog($"시작 code={code} batch={Application.isBatchMode} log={_autoLogPath ?? "(없음)"}");
            yield return null;
            yield return null;

            TutorialOverlay.AutoSkip = true;          // 클라 쪽 튜토리얼 오버레이(입력 잠금) 없이 첫 Tick 에 Brief
            LocalNav.Go(LocalScreen.Match);
            yield return null;

            var hub = NetHub.Instance;
            float nextRetry = 0f, nextMove = 0f, nextLog = 0f;
            Phase lastPhase = Phase.Title;
            bool nameSent = false, briefSent = false;
            int moveStep = 0;

            while (true)
            {
                yield return null;
                if (hub == null) { hub = NetHub.Instance; if (hub == null) continue; }
                float now = Time.realtimeSinceStartup;

                // 접속 전/실패: 일정 간격으로 다시 시도
                if (!hub.IsRunning)
                {
                    if (now >= nextRetry)
                    {
                        nextRetry = now + AutoRetryAfter + NetHub.ConnectAttempts;
                        bool ok = hub.StartClient(code);
                        AutoLog($"StartClient({code}) = {ok} status='{hub.Status}'");
                    }
                    nameSent = false; briefSent = false;
                }

                if (Session.Connected)
                {
                    var S = Session.State;
                    if (S.phase != lastPhase)
                    {
                        AutoLog($"phase {lastPhase} → {S.phase} (MySlot {Session.MySlot}, LocalSlot {Session.LocalSlot})");
                        lastPhase = S.phase;
                        if (S.phase != Phase.NameSetup) nameSent = false;
                        if (S.phase != Phase.StagePlay) briefSent = false;
                    }

                    if (S.phase == Phase.NameSetup && !nameSent && Session.MySlot >= 0)
                    {
                        nameSent = true;
                        Session.Send("Name", 0, 0, AutoJoinName);
                        Session.Send("Ready", 1);
                        AutoLog($"Name('{AutoJoinName}') + Ready 전송 (slot {Session.MySlot})");
                    }

                    if (S.phase == Phase.StagePlay)
                    {
                        int me = Session.MySlot;
                        bool briefed = me >= 0 && S.st != null && S.st.briefDone != null && me < S.st.briefDone.Length && S.st.briefDone[me];
                        if (!briefSent && !briefed && me >= 0)
                        {
                            briefSent = true;
                            Session.Send("Brief");
                            AutoLog($"Brief 전송 (slot {me})");
                        }
                        if (now >= nextMove)
                        {
                            nextMove = now + AutoMoveInterval;
                            AutoMove(moveStep++);
                        }
                    }
                }

                if (now >= nextLog)
                {
                    nextLog = now + AutoLogInterval;
                    AutoLog(Snapshot());
                }
            }
        }

        /// <summary>내가 소유한 아바타를 아트리움 안에서 조금 움직인다(작은 사각형 순환). 맵이 있으면 벽을 피한다. NetworkTransform 이 동기화한다.</summary>
        private void AutoMove(int step)
        {
            int me = Session.MySlot;
            if (me < 0 || !Session.Avatars.TryGetValue(me, out var av) || av == null || !av.IsOwner) return;
            Vector2 pos = av.transform.position;
            Vector2 d;
            switch (step % 4)
            {
                case 0: d = new Vector2(AutoMoveStep, 0f); break;
                case 1: d = new Vector2(0f, AutoMoveStep); break;
                case 2: d = new Vector2(-AutoMoveStep, 0f); break;
                default: d = new Vector2(0f, -AutoMoveStep); break;
            }
            var map = Session.Map;
            Vector2 next = map != null ? map.Move(pos, d, StageMaps.AvatarRadius) : pos + d;
            // 아트리움 밖으로는 나가지 않는다(문을 지나 다른 방으로 새지 않게)
            if (map != null && map.RoomAt(pos) == StageMaps.RoomAtrium && map.RoomAt(next) != StageMaps.RoomAtrium) next = pos;
            av.transform.position = new Vector3(next.x, next.y, 0f);
            AutoLog($"이동 #{step} ({pos.x:0.00},{pos.y:0.00}) → ({next.x:0.00},{next.y:0.00})");
        }

        private string Snapshot()
        {
            var hub = NetHub.Instance;
            int active = 0;
            foreach (var kv in Session.Avatars) if (kv.Value != null && kv.Value.gameObject.activeInHierarchy) active++;
            string mine = "-";
            if (Session.MySlot >= 0 && Session.Avatars.TryGetValue(Session.MySlot, out var av) && av != null)
                mine = $"({av.transform.position.x:0.00},{av.transform.position.y:0.00}) owner={av.IsOwner}";
            return $"phase={Session.State.phase} connected={Session.Connected} running={(hub != null && hub.IsRunning)} MySlot={Session.MySlot} LocalSlot={Session.LocalSlot} " +
                   $"sessionNet={(SessionNet.Instance != null)} avatars={Session.Avatars.Count}/active {active} mine={mine} clock={Session.Clock:0.0} status='{(hub != null ? hub.Status : "")}'";
        }

        private void AutoLog(string msg)
        {
            Debug.Log("[AutoJoin] " + msg);
            if (string.IsNullOrEmpty(_autoLogPath)) return;
            try
            {
                var dir = Path.GetDirectoryName(_autoLogPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(_autoLogPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
            }
            catch (Exception e) { Debug.LogWarning("[AutoJoin] 로그 쓰기 실패: " + e.Message); }
        }
    }
}
