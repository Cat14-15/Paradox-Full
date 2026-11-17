using Paradox.Core;
using Paradox.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Paradox.Client
{
    /// <summary>
    /// 게임 중 "나가기". 오른쪽 위 버튼(또는 P)으로 확인 창을 열고, 나가면 스테이지 선택으로 돌아간다.
    /// 확인 창 안에서는 Enter = 나가기, X = 계속하기. 버튼·창은 씬에 놓여 있다(Story1FlowBuilder).
    /// </summary>
    public sealed class Story1Pause : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsOpen = false; }

        public CanvasGroup confirm;
        public CanvasGroup button;
        public CanvasGroup[] extra;   // 같은 줄의 다른 단추들(같이 보이고 같이 숨는다)
        public string leaveScene = "Lobby";

        private bool _leaving;

        private void Awake() { SetOpen(false); }

        public void OpenFromButton() { if (!IsOpen && !Story1Scene.InputLocked) SetOpen(true); }
        public void Stay() { SetOpen(false); }

        public void Leave()
        {
            if (_leaving) return;
            _leaving = true;
            var hub = NetHub.Instance;
            if (hub != null && hub.IsRunning) hub.Shutdown();
            Bgm.Stop();
            SceneManager.LoadScene(leaveScene);
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (confirm != null) { confirm.alpha = open ? 1f : 0f; confirm.blocksRaycasts = open; confirm.interactable = open; }
            if (open) Sfx.Play("ui_select", 0.8f);
        }

        private void Update()
        {
            if (button != null)
            {
                bool show = !IsOpen && !Story1Scene.InputLocked && !Story1Flow.Ending;
                button.alpha = show ? 1f : 0f; button.blocksRaycasts = show; button.interactable = show;
                if (extra != null) foreach (var e in extra) if (e != null) { e.alpha = show ? 1f : 0f; e.blocksRaycasts = show; e.interactable = show; }
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (IsOpen)
            {
                if (kb.xKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame) { SetOpen(false); Sfx.Play("ui_back", 0.8f); }
                else if (kb.enterKey.wasPressedThisFrame) Leave();
                return;
            }
            if (kb.pKey.wasPressedThisFrame &&!Story1Scene.InputLocked && !Story1Flow.Ending) SetOpen(true);
        }
    }
}
