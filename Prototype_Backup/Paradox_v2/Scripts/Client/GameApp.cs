using Paradox.Core;
using Paradox.Net;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Paradox.Client
{
    /// <summary>
    /// 씬에 하나 놓는 부트스트랩. 캐릭터 프리팹 참조를 들고 있고, 카메라/조명/네트워크/화면 관리자를 코드로 준비한다.
    /// Paradox.unity 씬을 Play 하면 타이틀 화면부터 시작한다.
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
    }
}
