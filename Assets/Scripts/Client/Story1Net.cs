using System;
using System.Collections.Generic;
using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// Story1(월드·퍼즐) 온라인 동기화. 서버(권한자)는 판정하지 않고 "S1" 명령을 보낸 사람 슬롯을 붙여 모두에게 그대로 중계한다.
    /// 오프라인(접속 없음)에서는 같은 이벤트를 로컬에서 바로 돌려 같은 코드 경로를 쓴다.
    ///  · 시드/시계: 모든 PC 가 같은 퍼즐·같은 스파크 박자를 본다
    ///  · Clear   : 퍼즐 하나를 풀면 모두에게 반영(전원 복구, 패널 닫힘, 클리어 표시)
    ///  · Say     : 조작자의 진행 상황 한 줄(다른 역할 패널 하단에 표시)
    ///  · Round/FinalGo/FinalPress/FinalResult : 최종 퍼즐 동기화
    ///  · Pos     : 다른 플레이어의 월드 위치
    ///  · Signal  : 신호 카드(말 대신 숫자·방향·색·예/아니오)
    /// </summary>
    public static class Story1Net
    {
        public const int Clear = 1, Say = 2, Round = 3, FinalGo = 4, FinalPress = 5, FinalResult = 6, Pos = 7, Signal = 8, Break = 9, Pump = 10, Chat = 11, ValveSet = 12, GasSet = 13, BreakerSet = 14;
        public const int PFuse = 0, PGallery = 1, PRelic = 2, PWater = 3, PFinal = 4;

        public static event Action<GameEvent> Received;

        /// <summary>자유 채팅 한 줄(보낸 사람 슬롯, 내용). 내가 보낸 것도 서버를 한 바퀴 돌아 같은 경로로 들어온다.</summary>
        public static event Action<int, string> ChatLine;
        public const int ChatMax = 60;

        public static void SendChat(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;
            if (text.Length > ChatMax) text = text.Substring(0, ChatMax);
            Send(Chat, 0, text);
        }

        /// <summary>퍼즐별로 마지막으로 받은 조작자의 진행 한 줄.</summary>
        public static readonly string[] SayText = new string[5];
        public static int SayVersion;

        public static readonly Vector2[] RemotePos = new Vector2[Roles.Count];
        public static readonly float[] RemoteSeen = { -99f, -99f, -99f, -99f };

        /// <summary>관측자가 전원을 끊은(스파크가 꺼지는) 시각 / 내비게이터가 펌프를 돌려 수위가 멈추는 시각까지.</summary>
        public static float BreakUntil, PumpUntil;

        public static int FinalRound;          // 최종 퍼즐에서 마지막으로 알려진 라운드
        public static bool FinalGoing;         // 마지막(동시 입력) 단계가 시작됐는가

        public struct SignalEntry { public int slot, card; public float t; public string text, key; }
        public static readonly List<SignalEntry> Feed = new List<SignalEntry>();
        public static int FeedVersion;

        /// <summary>봇 동료가 자기 역할로 한마디 한다(기록 창에만 뜬다. 네트워크로 보내지 않는다).</summary>
        public static void BotSay(int role, string text, string key = null)
        {
            if (key != null)
                for (int i = Feed.Count - 1; i >= 0; i--)
                    if (Feed[i].slot == role && Feed[i].key == key)
                    {
                        var e = Feed[i];
                        if (e.text == text) return;   // 같은 말은 다시 올리지 않는다
                        e.text = text; e.t = Time.time; Feed[i] = e;   // 소리 없이 제자리에서 바꾼다
                        return;
                    }
            Feed.Add(new SignalEntry { slot = role, card = -1, t = Time.time, text = text, key = key });   // 봇의 말은 소리를 내지 않는다(FeedVersion 을 올리지 않음)
            while (Feed.Count > 60) Feed.RemoveAt(0);
        }

        // 신호 카드: 숫자 1~4, 방향 3, 색 4, 예/아니오/여기
        public static readonly string[] CardLabel = { "1", "2", "3", "4", "←", "↑", "→", "불", "물", "별", "풀", "응", "아니", "여기" };
        public const int CardCount = 14;

        /// <summary>접속해서 내 슬롯이 정해졌는가(진짜 온라인 경로).</summary>
        public static bool Online => Session.Connected && Session.MySlot >= 0;

        /// <summary>나 말고 사람인 동료가 있는가(신호 카드·위치 표시가 의미 있는 경우).</summary>
        public static bool HasHumanPeers
        {
            get
            {
                if (!Online) return false;
                for (int r = 0; r < Roles.Count; r++) if (IsRemoteHuman(r)) return true;
                return false;
            }
        }

        public static bool IsRemoteHuman(int role)
        {
            if (!Online || !Roles.Valid(role) || role == Session.MySlot) return false;
            var p = Session.State.players[role];
            return p != null && p.present && !p.isBot;
        }

        /// <summary>모두가 같은 시각을 보는 시계(서버 클록). 오프라인은 로컬 시간.</summary>
        public static float Now => Online ? Session.Clock : Time.unscaledTime;

        /// <summary>퍼즐 id 별 시드. 온라인에서는 방장·게임 시작 시각으로 정해져 모든 PC 가 같다.</summary>
        public static int Seed(int puzzleId)
        {
            if (!Online) return Environment.TickCount + puzzleId * 31 + RoundOf(puzzleId) * 7919;
            var S = Session.State;
            long h = (long)S.puzzleSeed * 104729L + puzzleId * 15485863L + RoundOf(puzzleId) * 2750159L + 7919L;   // 라운드가 바뀌면 새 문제
            return (int)(Math.Abs(h) % 1000003L) + 1;
        }

        // ---- 난이도 · 라운드 --------------------------------------------------------
        private const string DiffKey = "stranger.difficulty";
        private static int _localDiff = -1;

        /// <summary>지금 난이도. 온라인은 방장이 정한 값(서버 상태), 혼자 테스트는 마지막으로 고른 값.</summary>
        public static int Difficulty
        {
            get
            {
                if (Online) return Story1Rules.Clamp(Session.State.difficulty);
                if (_localDiff < 0) { try { _localDiff = PlayerPrefs.GetInt(DiffKey, Story1Rules.Easy); } catch { _localDiff = Story1Rules.Easy; } }
                return Story1Rules.Clamp(_localDiff);
            }
        }

        /// <summary>로비에서 난이도를 고른다(온라인은 방장만 — 서버가 확인). 혼자 테스트용 값도 기억한다.</summary>
        public static void SetDifficulty(int d)
        {
            d = Story1Rules.Clamp(d);
            _localDiff = d;
            try { PlayerPrefs.SetInt(DiffKey, d); PlayerPrefs.Save(); } catch { }
            if (Online && Session.IsHost) Session.Send(Cmd.Make("Diff", d));
        }

        // ---- 스피드런 --------------------------------------------------------------
        private const string SpeedKey = "stranger.speedrun";
        private static int _localSpeed = -1;

        /// <summary>스피드런 모드(이야기 건너뛰기 · 구간 기록 · 따로 집계하는 랭킹). 온라인은 방장이 정한 값.</summary>
        public static bool Speedrun
        {
            get
            {
                if (Online) return Session.State.speedrun;
                if (_localSpeed < 0) { try { _localSpeed = PlayerPrefs.GetInt(SpeedKey, 0); } catch { _localSpeed = 0; } }
                return _localSpeed == 1;
            }
        }

        public static void SetSpeedrun(bool on)
        {
            _localSpeed = on ? 1 : 0;
            try { PlayerPrefs.SetInt(SpeedKey, _localSpeed); PlayerPrefs.Save(); } catch { }
            if (Online && Session.IsHost) Session.Send(Cmd.Make("Speed", on ? 1 : 0));
        }

        /// <summary>이 클라이언트가 센 "푼 횟수"(내가 푼 것 + 동료가 푼 이벤트). 서버 상태와 큰 쪽을 쓴다(이벤트를 놓친 PC 도 따라잡도록).</summary>
        private static readonly int[] _roundsDone = new int[5];

        public static int RoundOf(int puzzleId)
        {
            if (puzzleId < 0 || puzzleId >= _roundsDone.Length) return 0;
            int r = _roundsDone[puzzleId];
            if (Online) r = Math.Max(r, Story1Rules.RoundsOf(Session.State.s1Rounds, puzzleId));
            return r;
        }

        public static int NeedRounds(int puzzleId) => Story1Rules.Need(Difficulty, puzzleId);

        /// <summary>방금 푼 것으로 이 퍼즐이 완전히 끝났는가(false 면 다음 라운드가 남았다).</summary>
        public static bool IsFinal(int puzzleId) => RoundOf(puzzleId) >= NeedRounds(puzzleId);

        public static string RoundLabel(int puzzleId)
        {
            int need = NeedRounds(puzzleId);
            return need <= 1 ? "" : "   (" + Math.Min(RoundOf(puzzleId) + 1, need) + " / " + need + ")";
        }

        /// <summary>다음 라운드가 시작됐다(퍼즐 id, 이제 몇 번째 라운드인지 1부터).</summary>
        public static event Action<int, int> RoundAdvanced;
        public static void AnnounceRound(int puzzleId) { RoundAdvanced?.Invoke(puzzleId, Math.Min(RoundOf(puzzleId) + 1, NeedRounds(puzzleId))); }

        static Story1Net() { Session.EventReceived -= OnEvent; Session.EventReceived += OnEvent; }   // 에디터 테스트에서도 수신되도록

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Received = null; ChatLine = null;
            for (int i = 0; i < SayText.Length; i++) SayText[i] = "";
            SayVersion = 0; FinalRound = 0; FinalGoing = false; BreakUntil = PumpUntil = 0f;
            for (int i = 0; i < _roundsDone.Length; i++) _roundsDone[i] = 0; RoundAdvanced = null;
            Feed.Clear(); FeedVersion = 0;
            for (int i = 0; i < Roles.Count; i++) { RemoteSeen[i] = -99f; RemotePos[i] = Vector2.zero; Track[i].Clear(); }
            Session.EventReceived -= OnEvent;
            Session.EventReceived += OnEvent;
        }

        public static void Send(int kind, int b = 0, string s = "", float f = 0f, Vector2? pos = null)
        {
            if (kind == Clear && b >= 0 && b < _roundsDone.Length) _roundsDone[b]++;   // 내가 푼 횟수는 바로 센다(내 화면은 이미 처리했으니 되돌아온 이벤트는 건너뛴다)
            if (Online)
            {
                var c = Cmd.Make("S1", kind, b, s, f);
                if (pos.HasValue) c.pos = Vec2.Of(pos.Value);
                Session.Send(c);
            }
            else
            {
                Session.Dispatch(new GameEvent { type = "S1", slot = Story1Role.Current, a = kind, b = b, s = s ?? "", f = f, pos = pos.HasValue ? Vec2.Of(pos.Value) : default(Vec2) });
            }
        }

        /// <summary>내가 보낸 이벤트인가(보낸 쪽은 자기 화면에서 이미 처리했다).</summary>
        public static bool IsMine(GameEvent e) => !Online || e.slot == Session.MySlot;

        private static readonly string[] _sent = new string[5];

        /// <summary>조작자의 진행 한 줄을 동료에게 알린다(사람 동료가 있고 내용이 바뀌었을 때만).</summary>
        public static void Tell(int puzzleId, string text)
        {
            if (!HasHumanPeers || Story1Role.Current != (int)GameRole.Operator) return;
            if (_sent[puzzleId] == text) return;
            _sent[puzzleId] = text;
            Send(Say, puzzleId, text);
        }

        /// <summary>조작자가 아닌 쪽 패널의 하단 줄에 조작자의 진행을 보여 준다.</summary>
        public static void ShowSay(int puzzleId, PixelText status, ref int version)
        {
            if (version == SayVersion) return;
            version = SayVersion;
            if (!Online || Story1Role.Current == (int)GameRole.Operator || status == null) return;
            status.text = SayText[puzzleId];
            status.color = new Color(0.95f, 0.85f, 0.55f);
        }

        public static void ResetWorld()
        {
            for (int i = 0; i < _sent.Length; i++) _sent[i] = null;
            for (int i = 0; i < SayText.Length; i++) SayText[i] = "";
            FinalRound = 0; FinalGoing = false; BreakUntil = PumpUntil = 0f;
            for (int i = 0; i < _roundsDone.Length; i++) _roundsDone[i] = 0;
            Feed.Clear(); FeedVersion++;
        }

        public static void ResetSent(int puzzleId) { _sent[puzzleId] = null; }

        private static void OnEvent(GameEvent e)
        {
            if (e == null || e.type != "S1") return;
            if (e.a == Clear && e.b >= 0 && e.b < _roundsDone.Length && !IsMine(e)) _roundsDone[e.b]++;   // 동료가 푼 횟수
            switch (e.a)
            {
                case Say:
                    if (e.b >= 0 && e.b < SayText.Length) { SayText[e.b] = e.s ?? ""; SayVersion++; }
                    break;
                case Chat: if (Roles.Valid(e.slot) && !string.IsNullOrEmpty(e.s)) ChatLine?.Invoke(e.slot, e.s); break;
                case Break: BreakUntil = Now + 2.2f; break;
                case Pump: PumpUntil = Now + 4f; break;
                case Round:
                    FinalRound = e.b;
                    break;
                case FinalGo:
                    FinalGoing = true;
                    break;
                case Pos:
                    if (Roles.Valid(e.slot) && !IsMine(e)) OnRemotePos(e.slot, e.pos.V);
                    break;
                case Signal:
                    Feed.Add(new SignalEntry { slot = e.slot, card = e.b, t = Time.time });
                    while (Feed.Count > 60) Feed.RemoveAt(0);
                    FeedVersion++;
                    break;
            }
            Received?.Invoke(e);
        }

        public static bool RemoteFresh(int role) => Roles.Valid(role) && Time.time - RemoteSeen[role] < 6f;

        /// <summary>다른 사람의 위치를 받았다(PosRpc / 오프라인 이벤트 공통).</summary>
        public static void OnRemotePos(int slot, Vector2 p)
        {
            if (!Roles.Valid(slot) || slot == Session.MySlot) return;
            RemotePos[slot] = p; RemoteSeen[slot] = Time.time;
            var tr = Track[slot];
            tr.Add(new PosSample { t = Time.time, p = p });
            while (tr.Count > 40) tr.RemoveAt(0);
        }

        /// <summary>내 위치를 모두에게(온라인일 때만, 비신뢰 RPC).</summary>
        public static void SendPos(Vector2 p)
        {
            if (!Online) return;
            var sn = Paradox.Net.SessionNet.Instance;
            if (sn != null && sn.Object != null && sn.Object.IsValid) sn.PosRpc(p.x, p.y);
        }

        private struct PosSample { public float t; public Vector2 p; }
        private static readonly List<PosSample>[] Track = { new List<PosSample>(), new List<PosSample>(), new List<PosSample>(), new List<PosSample>() };

        /// <summary>다른 사람의 시각 t 의 위치(받은 위치들 사이를 이어서). 아직 t 에 닿지 않았으면 가장 최근 위치.</summary>
        public static Vector2 RemoteAt(int role, float t)
        {
            if (!Roles.Valid(role)) return Vector2.zero;
            var tr = Track[role];
            if (tr.Count == 0) return RemotePos[role];
            if (t <= tr[0].t) return tr[0].p;
            for (int k = 1; k < tr.Count; k++)
            {
                if (tr[k].t < t) continue;
                var a = tr[k - 1]; var b = tr[k];
                if ((b.p - a.p).sqrMagnitude > 25f) return b.p;   // 문을 지나 순간이동
                return Vector2.Lerp(a.p, b.p, Mathf.InverseLerp(a.t, b.t, t));
            }
            return tr[tr.Count - 1].p;
        }
    }
}
