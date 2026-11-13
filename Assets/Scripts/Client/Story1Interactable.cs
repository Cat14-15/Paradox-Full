using System.Collections.Generic;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>Story1 에서 [E] 로 상호작용하는 월드 오브젝트(퓨즈함·밸브 등)의 공통 부모. Story1Scene 이 가장 가까운 것을 골라 프롬프트를 띄운다.</summary>
    public abstract class Story1Interactable : MonoBehaviour
    {
        public static readonly List<Story1Interactable> All = new List<Story1Interactable>();

        public float radius = 1.7f;
        /// <summary>프롬프트에 띄울 글(예: "퓨즈함 열기"). 비어 있으면 지금은 상호작용 불가.</summary>
        public abstract string Label { get; }
        public abstract void Activate();

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.8f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
