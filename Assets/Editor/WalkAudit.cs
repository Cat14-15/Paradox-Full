using System.IO;
using System.Text;
using Paradox.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Paradox.Editor
{
    /// <summary>배치 모드 점검: Story1 의 각 방에서 플레이어가 서 있을 수 있는 칸을 격자로 재고, 문까지 닿는지·막힌 곳을 PNG 와 로그로 낸다.</summary>
    public static class WalkAudit
    {
        public static void Run()
        {
            string outDir = System.Environment.GetEnvironmentVariable("PARADOX_AUDIT_DIR") ?? Path.Combine(Path.GetTempPath(), "walkaudit");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene("Assets/Scenes/Story1.unity");
            var sc = Object.FindFirstObjectByType<Story1Scene>();
            var pcol = sc.player.GetComponentInChildren<Collider2D>();
            var sb = new StringBuilder();
            Vector2 size; Vector2 off;
            float k = Story1Scene.CharScale;
            if (pcol is BoxCollider2D bc) { size = bc.size; off = bc.offset; }
            else if (pcol is CapsuleCollider2D cc) { size = cc.size; off = cc.offset; }
            else if (pcol is CircleCollider2D ci) { size = Vector2.one * ci.radius * 2f; off = ci.offset; }
            else { size = Vector2.one * 0.5f; off = Vector2.zero; }
            Vector2 ps = pcol.transform.lossyScale;
            size = Vector2.Scale(size, new Vector2(Mathf.Abs(ps.x), Mathf.Abs(ps.y))) * k;
            off = Vector2.Scale(off, ps) * k;
            sb.AppendLine("player collider " + pcol.GetType().Name + " size " + size + " off " + off);
            Physics2D.SyncTransforms();

            foreach (var d in sc.doors)
            {
                if (d == null || d.destination == null) continue;
                var ap = (Vector2)d.destination.position;
                var bl = Physics2D.OverlapBoxAll(ap + off, size, 0f);
                foreach (var b in bl)
                    if (b != null && !b.isTrigger && b.attachedRigidbody != sc.player)
                        sb.AppendLine("ARRIVE-BLOCKED door " + d.name + " -> " + d.destination.parent.name + " at " + ap + " by " + b.gameObject.name + " (" + b.GetType().Name + ") in " + (b.transform.parent != null ? b.transform.parent.name : "-"));
            }
            const float step = 0.125f;
            var filter = new ContactFilter2D { useTriggers = false };
            var hits = new Collider2D[8];
            foreach (var room in sc.rooms)
            {
                Vector2 c = room.transform.position;
                int w = Mathf.RoundToInt(room.size.x / step), h = Mathf.RoundToInt(room.size.y / step);
                var free = new bool[w, h];
                for (int x = 0; x < w; x++) for (int y = 0; y < h; y++)
                {
                    Vector2 p = c - room.size * 0.5f + new Vector2((x + 0.5f) * step, (y + 0.5f) * step);
                    int n = Physics2D.OverlapBox(p + off, size, 0f, filter, hits);
                    bool ok = true;
                    for (int i = 0; i < n; i++) if (hits[i] != null && hits[i].attachedRigidbody != sc.player && !hits[i].isTrigger) { ok = false; break; }
                    free[x, y] = ok;
                }
                // 문(자기 방 안의 것) 위치와 도착 자리에서 flood fill
                var seen = new bool[w, h];
                var q = new System.Collections.Generic.Queue<Vector2Int>();
                System.Action<Vector2> seed = wp =>
                {
                    var l = wp - (c - room.size * 0.5f);
                    int sx = Mathf.Clamp((int)(l.x / step), 0, w - 1), sy = Mathf.Clamp((int)(l.y / step), 0, h - 1);
                    // 가장 가까운 빈 칸
                    for (int r = 0; r < 12; r++)
                        for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++)
                        {
                            int x = sx + dx, y = sy + dy;
                            if (x < 0 || y < 0 || x >= w || y >= h || !free[x, y] || seen[x, y]) continue;
                            seen[x, y] = true; q.Enqueue(new Vector2Int(x, y)); return;
                        }
                };
                foreach (var d in sc.doors)
                {
                    if (d == null) continue;
                    if (d.destination != null && Contains(room, d.destination.position)) seed(d.destination.position);
                }
                while (q.Count > 0)
                {
                    var p = q.Dequeue();
                    int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
                    for (int i = 0; i < 4; i++)
                    {
                        int x = p.x + dx[i], y = p.y + dy[i];
                        if (x < 0 || y < 0 || x >= w || y >= h || seen[x, y] || !free[x, y]) continue;
                        seen[x, y] = true; q.Enqueue(new Vector2Int(x, y));
                    }
                }
                sb.AppendLine("== " + room.gameObject.name + " (cam " + room.cameraSize + ")");
                foreach (var d in sc.doors)
                {
                    if (d == null || !Contains(room, d.transform.position)) continue;
                    // 문 반경 안에 닿는 칸이 있는가
                    bool reach = false; float best = 99f;
                    for (int x = 0; x < w; x++) for (int y = 0; y < h; y++)
                    {
                        if (!seen[x, y]) continue;
                        Vector2 p = c - room.size * 0.5f + new Vector2((x + 0.5f) * step, (y + 0.5f) * step) - off;
                        float dd = Vector2.Distance(p, d.transform.position);
                        if (dd < best) best = dd;
                        if (dd <= d.radius) reach = true;
                    }
                    sb.AppendLine("  door " + d.name + " pos=" + d.transform.position + " radius=" + d.radius + " nearest=" + best.ToString("0.00") + " reach=" + reach);
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                for (int x = 0; x < w; x++) for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, seen[x, y] ? new Color(0.3f, 0.7f, 0.3f) : free[x, y] ? new Color(0.9f, 0.8f, 0.2f) : new Color(0.6f, 0.1f, 0.1f));
                foreach (var d in sc.doors)
                {
                    if (d == null || !Contains(room, d.transform.position)) continue;
                    var l = (Vector2)d.transform.position - (c - room.size * 0.5f);
                    int px = (int)(l.x / step), py = (int)(l.y / step);
                    for (int a = -2; a <= 2; a++) for (int b = -2; b <= 2; b++)
                        if (px + a >= 0 && px + a < w && py + b >= 0 && py + b < h) tex.SetPixel(px + a, py + b, Color.cyan);
                }
                tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, room.gameObject.name + ".png"), tex.EncodeToPNG());
                // 실제 그림 위에 겹친 버전(막힌 곳 = 붉게)
                var art = room.transform.Find("Art");
                var sr = art != null ? art.GetComponent<SpriteRenderer>() : null;
                if (sr != null && sr.sprite != null)
                {
                    var src = sr.sprite.texture;
                    var rt = RenderTexture.GetTemporary(src.width, src.height, 0);
                    Graphics.Blit(src, rt);
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var rd = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                    rd.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); rd.Apply();
                    RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                    for (int px = 0; px < rd.width; px++) for (int py = 0; py < rd.height; py++)
                    {
                        int gx = Mathf.Clamp(px * w / rd.width, 0, w - 1), gy = Mathf.Clamp(py * h / rd.height, 0, h - 1);
                        if (free[gx, gy]) continue;
                        var col = rd.GetPixel(px, py);
                        rd.SetPixel(px, py, Color.Lerp(col, new Color(1f, 0f, 0f), 0.55f));
                    }
                    foreach (var d in sc.doors)
                    {
                        if (d == null || !Contains(room, d.transform.position)) continue;
                        var l = (Vector2)d.transform.position - (c - room.size * 0.5f);
                        int px = (int)(l.x / room.size.x * rd.width), py = (int)(l.y / room.size.y * rd.height);
                        for (int a = -3; a <= 3; a++) for (int b = -3; b <= 3; b++)
                            if (px + a >= 0 && px + a < rd.width && py + b >= 0 && py + b < rd.height) rd.SetPixel(px + a, py + b, Color.cyan);
                    }
                    rd.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, room.gameObject.name + "_art.png"), rd.EncodeToPNG());
                }
            }
            File.WriteAllText(Path.Combine(outDir, "report.txt"), sb.ToString());
            Debug.Log("[WalkAudit] done\n" + sb);
        }

        /// <summary>문 도착 자리가 가구 콜라이더 안에 있으면 그 가구 바로 아래(가장 가까운 빈 자리)로 옮겨 저장한다.</summary>
        public static void FixArrive()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Story1.unity");
            var sc = Object.FindFirstObjectByType<Story1Scene>();
            var pcol = sc.player.GetComponentInChildren<CircleCollider2D>();
            float k = Story1Scene.CharScale;
            float rad = pcol.radius * Mathf.Abs(pcol.transform.lossyScale.x) * k;
            Vector2 off = pcol.offset * Mathf.Abs(pcol.transform.lossyScale.y) * k;
            Physics2D.SyncTransforms();
            int fixedN = 0;
            foreach (var d in sc.doors)
            {
                if (d == null || d.destination == null) continue;
                for (int tries = 0; tries < 20; tries++)
                {
                    Vector2 ap = d.destination.position;
                    var hit = Physics2D.OverlapCircle(ap + off, rad, ~0);
                    if (hit == null || hit.isTrigger) break;
                    Vector2 np = new Vector2(ap.x, hit.bounds.min.y - rad - 0.05f - off.y);
                    Debug.Log("[WalkAudit] move arrive " + d.name + " " + ap + " -> " + np + " (" + hit.name + ")");
                    d.destination.position = np; fixedN++;
                    Physics2D.SyncTransforms();
                }
            }
            if (fixedN > 0) { EditorSceneManager.MarkSceneDirty(sc.gameObject.scene); EditorSceneManager.SaveScene(sc.gameObject.scene); }
            Debug.Log("[WalkAudit] FixArrive moved " + fixedN);
        }

        private static bool Contains(MuseumRoom r, Vector2 p) => r.Bounds.Contains(p);
    }
}
