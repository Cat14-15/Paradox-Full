using Paradox.Language;
using Paradox.Roles;
using Paradox.Stage;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Comms
{
    /// <summary>
    /// 온보딩 + 상시 안내를 한곳에 모은 패널.
    ///  - 시작 시: 게임 설명 오버레이(무엇을/어떻게)
    ///  - 상시: 목표 + 진행도 + "지금 내 역할이 할 일" 한 줄
    ///  - 좌상단: 역할 상태 + 개발용 단축키
    /// (기존에 흩어져 겹치던 RoleHud/진행표시를 대체)
    /// </summary>
    public class GuidePanel : MonoBehaviour
    {
        private StageManager _stage;
        private Text _roleStatus;
        private Text _objective;
        private Text _todo;
        private Text _progress;
        private GameObject _intro;

        private void Start()
        {
            PuzzleOverlay.EnsureEventSystem();
            _stage = Object.FindFirstObjectByType<StageManager>();
            BuildHud();
            BuildIntro();
            RoleService.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy() => RoleService.Changed -= Refresh;

        private void Update()
        {
            // 인트로가 떠 있으면 게임 입력은 잠긴 상태 — Enter/Space로 시작
            if (_intro != null)
            {
                var k = Keyboard.current;
                if (k != null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) CloseIntro();
                return;
            }

            HandleDevKeys();

            if (_todo != null)
                _todo.text = $"<b>지금 할 일</b>\n{Instruction()}";
            if (_progress != null && _stage != null)
                _progress.text = "진행 — " + _stage.ProgressLine;
        }

        private void HandleDevKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            GameRole? pick = null;
            if (kb.digit1Key.wasPressedThisFrame) pick = GameRole.Operator;
            else if (kb.digit2Key.wasPressedThisFrame) pick = GameRole.Interpreter;
            else if (kb.digit3Key.wasPressedThisFrame) pick = GameRole.Navigator;
            else if (kb.digit4Key.wasPressedThisFrame) pick = GameRole.Observer;
            if (pick.HasValue)
            {
                if (shift) RoleService.StageLocalRole = pick.Value;
                else RoleService.LocalRole = pick.Value;
            }
        }

        private string Instruction()
        {
            string localTag = RoleService.CanReadSigns
                ? "\n<color=#FFE9B0>▶ 당신만 표지판을 읽습니다 — 아래 바에서 기호를 순서대로 전달하세요.</color>"
                : "";
            switch (RoleService.LocalRole)
            {
                case GameRole.Operator:
                    return "레버를 <b>누를 수 있는 유일한</b> 역할. 전달된 기호와 <b>같은 모양 레버</b>를 순서대로 눌러요. (출구·함정은 안 보임 — 안내 따라 이동)" + localTag;
                case GameRole.Interpreter:
                    return "표지판을 읽는 담당(현지인일 때). 보드에서 기호를 순서대로 전달하세요." + localTag;
                case GameRole.Navigator:
                    return "<b>출구와 지도(미니맵)</b>를 보는 유일한 역할. 어느 기호 레버가 어디 있는지 조작자에게 안내하세요." + localTag;
                case GameRole.Observer:
                    return "<b>함정</b>을 보는 유일한 역할. 바닥 빨간 함정을 피하도록 동료를 안내하세요." + localTag;
                default:
                    return "";
            }
        }

        private void Refresh()
        {
            if (_roleStatus != null)
            {
                string me = RoleNames.Korean(RoleService.LocalRole);
                string local = RoleNames.Korean(RoleService.StageLocalRole);
                string sign = RoleService.CanReadSigns ? "<color=#FFE9B0>읽힘</color>" : "<color=#E08080>???</color>";
                _roleStatus.text =
                    $"역할 <b>{me}</b>  ·  이번 현지인 <b>{local}</b>  ·  표지판 {sign}\n" +
                    "<size=13><color=#8892A0>[테스트] 1~4 내 역할  ·  Shift+1~4 현지인 바꾸기</color></size>";
            }
            if (_objective != null && _stage != null)
                _objective.text = _stage.ObjectiveText;
        }

        // ---------- UI ----------

        private void BuildHud()
        {
            var canvas = PuzzleOverlay.GetCanvas();
            var font = UIFontHelper.Load();

            // 왼쪽 세로 열: 역할 상태 → 지금 할 일 → 진행도 (겹침 방지)
            _roleStatus = Label(canvas, font, new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(560f, 46f),
                TextAnchor.UpperLeft, 16, new Color(0.92f, 0.95f, 1f));
            _roleStatus.horizontalOverflow = HorizontalWrapMode.Wrap;

            _todo = Label(canvas, font, new Vector2(0f, 1f), new Vector2(16f, -60f), new Vector2(560f, 76f),
                TextAnchor.UpperLeft, 15, new Color(0.95f, 0.97f, 0.75f));
            _todo.horizontalOverflow = HorizontalWrapMode.Wrap;

            _progress = Label(canvas, font, new Vector2(0f, 1f), new Vector2(16f, -150f), new Vector2(560f, 24f),
                TextAnchor.UpperLeft, 15, new Color(0.7f, 0.78f, 0.88f));

            // 가운데: 목표(짧게)
            _objective = Label(canvas, font, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(680f, 28f),
                TextAnchor.MiddleCenter, 17, new Color(1f, 0.95f, 0.7f));
        }

        private Text Label(Transform canvas, Font font, Vector2 anchor, Vector2 pos, Vector2 size,
            TextAnchor align, int fontSize, Color color)
        {
            var go = new GameObject("GuideText", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.alignment = align; t.color = color;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private void BuildIntro()
        {
            var canvas = PuzzleOverlay.GetCanvas();
            var font = UIFontHelper.Load();

            _intro = new GameObject("Intro", typeof(RectTransform));
            var back = (RectTransform)_intro.transform;
            back.SetParent(canvas, false);
            back.anchorMin = Vector2.zero; back.anchorMax = Vector2.one;
            back.offsetMin = Vector2.zero; back.offsetMax = Vector2.zero;
            _intro.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.08f, 0.92f);
            InteractionState.PushLock();

            var panel = new GameObject("Panel", typeof(RectTransform));
            var prt = (RectTransform)panel.transform;
            prt.SetParent(back, false);
            prt.anchorMin = new Vector2(0.5f, 0.5f); prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(720f, 420f);
            panel.AddComponent<Image>().color = new Color(0.11f, 0.13f, 0.18f, 1f);
            var v = panel.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(30, 30, 26, 26); v.spacing = 16;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            Line(prt, font, "PARADOX — 언어 장벽 협동", 26, new Color(1f, 0.95f, 0.7f), TextAnchor.MiddleCenter, 40);
            Line(prt, font,
                "네 사람은 서로 <b>말이 통하지 않습니다</b>. 이번 판의 '현지인'만 표지판을 읽을 수 있어요.\n" +
                "힘을 합쳐 <b>문 코드</b>를 알아내고, 레버를 켜서 모두 <b>도착존</b>으로 탈출하세요.",
                17, new Color(0.9f, 0.93f, 0.97f), TextAnchor.UpperLeft, 90);
            Line(prt, font,
                "<b>조작</b>   WASD·화살표 이동   ·   <b>E</b> 상호작용   ·   마우스로 버튼/카드 클릭\n" +
                "<b>테스트</b>   1~4 내 역할 바꾸기   ·   Shift+1~4 현지인 바꾸기\n" +
                "지금은 혼자 네 역할을 번갈아 맡아 한 판을 풀어볼 수 있습니다.",
                15, new Color(0.78f, 0.82f, 0.88f), TextAnchor.UpperLeft, 92);

            var btn = MakeButton(prt, font, "시작하기  (Enter)", new Color(0.30f, 0.45f, 0.60f), CloseIntro);
            btn.GetComponent<LayoutElement>().minHeight = 50;
        }

        private void CloseIntro()
        {
            if (_intro == null) return;
            Destroy(_intro);
            _intro = null;
            InteractionState.PopLock();
        }

        private Text Line(Transform parent, Font font, string text, int size, Color color, TextAnchor anchor, float minH)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = anchor;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            go.AddComponent<LayoutElement>().minHeight = minH;
            return t;
        }

        private Button MakeButton(Transform parent, Font font, string label, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = bg;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            go.AddComponent<LayoutElement>().minHeight = 48;
            var txt = new GameObject("Label", typeof(RectTransform));
            var trt = (RectTransform)txt.transform; trt.SetParent(go.transform, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var t = txt.AddComponent<Text>();
            t.font = font; t.text = label; t.fontSize = 19; t.color = Color.white; t.alignment = TextAnchor.MiddleCenter;
            return btn;
        }
    }
}
