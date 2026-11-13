using Paradox.Language;
using Paradox.Roles;
using Paradox.Stage;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Comms
{
    /// <summary>
    /// 역할에 따라 달라지는 하단 소통 도구 바.
    ///  - 외국인(통역/내비게이터/관측자): 5버튼 신호
    ///  - 조작자: 자유 채팅 입력
    /// 보낸 내용은 공용 MessageFeed로 흐른다.
    ///
    /// 개발 중 RoleHud의 1~4키로 역할을 바꾸면 이 바의 도구도 즉시 바뀐다.
    /// </summary>
    public class CommsHUD : MonoBehaviour
    {
        // 외국인용 5개 신호 (스테이지 1: 방향 안내 중심)
        private static readonly string[] Signals = { "← 왼쪽", "→ 오른쪽", "↑ 위", "↓ 아래", "● 확인" };
        // 통역(현지인)이 전달할 기호들
        private static readonly string[] Symbols = { "●", "◆", "▲", "■", "★" };

        private StageManager _stage;
        private RectTransform _toolArea;
        private Text _roleLabel;
        private InputField _chatField;
        private bool _chatLocked;

        private void Start()
        {
            PuzzleOverlay.EnsureEventSystem();
            _stage = Object.FindFirstObjectByType<StageManager>();
            BuildBar();
            RoleService.Changed += RebuildTools;
            RebuildTools();
        }

        private void OnDestroy()
        {
            RoleService.Changed -= RebuildTools;
            if (_chatLocked) { InteractionState.PopLock(); _chatLocked = false; }
        }

        private void Update()
        {
            // 채팅 입력 중에는 캐릭터가 WASD로 움직이지 않도록 잠근다.
            bool focused = _chatField != null && _chatField.isFocused;
            if (focused != _chatLocked)
            {
                if (focused) InteractionState.PushLock();
                else InteractionState.PopLock();
                _chatLocked = focused;
            }
        }

        private void BuildBar()
        {
            var canvas = PuzzleOverlay.GetCanvas();
            var font = UIFontHelper.Load();

            var barGo = new GameObject("CommsBar", typeof(RectTransform));
            var rt = (RectTransform)barGo.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 16f);
            rt.sizeDelta = new Vector2(760f, 92f);

            var bg = barGo.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.12f, 0.16f, 0.96f);

            var v = barGo.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 8, 8);
            v.spacing = 6;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            _roleLabel = MakeText(rt, font, "", 15, TextAnchor.MiddleLeft, 20);

            var toolGo = new GameObject("Tools", typeof(RectTransform));
            _toolArea = (RectTransform)toolGo.transform;
            _toolArea.SetParent(rt, false);
            var h = toolGo.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            toolGo.AddComponent<LayoutElement>().minHeight = 48;
        }

        private void RebuildTools()
        {
            if (_toolArea == null) return;

            if (_chatLocked) { InteractionState.PopLock(); _chatLocked = false; }
            _chatField = null;
            for (int i = _toolArea.childCount - 1; i >= 0; i--)
                Destroy(_toolArea.GetChild(i).gameObject);

            var role = RoleService.LocalRole;
            Color rc = RoleColor(role);
            string hex = ColorUtility.ToHtmlStringRGB(rc);
            string name = $"내 역할: <b><color=#{hex}>{RoleNames.Korean(role)}</color></b>";

            if (RoleService.CanReadSigns)
            {
                string seq = _stage != null ? _stage.SequenceText : "";
                _roleLabel.text = $"{name}  ·  <color=#FFE9B0>표지판 순서 = {seq}</color> (당신만 보임) — 아래에서 순서대로 전달";
                BuildRelayTool(rc);
            }
            else if (role == GameRole.Operator)
            {
                _roleLabel.text = $"{name}  ·  자유 채팅 (다른 역할은 외국어로 들려 못 알아들음)";
                BuildChatTool(rc);
            }
            else
            {
                _roleLabel.text = $"{name}  ·  5버튼 신호";
                BuildFiveButtons(rc, role);
            }
        }

        private void BuildRelayTool(Color rc)
        {
            var font = UIFontHelper.Load();
            MakeText(_toolArea, font, "전달▶", 16, TextAnchor.MiddleCenter, 20)
                .GetComponent<LayoutElement>().flexibleWidth = 0.4f;
            foreach (var s in Symbols)
            {
                string sym = s;
                MakeButton(_toolArea, font, sym, new Color(0.24f, 0.27f, 0.33f),
                    () => MessageFeed.Send("통역", "전달 → " + sym, new Color(0.95f, 0.85f, 0.4f)));
            }
        }

        private void BuildFiveButtons(Color rc, GameRole role)
        {
            var font = UIFontHelper.Load();
            foreach (var sig in Signals)
            {
                string s = sig;
                MakeButton(_toolArea, font, s, new Color(0.22f, 0.25f, 0.30f),
                    () => MessageFeed.Send(RoleNames.Korean(role), s, rc));
            }
        }

        private void BuildChatTool(Color rc)
        {
            var font = UIFontHelper.Load();

            _chatField = MakeInputField(_toolArea, font, "메시지를 입력하고 Enter…");
            var le = _chatField.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            _chatField.onEndEdit.AddListener(text =>
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                MessageFeed.SendChat(RoleNames.Korean(GameRole.Operator), text.Trim(), rc, GameRole.Operator);
                _chatField.text = "";
                _chatField.ActivateInputField();
            });

            MakeButton(_toolArea, font, "전송", new Color(0.28f, 0.40f, 0.55f), () =>
            {
                if (_chatField == null || string.IsNullOrWhiteSpace(_chatField.text)) return;
                MessageFeed.SendChat(RoleNames.Korean(GameRole.Operator), _chatField.text.Trim(), rc, GameRole.Operator);
                _chatField.text = "";
            }).GetComponent<LayoutElement>().flexibleWidth = 0.2f;
        }

        // ---------- 색 ----------

        private static Color RoleColor(GameRole role)
        {
            switch (role)
            {
                case GameRole.Operator: return new Color(0.95f, 0.75f, 0.30f);
                case GameRole.Interpreter: return new Color(0.42f, 0.82f, 0.55f);
                case GameRole.Navigator: return new Color(0.45f, 0.62f, 0.95f);
                case GameRole.Observer: return new Color(0.70f, 0.55f, 0.95f);
                default: return Color.white;
            }
        }

        // ---------- UI 헬퍼 ----------

        private Text MakeText(Transform parent, Font font, string content, int size, TextAnchor anchor, float minH)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font; t.text = content; t.fontSize = size; t.alignment = anchor;
            t.color = new Color(0.95f, 0.96f, 0.98f);
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            go.AddComponent<LayoutElement>().minHeight = minH;
            return t;
        }

        private Button MakeButton(Transform parent, Font font, string label, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            go.AddComponent<LayoutElement>().minHeight = 46;

            var txt = new GameObject("Label", typeof(RectTransform));
            var trt = (RectTransform)txt.transform;
            trt.SetParent(go.transform, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var t = txt.AddComponent<Text>();
            t.font = font; t.text = label; t.fontSize = 18; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            return btn;
        }

        private InputField MakeInputField(Transform parent, Font font, string placeholder)
        {
            var go = new GameObject("ChatField", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.20f, 0.24f);
            go.AddComponent<LayoutElement>().minHeight = 44;

            var input = go.AddComponent<InputField>();
            input.targetGraphic = img;

            var ph = new GameObject("Placeholder", typeof(RectTransform));
            var prt = (RectTransform)ph.transform;
            prt.SetParent(go.transform, false);
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(10, 6); prt.offsetMax = new Vector2(-10, -6);
            var pt = ph.AddComponent<Text>();
            pt.font = font; pt.fontSize = 16; pt.color = new Color(0.6f, 0.62f, 0.66f);
            pt.alignment = TextAnchor.MiddleLeft; pt.text = placeholder; pt.supportRichText = false;

            var txt = new GameObject("Text", typeof(RectTransform));
            var trt = (RectTransform)txt.transform;
            trt.SetParent(go.transform, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10, 6); trt.offsetMax = new Vector2(-10, -6);
            var t = txt.AddComponent<Text>();
            t.font = font; t.fontSize = 16; t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft; t.supportRichText = false;

            input.textComponent = t;
            input.placeholder = pt;
            input.text = "";
            return input;
        }
    }
}
