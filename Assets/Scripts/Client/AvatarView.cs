using Paradox.Core;
using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 아바타의 겉모습: 역할 전용 픽셀 캐릭터(4방향 걷기 시트)가 있으면 그것을, 없으면 종이인형 프리팹/원을 쓴다.
    /// 여기에 이름표("나 · 통역" / "조작자" + 플레이어 이름) + 역할 색 발판 + 말풍선(글) + 카드 말풍선(휠 신호)이 붙는다.
    /// 네트워크와 무관하게 이동으로 걸음 애니메이션을 낸다.
    /// </summary>
    public class AvatarView : MonoBehaviour
    {
        private Transform _visual;
        private CharacterWalkAnimator _anim;
        private SpriteRenderer _pixel;      // 픽셀 캐릭터 모드
        private int _pixelSlot = -1;
        private int _dir = PixelSprites.DirDown;
        private float _animT;
        private TextMesh _label;
        private TextMesh _bubble;
        private TextMesh _bubbleSub;
        private SpriteRenderer _ring;
        private SpriteRenderer _badge;
        private SpriteRenderer _bubbleBg;
        private float _bubbleUntil;

        // 카드 말풍선(휠 신호): 색 카드 + 기호 + 단어
        private SpriteRenderer _cardBg;
        private SpriteRenderer _cardEdge;
        private TextMesh _cardSymbol;
        private SpriteRenderer _cardIcon;   // 위험·you 는 글리프 대신 픽셀 아이콘(D-A7)
        private TextMesh _cardWord;
        private float _cardUntil;
        private const float CardIconScale = 0.62f;

        private int _facing = 1;
        private bool _moving;
        private int _character = -1;
        private float _lift;                // 말풍선 높이 오프셋(홀수 슬롯은 조금 위 — 옆 사람 말풍선과 겹치지 않게, D10)
        private Vector3 _lastPos;
        private float _lastMoveTime = -10f;
        private bool _visible = true;

        public bool Moving => _moving;
        public bool IsPixel => _pixel != null;

        public const float CardDuration = 2.6f;

        public void Build(int slot, int character, string playerName)
        {
            var app = GameApp.Instance;
            bool pixel = app != null && app.HasRoleArt(slot);
            int wantCharacter = pixel ? -2 - slot : character;
            _lift = (slot % 2) == 1 ? 0.7f : 0f;

            if (_visual != null && _character == wantCharacter)
            {
                SetLabel(slot, playerName);
                return;
            }
            if (_visual != null) Destroy(_visual.gameObject);
            _visual = null; _pixel = null; _anim = null;
            _character = wantCharacter;

            if (pixel)
            {
                var vis = new GameObject("Visual");
                vis.transform.SetParent(transform, false);
                _pixel = vis.AddComponent<SpriteRenderer>();
                _pixel.sortingOrder = 1;
                _pixelSlot = slot;
                _pixel.sprite = PixelSprites.Get(slot, _dir, PixelSprites.IdleFrame);
                _visual = vis.transform;
                var oldAnim = GetComponent<CharacterWalkAnimator>();
                if (oldAnim != null) Destroy(oldAnim);
            }
            else
            {
                var prefab = app != null ? app.CharacterPrefab(character) : null;
                GameObject vis;
                if (prefab != null)
                {
                    vis = Instantiate(prefab, transform);
                    vis.name = "Visual";
                    vis.transform.localPosition = Vector3.zero;
                    vis.transform.localScale = Vector3.one * 0.95f;
                }
                else
                {
                    vis = new GameObject("Visual");
                    vis.transform.SetParent(transform, false);
                    var sr = vis.AddComponent<SpriteRenderer>();
                    sr.sprite = SpriteLib.Disc();
                    sr.color = Roles.Color(slot);
                    sr.sortingOrder = 1;
                }
                _visual = vis.transform;
                _anim = GetComponent<CharacterWalkAnimator>() ?? gameObject.AddComponent<CharacterWalkAnimator>();
                _anim.Rebind();
                _anim.MovingProvider = () => _moving;
            }

            // 박물관 맵(y ±9)에서는 배율 100이 포화되므로 50을 쓴다(StageWorld의 정적 프롭과 동일)
            var sorter = GetComponent<YDepthSort>() ?? gameObject.AddComponent<YDepthSort>();
            sorter.Configure(50f, -0.45f);

            if (_ring == null)
            {
                var ringGo = new GameObject("Ring");
                ringGo.transform.SetParent(transform, false);
                ringGo.transform.localPosition = new Vector3(0f, -0.52f, 0f);
                ringGo.transform.localScale = new Vector3(1.1f, 0.5f, 1f);
                _ring = ringGo.AddComponent<SpriteRenderer>();
                _ring.sprite = SpriteLib.Ring(0.2f);
                _ring.sortingOrder = -6;
            }
            var rc = Roles.Color(slot);
            _ring.color = new Color(rc.r, rc.g, rc.b, 0.8f);

            if (_badge == null)
            {
                var badgeGo = new GameObject("Badge");
                badgeGo.transform.SetParent(transform, false);
                badgeGo.transform.localPosition = new Vector3(0f, 1.42f, 0f);
                badgeGo.transform.localScale = new Vector3(1.3f, 0.34f, 1f);
                _badge = badgeGo.AddComponent<SpriteRenderer>();
                _badge.sprite = SpriteLib.White;
                _badge.sortingOrder = 39;
            }
            _badge.color = new Color(rc.r * 0.55f, rc.g * 0.55f, rc.b * 0.55f, 0.92f);
            if (_label == null)
                _label = WorldText.Make(transform, "", new Vector3(0f, 1.27f, 0f), 0.06f, Color.white, 40, TextAnchor.LowerCenter, 56);
            SetLabel(slot, playerName);

            if (_bubbleBg == null)
            {
                var bgGo = new GameObject("BubbleBg");
                bgGo.transform.SetParent(transform, false);
                bgGo.transform.localPosition = new Vector3(0f, 1.98f, 0f);
                bgGo.transform.localScale = new Vector3(1.5f, 0.6f, 1f);
                _bubbleBg = bgGo.AddComponent<SpriteRenderer>();
                _bubbleBg.sprite = SpriteLib.White;
                _bubbleBg.color = new Color(0.05f, 0.06f, 0.09f, 0.9f);
                _bubbleBg.sortingOrder = 44;
                _bubbleBg.enabled = false;
            }
            if (_bubble == null)
            {
                _bubble = WorldText.Make(transform, "", new Vector3(0f, 2.02f, 0f), 0.062f, Color.white, 45, TextAnchor.MiddleCenter, 56);
                _bubble.gameObject.name = "Bubble";
                _bubble.gameObject.SetActive(false);
            }
            if (_bubbleSub == null)
            {
                _bubbleSub = WorldText.Make(transform, "", new Vector3(0f, 1.78f, 0f), 0.045f, new Color(0.7f, 0.6f, 0.6f), 45, TextAnchor.UpperCenter, 44);
                _bubbleSub.gameObject.name = "BubbleSub";
                _bubbleSub.gameObject.SetActive(false);
            }
            BuildCard();
            _lastPos = transform.position;
            SetVisible(_visible);
        }

        private void BuildCard()
        {
            if (_cardBg != null) return;
            var edgeGo = new GameObject("CardEdge");
            edgeGo.transform.SetParent(transform, false);
            edgeGo.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            edgeGo.transform.localScale = new Vector3(1.0f, 1.18f, 1f);
            _cardEdge = edgeGo.AddComponent<SpriteRenderer>();
            _cardEdge.sprite = SpriteLib.White;
            _cardEdge.color = new Color(0.05f, 0.06f, 0.09f, 0.95f);
            _cardEdge.sortingOrder = 43;
            _cardEdge.enabled = false;

            var bgGo = new GameObject("CardBg");
            bgGo.transform.SetParent(transform, false);
            bgGo.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            bgGo.transform.localScale = new Vector3(0.9f, 1.08f, 1f);
            _cardBg = bgGo.AddComponent<SpriteRenderer>();
            _cardBg.sprite = SpriteLib.White;
            _cardBg.sortingOrder = 44;
            _cardBg.enabled = false;

            _cardSymbol = WorldText.Make(transform, "", new Vector3(0f, 2.28f, 0f), 0.16f, Color.black, 46, TextAnchor.MiddleCenter, 64);
            _cardSymbol.gameObject.name = "CardSymbol";
            _cardSymbol.gameObject.SetActive(false);
            var icGo = new GameObject("CardIcon");
            icGo.transform.SetParent(transform, false);
            icGo.transform.localPosition = new Vector3(0f, 2.28f, 0f);
            icGo.transform.localScale = Vector3.one * CardIconScale;
            _cardIcon = icGo.AddComponent<SpriteRenderer>();
            _cardIcon.sortingOrder = 46;
            _cardIcon.enabled = false;
            _cardWord = WorldText.Make(transform, "", new Vector3(0f, 1.78f, 0f), 0.055f, Color.black, 46, TextAnchor.MiddleCenter, 48);
            _cardWord.gameObject.name = "CardWord";
            _cardWord.gameObject.SetActive(false);
        }

        public void SetLabel(int slot, string playerName)
        {
            if (_label == null) return;
            string hex = Roles.Hex(slot);
            bool mine = Session.LocalSlot == slot;
            string head = mine ? "나 · " + Roles.Name(slot) : Roles.Name(slot);
            _label.text = $"<color=#{hex}><b>{head}</b></color>\n<size=44>{playerName}</size>";
            if (_badge != null)
            {
                var rc = Roles.Color(slot);
                _badge.color = mine ? new Color(rc.r * 0.75f, rc.g * 0.75f, rc.b * 0.75f, 0.95f) : new Color(rc.r * 0.45f, rc.g * 0.45f, rc.b * 0.45f, 0.9f);
                float w = 0.7f + head.Length * 0.2f;   // D10: 이름 길이에 맞춘 좁은 배지
                _badge.transform.localScale = new Vector3(w, 0.34f, 1f);
            }
        }

        public void SetVisible(bool v)
        {
            _visible = v;
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i).gameObject;
                if (IsBubblePart(c)) continue;
                c.SetActive(v);
            }
            if (!v) { HideBubble(); HideCard(); }
        }

        private bool IsBubblePart(GameObject c)
        {
            string n = c.name;
            return n == "BubbleBg" || n == "Bubble" || n == "BubbleSub" || n == "CardBg" || n == "CardEdge" || n == "CardSymbol" || n == "CardIcon" || n == "CardWord";
        }

        /// <summary>소유자가 입력으로 움직일 때 호출. dx/dy는 이번 프레임 변위.</summary>
        public void SetMoving(bool moving, float dx, float dy = 0f)
        {
            _moving = moving;
            if (Mathf.Abs(dx) > 0.001f) _facing = dx < 0f ? -1 : 1;
            if (moving) _dir = PixelSprites.DirectionFor(dx, dy, _dir);
            ApplyFacing();
        }

        /// <summary>원격 아바타: 위치 변화로 걸음/방향 판단.</summary>
        public void UpdateRemote()
        {
            Vector3 cur = transform.position;
            Vector3 d = cur - _lastPos;
            _lastPos = cur;
            if (d.sqrMagnitude > 1e-6f)
            {
                _lastMoveTime = Time.time;
                if (Mathf.Abs(d.x) > 0.0005f) _facing = d.x < 0f ? -1 : 1;
                _dir = PixelSprites.DirectionFor(d.x, d.y, _dir);
            }
            _moving = Time.time - _lastMoveTime < 0.12f;
            ApplyFacing();
        }

        private void ApplyFacing()
        {
            if (_visual == null || _pixel != null) return; // 픽셀 캐릭터는 방향별 그림이 따로 있어 뒤집지 않는다
            var s = _visual.localScale;
            s.x = Mathf.Abs(s.x) * _facing;
            _visual.localScale = s;
        }

        // ---------------------------------------------------------------- 말풍선(글)

        /// <summary>글 말풍선. sub가 있으면 아래에 작은 글씨(예: "(외국어)")를 붙인다.</summary>
        public void ShowBubble(string text, Color color, float duration = 2.4f, string sub = null)
        {
            if (_bubble == null || !_visible) return;
            HideCard();
            _bubble.text = text;
            _bubble.color = color;
            _bubble.gameObject.SetActive(true);
            _bubble.transform.localPosition = new Vector3(0f, 2.02f + _lift, 0f);
            if (_bubbleBg != null)
            {
                _bubbleBg.enabled = true;
                float w = Mathf.Clamp(0.5f + text.Length * 0.27f, 1.0f, 3.8f);
                _bubbleBg.transform.localScale = new Vector3(w, string.IsNullOrEmpty(sub) ? 0.62f : 0.9f, 1f);
                _bubbleBg.transform.localPosition = new Vector3(0f, (string.IsNullOrEmpty(sub) ? 1.98f : 1.9f) + _lift, 0f);
            }
            if (_bubbleSub != null)
            {
                _bubbleSub.text = sub ?? "";
                _bubbleSub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
                _bubbleSub.transform.localPosition = new Vector3(0f, 1.78f + _lift, 0f);
            }
            _bubbleUntil = Time.time + duration;
        }

        private void HideBubble()
        {
            if (_bubble != null) _bubble.gameObject.SetActive(false);
            if (_bubbleSub != null) _bubbleSub.gameObject.SetActive(false);
            if (_bubbleBg != null) _bubbleBg.enabled = false;
        }

        /// <summary>말풍선·카드를 모두 지운다(호스트가 조종 슬롯을 바꿀 때 — 이전 시점 기준의 문구가 새 역할에 새지 않게).</summary>
        public void ClearSpeech()
        {
            HideBubble();
            HideCard();
        }

        // ---------------------------------------------------------------- 카드 말풍선(휠 신호)

        /// <summary>휠 카드 색. yes/no 흰, where 분홍, go 초록, you 노랑, danger 빨강.</summary>
        public static Color CardColor(string id)
        {
            switch (Norm(id))
            {
                case "WHERE": return new Color(0.95f, 0.66f, 0.78f);
                case "GO": return new Color(0.55f, 0.88f, 0.62f);
                case "YOU": return new Color(0.97f, 0.83f, 0.50f);
                case "DANGER": return new Color(0.95f, 0.45f, 0.42f);
                default: return new Color(0.96f, 0.95f, 0.92f);
            }
        }

        /// <summary>휠 카드 기호(폰트가 지원하는 기호만 쓴다).</summary>
        public static string CardSymbol(string id)
        {
            switch (Norm(id))
            {
                case "YES": return "○";
                case "NO": return "×";
                case "WHERE": return "?";
                case "GO": return "→";
                case "YOU": return "★";
                case "DANGER": return "!";
                default: return "?";
            }
        }

        /// <summary>휠 카드 단어(영문 소문자, 위험만 한국어). 표는 Comms.SignalWord 한 곳에만 둔다.</summary>
        public static string CardWord(string id) => Comms.SignalWord(Norm(id));

        private static string Norm(string id) => string.IsNullOrEmpty(id) ? "" : id.Trim().ToUpperInvariant();

        /// <summary>휠 신호 카드 말풍선: 색 카드 + 기호 + 단어. id ∈ YES/NO/WHERE/GO/YOU/DANGER.</summary>
        public void ShowCard(string id, float duration = CardDuration)
        {
            if (_cardBg == null || !_visible) return;
            HideBubble();
            var c = CardColor(id);
            _cardBg.color = c;
            _cardBg.enabled = true;
            if (_cardEdge != null) _cardEdge.enabled = true;
            var ink = new Color(0.08f, 0.09f, 0.12f);
            string iconKind = Comms.SignalIconKind(Norm(id));
            if (iconKind != null && _cardIcon != null)
            {
                _cardIcon.sprite = SpriteLib.Icon(iconKind);
                _cardIcon.color = Color.white;
                _cardIcon.enabled = true;
                _cardSymbol.gameObject.SetActive(false);
            }
            else
            {
                if (_cardIcon != null) _cardIcon.enabled = false;
                _cardSymbol.text = CardSymbol(id);
                _cardSymbol.color = ink;
                _cardSymbol.gameObject.SetActive(true);
            }
            _cardWord.text = CardWord(id);
            _cardWord.color = ink;
            _cardWord.gameObject.SetActive(true);
            _cardUntil = Time.time + duration;
        }

        private void HideCard()
        {
            if (_cardBg != null) _cardBg.enabled = false;
            if (_cardEdge != null) _cardEdge.enabled = false;
            if (_cardSymbol != null) _cardSymbol.gameObject.SetActive(false);
            if (_cardIcon != null) _cardIcon.enabled = false;
            if (_cardWord != null) _cardWord.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_pixel != null)
            {
                int frame;
                if (_moving)
                {
                    _animT += Time.deltaTime;
                    frame = PixelSprites.WalkCycle[(int)(_animT / PixelSprites.FrameTime) % PixelSprites.WalkCycle.Length];
                }
                else
                {
                    _animT = 0f;
                    frame = PixelSprites.IdleFrame;
                }
                var sp = PixelSprites.Get(_pixelSlot, _dir, frame);
                if (sp != null && _pixel.sprite != sp) _pixel.sprite = sp;
            }

            if (_bubble != null && _bubble.gameObject.activeSelf && Time.time > _bubbleUntil) HideBubble();

            if (_cardBg != null && _cardBg.enabled)
            {
                if (Time.time > _cardUntil) HideCard();
                else
                {
                    // 카드가 살짝 떠오르며 마지막 0.4초에 줄어든다
                    float left = _cardUntil - Time.time;
                    float k = left < 0.4f ? Mathf.Clamp01(left / 0.4f) : 1f;
                    float bob = 0.04f * Mathf.Sin(Time.time * 5f) + _lift;
                    _cardBg.transform.localScale = new Vector3(0.9f * k, 1.08f * k, 1f);
                    if (_cardEdge != null) _cardEdge.transform.localScale = new Vector3(1.0f * k, 1.18f * k, 1f);
                    _cardBg.transform.localPosition = new Vector3(0f, 2.15f + bob, 0f);
                    if (_cardEdge != null) _cardEdge.transform.localPosition = new Vector3(0f, 2.15f + bob, 0f);
                    _cardSymbol.transform.localPosition = new Vector3(0f, 2.28f + bob, 0f);
                    if (_cardIcon != null)
                    {
                        _cardIcon.transform.localPosition = new Vector3(0f, 2.28f + bob, 0f);
                        _cardIcon.transform.localScale = Vector3.one * (CardIconScale * k);
                    }
                    _cardWord.transform.localPosition = new Vector3(0f, 1.78f + bob, 0f);
                }
            }
        }
    }
}
