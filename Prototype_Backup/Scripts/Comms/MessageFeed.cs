using System.Collections.Generic;
using System.Text;
using Paradox.Language;
using Paradox.Roles;
using UnityEngine;
using UnityEngine.UI;

namespace Paradox.Comms
{
    /// <summary>
    /// 공용 메시지 피드 — 단, "내 입장에서" 다르게 보인다.
    ///  - 조작자의 자유 채팅은 그 사람 언어라, 다른 역할에겐 <b>외국어로(못 알아듣게)</b> 보인다.
    ///  - 5버튼 신호·기호 전달·시스템 메시지는 약속된 공용 채널이라 모두 같게 보인다.
    /// 역할을 바꾸면 피드가 그 역할 입장으로 다시 렌더링된다.
    /// </summary>
    public class MessageFeed : MonoBehaviour
    {
        public static MessageFeed Instance { get; private set; }

        private const int MaxLines = 8;

        private class Msg
        {
            public string sender;
            public string text;
            public string hex;
            public bool isChat;       // 언어 장벽 대상(자유 채팅)
            public GameRole senderRole;
        }

        private readonly List<Msg> _msgs = new List<Msg>();
        private Text _text;

        private void Awake()
        {
            Instance = this;
            BuildUI();
            RoleService.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RoleService.Changed -= Refresh;
        }

        /// <summary>공용(모두 같게 보이는) 메시지: 신호·기호·시스템.</summary>
        public static void Send(string sender, string content, Color color)
        {
            Instance?.Post(sender, content, color, false, GameRole.Operator);
        }

        /// <summary>자유 채팅(보낸 사람 언어) — 다른 역할에겐 외국어로 보임.</summary>
        public static void SendChat(string sender, string content, Color color, GameRole senderRole)
        {
            Instance?.Post(sender, content, color, true, senderRole);
        }

        private void Post(string sender, string content, Color color, bool isChat, GameRole senderRole)
        {
            _msgs.Add(new Msg
            {
                sender = sender,
                text = content,
                hex = ColorUtility.ToHtmlStringRGB(color),
                isChat = isChat,
                senderRole = senderRole
            });
            while (_msgs.Count > MaxLines) _msgs.RemoveAt(0);
            Refresh();
        }

        private void BuildUI()
        {
            var canvas = PuzzleOverlay.GetCanvas();
            var panelGo = new GameObject("MessageFeedPanel", typeof(RectTransform));
            var rt = (RectTransform)panelGo.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-16f, -16f);
            rt.sizeDelta = new Vector2(360f, 196f);
            panelGo.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 0.82f);

            var txtGo = new GameObject("Text", typeof(RectTransform));
            var trt = (RectTransform)txtGo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12, 10); trt.offsetMax = new Vector2(-12, -10);
            _text = txtGo.AddComponent<Text>();
            _text.font = UIFontHelper.Load();
            _text.fontSize = 15;
            _text.color = new Color(0.92f, 0.94f, 0.98f);
            _text.alignment = TextAnchor.LowerLeft;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Truncate;
            _text.supportRichText = true;
        }

        private void Refresh()
        {
            if (_text == null) return;
            var sb = new StringBuilder("<size=13><color=#8892A0>[ 메시지 피드 — 내 입장 ]</color></size>\n");
            foreach (var m in _msgs)
            {
                string shown = (m.isChat && RoleService.LocalRole != m.senderRole)
                    ? $"<color=#C08890>{Garble(m.text)}</color> <size=12><color=#7A6A6A>(외국어)</color></size>"
                    : m.text;
                sb.Append($"<color=#{m.hex}><b>{m.sender}</b></color> : {shown}\n");
            }
            _text.text = sb.ToString();
        }

        // 텍스트를 알아볼 수 없는 외국 문자열로 결정적으로 바꾼다(언어 장벽 표현).
        private static string Garble(string text)
        {
            const string bank = "aeioukstnrmlvdbgphz";
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) { sb.Append(' '); continue; }
                int h = (c * 13 + 7) & 0x7fffffff;
                sb.Append(bank[h % bank.Length]);
            }
            return sb.ToString();
        }
    }
}
