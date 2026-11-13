using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Paradox.Core
{
    /// <summary>
    /// 모든 화면 UI가 올라가는 오버레이 캔버스와 EventSystem을 필요할 때 한 번 만든다.
    /// 씬에 캔버스를 배치하지 않아도 코드만으로 UI를 띄울 수 있게 보장한다.
    /// </summary>
    public static class UIRoot
    {
        private static Canvas _canvas;

        public static readonly Vector2 Reference = new Vector2(1600, 900);

        public static RectTransform Canvas
        {
            get
            {
                if (_canvas == null)
                {
                    var go = new GameObject("UIRootCanvas");
                    _canvas = go.AddComponent<Canvas>();
                    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    _canvas.sortingOrder = 100;
                    var scaler = go.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = Reference;
                    scaler.matchWidthOrHeight = 0.5f;
                    go.AddComponent<GraphicRaycaster>();
                    Object.DontDestroyOnLoad(go);
                }
                return (RectTransform)_canvas.transform;
            }
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                var module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
                Object.DontDestroyOnLoad(es);
            }
        }
    }
}
