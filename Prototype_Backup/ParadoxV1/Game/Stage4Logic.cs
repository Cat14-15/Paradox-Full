using System.Collections.Generic;
using UnityEngine;

namespace Paradox.Game
{
    /// <summary>
    /// Stage 4 — 한국 · 지하상가 화재 · 우선순위 판단 + 멀티태스킹 (완전 반전, 시간 제한 최고).
    ///  조작자(현지인)만 방송 자막·표지판·전체 지도를 본다. 채팅은 막히고 버튼 5개 + 글자 카드만 가능.
    ///  세 친구는 연기(안개) 속에서 미니맵·고글 없이 조작자의 방향 신호를 따라 각자의 장치(소화기·비상벨·셔터 스위치)를 작동한다.
    ///  불길은 조작자만 멀리서 보이므로 ⚠로 경고해야 한다. 장치 3개가 모두 작동하면 불이 잦아들고,
    ///  방송의 탈출 암호를 글자 카드로 받은 세 친구가 셔터 키패드에 4초 안에 동시에 입력하면 셔터가 열린다. 전원 원 안 → 클리어.
    /// </summary>
    public static class Stage4Logic
    {
        private const int Op = (int)GameRole.Operator, In = (int)GameRole.Interpreter, Na = (int)GameRole.Navigator, Ob = (int)GameRole.Observer;
        public const float CodeWindow = 4f;
        public const float SpreadEvery = 25f;
        public const int MaxFires = 14;
        public const float FireRadius = 1.1f;

        public static readonly string[] DeviceNames = { "", "소화기", "비상벨", "셔터 스위치" };

        public static void Start(GameLogic g)
        {
            var st = g.S.st;
            var rng = new Rng(st.seed);
            st.word = StageDefs.CodeWords[rng.Range(0, StageDefs.CodeWords.Length)];
            st.broadcast = $"[비상 안내] 지하 1층에서 화재가 발생했습니다. 소화기·비상벨·셔터 스위치를 모두 작동하면 탈출구가 열립니다. 탈출 셔터 암호는 '{st.word}' 입니다. 침착하게 동쪽(오른쪽 끝) 비상구로 대피하세요.";
            for (int i = 0; i < 4; i++) { st.device[i] = false; st.keypad[i] = ""; st.codeClock[i] = -99f; st.lastBurn[i] = -99f; }
            st.devicesDone = false;
            st.codeDone = false;
            st.fires.Clear();
            var cands = new List<Vector2>(StageMaps.Stage4FireCandidates);
            // 출발 지점·장치·키패드 근처는 제외
            cands.RemoveAll(p => Vector2.Distance(p, new Vector2(2f, 2f)) < 4f || (p.x > 42f && p.y > 6f && p.y < 14f));
            for (int k = 0; k < 6 && cands.Count > 0; k++)
            {
                int i = rng.Range(0, cands.Count);
                st.fires.Add(Vec2.Of(cands[i]));
                cands.RemoveAt(i);
            }
            st.lastSpread = g.Clock;
            GameLogic.ResetLetters(st);
            st.step = 0;
            st.moveMode = 1;
            UpdateStatus(g);
        }

        public static void Apply(GameLogic g, Cmd c)
        {
            var st = g.S.st;
            int slot = c.slot;
            switch (c.type)
            {
                case "Pick":
                    if (slot != Op) return;
                    g.PickToTray(slot, c.s);
                    break;

                case "SendLetters":
                    if (slot != Op) return;
                    g.SendTray(Op, 1, new[] { 1, 2, 3 }, 1.3f);
                    break;

                case "ClearLetters":
                    if (slot != Op) return;
                    GameLogic.ResetLetters(st);
                    g.Send(new GameEvent { type = "LettersCleared", slot = slot });
                    g.Dirty = true;
                    break;

                case "Operate":
                    if (slot < 1 || slot > 3) return;
                    if (st.device[slot]) return;
                    if (!g.Near(slot, "dev" + slot, 2.0f)) { g.Toast("내 장치 앞으로 가세요 — 조작자의 방향 신호를 따라", slot, GameLogic.ToneWarn); return; }
                    st.device[slot] = true;
                    g.Send(new GameEvent { type = "Device", slot = slot });
                    g.Toast($"{Roles.Name(slot)}이(가) {DeviceNames[slot]}를 작동했다!", -1, GameLogic.ToneGood);
                    if (st.device[1] && st.device[2] && st.device[3])
                    {
                        st.devicesDone = true;
                        st.fires.Clear();
                        g.Flash(GameLogic.ToneGood);
                        g.Send(new GameEvent { type = "DevicesDone" });
                        g.Toast("불길이 잦아들고 연기가 옅어진다! 이제 동쪽 셔터로 — 암호가 필요하다", -1, GameLogic.ToneGood);
                    }
                    UpdateStatus(g);
                    g.Dirty = true;
                    break;

                case "Key":
                    {
                        if (slot < 1 || slot > 3 || st.codeDone) return;
                        if (!GameLogic.ValidSyllable(c.s)) return;
                        if (!g.Near(slot, "pad" + slot, 2.0f)) { g.Toast("셔터 옆 내 키패드 앞으로 가세요", slot, GameLogic.ToneWarn); return; }
                        if (st.keypad[slot].Length >= st.word.Length) return;
                        st.keypad[slot] += c.s;
                        if (st.keypad[slot].Length == st.word.Length)
                        {
                            if (st.keypad[slot] == st.word)
                            {
                                st.codeClock[slot] = g.Clock;
                                g.Send(new GameEvent { type = "KeyOk", slot = slot });
                                g.Toast($"{Roles.Name(slot)} 키패드 일치! (4초 안에 세 명 모두)", -1, GameLogic.ToneGood);
                                TryCode(g);
                            }
                            else
                            {
                                st.keypad[slot] = "";
                                g.Send(new GameEvent { type = "KeyBad", slot = slot });
                                g.Penalty(3f, "암호가 틀렸다", slot);
                            }
                        }
                        g.Dirty = true;
                    }
                    break;

                case "KeyClear":
                    if (slot < 1 || slot > 3) return;
                    st.keypad[slot] = "";
                    st.codeClock[slot] = -99f;
                    g.Dirty = true;
                    break;
            }
        }

        public static void OnSignal(GameLogic g, int slot, string id) { }

        private static void TryCode(GameLogic g)
        {
            var st = g.S.st;
            float min = float.MaxValue, max = float.MinValue;
            int ok = 0;
            for (int i = 1; i <= 3; i++)
            {
                if (st.codeClock[i] < -90f) continue;
                ok++;
                min = Mathf.Min(min, st.codeClock[i]);
                max = Mathf.Max(max, st.codeClock[i]);
            }
            if (ok < 3) return;
            if (max - min <= CodeWindow)
            {
                st.codeDone = true;
                g.Flash(GameLogic.ToneGood);
                g.Send(new GameEvent { type = "CodeDone" });
                g.Toast("암호 일치! 셔터가 열린다 — 전원 원 안으로!", -1, GameLogic.ToneGood);
                UpdateStatus(g);
            }
            else
            {
                // 오래된 입력은 만료 — 다시 입력해야 한다
                for (int i = 1; i <= 3; i++)
                {
                    if (st.codeClock[i] < -90f) continue;
                    if (max - st.codeClock[i] > CodeWindow) { st.codeClock[i] = -99f; st.keypad[i] = ""; }
                }
                g.Toast("세 명이 4초 안에 함께 입력해야 한다 — 먼저 입력한 키패드가 초기화됐다", -1, GameLogic.ToneWarn);
            }
            g.Dirty = true;
        }

        public static void Tick(GameLogic g, float dt)
        {
            var st = g.S.st;
            if (g.Map == null) return;
            g.DeliverLetters();

            // 불 번짐
            if (!st.devicesDone && g.Clock - st.lastSpread > SpreadEvery && st.fires.Count < MaxFires)
            {
                var rng = new Rng(st.seed + st.fires.Count * 31 + Mathf.FloorToInt(g.Clock));
                int added = 0;
                for (int tries = 0; tries < 40 && added < 2; tries++)
                {
                    var cand = StageMaps.Stage4FireCandidates[rng.Range(0, StageMaps.Stage4FireCandidates.Length)];
                    if (cand.x > 42f && cand.y > 6f && cand.y < 14f) continue;
                    bool dup = false;
                    foreach (var f in st.fires) if (Vector2.Distance(f.V, cand) < 1.5f) { dup = true; break; }
                    if (dup) continue;
                    st.fires.Add(Vec2.Of(cand));
                    added++;
                }
                st.lastSpread = g.Clock;
                if (added > 0)
                {
                    g.Send(new GameEvent { type = "Spread" });
                    g.Toast("불이 번진다! 친구들에게 ⚠로 경고하세요", Op, GameLogic.ToneWarn);
                }
                g.Dirty = true;
            }

            // 화상
            for (int i = 0; i < 4; i++)
            {
                var p = g.Pos(i);
                if (p == null) continue;
                foreach (var f in st.fires)
                {
                    if (Vector2.Distance(p.Value, f.V) > FireRadius) continue;
                    if (g.Clock - st.lastBurn[i] < 1.5f) break;
                    st.lastBurn[i] = g.Clock;
                    st.penalty += 5f;
                    Vector2 away = (p.Value - f.V);
                    if (away.sqrMagnitude < 0.01f) away = Vector2.left;
                    g.Send(new GameEvent { type = "Knock", slot = i, pos = Vec2.Of(away.normalized) });
                    g.Send(new GameEvent { type = "Burn", slot = i });
                    g.Toast("뜨거워! 불길에 데였다 (-5초)", i, GameLogic.ToneBad);
                    g.Dirty = true;
                    break;
                }
            }

            if (st.codeDone && g.AllInZones()) g.Cleared();
        }

        public static void UpdateStatus(GameLogic g)
        {
            var st = g.S.st;
            int done = 0;
            for (int i = 1; i <= 3; i++) if (st.device[i]) done++;
            if (!st.devicesDone) st.status = $"장치 {done}/3 작동 · 조작자가 방향·경고 신호로 안내";
            else if (!st.codeDone) st.status = "셔터 암호 입력 · 세 친구가 4초 안에 동시에";
            else st.status = "셔터 개방 · 전원 원 안으로";
        }
    }
}
