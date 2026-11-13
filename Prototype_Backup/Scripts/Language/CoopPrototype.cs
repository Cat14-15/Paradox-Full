using UnityEngine;

namespace Paradox.Language
{
    /// <summary>
    /// 언어 협동 퍼즐을 단독으로 열어보는 테스트 씬용 부트스트래퍼.
    /// (오버월드 없이 퍼즐 UI만 바로 띄운다. 인게임에서는 PuzzleStation이 대신 연다.)
    /// 빈 씬에 이 컴포넌트 하나만 있으면 카메라/이벤트시스템/캔버스를 만들고 퍼즐을 연다.
    /// </summary>
    public class CoopPrototype : MonoBehaviour
    {
        private static readonly Color BgDark = new Color(0.11f, 0.12f, 0.16f);

        private LanguagePuzzleView _view;

        private void Awake()
        {
            if (Camera.main == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = BgDark;
                cam.orthographic = true;
            }

            PuzzleOverlay.EnsureEventSystem();
            _view = new LanguagePuzzleView(PuzzleOverlay.GetCanvas(), LanguageDatabase.CreateDefault());
            _view.Open();
        }
    }
}
