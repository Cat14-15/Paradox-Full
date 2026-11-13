using System.Collections;
using System.Linq;
using NUnit.Framework;
using Paradox.Client;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Paradox.Tests
{
    /// <summary>
    /// 마우스 입력이 Input System → EventSystem(InputSystemUIInputModule) → UGUI 버튼까지 실제로 닿는지 검증한다.
    /// 가상 마우스 상태 이벤트를 큐에 넣어 한 프레임 뒤 처리되게 한다(사람이 클릭하는 것과 같은 경로).
    /// </summary>
    public class InputTests
    {
        private Mouse _mouse;

        private void Pointer(Vector2 screenPos, bool down)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screenPos, buttons = (ushort)(down ? 1 : 0) });
        }

        /// <summary>이동 → 누름 → 뗌. 각 단계 사이에 프레임을 넘겨 EventSystem 이 처리하게 한다.</summary>
        private IEnumerator Click(Vector2 screenPos)
        {
            Pointer(screenPos, false); yield return null; yield return null;
            Diag("move", screenPos, _mouse);
            Pointer(screenPos, true); yield return null; yield return null;
            Diag("down", screenPos, _mouse);
            Pointer(screenPos, false); yield return null; yield return null;
            Diag("up", screenPos, _mouse);
        }

        private static Vector2 ScreenPointOf(RectTransform rt)
        {
            var world = rt.TransformPoint(rt.rect.center);
            return RectTransformUtility.WorldToScreenPoint(null, world);
        }

        private static Button FindButtonWithText(string contains)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var t = b.GetComponentInChildren<Text>(true);
                if (t != null && t.text != null && t.text.Contains(contains)) return b;
            }
            return null;
        }

        private InputSettings.BackgroundBehavior _bg;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode _editorBehavior;
#endif

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return Flow.Boot();
            _bg = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; // 배치 모드(포커스 없음)에서도 가상 마우스 이벤트가 처리되게
#if UNITY_EDITOR
            _editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; // 게임 뷰 포커스 없이도 장치 이벤트가 게임으로
#endif
            _mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            Debug.Log($"[InputTests] mouse={_mouse?.name} enabled={_mouse?.enabled} focus={Application.isFocused} es={(EventSystem.current != null ? EventSystem.current.name : "null")} module={(EventSystem.current != null && EventSystem.current.currentInputModule != null ? EventSystem.current.currentInputModule.GetType().Name : "null")}");
        }

        /// <summary>진단: 장치 상태·EventSystem·해당 지점의 UI 레이캐스트 결과를 로그로 남긴다.</summary>
        private static void Diag(string tag, Vector2 screenPos, Mouse mouse)
        {
            var es = EventSystem.current;
            string mod = "null";
            if (es != null && es.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule m)
                mod = $"ISUIM point={(m.point != null && m.point.action != null ? m.point.action.enabled.ToString() : "none")} click={(m.leftClick != null && m.leftClick.action != null ? m.leftClick.action.enabled.ToString() : "none")} asset={(m.actionsAsset != null ? m.actionsAsset.name : "null")}";
            var hits = new List<RaycastResult>();
            if (es != null)
            {
                var ped = new PointerEventData(es) { position = screenPos };
                es.RaycastAll(ped, hits);
            }
            string top = hits.Count > 0 ? $"{hits[0].gameObject.name} (parent {hits[0].gameObject.transform.parent?.name}) x{hits.Count}" : "none";
            Debug.Log($"[InputTests] {tag}: pos={screenPos} devicePos={mouse.position.ReadValue()} pressed={mouse.leftButton.isPressed} module={mod} rayTop={top} screen={Screen.width}x{Screen.height} sel={(es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "-")}");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.settings.backgroundBehavior = _bg;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorBehavior;
#endif
            yield return Flow.Teardown();
        }

        [UnityTest]
        public IEnumerator Title_ClickStartButton_GoesToPrologue()
        {
            yield return Flow.ToLocal(LocalScreen.Title, "TitleScreen");
            var btn = FindButtonWithText("시작하기");
            Assert.IsNotNull(btn, "타이틀의 [시작하기] 버튼을 찾지 못함");
            yield return Click(ScreenPointOf((RectTransform)btn.transform));
            yield return Flow.Until(() => LocalNav.Screen == LocalScreen.Prologue, 3f, "마우스 클릭으로 [시작하기] → 프롤로그");
        }

        [UnityTest]
        public IEnumerator Prologue_ClickAnywhere_AdvancesLine_AndSkipButtonWorks()
        {
            yield return Flow.ToLocal(LocalScreen.Prologue, "PrologueScreen");
            var pro = PrologueScreen.Instance;
            Assert.IsNotNull(pro, "PrologueScreen.Instance 없음");
            Assert.AreEqual(0, pro.Index);
            yield return Flow.WaitReal(1.4f); // 장면 전환 암전(1.1초)이 걷힐 때까지

            // 사람이 자연스럽게 누르는 자리: 화면 가운데, 그리고 하단 대사창 위
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            for (int i = 0; i < 3 && pro.Index == 0; i++) yield return Click(center);
            Assert.AreEqual(1, pro.Index, "화면 가운데 클릭으로 대사가 다음 줄로 넘어가야 함");
            var box = new Vector2(Screen.width * 0.5f, Screen.height * 0.12f);
            yield return Flow.WaitReal(1.4f); // 다음 줄에 장면 전환이 있으면 암전
            for (int i = 0; i < 3 && pro.Index == 1; i++) yield return Click(box);
            Assert.AreEqual(2, pro.Index, "대사창 위를 클릭해도 다음 줄로 넘어가야 함");

            var skip = FindButtonWithText("SKIP");
            Assert.IsNotNull(skip, "SKIP 버튼을 찾지 못함");
            Assert.IsTrue(skip.gameObject.activeInHierarchy && skip.interactable, "SKIP 버튼이 활성 상태여야 함");
            yield return Click(ScreenPointOf((RectTransform)skip.transform));
            yield return Flow.Until(() => LocalNav.Screen == LocalScreen.StageSelect, 3f, "SKIP 클릭 → 스테이지 선택");
        }
    }
}
