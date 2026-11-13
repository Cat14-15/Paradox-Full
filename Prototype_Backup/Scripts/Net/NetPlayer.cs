#if PARADOX_NETCODE
using Paradox.Language;
using Paradox.Roles;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paradox.Net
{
    /// <summary>
    /// 네트워크 플레이어. 서버가 접속 순서대로 4역할을 배정하고,
    /// 소유자(자기 클라이언트)만 자기 캐릭터를 움직인다.
    /// 각자 머리 위에 역할 라벨이 뜨고, 역할별로 색이 다르다.
    /// </summary>
    public class NetPlayer : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 3f;

        // 서버가 배정, 모두가 읽음
        public readonly NetworkVariable<int> RoleIndex =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private static int _serverAssignCounter;

        private SpriteRenderer _sr;
        private TextMesh _label;

        private static readonly Color[] RoleColors =
        {
            new Color(0.95f, 0.75f, 0.30f), // Operator
            new Color(0.42f, 0.82f, 0.55f), // Interpreter
            new Color(0.45f, 0.62f, 0.95f), // Navigator
            new Color(0.70f, 0.55f, 0.95f), // Observer
        };

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _label = GetComponentInChildren<TextMesh>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                RoleIndex.Value = _serverAssignCounter % 4;
                _serverAssignCounter++;
            }

            RoleIndex.OnValueChanged += OnRoleChanged;
            ApplyVisual();

            if (IsOwner)
            {
                ApplyOwnerRole();
                var cam = Camera.main;
                if (cam != null)
                {
                    var follow = cam.GetComponent<CameraFollow>();
                    if (follow != null) follow.target = transform;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            RoleIndex.OnValueChanged -= OnRoleChanged;
        }

        private void OnRoleChanged(int previous, int current)
        {
            ApplyVisual();
            if (IsOwner) ApplyOwnerRole();
        }

        private void ApplyOwnerRole()
        {
            // 내 캐릭터의 역할을 로컬 역할 상태에 반영 → 표지판 ??? 규칙에 사용
            RoleService.LocalRole = (GameRole)RoleIndex.Value;
        }

        private void ApplyVisual()
        {
            int idx = Mathf.Clamp(RoleIndex.Value, 0, 3);
            if (_sr != null) _sr.color = RoleColors[idx];
            if (_label != null)
            {
                _label.text = RoleNames.Korean((GameRole)idx) + (IsOwner ? " (나)" : "");
                _label.color = RoleColors[idx];
            }
        }

        private void Update()
        {
            if (!IsOwner) return;
            if (InteractionState.InputLocked) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            Vector2 mv = Vector2.zero;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv.y += 1f;
            mv = Vector2.ClampMagnitude(mv, 1f);

            transform.position += (Vector3)(mv * moveSpeed * Time.deltaTime);
        }
    }
}
#endif
