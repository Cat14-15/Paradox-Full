using System;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Paradox.Client
{
    [Serializable]
    public class FuseGfx
    {
        public Image body, cap;
        public void Set(int color, bool show)
        {
            if (body == null) return;
            body.gameObject.SetActive(show && color >= 0);
            if (!show || color < 0) return;
            var c = FusePanel.Colors[color];
            body.color = c;
            if (cap != null) cap.color = Color.Lerp(c, Color.white, 0.45f);
        }
    }

    /// <summary>
    /// 퍼즐 1 퓨즈함 화면(씬에 놓인 UI). 보는 사람의 역할에 따라 다른 면이 보인다.
    ///  조작자 — 퓨즈를 직접 꽂는다 / 통역 — 필리핀어 설명서 / 관측자 — 퓨즈함 속 그을음 / 내비게이터 — 배선도.
    /// 정답 판정은 FusePuzzle(순수 규칙)이 한다.
    /// </summary>
    public sealed class FusePanel : MonoBehaviour
    {
        public static readonly Color[] Colors =
        {
            new Color(0.92f, 0.27f, 0.27f), new Color(0.27f, 0.52f, 0.96f), new Color(0.98f, 0.80f, 0.22f), new Color(0.31f, 0.82f, 0.42f)
        };

        public static bool IsOpen { get; private set; }

        [Header("공통")]
        public CanvasGroup group;
        public RectTransform content;          // 흔들림(오답)용
        public PixelText title, roleText, hint, status;
        public Image flash;                    // 성공/실패 번쩍임
        public RectTransform[] slotRects = new RectTransform[4];
        public Image[] slotFrames = new Image[4];
        public PixelText[] slotNums = new PixelText[4];
        public FuseGfx[] slotFuses = new FuseGfx[4];

        [Header("조작자 면")]
        public GameObject viewOperator;
        public Image cursor;
        public RectTransform[] trayRects = new RectTransform[4];
        public FuseGfx[] trayFuses = new FuseGfx[4];
        public PixelText[] trayLabels = new PixelText[4];

        [Header("통역 면(설명서)")]
        public GameObject viewInterpreter;
        public PixelText[] manualTl = new PixelText[2];
        public PixelText[] manualKo = new PixelText[2];
        public PixelText manualNote;

        [Header("관측자 면(그을음)")]
        public GameObject viewObserver;
        public RectTransform[] sootRects = new RectTransform[2];
        public Image[] sootSpark = new Image[2];

        [Header("내비게이터 면(배선도)")]
        public GameObject viewNavigator;
        public RectTransform[] lugRects = new RectTransform[4];
        public Image[] lugImages = new Image[4];
        public RectTransform wire;
        public Image wireImage;
        public PixelText[] wireQ = new PixelText[3];   // 연결이 보이지 않는 줄의 "?"

        private FuseBox _box;
        private FusePuzzle _puzzle;
        private readonly int[] _slots = { -1, -1, -1, -1 };
        private int _cur;
        private int _openedFrame;
        private float _shake, _flashA, _closeAt = -1f;
        private Color _flashCol = Color.white;
        private bool _solved;
        private int _sayVer = -1;
        private int _fails;

        private void Awake() { if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; } }

        public void Open(FuseBox box, FusePuzzle puzzle)
        {
            _box = box; _puzzle = puzzle;
            for (int i = 0; i < 4; i++) _slots[i] = -1;
            _cur = 0; _solved = false; _closeAt = -1f; _flashA = 0f;
            group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true;
            IsOpen = true; _openedFrame = Time.frameCount; _sayVer = -1; _fails = 0; Story1Net.ResetSent(Story1Net.PFuse);
            if (status != null) status.text = "";
            Refresh();
            Sfx.Play("ui_select", 0.8f);
        }

        public void Close()
        {
            IsOpen = false;
            SoloBots.Hide();
            group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;
            Sfx.Play("ui_back", 0.8f);
        }

        // 역할에 맞는 면을 켜고 내용을 채운다
        private void Refresh()
        {
            int role = Story1Role.Current;
            bool op = role == (int)GameRole.Operator, tr = role == (int)GameRole.Interpreter, ob = role == (int)GameRole.Observer, nv = role == (int)GameRole.Navigator;
            viewOperator.SetActive(op); viewInterpreter.SetActive(tr); viewObserver.SetActive(ob); viewNavigator.SetActive(nv);
            for (int s = 0; s < 4; s++) slotRects[s].gameObject.SetActive(!tr);

            Color rc = Roles.Color(role);
            roleText.text = "역할 · " + Roles.Name(role) + (Story1Role.Solo ? "    [Tab] 역할 바꾸기 (혼자 테스트)" : "");
            roleText.color = rc;

            if (op)
            {
                title.text = "분전반 · 퓨즈함" + Story1Net.RoundLabel(Story1Net.PFuse);
                hint.text = "← →  칸 고르기     1~4  퓨즈 꽂기     Backspace  비우기     Enter  전원 투입";
                for (int s = 0; s < 4; s++) slotFuses[s].Set(_slots[s], true);
                cursor.rectTransform.anchoredPosition = slotRects[_cur].anchoredPosition;
                cursor.gameObject.SetActive(!_solved);
                for (int c = 0; c < 4; c++) { trayFuses[c].Set(c, true); trayLabels[c].text = (c + 1) + "  " + FusePuzzle.ColorKo[c]; }
            }
            else
            {
                for (int s = 0; s < 4; s++) slotFuses[s].Set(-1, false);
                cursor.gameObject.SetActive(false);
            }

            if (tr)
            {
                title.text = "정비 설명서 (필리핀어)";
                for (int i = 0; i < 2; i++)
                {
                    manualTl[i].text = FusePuzzle.ManualTagalog(_puzzle.order[i]);
                    manualKo[i].text = "→ " + FusePuzzle.ManualKorean(_puzzle.order[i]);
                }
                manualNote.text = "퓨즈는 왼쪽에서 오른쪽으로 1~4번 슬롯입니다.";
                hint.text = "이 글은 당신만 읽을 수 있어요. 팀원에게 신호로 알려 주세요.";
            }
            if (ob)
            {
                title.text = "퓨즈함 속 (그을음)";
                for (int i = 0; i < 2; i++)
                {
                    var b = _puzzle.banned[i];
                    sootRects[i].anchoredPosition = slotRects[b.slot].anchoredPosition + new Vector2(0f, -55f);
                    sootSpark[i].color = Colors[b.color];
                }
                hint.text = "그을음이 난 슬롯과 불꽃의 색을 팀원에게 알려 주세요. (그 색 퓨즈는 그 슬롯에 못 꽂아요)";
            }
            if (nv)
            {
                title.text = "배선도";
                var w = _puzzle.wired;
                for (int c = 0; c < 4; c++) lugImages[c].color = Colors[c];
                Vector2 a = lugRects[w.color].anchoredPosition, b2 = slotRects[w.slot].anchoredPosition + new Vector2(0f, -105f);
                Vector2 d = b2 - a;
                wire.anchoredPosition = (a + b2) * 0.5f;
                wire.sizeDelta = new Vector2(d.magnitude, 10f);
                wire.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                wireImage.color = Colors[w.color];
                hint.text = "이 색의 선만 어느 슬롯에 이어져 있는지 보입니다. 팀원에게 알려 주세요.";
            }
            for (int s = 0; s < 4; s++) slotNums[s].text = (s + 1).ToString();
            if (hint != null) hint.text += "     Q  닫기";
            SoloBots.Sync(BotLines.Fuse(_puzzle));
            if (op && !_solved) Story1Net.Tell(Story1Net.PFuse, "조작자: 퓨즈 " + CountFilled() + " / 4칸");
        }

        private void Update()
        {
            // 흔들림·번쩍임
            if (content != null)
            {
                _shake = Mathf.MoveTowards(_shake, 0f, Time.unscaledDeltaTime * 30f);
                content.anchoredPosition = _shake > 0.01f ? new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * _shake : Vector2.zero;
            }
            if (flash != null)
            {
                _flashA = Mathf.MoveTowards(_flashA, 0f, Time.unscaledDeltaTime * 2.2f);
                var c = _flashCol; c.a = _flashA; flash.color = c;
            }
            if (!IsOpen) return;
            Story1Net.ShowSay(Story1Net.PFuse, status, ref _sayVer);

            if (_closeAt > 0f && Time.unscaledTime >= _closeAt) { Close(); _box.OnSolved(); return; }
            if (_solved) return;
            if (Time.frameCount == _openedFrame) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) { Close(); return; }   // 웹에서는 Esc 가 전체화면을 풀어 버려서 Q 도 쓴다
            if (kb.tabKey.wasPressedThisFrame && Story1Role.Solo) { Story1Role.Cycle(); Sfx.Play("ui_hover", 0.8f); Refresh(); return; }

            if (Story1Role.Current != (int)GameRole.Operator) return;

            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) MoveCursor(-1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) MoveCursor(1);
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Place(0);
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Place(1);
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Place(2);
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Place(3);
            if (kb.backspaceKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame) { _slots[_cur] = -1; Sfx.Play("ui_back", 0.6f); Refresh(); }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Submit();

            var ms = Mouse.current;
            if (ms != null && ms.leftButton.wasPressedThisFrame)
            {
                Vector2 p = ms.position.ReadValue();
                for (int s = 0; s < 4; s++) if (Hit(slotRects[s], p)) { _cur = s; Sfx.Play("ui_hover", 0.7f); Refresh(); return; }
                for (int c = 0; c < 4; c++) if (Hit(trayRects[c], p)) { Place(c); return; }
            }
        }

        private int CountFilled() { int n = 0; foreach (int c in _slots) if (c >= 0) n++; return n; }

        private static bool Hit(RectTransform r, Vector2 screen) => RectTransformUtility.RectangleContainsScreenPoint(r, screen, null);

        private void MoveCursor(int d)
        {
            _cur = (_cur + d + 4) % 4;
            Sfx.Play("ui_hover", 0.7f);
            Refresh();
        }

        // 퓨즈 color 를 현재 칸에 꽂는다. 이미 다른 칸에 있으면 그 칸과 서로 바꾼다.
        private void Place(int color)
        {
            int from = Array.IndexOf(_slots, color);
            int old = _slots[_cur];
            if (from >= 0 && from != _cur) _slots[from] = old;
            _slots[_cur] = color;
            Sfx.Play("ui_select", 0.7f);
            if (_cur < 3 && !FusePuzzle.IsFull(_slots)) _cur++;
            Refresh();
        }

        private void Submit()
        {
            if (!FusePuzzle.IsFull(_slots))
            {
                status.text = "빈 칸이 있어요. 네 칸을 모두 채워 주세요.";
                status.color = new Color(0.95f, 0.8f, 0.45f);
                Sfx.Play("ui_back", 0.7f);
                return;
            }
            if (_puzzle.Check(_slots))
            {
                _solved = true;
                status.text = Story1Net.RoundOf(Story1Net.PFuse) + 1 < Story1Net.NeedRounds(Story1Net.PFuse) ? "성공! 한 번 더 — 새 퓨즈 문제가 나와요" : "전원 투입 성공!";
                status.color = new Color(0.45f, 0.95f, 0.6f);
                _flashCol = new Color(0.5f, 1f, 0.6f); _flashA = 0.55f;
                Sfx.Play("power_on", 1f);
                _closeAt = Time.unscaledTime + 1.4f;
                Story1Net.Send(Story1Net.Clear, Story1Net.PFuse);
                Refresh();
            }
            else
            {
                _fails++;
                status.text = "퓨즈가 터졌다! 단서를 다시 맞춰 보자.";
                status.color = new Color(1f, 0.5f, 0.45f);
                int hint = Story1Rules.HintAfter(Story1Net.Difficulty);
                if (_fails >= hint) { int hs = (_fails - hint) % 4; status.text = "힌트: " + (hs + 1) + "번 슬롯은 " + FusePuzzle.ColorKo[_puzzle.answer[hs]] + " 퓨즈"; status.color = new Color(0.95f, 0.85f, 0.45f); }
                _flashCol = new Color(1f, 0.35f, 0.3f); _flashA = 0.6f; _shake = 14f;
                Sfx.Play("fuse_pop", 1f);
                for (int i = 0; i < 4; i++) _slots[i] = -1;
                _cur = 0;
                Refresh();
                Story1Net.Tell(Story1Net.PFuse, "조작자: 퓨즈가 터졌다! 다시 꽂는 중");
            }
        }
    }
}
