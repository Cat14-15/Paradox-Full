#if PARADOX_NETCODE
using System.Reflection;
using Paradox.Language;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Net
{
    /// <summary>
    /// 온라인 4인 골격 부트스트래퍼. 빈 씬에 이 컴포넌트 하나만 있으면
    /// 카메라 / NetworkManager / 유니티 트랜스포트 / 플레이어 프리팹 / 로비 UI를
    /// 모두 런타임에 만든다. (에디터 프리팹 배선 불필요)
    ///
    /// 사용: Online 씬 열고 Play → "호스트 시작"으로 방 열기.
    /// 같은 PC에서 2번째 인스턴스(빌드 또는 ParrelSync)로 "참가(로컬)"하면 접속 테스트 가능.
    /// 서버가 접속 순서대로 조작자/통역/내비게이터/관측자를 배정한다.
    /// </summary>
    public class NetGame : MonoBehaviour
    {
        // 런타임 생성 프리팹의 고정 해시(호스트/클라 동일 코드 → 동일 값이라 매칭됨)
        private const uint PlayerPrefabHash = 0x50415831; // "PAX1"
        private const ushort Port = 7777;

        private NetworkManager _nm;
        private UnityTransport _utp;
        private GameObject _playerTemplate;

        private GameObject _lobbyPanel;
        private InputField _ipField;
        private Text _status;

        private void Awake()
        {
            EnsureCamera();
            SetupNetwork();
            BuildLobbyUI();
        }

        private void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.20f, 0.35f, 0.28f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            go.transform.position = new Vector3(0, 0, -10);
            go.AddComponent<CameraFollow>(); // 소유 플레이어가 target을 자기 자신으로 설정
        }

        private void SetupNetwork()
        {
            var nmGo = new GameObject("NetworkManager");
            _nm = nmGo.AddComponent<NetworkManager>();
            _utp = nmGo.AddComponent<UnityTransport>();

            if (_nm.NetworkConfig == null) _nm.NetworkConfig = new NetworkConfig();
            _nm.NetworkConfig.NetworkTransport = _utp;
            _utp.SetConnectionData("127.0.0.1", Port);

            _playerTemplate = BuildPlayerTemplate();
            _nm.NetworkConfig.PlayerPrefab = _playerTemplate;
        }

        private GameObject BuildPlayerTemplate()
        {
            var go = new GameObject("NetPlayer");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeCircleSprite();
            sr.color = Color.white;

            var no = go.AddComponent<NetworkObject>();
            SetGlobalObjectIdHash(no, PlayerPrefabHash);

            go.AddComponent<OwnerNetworkTransform>();
            go.AddComponent<NetPlayer>();

            // 머리 위 역할 라벨
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            var font = UIFontHelper.Load();
            tm.font = font;
            tm.characterSize = 0.08f;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            labelGo.GetComponent<MeshRenderer>().material = font.material;
            labelGo.GetComponent<MeshRenderer>().sortingOrder = 60;

            go.SetActive(false);
            DontDestroyOnLoad(go);
            return go;
        }

        // NetworkObject.GlobalObjectIdHash는 보통 에디터가 굽지만,
        // 런타임 생성 프리팹은 리플렉션으로 고정값을 넣어 호스트/클라 매칭을 맞춘다.
        private static void SetGlobalObjectIdHash(NetworkObject no, uint hash)
        {
            var f = typeof(NetworkObject).GetField("GlobalObjectIdHash",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) f.SetValue(no, hash);
            else Debug.LogWarning("[NetGame] GlobalObjectIdHash 필드를 못 찾음 — NGO 버전에 따라 프리팹 매칭이 필요할 수 있음.");
        }

        // ---------- 로비 UI ----------

        private void BuildLobbyUI()
        {
            PuzzleOverlay.EnsureEventSystem();
            var canvas = PuzzleOverlay.GetCanvas();
            var font = UIFontHelper.Load();

            _lobbyPanel = NewRect("Lobby", canvas, out var rt);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 320f);
            var bg = _lobbyPanel.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);
            var v = _lobbyPanel.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 20, 20);
            v.spacing = 12;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            MakeText(rt, font, "PARADOX 온라인 (골격 테스트)", 22, TextAnchor.MiddleCenter, 34);
            MakeText(rt, font, "호스트가 방을 열고, 나머지는 참가하세요.", 15, TextAnchor.MiddleCenter, 26);

            MakeButton(rt, font, "호스트 시작 (Host)", () =>
            {
                _utp.SetConnectionData("127.0.0.1", Port);
                bool ok = _nm.StartHost();
                SetStatus(ok ? "호스트 시작됨. 친구들이 참가하면 역할이 배정됩니다." : "호스트 시작 실패");
                if (ok) _lobbyPanel.SetActive(false);
            });

            _ipField = MakeInputField(rt, font, "127.0.0.1");

            MakeButton(rt, font, "참가 (Join)", () =>
            {
                string ip = string.IsNullOrWhiteSpace(_ipField.text) ? "127.0.0.1" : _ipField.text.Trim();
                _utp.SetConnectionData(ip, Port);
                bool ok = _nm.StartClient();
                SetStatus(ok ? $"{ip} 에 접속 시도 중..." : "참가 시작 실패");
                if (ok) _lobbyPanel.SetActive(false);
            });

            // 화면 상단 상태 표시(로비와 별개로 항상 보임)
            var statusGo = NewRect("NetStatus", canvas, out var srt);
            srt.anchorMin = new Vector2(0.5f, 1f);
            srt.anchorMax = new Vector2(0.5f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, -12f);
            srt.sizeDelta = new Vector2(900f, 30f);
            _status = statusGo.AddComponent<Text>();
            _status.font = font; _status.fontSize = 17; _status.alignment = TextAnchor.MiddleCenter;
            _status.color = new Color(0.9f, 0.95f, 1f);
            _status.horizontalOverflow = HorizontalWrapMode.Overflow;
            _status.text = "";
        }

        private void Update()
        {
            if (_status == null || _nm == null) return;
            if (_nm.IsServer || _nm.IsClient)
            {
                string mode = _nm.IsHost ? "호스트" : (_nm.IsServer ? "서버" : "클라이언트");
                _status.text = $"[{mode}] 접속 인원: {_nm.ConnectedClientsList.Count}   (WASD/화살표로 이동)";
            }
        }

        private void SetStatus(string s)
        {
            if (_status != null) _status.text = s;
            Debug.Log("[NetGame] " + s);
        }

        // ---------- UI 헬퍼 ----------

        private GameObject NewRect(string name, Transform parent, out RectTransform rt)
        {
            var go = new GameObject(name, typeof(RectTransform));
            rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return go;
        }

        private Text MakeText(Transform parent, Font font, string content, int size, TextAnchor anchor, float minH)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font; t.text = content; t.fontSize = size; t.alignment = anchor;
            t.color = new Color(0.95f, 0.96f, 0.98f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            go.AddComponent<LayoutElement>().minHeight = minH;
            return t;
        }

        private Button MakeButton(Transform parent, Font font, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.28f, 0.40f, 0.55f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            go.AddComponent<LayoutElement>().minHeight = 46;

            var txt = new GameObject("Label", typeof(RectTransform));
            var trt = (RectTransform)txt.transform;
            trt.SetParent(go.transform, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var t = txt.AddComponent<Text>();
            t.font = font; t.text = label; t.fontSize = 18; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            return btn;
        }

        private InputField MakeInputField(Transform parent, Font font, string initial)
        {
            var go = new GameObject("IPField", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.20f, 0.24f);
            go.AddComponent<LayoutElement>().minHeight = 40;

            var input = go.AddComponent<InputField>();
            input.targetGraphic = img;

            var txt = new GameObject("Text", typeof(RectTransform));
            var trt = (RectTransform)txt.transform;
            trt.SetParent(go.transform, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10, 6); trt.offsetMax = new Vector2(-10, -6);
            var t = txt.AddComponent<Text>();
            t.font = font; t.fontSize = 17; t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.supportRichText = false;

            input.textComponent = t;
            input.text = initial;
            return input;
        }

        // ---------- 스프라이트 생성 ----------

        private static Sprite MakeCircleSprite()
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float c = size * 0.5f;
            float r = size * 0.46f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                    Color col = Color.white;
                    col.a = d > 1f ? 0f : Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.85f, 1f, d));
                    px[y * size + x] = col;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.3f), 64f);
        }
    }
}
#endif
