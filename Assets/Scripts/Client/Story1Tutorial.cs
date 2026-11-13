using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paradox.Client
{
    /// <summary>
    /// Story1 조작 안내(씬에 놓인 UI). 처음 들어오면 한 번 떠서 이동·상호작용(E)·닫기(Q)·신호 카드를 알려 주고,
    /// 이후에는 H 키로 언제든 다시 볼 수 있다. 떠 있는 동안 주인공은 멈춘다.
    /// </summary>
    public sealed class Story1Tutorial : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsOpen = false; }

        private const string Key = "stranger.s1.tutorialSeen";

        public CanvasGroup group;
        public PixelText[] keys = new PixelText[5];
        public PixelText[] descs = new PixelText[5];
        public PixelText hint, roleLine;

        private float _shownAt, _lockSeconds = 0.4f;
        private bool _first;

        private void Awake()
        {
            transform.SetAsLastSibling();
            Close(false);
        }

        private void Start()
        {
            bool seen = false;
            try { seen = PlayerPrefs.GetInt(Key, 0) == 1; } catch { }
            _first = !seen;
            if (!seen) Open();
        }

        private void Open()
        {
            IsOpen = true; _shownAt = Time.unscaledTime;
            _lockSeconds = _first ? 4f : 0.4f;   // 처음 한 번은 건너뛸 수 없고(4초는 읽어야 한다), 다시 볼 때는 바로 닫을 수 있다
            int me = Story1Role.Current;
            roleLine.text = "당신의 역할   " + Roles.Name(me) + "  ·  " + RoleDesc(me);
            roleLine.color = Roles.Color(me);
            group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true;
            bool online = Story1Net.HasHumanPeers;
            bool solo = Story1Role.Solo;   // 에디터 혼자 테스트일 때만 Tab 으로 역할을 바꾼다
            string[] k = { "W A S D", "E", "Q", online ? "마우스" : solo ? "Tab" : "", "H" };
            string[] d =
            {
                "이동  (방향키도 돼요)",
                "문이나 물건 앞에서 상호작용",
                "퍼즐 화면 닫기",
                online ? "아래 카드로 신호 보내기" : solo ? "역할 바꾸기 (혼자 테스트)" : "",
                "이 도움말 다시 보기"
            };
            int row = 0;   // 빈 줄은 건너뛰어 위로 붙인다
            for (int i = 0; i < keys.Length; i++)
            {
                if (string.IsNullOrEmpty(d[i])) continue;
                keys[row].text = k[i]; descs[row].text = d[i]; row++;
            }
            for (; row < keys.Length; row++) { keys[row].text = ""; descs[row].text = ""; }
            hint.text = "";
            Sfx.Play("ui_select", 0.8f);
        }

        private static string RoleDesc(int role)
        {
            switch (role)
            {
                case (int)Paradox.Game.GameRole.Operator: return "퍼즐을 직접 만져요";
                case (int)Paradox.Game.GameRole.Interpreter: return "외국어를 읽고 뜻을 알려요";
                case (int)Paradox.Game.GameRole.Navigator: return "지도와 배선을 보고 길을 알려요";
                default: return "숨은 단서를 찾아 알려요";
            }
        }

        private void Close(bool save)
        {
            IsOpen = false;
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
            if (save) { try { PlayerPrefs.SetInt(Key, 1); PlayerPrefs.Save(); } catch { } }
        }

        private void Update()
        {
            var kb = Keyboard.current; var ms = Mouse.current;
            if (IsOpen)
            {
                float wait = _lockSeconds - (Time.unscaledTime - _shownAt);
                hint.text = wait > 0f ? "[E] 가 화면에 뜨면 그 앞에서 E 를 눌러요" : (_first ? "아무 키나 눌러 시작" : "아무 키나 눌러 닫기");
                if (wait > 0f) return;
                bool press = (kb != null && kb.anyKey.wasPressedThisFrame) || (ms != null && ms.leftButton.wasPressedThisFrame);
                if (press) { _first = false; Close(true); Sfx.Play("ui_back", 0.8f); }
                return;
            }
            if (kb != null && kb.hKey.wasPressedThisFrame && !Story1Scene.InputLocked) Open();
        }
    }
}
