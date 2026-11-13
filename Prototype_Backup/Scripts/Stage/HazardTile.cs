using Paradox.Comms;
using Paradox.Language;
using Paradox.Roles;
using UnityEngine;

namespace Paradox.Stage
{
    /// <summary>
    /// 관측자 전용 함정. 관측자에게만 빨간 원으로 보인다. 다른 역할에겐 안 보이지만
    /// 지나가려 하면 막힌다("보이지 않는 벽"). 관측자가 위치를 알려줘야 동료가 피해서 간다.
    /// </summary>
    public class HazardTile : MonoBehaviour
    {
        [SerializeField] private float radius = 1.2f;

        private Player[] _players;
        private SpriteRenderer _disc;
        private TextMesh _label;
        private float _warnCooldown;
        private static Sprite _sprite;

        private void Start()
        {
            _players = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
            BuildVisual();
            RoleService.Changed += UpdateVisibility;
            UpdateVisibility();
        }

        private void OnDestroy() => RoleService.Changed -= UpdateVisibility;

        private void UpdateVisibility()
        {
            bool observer = RoleService.LocalRole == GameRole.Observer;
            if (_disc != null) _disc.enabled = observer;
            if (_label != null) _label.gameObject.SetActive(observer);
        }

        private void BuildVisual()
        {
            var discGo = new GameObject("HazardDisc");
            discGo.transform.SetParent(transform, false);
            discGo.transform.localScale = Vector3.one * (radius * 2f);
            _disc = discGo.AddComponent<SpriteRenderer>();
            _disc.sprite = GetSprite();
            _disc.color = new Color(0.95f, 0.25f, 0.25f, 0.5f);
            _disc.sortingOrder = -450;

            var labelGo = new GameObject("HazardLabel");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            _label = labelGo.AddComponent<TextMesh>();
            var font = UIFontHelper.Load();
            _label.font = font; _label.characterSize = 0.07f; _label.fontSize = 56;
            _label.anchor = TextAnchor.MiddleCenter; _label.alignment = TextAlignment.Center;
            _label.color = new Color(1f, 0.7f, 0.7f); _label.text = "위험";
            labelGo.GetComponent<MeshRenderer>().material = font.material;
            labelGo.GetComponent<MeshRenderer>().sortingOrder = -400;
        }

        private void Update()
        {
            if (_players == null) return;
            _warnCooldown -= Time.deltaTime;
            bool observer = RoleService.LocalRole == GameRole.Observer;

            foreach (var p in _players)
            {
                if (p == null) continue;
                Vector3 pos = p.transform.position;
                Vector2 delta = new Vector2(pos.x - transform.position.x, pos.y - transform.position.y);
                if (delta.sqrMagnitude < radius * radius)
                {
                    // 반경 밖으로 밀어낸다(보이지 않는 벽)
                    Vector2 dir = delta.sqrMagnitude < 0.0001f ? Vector2.up : delta.normalized;
                    pos.x = transform.position.x + dir.x * radius;
                    pos.y = transform.position.y + dir.y * radius;
                    p.transform.position = pos;

                    if (!observer && _warnCooldown <= 0f)
                    {
                        MessageFeed.Send("!", "보이지 않는 무언가에 막혔다 — 관측자에게 물어보세요", new Color(0.95f, 0.6f, 0.6f));
                        _warnCooldown = 1.5f;
                    }
                }
            }
        }

        // 배경 픽셀과 어울리도록 저해상도 + Point 필터(계단식 픽셀 링).
        private static Sprite GetSprite()
        {
            if (_sprite != null) return _sprite;
            int size = 22; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            float c = (size - 1) * 0.5f, r = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                    Color col = Color.white; col.a = d > 1f ? 0f : (d > 0.74f ? 1f : 0.35f);
                    px[y * size + x] = col;
                }
            tex.SetPixels(px); tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _sprite;
        }
    }
}
