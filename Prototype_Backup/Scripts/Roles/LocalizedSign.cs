using Paradox.Language;
using UnityEngine;

namespace Paradox.Roles
{
    /// <summary>
    /// 스테이지 배경 국가의 언어로 쓰인 표지판/경고문.
    /// 이번 스테이지의 현지인(RoleService.CanReadSigns == true)에게만 원래 글자가 보이고,
    /// 나머지 전원에게는 ???로 보인다.
    ///
    /// 월드에 떠 있는 글자는 런타임 TextMesh로 만든다(스프라이트/폰트 배치 불필요).
    /// </summary>
    public class LocalizedSign : MonoBehaviour
    {
        [TextArea]
        [Tooltip("현지인에게 보이는 실제 표지판 내용.")]
        [SerializeField] private string realText = "비상구 → 오른쪽";

        [Tooltip("켜면 StageManager가 이 표지판에 '문 코드'를 채워 넣는다.")]
        [SerializeField] private bool isCodeSign = false;

        [SerializeField] private float height = 0.9f;

        private TextMesh _tm;

        public bool IsCodeSign => isCodeSign;

        /// <summary>표지판 내용을 바꾼다(StageManager가 코드를 주입할 때 사용).</summary>
        public void SetRealText(string text)
        {
            realText = text;
            Refresh();
        }

        private void Awake()
        {
            BuildMarker();
            RoleService.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            RoleService.Changed -= Refresh;
        }

        private void BuildMarker()
        {
            var go = new GameObject("SignText");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);

            _tm = go.AddComponent<TextMesh>();
            var font = UIFontHelper.Load();
            _tm.font = font;
            _tm.characterSize = 0.045f;
            _tm.fontSize = 90;
            _tm.anchor = TextAnchor.LowerCenter;
            _tm.alignment = TextAlignment.Center;

            var mr = go.GetComponent<MeshRenderer>();
            mr.material = font.material;
            mr.sortingOrder = 60;
        }

        private void Refresh()
        {
            if (_tm == null) return;
            bool readable = RoleService.CanReadSigns;
            _tm.text = readable ? realText : "???";
            _tm.color = readable ? new Color(1f, 0.95f, 0.75f) : new Color(0.75f, 0.4f, 0.4f);
        }
    }
}
