using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Paradox.Client
{
    /// <summary>랭킹 기록 한 줄. 팀 이름은 4자리(AI 는 "AI").</summary>
    [Serializable]
    public class RankEntry
    {
        public float time;
        public string[] team = new string[0];
        public string date = "";
        public int cats;
        [NonSerialized] public bool mine;   // 방금 세운 기록(목록에서 강조)
    }

    [Serializable]
    internal class RankList { public List<RankEntry> items = new List<RankEntry>(); }

    /// <summary>
    /// 랭킹: 로컬(난이도별 상위 5, PlayerPrefs JSON) + 온라인(웹 배포의 /api/rank, Upstash Redis).
    /// 온라인은 실패해도 게임에 영향이 없다(5초 타임아웃, 결과가 없으면 로컬만 보여 준다).
    /// </summary>
    public static class Ranking
    {
        public const int LocalKeep = 5;
        private const string Key = "stranger.rank.d";
        public const string DefaultUrl = "https://stranger-bssm.vercel.app/api/rank";

        // ---------------------------------------------------------------- 로컬
        public static List<RankEntry> Local(int diff)
        {
            try
            {
                string s = PlayerPrefs.GetString(Key + diff, "");
                if (string.IsNullOrEmpty(s)) return new List<RankEntry>();
                var l = JsonUtility.FromJson<RankList>(s);
                return l != null && l.items != null ? l.items : new List<RankEntry>();
            }
            catch { return new List<RankEntry>(); }
        }

        /// <summary>로컬 랭킹에 넣고 순위(1부터)를 돌려준다. 상위 5 밖이어도 몇 위인지는 알려 준다(저장은 5개까지).</summary>
        public static int AddLocal(int diff, RankEntry e)
        {
            var l = Local(diff);
            l.Add(e);
            l.Sort((a, b) => a.time.CompareTo(b.time));
            int place = l.IndexOf(e) + 1;
            while (l.Count > LocalKeep) l.RemoveAt(l.Count - 1);
            try { PlayerPrefs.SetString(Key + diff, JsonUtility.ToJson(new RankList { items = l })); PlayerPrefs.Save(); } catch { }
            return place;
        }

        /// <summary>이번 판 기록(팀 이름: 사람은 닉네임, AI 자리는 "AI").</summary>
        public static RankEntry Make(float seconds, int cats)
        {
            var team = new string[4];
            for (int i = 0; i < 4; i++) team[i] = Story1Role.IsBot(i) ? "AI" : StoryText.NameOf(i);
            return new RankEntry { time = Mathf.Round(seconds * 10f) / 10f, team = team, date = DateTime.Now.ToString("yyyy-MM-dd"), cats = cats, mine = true };
        }

        /// <summary>기록표 번호: 0~2 스토리(난이도) · 3~5 스피드런.</summary>
        public const int Modes = 3;
        public static int Board(int diff, bool speedrun) => Mathf.Clamp(diff, 0, 2) + (speedrun ? Modes : 0);

        public static string TimeTextFine(float t) { t = Mathf.Max(0f, t); int m = (int)(t / 60f); return m + ":" + (t - m * 60f).ToString("00.0"); }

        public static string TimeText(float t) { int s = Mathf.Max(0, Mathf.RoundToInt(t)); return (s / 60) + ":" + (s % 60).ToString("00"); }

        public static string TeamText(RankEntry e)
        {
            if (e == null || e.team == null) return "";
            return string.Join(" · ", e.team);
        }

        // ---------------------------------------------------------------- 온라인
        /// <summary>웹은 지금 주소와 같은 곳의 /api/rank, 그 밖(에디터 · PC)은 기본 주소.</summary>
        public static string Url
        {
            get
            {
                if (Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    try
                    {
                        var u = new Uri(Application.absoluteURL);
                        if (u.Scheme == "http" || u.Scheme == "https") return u.GetLeftPart(UriPartial.Authority) + "/api/rank";
                    }
                    catch { }
                }
                return DefaultUrl;
            }
        }

        /// <summary>온라인 기록을 보낼 수 있는가(웹 배포에서만 — 에디터 · PC 시험 기록이 섞이지 않게).</summary>
        public static bool CanSubmit => Application.platform == RuntimePlatform.WebGLPlayer;

        private static bool _submitted;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _submitted = false; _runner = null; }

        /// <summary>클리어 1회 제출(같은 판에서 두 번 보내지 않는다). done(순위 또는 -1).</summary>
        public static void Submit(int diff, RankEntry e, Action<int> done = null)
        {
            if (!CanSubmit || e == null || _submitted) { done?.Invoke(-1); return; }
            _submitted = true;
            Runner.StartCoroutine(PostRoutine(diff, e, done));
        }

        /// <summary>새 판이 시작될 때 부른다(다음 클리어를 다시 보낼 수 있게).</summary>
        public static void NewRun() { _submitted = false; }

        [Serializable] private class PostBody { public int diff; public float time; public string[] team; public int cats; }
        [Serializable] private class PostResult { public bool ok; public int rank = -1; }

        private static IEnumerator PostRoutine(int diff, RankEntry e, Action<int> done)
        {
            string json = JsonUtility.ToJson(new PostBody { diff = diff, time = e.time, team = e.team, cats = e.cats });
            using (var req = new UnityWebRequest(Url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 5;
                yield return req.SendWebRequest();
                int rank = -1;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try { var r = JsonUtility.FromJson<PostResult>(req.downloadHandler.text); if (r != null && r.ok) rank = r.rank; } catch { }
                }
                done?.Invoke(rank);
            }
        }

        /// <summary>온라인 상위 20. 실패하면 null.</summary>
        public static void FetchOnline(int diff, Action<List<RankEntry>> done)
        {
            Runner.StartCoroutine(GetRoutine(diff, done));
        }

        private static IEnumerator GetRoutine(int diff, Action<List<RankEntry>> done)
        {
            using (var req = UnityWebRequest.Get(Url + "?diff=" + diff))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();
                List<RankEntry> list = null;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try { var l = JsonUtility.FromJson<RankList>(req.downloadHandler.text); if (l != null && l.items != null) list = l.items; } catch { }
                }
                done?.Invoke(list);
            }
        }

        private static RankRunner _runner;
        private static RankRunner Runner
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("RankRunner");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    go.hideFlags = HideFlags.HideInHierarchy;
                    _runner = go.AddComponent<RankRunner>();
                }
                return _runner;
            }
        }
    }

    internal sealed class RankRunner : MonoBehaviour { }
}
