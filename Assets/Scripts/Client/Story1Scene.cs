using System.Collections;
using Paradox.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// Story1.unity 전용 컨트롤러. 방(MuseumRoom)·문(RoomDoor)·벽 콜라이더·주인공·카메라·페이드/프롬프트 UI 는 모두 씬에 직접 배치돼 있고,
    /// 이 컴포넌트는 입력 → 주인공 이동, 걷기 모션, 문 앞 프롬프트([E]), 문 이동(검은 화면 → 순간이동 → 카메라 고정)만 맡는다.
    /// </summary>
    public class Story1Scene : MonoBehaviour
    {
        [Header("씬에 배치된 오브젝트")]
        public Rigidbody2D player;
        public SpriteRenderer playerRenderer;
        public Camera cam;
        public MuseumRoom[] rooms;
        public RoomDoor[] doors;
        [Tooltip("화면 전체를 덮는 검은 이미지의 CanvasGroup")]
        public CanvasGroup fade;
        public PixelText prompt;   // 픽셀 폰트(웹/OS 폰트 없는 PC 에서도 한글이 나온다)

        [Tooltip("주인공 걷기 스프라이트: 방향(아래/위/왼/오른) × 5프레임 = 20장, 순서 dir*5+frame")]
        public Sprite[] frames = new Sprite[20];

        public float speed = 4.3f;
        public float fadeOut = 0.2f, fadeIn = 0.25f;

        /// <summary>테스트용: 0 이 아니면 키보드 대신 이 방향으로 걷는다.</summary>
        [System.NonSerialized] public Vector2 testInput;
        /// <summary>테스트용: true 로 두면 E 를 누른 것으로 친다(한 번 쓰면 꺼짐).</summary>
        [System.NonSerialized] public bool testUse;

        /// <summary>퍼즐 화면이 열려 있는 동안 주인공을 멈춘다.</summary>
        public static bool InputLocked => FusePanel.IsOpen || GalleryPanel.IsOpen || WaterPanel.IsOpen || FinalPanel.IsOpen || RelicPanel.IsOpen || Story1Flow.Ending || Story1Tutorial.IsOpen || Story1Map.IsOpen || Story1Pause.IsOpen || SignalBar.IsOpen || Story1RoomInfo.IsOpen;

        /// <summary>방에 들어갈 때(방 이름). 화면 위 방 이름 표시가 듣는다.</summary>
        public static event System.Action<string> RoomEntered;

        public bool Busy { get; private set; }
        public MuseumRoom CurrentRoom { get; private set; }

        private Vector2 _move;
        private int _dir = PixelSprites.DirDown;
        private float _animT;
        private RoomDoor _near;

        // 스테이지를 다시 해도(스테이지 선택에서 되돌아와도) 항상 같은 처음 상태에서 시작하도록 전역 상태를 비운다
        private void Awake()
        {
            Story1Blackout.ResetState(); GalleryPanel.ResetState(); RelicPanel.ResetState(); WaterPanel.ResetState(); FinalPanel.ResetState();
            Story1Net.ResetWorld();
            if (player != null) player.transform.localScale *= CharScale;   // 캐릭터를 더 크게
        }

        public const float CharScale = 1.35f;

        private void Start()
        {
            if (fade != null) fade.alpha = 0f;
            ShowPrompt(null);
            Bgm.Play("story1", 0.3f);   // 정전 직후의 불안한 분위기
            MuseumForeground.Install(rooms);
            EnterRoom(RoomAt(player.position), false);
        }

        private MuseumRoom RoomAt(Vector2 p)
        {
            MuseumRoom best = null; float bd = float.MaxValue;
            foreach (var r in rooms)
            {
                float d = ((Vector2)r.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        private void EnterRoom(MuseumRoom r, bool snap)
        {
            bool changed = CurrentRoom != r;
            CurrentRoom = r;
            if (r == null) return;
            if (changed) RoomEntered?.Invoke(string.IsNullOrEmpty(r.roomName) ? r.gameObject.name : r.roomName);
            var depthSort = player.GetComponent<YDepthSort>();
            if (depthSort != null) depthSort.SetReferenceY(r.transform.position.y);
            cam.orthographicSize = r.cameraSize;
            cam.transform.position = new Vector3(r.transform.position.x, r.transform.position.y, -10f);
        }

        private Sprite Frame(int frame)
        {
            int i = _dir * PixelSprites.Cols + Mathf.Clamp(frame, 0, PixelSprites.Cols - 1);
            var rf = Story1Companions.RoleFrames;
            int role = Story1Role.Current;
            if (rf != null && role >= 0 && role < rf.Length && rf[role] != null && rf[role][i] != null) return rf[role][i];
            return frames[i];
        }

        private void ShowPrompt(string text)
        {
            if (prompt == null) return;
            bool show = !string.IsNullOrEmpty(text);
            if (prompt.gameObject.activeSelf != show) prompt.gameObject.SetActive(show);
            if (show) prompt.text = text;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            Vector2 d = Vector2.zero;
            if (!Busy && !InputLocked && kb != null)
            {
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) d.x -= 1f;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) d.x += 1f;
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) d.y += 1f;
                if (kb.downArrowKey.isPressed || kb.sKey.isPressed) d.y -= 1f;
            }
            if (!Busy && !InputLocked && testInput != Vector2.zero) d = testInput;
            _move = d.normalized;

            // 모션은 실제로 움직이고 있는지(벽에 막히면 멈춤)로 정한다
            bool walking = _move.sqrMagnitude > 0f && player.linearVelocity.sqrMagnitude > 0.05f;
            if (_move.sqrMagnitude > 0f) _dir = PixelSprites.DirectionFor(_move.x, _move.y, _dir);
            if (walking)
            {
                _animT += Time.deltaTime;
                playerRenderer.sprite = Frame(PixelSprites.WalkCycle[(int)(_animT / PixelSprites.FrameTime) % PixelSprites.WalkCycle.Length]);
            }
            else
            {
                _animT = 0f;
                playerRenderer.sprite = Frame(PixelSprites.IdleFrame);
            }

            // 문 앞 프롬프트
            _near = null;
            _nearThing = null;
            if (!Busy && !InputLocked)
            {
                Vector2 p = player.position;
                float bd = float.MaxValue;
                foreach (var door in doors)
                {
                    if (door == null || door.destination == null) continue;
                    float dist = Vector2.Distance(p, door.transform.position);
                    if (dist <= door.radius + (door == _lastShown ? 0.2f : 0f) && dist < bd) { bd = dist; _near = door; }
                }
            }
            // 월드 상호작용(퓨즈함 등) — 문보다 가까우면 그쪽이 우선
            if (!Busy && !InputLocked)
            {
                float bt = _near != null ? Vector2.Distance(player.position, _near.transform.position) : float.MaxValue;
                foreach (var it in Story1Interactable.All)
                {
                    if (it == null || string.IsNullOrEmpty(it.Label)) continue;
                    float dist = Vector2.Distance(player.position, it.transform.position);
                    if (dist <= it.radius && dist < bt) { bt = dist; _nearThing = it; _near = null; }
                }
            }
            _lastShown = _near;

            ShowPrompt(_nearThing != null ? "[E]  " + _nearThing.Label : _near != null ? "[E]  " + _near.label + "" : null);

            bool use = testUse || (kb != null && (Application.isFocused || Application.platform == RuntimePlatform.WebGLPlayer) && kb.eKey.wasPressedThisFrame);
            testUse = false;
            if (use && _nearThing != null) _nearThing.Activate();
            else if (use && _near != null) StartCoroutine(Pass(_near));
        }

        private RoomDoor _lastShown;
        private Story1Interactable _nearThing;

        private void FixedUpdate()
        {
            player.linearVelocity = Busy ? Vector2.zero : _move * speed;
        }

        // 문을 지난다: 화면이 검게 → 상대편에 놓이고 카메라가 그 방으로 → 화면이 돌아온다
        private IEnumerator Pass(RoomDoor door)
        {
            if (Busy) yield break;
            Busy = true;
            ShowPrompt(null);
            player.linearVelocity = Vector2.zero;
            yield return Fade(1f, fadeOut);
            Vector2 to = door.destination.position;
            player.position = to;
            player.transform.position = to;
            EnterRoom(RoomAt(to), true);
            yield return null;
            yield return Fade(0f, fadeIn);
            Busy = false;
        }

        private IEnumerator Fade(float to, float seconds)
        {
            if (fade == null) yield break;
            float from = fade.alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) { fade.alpha = Mathf.Lerp(from, to, t / seconds); yield return null; }
            fade.alpha = to;
        }
    }
}
