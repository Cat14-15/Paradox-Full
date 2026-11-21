using System.Collections.Generic;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 혼자 테스트할 때 주인공을 따라다니는 AI 동료(봇이 맡은 역할만). 월드에 보이는 캐릭터일 뿐이고,
    /// 퍼즐 안에서 하는 말은 SoloBots 가 맡는다. 주인공이 지나온 길을 조금 늦게 밟아 따라오며, 문을 지나면 뒤따라 나타난다.
    /// 온라인(진짜 동료가 있을 때)에는 숨는다.
    /// </summary>
    public sealed class Story1Companions : MonoBehaviour
    {
        public Story1Scene scene;
        [Tooltip("역할 번호 순서(조작자·통역·내비게이터·관측자)의 역할 시트(160x256)")]
        public Texture2D[] sheets = new Texture2D[4];
        public float[] delays = { 1.65f, 0.55f, 1.1f, 1.65f };
        public Vector2[] offsets = { new Vector2(0f, -1.1f), new Vector2(-0.7f, -0.15f), new Vector2(0.7f, -0.15f), new Vector2(0f, -0.65f) };

        /// <summary>역할별 걷기 스프라이트(방향×5프레임). 주인공도 자기 역할의 모습으로 그려 모두 똑같아 보이지 않게 한다.</summary>
        public static Sprite[][] RoleFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { RoleFrames = null; }

        // 서로 1유닛 이상 떨어지도록 주인공 둘레에 넓게 펼친다
        private static readonly Vector2[] SpreadOffsets = { new Vector2(-1.0f, -1.2f), new Vector2(-1.3f, 0.05f), new Vector2(1.3f, 0.05f), new Vector2(1.0f, -1.2f) };
        private const float MinGap = 1.15f;   // 캐릭터가 커졌으니 간격도 넓게

        private struct Sample { public float t; public Vector2 p; }

        private readonly List<Sample> _hist = new List<Sample>();
        private SpriteRenderer[] _sr;
        private Transform[] _tr;
        private YDepthSort[] _depth;
        private Vector2[] _last;
        private Vector2[] _off;
        private Vector2[] _vel;
        private int[] _dir;
        private float[] _anim;
        private Sprite[][] _sprites;

        private void Start()
        {
            offsets = SpreadOffsets;   // 씬에 저장된 옛 값(서로 겹침) 대신 항상 이 값을 쓴다
            int n = sheets.Length;
            _sr = new SpriteRenderer[n]; _tr = new Transform[n]; _depth = new YDepthSort[n];
            _last = new Vector2[n]; _off = new Vector2[n]; _vel = new Vector2[n]; _dir = new int[n]; _anim = new float[n]; _sprites = new Sprite[n][];
            RoleFrames = _sprites;
            for (int i = 0; i < n; i++)
            {
                var g = new GameObject("Companion_" + Roles.Name(i));
                g.transform.SetParent(transform, false);
                g.transform.localScale = scene.player.transform.lossyScale;
                _tr[i] = g.transform;
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sortingOrder = scene.playerRenderer.sortingOrder;
                _sr[i] = sr;
                var d = g.AddComponent<YDepthSort>(); d.Configure(50f, -0.45f); _depth[i] = d;
                _sprites[i] = Slice(sheets[i]);
                sr.sprite = _sprites[i][PixelSprites.IdleFrame];
                _last[i] = scene.player.position;
                g.SetActive(false);
            }
        }

        public static Sprite[] Slice(Texture2D tex)
        {
            var r = new Sprite[PixelSprites.Rows * PixelSprites.Cols];
            if (tex == null) return r;
            for (int dir = 0; dir < PixelSprites.Rows; dir++)
                for (int f = 0; f < PixelSprites.Cols; f++)
                {
                    var rect = new Rect(f * PixelSprites.CellW, (PixelSprites.Rows - 1 - dir) * PixelSprites.CellH, PixelSprites.CellW, PixelSprites.CellH);
                    r[dir * PixelSprites.Cols + f] = Sprite.Create(tex, rect, PixelSprites.Pivot, PixelSprites.PPU, 0, SpriteMeshType.FullRect);
                }
            return r;
        }

        // ---- 봇이 자기 역할로 하는 일: 기록 창에 길·단서를 알려 준다(조작자가 사람일 때나 봇일 때나 같다)
        private float _nextAdvice = 6f;
        private int _adviceStep;

        private void Advise()
        {
            if (Story1Scene.InputLocked || scene.Busy || Time.time < _nextAdvice) return;
            _nextAdvice = Time.time + 11f;
            bool g = GalleryPanel.Cleared, r = RelicPanel.Cleared, w = WaterPanel.Cleared;
            int na = (int)GameRole.Navigator, it = (int)GameRole.Interpreter, ob = (int)GameRole.Observer;
            string where, what;
            if (!Story1Blackout.PowerOn) { where = "입구 안내실 퓨즈함"; what = "퓨즈함 앞에서 E"; }
            else if (g && r && w) { where = "중앙 홀 바닥의 원"; what = "네 명이 같이 밟아요"; }
            else if (!g) { where = "회화 전시실 물웅덩이"; what = "물웅덩이 건너기"; }
            else if (!r) { where = "수장고 유물 전시대"; what = "유물 맞추기"; }
            else { where = "학예실 배수 밸브"; what = "배수 밸브 열기"; }
            int step = _adviceStep++ % 3;
            if (step == 0 && Story1Role.IsBot(na)) { Story1Net.BotSay(na, "가야 할 곳  " + where); return; }
            if (step == 1 && Story1Role.IsBot(it)) { Story1Net.BotSay(it, "외국어 글은 내가 읽어 줄게요"); return; }
            if (step == 2 && Story1Role.IsBot(ob)) { Story1Net.BotSay(ob, "숨은 단서를 찾는 중이에요"); return; }
            if (Story1Role.IsBot(na)) Story1Net.BotSay(na, "가야 할 곳  " + where);
        }

        /// <summary>역할 role 의 동료가 지금 화면에 있다면 그 월드 위치.</summary>
        public bool TryGetPos(int role, out Vector2 p)
        {
            p = default;
            if (_sr == null || role < 0 || role >= _sr.Length || !_sr[role].gameObject.activeSelf) return false;
            p = _tr[role].position;
            return true;
        }

        private void LateUpdate()
        {
            if (_sr == null) return;
                        float now = Time.time;
            Vector2 pp = scene.player.position;
            if (_hist.Count == 0 || (_hist[_hist.Count - 1].p - pp).sqrMagnitude > 1e-6f || now - _hist[_hist.Count - 1].t > 0.25f)
                _hist.Add(new Sample { t = now, p = pp });
            while (_hist.Count > 2 && _hist[1].t < now - 3f) _hist.RemoveAt(0);

            float refY = scene.CurrentRoom != null ? scene.CurrentRoom.transform.position.y : 0f;
            for (int i = 0; i < _sr.Length; i++)
            {
                bool remote = Story1Net.IsRemoteHuman(i) && Story1Net.RemoteFresh(i);
                bool show = Story1Role.IsBot(i) || remote;
                if (_sr[i].gameObject.activeSelf != show) _sr[i].gameObject.SetActive(show);
                if (!show) continue;
                // 사람 동료: 받은 위치들을 조금 늦게(0.2초) 이어서 보여 줘 끊김 없이 부드럽게. 봇: 주인공이 지나온 길을 따라간다.
                Vector2 p;
                if (remote) p = Story1Net.RemoteAt(i, now - 0.35f);
                else
                {
                    Vector2 basePos = At(now - delays[i]);          // 주인공이 지나온 길(항상 걸을 수 있는 곳)
                    p = basePos + offsets[i];
                    if (Blocked(basePos, p)) { p = basePos + offsets[i] * 0.5f; if (Blocked(basePos, p)) p = basePos; }   // 벽 너머로 나가지 않는다
                }
                // 밀어내기(겹침 해소)는 하지 않는다: 주인공이 다가가면 동료가 튕겨 나가던 원인. 정해진 간격(offsets)으로만 따라온다.
                Vector2 d = p - _last[i];
                if (d.sqrMagnitude > 25f) { _last[i] = p; d = Vector2.zero; }   // 문을 지난 순간이동은 걸어가지 않고 바로
                // 속도가 아주 느릴 땐(밀림·보정으로 생기는 미세한 움직임) 걷는 모션/방향 전환을 하지 않는다 → 앞뒤로 흔들려 보이지 않게
                float speed = d.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                bool moving = speed > 0.6f && d.sqrMagnitude < 4f;
                if (moving)
                {
                    // 이동 방향을 조금 부드럽게 모아서(저역 통과) 방향이 순간적으로 뒤집히는 걸 막는다
                    _vel[i] = Vector2.Lerp(_vel[i], d / Mathf.Max(Time.deltaTime, 0.0001f), 1f - Mathf.Exp(-10f * Time.deltaTime));
                    if (_vel[i].magnitude > 0.5f) _dir[i] = PixelSprites.DirectionFor(_vel[i].x, _vel[i].y, _dir[i]);
                    _anim[i] += Time.deltaTime;
                }
                else { _anim[i] = 0f; _vel[i] = Vector2.zero; }
                _last[i] = p;
                _tr[i].position = new Vector3(p.x, p.y, 0f);
                _depth[i].SetReferenceY(refY);
                int frame = moving ? PixelSprites.WalkCycle[(int)(_anim[i] / PixelSprites.FrameTime) % PixelSprites.WalkCycle.Length] : PixelSprites.IdleFrame;
                var sp = _sprites[i][_dir[i] * PixelSprites.Cols + frame];
                if (sp != null) _sr[i].sprite = sp;
            }
        }

        // 겹쳐 보이지 않게: 주인공과 앞 순서 동료들에게서 MinGap 보다 가까우면 그만큼 밀어낸다(그리기 위치만)
        private Vector2 Separate(int i, Vector2 p, Vector2 player)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                p = Push(p, player, i);
                for (int j = 0; j < i; j++) if (_sr[j].gameObject.activeSelf) p = Push(p, (Vector2)_tr[j].position, i);
            }
            return p;
        }

        private static Vector2 Push(Vector2 p, Vector2 other, int i)
        {
            Vector2 v = p - other;
            float m = v.magnitude;
            if (m >= MinGap) return p;
            Vector2 dir = m > 0.01f ? v / m : SpreadOffsets[i].normalized;
            return other + dir * MinGap;
        }

        private static readonly RaycastHit2D[] _hits = new RaycastHit2D[8];
        private ContactFilter2D _filter = new ContactFilter2D { useTriggers = false, useLayerMask = false };

        // from → to 사이에 벽·가구(주인공 말고 막는 콜라이더)가 있는가
        private bool Blocked(Vector2 from, Vector2 to)
        {
            int n = Physics2D.Linecast(from, to, _filter, _hits);
            for (int k = 0; k < n; k++)
            {
                var c = _hits[k].collider;
                if (c == null || c.attachedRigidbody == scene.player) continue;
                return true;
            }
            return false;
        }

        // 시각 t 에 주인공이 있던 자리. 문을 지나 순간이동한 구간은 보간하지 않고 도착 지점으로 바로 간다.
        private Vector2 At(float t)
        {
            if (_hist.Count == 0) return scene.player.position;
            if (t <= _hist[0].t) return _hist[0].p;
            for (int k = 1; k < _hist.Count; k++)
            {
                if (_hist[k].t < t) continue;
                var a = _hist[k - 1]; var b = _hist[k];
                if ((b.p - a.p).sqrMagnitude > 25f) return b.p;
                float u = Mathf.InverseLerp(a.t, b.t, t);
                return Vector2.Lerp(a.p, b.p, u);
            }
            return _hist[_hist.Count - 1].p;
        }
    }
}
