using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 혼자 테스트할 때 퍼즐 화면 아래에 뜨는 "AI 동료" 말풍선 띠(씬에 놓인 UI). 통역·내비게이터·관측자 AI 가 자기가 아는 것을 조작자에게 말해 준다.
    /// 온라인(진짜 동료가 있을 때)이나 Tab 으로 다른 역할을 보는 중에는 숨는다.
    /// </summary>
    public sealed class SoloBots : MonoBehaviour
    {
        public CanvasGroup group;
        public GameObject[] rows = new GameObject[3];
        public PixelText[] names = new PixelText[3];
        public PixelText[] lines = new PixelText[3];

        private static SoloBots _i;
        private bool _visible;
        private float _t0;

        private void Awake()
        {
            _i = this;
            transform.SetAsLastSibling();   // 퍼즐 화면의 어두운 배경보다 위에
            if (group != null) group.alpha = 0f;
        }

        private void OnDestroy() { if (_i == this) _i = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _i = null; }

        private int _count;

        /// <summary>say = [통역, 내비게이터, 관측자] 의 말. AI 가 맡은 역할의 말만 띄운다. 맡은 AI 가 없으면 숨긴다.</summary>
        public static void Sync(string[] say)
        {
            if (_i == null) return;
            if (say != null && say.Length >= 3) _i.Show(say); else _i.HideNow();
        }

        public static void Hide() { if (_i != null) _i.HideNow(); }

        private void Show(string[] say)
        {
            int k = 0;
            for (int i = 0; i < 3 && k < rows.Length; i++)
            {
                int role = BotLines.RoleOrder[i];
                if (!Story1Role.IsBot(role) || string.IsNullOrEmpty(say[i])) continue;
                names[k].text = Roles.Name(role) + " AI"; names[k].color = Roles.Color(role);
                lines[k].text = say[i];
                k++;
            }
            _count = k;
            ((RectTransform)transform).anchoredPosition = new Vector2(0f, Story1Net.HasHumanPeers ? 488f : -488f);   // 사람 동료가 있으면 신호 카드 띠가 아래를 쓴다
            if (k == 0) { HideNow(); return; }
            if (!_visible) { _visible = true; _t0 = Time.unscaledTime; }
            Tick();
        }

        private void HideNow() { _visible = false; if (group != null) group.alpha = 0f; }

        private void Update() { if (_visible) Tick(); }

        // 한 줄씩 차례로 나타난다(동료가 하나씩 말하는 느낌)
        private void Tick()
        {
            group.alpha = 1f;
            for (int i = 0; i < 3; i++) rows[i].SetActive(i < _count && Time.unscaledTime >= _t0 + 0.35f * i);
        }
    }
}
