using Paradox.Core;
using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 아바타의 겉모습: 역할 전용 픽셀 캐릭터(4방향 걷기 시트)가 있으면 그것을, 없으면 선택한 종이인형 캐릭터 프리팹을 쓴다.
    /// 여기에 이름표 + 역할 배지 + 역할 색 발판 + 신호 말풍선이 붙는다. 네트워크와 무관하게 이동으로 걸음 애니메이션을 낸다.
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
        private SpriteRenderer _ring;
        private SpriteRenderer _badge;
        private SpriteRenderer _bubbleBg;
        private float _bubbleUntil;
        private int _facing = 1;
        private bool _moving;
        private int _character = -1;
        private Vector3 _lastPos;
        private float _lastMoveTime = -10f;
        private bool _visible = true;

        public bool Moving => _moving;
        public bool IsPixel => _pixel != null;

        public void Build(int slot, int character, string playerName)
        {
            var app = GameApp.Instance;
            bool pixel = app != null && app.HasRoleArt(slot);
            int wantCharacter = pixel ? -2 - slot : character;

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

            var sorter = GetComponent<YDepthSort>() ?? gameObject.AddComponent<YDepthSort>();
            sorter.Configure(100f, -0.45f);

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
                bgGo.transform.localPosition = new Vector3(0f, 1.95f, 0f);
                bgGo.transform.localScale = new Vector3(1.5f, 0.6f, 1f);
                _bubbleBg = bgGo.AddComponent<SpriteRenderer>();
                _bubbleBg.sprite = SpriteLib.White;
                _bubbleBg.color = new Color(0.05f, 0.06f, 0.09f, 0.9f);
                _bubbleBg.sortingOrder = 44;
                _bubbleBg.enabled = false;
            }
            if (_bubble == null)
            {
                _bubble = WorldText.Make(transform, "", new Vector3(0f, 1.95f, 0f), 0.075f, Color.white, 45, TextAnchor.MiddleCenter, 56);
                _bubble.gameObject.SetActive(false);
            }
            _lastPos = transform.position;
            SetVisible(_visible);
        }

        public void SetLabel(int slot, string playerName)
        {
            if (_label == null) return;
            string hex = Roles.Hex(slot);
            bool mine = Session.LocalSlot == slot;
            string tag = mine ? "★ " : "";
            _label.text = $"<color=#{hex}><b>{tag}{Roles.Name(slot)}</b></color>\n<size=44>{playerName}</size>";
            if (_badge != null)
            {
                var rc = Roles.Color(slot);
                _badge.color = mine ? new Color(rc.r * 0.75f, rc.g * 0.75f, rc.b * 0.75f, 0.95f) : new Color(rc.r * 0.45f, rc.g * 0.45f, rc.b * 0.45f, 0.9f);
                float w = 0.9f + Roles.Name(slot).Length * 0.24f + (mine ? 0.3f : 0f);
                _badge.transform.localScale = new Vector3(w, 0.34f, 1f);
            }
        }

        public void SetVisible(bool v)
        {
            _visible = v;
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i).gameObject;
                if (c.name == "BubbleBg" || c.name == "Text" && _bubble != null && c == _bubble.gameObject) continue;
                c.SetActive(v);
            }
            if (!v)
            {
                if (_bubbleBg != null) _bubbleBg.enabled = false;
                if (_bubble != null) _bubble.gameObject.SetActive(false);
            }
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

        public void ShowBubble(string text, Color color, float duration = 2.4f)
        {
            if (_bubble == null || !_visible) return;
            _bubble.text = text;
            _bubble.color = color;
            _bubble.gameObject.SetActive(true);
            if (_bubbleBg != null)
            {
                _bubbleBg.enabled = true;
                float w = Mathf.Clamp(0.5f + text.Length * 0.32f, 1.0f, 4.5f);
                _bubbleBg.transform.localScale = new Vector3(w, 0.62f, 1f);
            }
            _bubbleUntil = Time.time + duration;
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

            if (_bubble != null && _bubble.gameObject.activeSelf && Time.time > _bubbleUntil)
            {
                _bubble.gameObject.SetActive(false);
                if (_bubbleBg != null) _bubbleBg.enabled = false;
            }
        }
    }
}
