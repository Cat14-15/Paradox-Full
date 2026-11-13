using UnityEngine;
using UnityEngine.Rendering;

namespace Paradox.Core
{
    /// <summary>
    /// 2.5D 깊이 정렬. 화면에서 아래(작은 Y)에 있는 오브젝트가 앞에 그려지도록
    /// 이 오브젝트의 SortingGroup 정렬 순서를 Y 위치로부터 계산한다.
    /// 박물관 맵(y ±9)에서는 scale 100이 ±900 클램프에 포화되므로 아바타·정적 프롭 모두 scale 50을 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class YDepthSort : MonoBehaviour
    {
        [Tooltip("Y 1유닛당 정렬 순서 변화량.")]
        [SerializeField] private float sortScale = 100f;

        [Tooltip("발밑 기준으로 정렬하고 싶을 때의 보정(예: 캐릭터는 -0.45).")]
        [SerializeField] private float yOffset = 0f;

        private SortingGroup _group;
        private float _referenceY;

        public void SetReferenceY(float referenceY) => _referenceY = referenceY;

        /// <summary>기본 배율(100)로 정렬 순서를 계산한다(옛 호출 호환).</summary>
        public static int OrderFor(float y) => OrderFor(y, 100f);

        /// <summary>주어진 배율로 정렬 순서를 계산한다. 박물관 맵은 50을 쓴다.</summary>
        public static int OrderFor(float y, float scale) => Mathf.Clamp(Mathf.RoundToInt(-y * scale), -900, 900);

        public void Configure(float scale, float offset)
        {
            sortScale = scale;
            yOffset = offset;
        }

        private void Awake()
        {
            _group = GetComponent<SortingGroup>();
            if (_group == null) _group = gameObject.AddComponent<SortingGroup>();
        }

        private void LateUpdate()
        {
            if (_group == null) return;
            _group.sortingOrder = OrderFor(transform.position.y + yOffset - _referenceY, sortScale);
        }
    }
}
