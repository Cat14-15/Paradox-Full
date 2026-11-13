using Paradox.Language;
using UnityEngine;

namespace Paradox.Stage
{
    /// <summary>
    /// 협동 도착존. 지정 구역에 플레이어가 서 있으면 점유(occupied) 상태가 된다.
    /// 스테이지 목표는 필요한 도착존이 모두 동시에 점유되는 것(→ 문 열림/클리어).
    /// (지금은 로컬 Player만 감지. 네트워킹 시 각 NetPlayer도 감지하도록 확장)
    /// </summary>
    public class ArrivalZone : MonoBehaviour
    {
        [SerializeField] private float radius = 1.1f;

        public bool IsOccupied { get; private set; }

        private Player[] _players;
        private SpriteRenderer _disc;
        private TextMesh _label;

        private void Awake()
        {
            BuildVisual();
        }

        private void Start()
        {
            _players = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        }

        private void BuildVisual()
        {
            var go = new GameObject("Disc");
            go.transform.SetParent(transform, false);
            _disc = go.AddComponent<SpriteRenderer>();
            _disc.sprite = MakeDiscSprite();
            _disc.sortingOrder = -500; // 바닥 위, 캐릭터 아래
            go.transform.localScale = Vector3.one * (radius * 2f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            _label = labelGo.AddComponent<TextMesh>();
            var font = UIFontHelper.Load();
            _label.font = font;
            _label.characterSize = 0.08f;
            _label.fontSize = 48;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            labelGo.GetComponent<MeshRenderer>().material = font.material;
            labelGo.GetComponent<MeshRenderer>().sortingOrder = -400;
        }

        private void Update()
        {
            bool occupied = false;
            if (_players != null)
            {
                foreach (var p in _players)
                {
                    if (p == null) continue;
                    if (Vector2.Distance(p.transform.position, transform.position) <= radius)
                    {
                        occupied = true;
                        break;
                    }
                }
            }
            IsOccupied = occupied;

            if (_disc != null)
                _disc.color = occupied ? new Color(0.4f, 0.9f, 0.5f, 0.55f) : new Color(0.9f, 0.9f, 0.5f, 0.30f);
            if (_label != null)
                _label.text = occupied ? "도착!" : "도착존";
        }

        // 배경 픽셀과 어울리도록 저해상도 + Point 필터(계단식 픽셀 링).
        private static Sprite MakeDiscSprite()
        {
            int size = 22;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            float c = (size - 1) * 0.5f;
            float r = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                    Color col = Color.white;
                    if (d > 1f) col.a = 0f;          // 바깥
                    else if (d > 0.74f) col.a = 1f;  // 픽셀 테두리 링
                    else col.a = 0.30f;              // 안쪽 옅은 칠
                    px[y * size + x] = col;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
