using UnityEngine;

namespace Paradox.Core
{
    /// <summary>
    /// 종이인형(paper-doll) 캐릭터를 위한 절차적 걸음 애니메이션.
    /// 스프라이트시트 없이 Body/Head/발 파츠의 위치·회전을 코드로 흔들어
    /// 걸을 때 발을 번갈아 들고 몸이 통통 튀는 워크 사이클을 만든다.
    /// 하위에서 "Body", "Head", "Left_Foot", "Right_Foot" 이름의 파츠를 자동으로 찾는다.
    /// 움직임 여부는 MovingProvider(있으면)로, 없으면 위치 변화로 판단한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterWalkAnimator : MonoBehaviour
    {
        [Header("파츠 이름 (하위에서 자동 검색)")]
        [SerializeField] private string bodyName = "Body";
        [SerializeField] private string headName = "Head";
        [SerializeField] private string leftFootName = "Left_Foot";
        [SerializeField] private string rightFootName = "Right_Foot";

        [Header("걸음 사이클")]
        [SerializeField] private float stepsPerSecond = 3.2f;
        [SerializeField] private float footLift = 0.09f;
        [SerializeField] private float footStride = 0.06f;

        [Header("몸통 반동")]
        [SerializeField] private float bodyBob = 0.045f;
        [SerializeField] private float headTilt = 4f;
        [SerializeField] private float bodyLean = 2.5f;

        [Header("전환/정지")]
        [SerializeField] private float blendSpeed = 10f;
        [SerializeField] private bool idleBreathing = true;
        [SerializeField] private float idleBreathAmount = 0.012f;
        [SerializeField] private float idleBreathSpeed = 1.6f;

        public System.Func<bool> MovingProvider;

        private Transform body, head, leftFoot, rightFoot;
        private Vector3 bodyBase, headBase, leftFootBase, rightFootBase;
        private Quaternion bodyRotBase, headRotBase;

        private float phase;
        private float walkBlend;
        private float idlePhase;
        private Vector3 lastPos;
        private float lastMoveTime = -10f;

        private void Awake()
        {
            Rebind();
            lastPos = transform.position;
        }

        /// <summary>파츠가 나중에 붙었을 때(런타임 생성) 다시 찾는다.</summary>
        public void Rebind()
        {
            body = FindDeepChild(transform, bodyName);
            head = FindDeepChild(transform, headName);
            leftFoot = FindDeepChild(transform, leftFootName);
            rightFoot = FindDeepChild(transform, rightFootName);

            if (body != null) { bodyBase = body.localPosition; bodyRotBase = body.localRotation; }
            if (head != null) { headBase = head.localPosition; headRotBase = head.localRotation; }
            if (leftFoot != null) leftFootBase = leftFoot.localPosition;
            if (rightFoot != null) rightFootBase = rightFoot.localPosition;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            bool moving = IsMoving();
            walkBlend = Mathf.MoveTowards(walkBlend, moving ? 1f : 0f, blendSpeed * dt);

            if (moving)
            {
                phase += stepsPerSecond * Mathf.PI * dt;
                if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;
            }

            idlePhase += idleBreathSpeed * dt;

            float leftSin = Mathf.Sin(phase);
            float rightSin = Mathf.Sin(phase + Mathf.PI);

            if (leftFoot != null)
            {
                Vector3 p = leftFootBase;
                p.y += Mathf.Max(0f, leftSin) * footLift * walkBlend;
                p.x += leftSin * footStride * walkBlend;
                leftFoot.localPosition = p;
            }

            if (rightFoot != null)
            {
                Vector3 p = rightFootBase;
                p.y += Mathf.Max(0f, rightSin) * footLift * walkBlend;
                p.x += rightSin * footStride * walkBlend;
                rightFoot.localPosition = p;
            }

            float bob = Mathf.Abs(Mathf.Sin(phase)) * bodyBob * walkBlend;
            float lean = Mathf.Sin(phase) * bodyLean * walkBlend;
            float idle = idleBreathing ? Mathf.Sin(idlePhase) * idleBreathAmount * (1f - walkBlend) : 0f;

            if (body != null)
            {
                Vector3 p = bodyBase;
                p.y += bob + idle;
                body.localPosition = p;
                body.localRotation = bodyRotBase * Quaternion.Euler(0f, 0f, lean);
            }

            if (head != null)
            {
                Vector3 p = headBase;
                p.y += bob + idle;
                head.localPosition = p;
                float tilt = Mathf.Sin(phase) * headTilt * walkBlend;
                head.localRotation = headRotBase * Quaternion.Euler(0f, 0f, -tilt);
            }
        }

        private bool IsMoving()
        {
            if (MovingProvider != null) return MovingProvider();

            Vector3 cur = transform.position;
            bool moved = (cur - lastPos).sqrMagnitude > 1e-6f;
            lastPos = cur;
            if (moved) lastMoveTime = Time.time;
            // 네트워크 보간으로 위치가 띄엄띄엄 갱신되어도 걸음이 끊기지 않게 짧은 여유를 둔다.
            return Time.time - lastMoveTime < 0.12f;
        }

        private static Transform FindDeepChild(Transform root, string targetName)
        {
            if (string.IsNullOrEmpty(targetName)) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == targetName) return child;
                Transform found = FindDeepChild(child, targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
