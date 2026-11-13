using Paradox.Comms;
using Paradox.Language;
using Paradox.Roles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paradox.Stage
{
    /// <summary>
    /// 기호가 붙은 레버. 조작자만 보이고(RoleViewManager) 조작자만 E로 누른다.
    /// 누르면 그 기호가 StageManager의 입력 순서에 추가된다.
    /// 조작자는 보드에 전달된 기호와 '같은 모양'의 레버를 순서대로 누르면 된다(매칭).
    /// </summary>
    public class SymbolLever : MonoBehaviour
    {
        [SerializeField] private string symbol = "●";
        [SerializeField] private float interactRadius = 1.5f;

        public string Symbol => symbol;

        private Transform _player;
        private StageManager _stage;
        private TextMesh _marker;
        private SpriteRenderer _disc;
        private float _flash;
        private static Sprite _sprite;

        private void Awake()
        {
            var p = Object.FindFirstObjectByType<Player>();
            if (p != null) _player = p.transform;
            BuildVisual();
        }

        private void Start()
        {
            _stage = Object.FindFirstObjectByType<StageManager>();
        }

        private void BuildVisual()
        {
            var discGo = new GameObject("Disc");
            discGo.transform.SetParent(transform, false);
            discGo.transform.localScale = Vector3.one * 0.9f;
            _disc = discGo.AddComponent<SpriteRenderer>();
            _disc.sprite = GetSprite();
            _disc.color = new Color(0.30f, 0.34f, 0.40f);
            _disc.sortingOrder = 5;

            var markGo = new GameObject("Marker");
            markGo.transform.SetParent(transform, false);
            _marker = markGo.AddComponent<TextMesh>();
            var font = UIFontHelper.Load();
            _marker.font = font;
            _marker.characterSize = 0.12f;
            _marker.fontSize = 64;
            _marker.anchor = TextAnchor.MiddleCenter;
            _marker.alignment = TextAlignment.Center;
            _marker.color = Color.white;
            _marker.text = symbol;
            var mr = markGo.GetComponent<MeshRenderer>();
            mr.material = font.material;
            mr.sortingOrder = 6;
        }

        private void Update()
        {
            if (_player == null) return;

            if (_flash > 0f)
            {
                _flash -= Time.deltaTime;
                _disc.color = Color.Lerp(new Color(0.30f, 0.34f, 0.40f), new Color(0.5f, 0.9f, 0.6f), _flash / 0.3f);
            }

            float dist = Vector2.Distance(_player.position, transform.position);
            bool near = dist <= interactRadius;
            bool isOperator = RoleService.LocalRole == GameRole.Operator;

            _marker.text = (near && isOperator) ? symbol + "  [E]" : symbol;

            if (near && isOperator)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.eKey.wasPressedThisFrame)
                {
                    _flash = 0.3f;
                    MessageFeed.Send("조작자", $"{symbol} 레버 누름", new Color(0.95f, 0.85f, 0.4f));
                    if (_stage != null) _stage.OnLeverPulled(symbol);
                }
            }
        }

        private static Sprite GetSprite()
        {
            if (_sprite != null) return _sprite;
            int size = 56; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float c = size * 0.5f, r = size * 0.47f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                    Color col = Color.white; col.a = d > 1f ? 0f : Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.86f, 1f, d));
                    px[y * size + x] = col;
                }
            tex.SetPixels(px); tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 56f);
            return _sprite;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
