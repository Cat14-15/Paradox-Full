using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Paradox.Client
{
    /// <summary>
    /// UI 는 1920×1080 기준으로 놓여 있다. 창이 더 좁은 비율(예: 맥북·브라우저)이면 양옆이 잘리고, 더 넓으면 위아래가 잘리던 것을
    /// 막는다 — 항상 1920×1080 영역 전체가 화면 안에 들어오도록 가로/세로 기준을 비율에 따라 바꾼다. 모든 씬의 캔버스에 자동으로 붙는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CanvasFit : MonoBehaviour
    {
        private CanvasScaler _scaler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            AttachAll();
        }

        private static void OnLoaded(Scene s, LoadSceneMode m) { AttachAll(); }

        private static void AttachAll()
        {
            foreach (var sc in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include))
            {
                if (sc.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
                if (sc.GetComponent<CanvasFit>() == null) sc.gameObject.AddComponent<CanvasFit>();
            }
        }

        private void OnEnable() { _scaler = GetComponent<CanvasScaler>(); Apply(); }
        private void Update() { Apply(); }

        private void Apply()
        {
            if (_scaler == null || Screen.height <= 0) return;
            float aspect = (float)Screen.width / Screen.height;
            float refAspect = _scaler.referenceResolution.x / _scaler.referenceResolution.y;
            _scaler.matchWidthOrHeight = aspect < refAspect ? 0f : 1f;
        }
    }
}
