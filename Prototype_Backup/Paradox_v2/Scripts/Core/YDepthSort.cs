using UnityEngine;
using UnityEngine.Rendering;

namespace Paradox.Core
{
    /// <summary>
    /// 2.5D 깊이 정렬. 화면에서 아래(작은 Y)에 있는 오브젝트가 앞에 그려지도록
    /// 이 오브젝트의 SortingGroup 정렬 순서를 Y 위치로부터 계산한다.
    /// 종이인형 캐릭터의 내부 파츠 순서(머리/몸/발)는 그대로 유지된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class YDepthSort : MonoBehaviour
    {
        [Tooltip("Y 1유닛당 정렬 순서 변화량.")]
        [SerializeField] private float sortScale = 100f;

        [Tooltip("발밑 기준으로 정렬하고 싶을 때의 보정(예: 캐릭터는 -0.4).")]
        [SerializeField] private float yOffset = 0f;

        private SortingGroup _group;

        public static int OrderFor(float y, float scale = 100f) => Mathf.Clamp(Mathf.RoundToInt(-y * scale), -900, 900);

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
            _group.sortingOrder = OrderFor(transform.position.y + yOffset, sortScale);
        }
    }
}
