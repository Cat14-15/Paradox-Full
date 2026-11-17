using Paradox.Core;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>퍼즐 화면이 열려 있을 때 오른쪽 위에 뜨는 "X 닫기" 단추(키보드 X/Q/Esc 와 같은 일을 클릭으로). 씬에 놓여 있다(Story1FlowBuilder).</summary>
    public sealed class Story1CloseButton : MonoBehaviour
    {
        public CanvasGroup group;
        public CanvasGroup signalGroup;   // 퍼즐 화면 위 "C 신호" 단추

        private static bool Any => FusePanel.IsOpen || GalleryPanel.IsOpen || WaterPanel.IsOpen || RelicPanel.IsOpen || FinalPanel.IsOpen;

        private void Update()
        {
            if (group == null) return;
            bool show = Any;
            group.alpha = show ? 1f : 0f; group.blocksRaycasts = show; group.interactable = show;
            if (signalGroup != null)
            {
                bool s2 = show && !SignalBar.IsOpen;
                signalGroup.alpha = s2 ? 1f : 0f; signalGroup.blocksRaycasts = s2; signalGroup.interactable = s2;
            }
        }

        public void Click()
        {
            if (FusePanel.IsOpen) FindAnyObjectByType<FusePanel>()?.Close();
            else if (GalleryPanel.IsOpen) FindAnyObjectByType<GalleryPanel>()?.Close();
            else if (WaterPanel.IsOpen) FindAnyObjectByType<WaterPanel>()?.Close();
            else if (RelicPanel.IsOpen) FindAnyObjectByType<RelicPanel>()?.Close();
            else if (FinalPanel.IsOpen) FindAnyObjectByType<FinalPanel>()?.Close();
            else return;
            Sfx.Play("ui_back", 0.8f);
        }
    }
}
