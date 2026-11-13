using UnityEngine;

namespace Paradox.Game
{
    /// <summary>
    /// 봇의 "손" (서버). 사람이 조종하지 않는 봇 슬롯이 자기 역할의 일을 사람처럼 약간의 지연을 두고 수행해,
    /// 혼자(또는 2~3명) 테스트해도 실제 4인 플레이와 같은 흐름이 굴러가게 한다.
    /// 이동(어디로 걸어갈지)은 호스트 클라이언트의 BotBrain이 맡고, 여기서는 명령(글자·중계·경고·레버·콘솔·수문·표시·확인·장치·키패드)만 낸다.
    /// </summary>
    public class BotDriver
    {
        private const int Op = 0, In = 1, Na = 2, Ob = 3;

        private readonly GameLogic _g;
        private readonly float[] _next = new float[4];
        private int _lastStage = -1, _lastStep = -1;
        private float _goClock = -99f;
        private bool _pendingPullIn;
        private int _obsIdx, _navIdx, _intIdx;
        private float _lastDirSignal = -99f, _lastWarn = -99f;

        public BotDriver(GameLogic g) { _g = g; }

        private StageState St => _g.S.st;
        private float Clock => _g.Clock;

        private bool Active(int slot) => _g.IsBot(slot) && _g.S.hostControlSlot != slot;
        private bool Ready(int slot) => Clock >= _next[slot];
        private void Wait(int slot, float sec) => _next[slot] = Clock + sec;

        private void Do(int slot, Cmd c)
        {
            c.slot = slot;
            _g.Apply(c, true);
        }

        private void DoSignal(int slot, string id, int target = -1) => Do(slot, Cmd.Make("Signal", 0, target, id));

        public void OnSignal(int from, string id, int target)
        {
            if (id == "GO") _goClock = Clock;
        }

        public void Tick(float dt)
        {
            var st = St;
            if (st.stage != _lastStage || st.step != _lastStep)
            {
                _lastStage = st.stage; _lastStep = st.step;
                for (int i = 0; i < 4; i++) _next[i] = Clock + 1.2f + i * 0.4f;
                _obsIdx = _navIdx = _intIdx = 0;
                _pendingPullIn = false;
            }
            switch (st.stage)
            {
                case 1: Stage1(st); break;
                case 2: Stage2(st); break;
                case 3: Stage3(st); break;
                case 4: Stage4(st); break;
            }
        }

        // ------------------------------------------------------------------ 공용: 단어 보내기

        /// <summary>발신 역할 봇이 단어를 한 글자씩 트레이에 담고 보낸다. 이미 전달 중/도착한 글자가 있으면 기다린다.</summary>
        private void SendWord(int slot, string word, int receiver)
        {
            var st = St;
            if (!Active(slot) || !Ready(slot) || string.IsNullOrEmpty(word)) return;
            bool receiverHas = st.received[receiver].Length > 0;
            if (st.inflight.Count > 0 || receiverHas) return;
            if (st.picked.Length < word.Length)
            {
                Do(slot, Cmd.Make("Pick", 0, 0, word[st.picked.Length].ToString()));
                Wait(slot, 0.6f);
            }
            else
            {
                Do(slot, Cmd.Make("SendLetters"));
                Wait(slot, 2.5f);
            }
        }

        // ------------------------------------------------------------------ Stage 1

        private void Stage1(StageState st)
        {
            if (st.step != 0 || st.signalCut) return;

            SendWord(In, st.word, Op);

            if (Active(Na) && Ready(Na) && Stage1Logic.RelayReady(st, Clock))
            {
                Do(Na, Cmd.Make("Relay"));
                Wait(Na, 0.9f);
            }

            if (Active(Ob) && Ready(Ob) && Stage1Logic.GuardGlow(st, Clock))
            {
                DoSignal(Ob, "WARN");
                Wait(Ob, 1.5f);
            }

            if (Active(Op) && Ready(Op) && Stage1Logic.CanPullLever(st))
            {
                string word = st.received[Op];
                int side = -1;
                for (int s = 0; s < 2; s++) if (Stage1Logic.LeverLabel(st, st.fork, s) == word) side = s;
                if (side < 0)
                {
                    // 라벨과 맞는 단어가 아니면 다시 요청한다
                    DoSignal(Op, "ASK");
                    Wait(Op, 3f);
                }
                else
                {
                    Do(Op, Cmd.Make("Lever", side));
                    Wait(Op, 2f);
                }
            }
        }

        // ------------------------------------------------------------------ Stage 2

        private void Stage2(StageState st)
        {
            if (st.step == 0)
            {
                SendWord(Na, st.word, Op);

                // 조작자: 모드 맞추기, 잡기/놓기(파도 회피)
                if (Active(Op) && Ready(Op) && _g.Near(Op, "console", 2f))
                {
                    if (st.received[Op] == st.word && st.consoleMode != st.requiredMode)
                    {
                        Do(Op, Cmd.Make("Console", st.requiredMode));
                        Wait(Op, 0.8f);
                    }
                    else if (st.consoleMode == st.requiredMode)
                    {
                        float w = Stage2Logic.WavePhase(st, Clock);
                        if (st.holding && w >= Stage2Logic.WavePeriod - 1.6f) { Do(Op, Cmd.Make("Hold", 0)); Wait(Op, 0.5f); }
                        else if (!st.holding && w < Stage2Logic.WavePeriod - 3.5f && Clock >= st.knockUntil) { Do(Op, Cmd.Make("Hold", 1)); Wait(Op, 0.5f); }
                    }
                }

                // 통역: 조건이 갖춰지고 물살이 약할 때 GO → 바로 당김
                bool conditions = st.holding && _g.MeterA >= 65f && st.consoleMode == st.requiredMode;
                // 관측자가 GO를 받고 당길 때(약 1초 뒤)에도 물살이 약하도록 미리 내다본다
                if (Active(In) && Ready(In) && !_pendingPullIn && conditions
                    && Stage2Logic.Current(Clock + 1.0f) < 30f && Stage2Logic.Current(Clock + 0.4f) < 34f
                    && Clock - st.pullClock[In] > 3f && _g.Near(In, "wheelB", 2f))
                {
                    DoSignal(In, "GO");
                    _pendingPullIn = true;
                    Wait(In, 0.35f);
                }
                else if (Active(In) && _pendingPullIn && Ready(In))
                {
                    Do(In, Cmd.Make("Pull"));
                    _pendingPullIn = false;
                    Wait(In, 2f);
                }

                // 관측자: GO 신호를 받으면 잠깐 뒤에 당긴다(동기화 대기)
                if (Active(Ob) && Ready(Ob) && Clock - _goClock > 0.4f && Clock - _goClock < 1.2f
                    && Clock - st.pullClock[Ob] > 3f && _g.Near(Ob, "wheelA", 2f))
                {
                    Do(Ob, Cmd.Make("Pull"));
                    Wait(Ob, 2f);
                }
            }
            else
            {
                // 내비게이터: 물이 안 차는 쪽으로 방향 신호
                if (Active(Na) && Clock - _lastDirSignal > 6f)
                {
                    _lastDirSignal = Clock;
                    DoSignal(Na, st.exitUp ? "U" : "D");
                }
            }
        }

        // ------------------------------------------------------------------ Stage 3

        private void Stage3(StageState st)
        {
            int start = StageMaps.Stage3StartSector;
            // 표시: 관측자(초록), 내비(구조 안전), 통역(감지기 위험)
            if (Active(Ob) && Ready(Ob) && _obsIdx < Stage3Logic.Sectors)
            {
                int i = _obsIdx++;
                if (i != start && st.gas[i] == 0 && !st.markObs[i]) { Do(Ob, Cmd.Make("Mark", i)); Wait(Ob, 0.8f); }
            }
            if (Active(Na) && Ready(Na) && _navIdx < Stage3Logic.Sectors)
            {
                int i = _navIdx++;
                if (i != start && !st.blocked[i] && !st.markNav[i]) { Do(Na, Cmd.Make("Mark", i)); Wait(Na, 0.8f); }
            }
            if (Active(In) && Ready(In) && _intIdx < Stage3Logic.Sectors)
            {
                int i = _intIdx++;
                if (i != start && st.ppm[i] >= Stage3Logic.PpmDanger && !st.markInt[i]) { Do(In, Cmd.Make("Mark", i)); Wait(In, 1.1f); }
            }

            // 확인 패널: 자기 패널 앞에 서 있으면 누른다
            TryConfirm(st, Ob, "confObs", st.confirmObs, st.confirmObsSector);
            TryConfirm(st, Na, "confNav", st.confirmNav, st.confirmNavSector);
        }

        private void TryConfirm(StageState st, int slot, string propPrefix, bool confirmed, int confirmedSector)
        {
            if (!Active(slot) || !Ready(slot) || _g.Map == null) return;
            var p = _g.Pos(slot);
            if (p == null) return;
            int s = _g.Map.SectorAt(p.Value);
            if (s < 0 || s == StageMaps.Stage3StartSector) return;
            if (confirmed && confirmedSector == s) return;
            if (!_g.Near(slot, propPrefix + s, 1.8f)) return;
            Do(slot, Cmd.Make("Confirm"));
            Wait(slot, 2f);
        }

        // ------------------------------------------------------------------ Stage 4

        private void Stage4(StageState st)
        {
            // 조작자: 암호 전송, 방향 신호, 불 경고
            if (Active(Op))
            {
                if (Ready(Op) && st.received[1].Length == 0 && st.inflight.Count == 0) SendWord(Op, st.word, 1);
                else if (Clock - _lastDirSignal > 7f)
                {
                    _lastDirSignal = Clock;
                    for (int s = 1; s <= 3; s++)
                    {
                        if (st.device[s]) continue;
                        var p = _g.Pos(s);
                        var dev = _g.Map != null ? _g.Map.Prop("dev" + s) : null;
                        if (p == null || dev == null) continue;
                        Vector2 d = dev.pos - p.Value;
                        string id = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x > 0 ? "R" : "L") : (d.y > 0 ? "U" : "D");
                        DoSignal(Op, id, s);
                        break; // 한 번에 한 명씩
                    }
                }
                if (Clock - _lastWarn > 3f)
                {
                    for (int s = 1; s <= 3; s++)
                    {
                        var p = _g.Pos(s);
                        if (p == null) continue;
                        foreach (var f in st.fires)
                        {
                            if (Vector2.Distance(p.Value, f.V) < 2.6f) { _lastWarn = Clock; DoSignal(Op, "WARN", s); break; }
                        }
                        if (_lastWarn == Clock) break;
                    }
                }
            }

            // 친구 봇: 장치 앞이면 작동, 키패드 앞이면(모두 모였을 때) 암호 입력
            bool allNearPads = true;
            for (int s = 1; s <= 3; s++) if (!_g.Near(s, "pad" + s, 2f)) { allNearPads = false; break; }
            for (int s = 1; s <= 3; s++)
            {
                if (!Active(s) || !Ready(s)) continue;
                if (!st.device[s] && _g.Near(s, "dev" + s, 2f)) { Do(s, Cmd.Make("Operate")); Wait(s, 1f); continue; }
                if (st.devicesDone && !st.codeDone && allNearPads && st.received[s].Length >= st.word.Length && st.codeClock[s] < -90f)
                {
                    int n = st.keypad[s].Length;
                    if (n < st.received[s].Length)
                    {
                        Do(s, Cmd.Make("Key", 0, 0, st.received[s][n].ToString()));
                        Wait(s, 0.45f);
                    }
                }
            }
        }
    }
}
