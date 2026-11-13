using System.Collections.Generic;
using UnityEngine;

namespace Paradox.Game
{
    /// <summary>
    /// 봇의 "손"(서버). 사람이 조종하지 않는 봇 슬롯이 자기 역할의 일을 사람처럼 약간의 지연을 두고 수행한다.
    /// 이동(어디로 걸어갈지)은 호스트 클라이언트의 BotBrain이 맡고, 여기서는 명령만 낸다. 팝업 개념이 없으므로 봇은 근접(2.2)만 하면 명령한다.
    /// 지시 게이팅: 조작자·통역·관측자 봇은 lastPingRoom == 목표 방, 또는 내비의 GO 신호 이후, 또는 단계 시작 25초 후에 움직인다. 내비 봇은 단계 시작 2초 후부터.
    /// 봇 통역의 채팅 해석: 구조화 값(a=1 홀짝 b, a=2 점 비트마스크 b)이 있으면 그것을, 없으면 텍스트 — "짝" 포함 → 짝수, "홀" 포함 → 홀수; "점자" 뒤 숫자 목록을 집합으로 만들어 BrailleDots와 일치하는 숫자를 찾는다.
    /// 못 읽으면 Signal WHERE로 되묻고 6초 뒤 재시도한다. 브리핑 게이트(D1): 봇은 스테이지 시작 1.5초 뒤 자동으로 Brief 를 보낸다.
    /// </summary>
    public class BotDriver
    {
        private const int Op = 0, In = 1, Na = 2, Ob = 3;
        private const float Reach = Stage1Logic.NearReach;
        private const float InstructTimeout = 25f;   // 지시가 없어도 이 시간이 지나면 움직인다
        private const float NavStartDelay = 2f;
        private const float RepeatChatEvery = 20f;   // 조작자 봇이 같은 정보를 다시 말하는 간격
        private const float AskEvery = 6f;           // 통역 봇이 WHERE로 되묻는 간격

        private readonly GameLogic _g;
        private readonly float[] _next = new float[4];

        // 단계 키(바뀌면 단계별 기억을 지운다)
        private int _lastStage = -1, _lastStep = -1, _lastRetries = -1, _lastAttempts = -1;
        private float _phaseStart = -99f;
        private float _goClock = -99f;

        // 라운드 추적(점자 채팅을 기다리는 시간 기준 — 색 신고는 서버가 라운드마다 지우므로 st.colorReport 만 보면 된다)
        private int _lastRound = -1;
        private float _roundStart = -99f;

        // 채팅에서 얻은 정보(봇 통역이 읽은 것)
        private int _parityHint = -1;      // 0 짝 1 홀
        private float _parityClock = -99f;
        private int _dotsDigit = -1;       // 점자 숫자
        private int _dotsRound = -1;       // 그 숫자가 속한 라운드
        private float _dotsClock = -99f;
        private bool _unreadable;          // 마지막 조작자 채팅을 해석하지 못했다
        private float _lastAsk = -99f;

        // 역할별 진행 기억
        private bool _navPingedGate1, _navPingedGate2, _navBoardTried;
        private int _obGate1Sent = -1;
        private int _obRoundSent = -1;
        private float _opChatClock = -99f;
        private int _opDotsRound = -1;
        private int _opConfirmedCard = -1;
        private int _opFailsSeen;
        private int _inCardSent = -1;
        private int _inDirRound = -1;
        private float _inWaitSince = -99f;

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

        private void DoSignal(int slot, string id) => Do(slot, Cmd.Make("Signal", 0, -1, id));

        // ------------------------------------------------------------------ 훅

        /// <summary>휠 신호 훅: 내비게이터의 GO 이후에는 지시를 받은 것으로 본다.</summary>
        public void OnSignal(int from, string id)
        {
            if (from == Na && id == "GO") _goClock = Clock;
        }

        /// <summary>채팅 훅: 봇 통역이 조작자의 채팅을 읽는다. 구조화 값 a/b(팝업 빠른 문구)가 있으면 우선, 없으면 텍스트 파서(사람의 자유 문장도 최대한 해석).</summary>
        public void OnChat(int slot, string text, int a = 0, int b = 0)
        {
            if (slot != Op || string.IsNullOrEmpty(text)) return;
            bool understood = false;

            if (a == 1) { _parityHint = b != 0 ? 1 : 0; _parityClock = Clock; understood = true; }
            else if (text.Contains("짝")) { _parityHint = 0; _parityClock = Clock; understood = true; }
            else if (text.Contains("홀")) { _parityHint = 1; _parityClock = Clock; understood = true; }

            int digit = a == 2 ? DigitOfMask(b) : ParseDots(text);
            if (digit >= 0)
            {
                _dotsDigit = digit;
                _dotsRound = RoundKey(St);
                _dotsClock = Clock;
                understood = true;
            }

            _unreadable = !understood;
        }

        /// <summary>"점자 1,2,4" / "1 2 4" / "점 1번 2번 4번" 같은 문장에서 점 번호 집합을 뽑아 점자 숫자로 바꾼다. 못 읽으면 -1.</summary>
        public static int ParseDots(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            int at = text.IndexOf("점자");
            string body = at >= 0 ? text.Substring(at + 2) : text;
            var dots = new HashSet<int>();
            bool sawDigit = false;
            foreach (char ch in body)
            {
                if (ch < '0' || ch > '9') continue;
                sawDigit = true;
                int d = ch - '0';
                if (d < 1 || d > 6) return -1;   // 점 번호는 1~6뿐
                dots.Add(d);
            }
            if (!sawDigit) return -1;
            return Stage1Logic.DigitOfDots(dots);
        }

        /// <summary>점 비트마스크(1&lt;&lt;(점-1)) → 점자 숫자. 없으면 -1.</summary>
        public static int DigitOfMask(int mask)
        {
            if (mask <= 0) return -1;
            var dots = new HashSet<int>();
            for (int d = 1; d <= 6; d++) if ((mask & (1 << (d - 1))) != 0) dots.Add(d);
            return Stage1Logic.DigitOfDots(dots);
        }

        private static int RoundKey(StageState st) => st.round + st.gate2Attempts * 100;

        // ------------------------------------------------------------------ 틱

        public void Tick(float dt)
        {
            var st = St;
            if (st.stage != _lastStage || st.step != _lastStep || st.retries != _lastRetries)
            {
                _lastStage = st.stage; _lastStep = st.step; _lastRetries = st.retries; _lastAttempts = st.gate2Attempts;
                _phaseStart = Clock;
                for (int i = 0; i < 4; i++) _next[i] = Clock + 1.2f + i * 0.4f;
                ResetRoundMemory();
                _navPingedGate1 = _navPingedGate2 = _navBoardTried = false;
                _obGate1Sent = -1;
                _opChatClock = -99f;
                _opConfirmedCard = -1;
                _opFailsSeen = st.calcFails;
                _inCardSent = -1;
                _inWaitSince = -99f;
                _parityHint = -1;
                _goClock = -99f;
            }
            if (st.gate2Attempts != _lastAttempts)
            {
                _lastAttempts = st.gate2Attempts;
                ResetRoundMemory();
            }
            if (st.round != _lastRound)
            {
                _lastRound = st.round;
                _roundStart = Clock;
            }

            // 브리핑 게이트: 봇은 스테이지 시작 1.5초 뒤 자동으로 브리핑을 마친다(호스트가 조종 중인 봇도 포함)
            if (!st.timerRunning && st.briefDone != null && Clock - st.startClock >= Stage1Logic.BotBriefDelay)
                for (int i = 0; i < 4; i++)
                    if (_g.IsBot(i) && i < st.briefDone.Length && !st.briefDone[i]) Do(i, Cmd.Make("Brief"));
            // 타이머가 돌기 전에는 서버가 관문 명령을 거부하므로 봇도 기다린다(거부 토스트가 쌓이지 않게)
            if (!st.timerRunning) return;

            switch (st.step)
            {
                case 0: Gate1(st); break;
                case 1: Gate2(st); break;
                default: break;   // 최종 집결은 이동(BotBrain)만
            }
        }

        private void ResetRoundMemory()
        {
            _lastRound = -1;
            _roundStart = Clock;
            _dotsDigit = -1; _dotsRound = -1;
            _obRoundSent = -1;
            _opDotsRound = -1;
            _inDirRound = -1;
            _unreadable = false;
        }

        /// <summary>조작자·통역·관측자 봇이 움직여도 되는가(내비 지시 게이팅).</summary>
        private bool Instructed(StageState st, int room)
        {
            if (st.lastPingRoom == room) return true;
            if (_goClock >= _phaseStart) return true;
            return Clock - _phaseStart >= InstructTimeout;
        }

        // ------------------------------------------------------------------ 관문 1

        private void Gate1(StageState st)
        {
            // 내비게이터: 현황판 → OpenBoard → Ping 1 + GO
            if (Active(Na) && Ready(Na) && Clock - _phaseStart >= NavStartDelay)
            {
                if (!st.boardOpened && _g.Near(Na, "board", Reach))
                {
                    Do(Na, Cmd.Make("OpenBoard"));
                    _navBoardTried = true;
                    Wait(Na, 1.2f);
                }
                else if (!_navPingedGate1 && (st.boardOpened || Clock - _phaseStart >= 20f))
                {
                    Do(Na, Cmd.Make("Ping", StageMaps.RoomDesk));
                    DoSignal(Na, "GO");
                    _navPingedGate1 = true;
                    Wait(Na, 3f);
                }
            }

            bool go = Instructed(st, StageMaps.RoomDesk);

            // 관측자: 램프 앞 → 2초 뒤 색 신고
            if (go && Active(Ob) && Ready(Ob) && _g.Near(Ob, "lamp", Reach))
            {
                if (_obGate1Sent != st.lampColor || st.colorReport != st.lampColor)
                {
                    if (_obGate1Sent == -1) { _obGate1Sent = -2; Wait(Ob, 2f); }   // 색을 "읽는" 시간(한 번만)
                    else
                    {
                        Do(Ob, Cmd.Make("Color", st.lampColor));
                        _obGate1Sent = st.lampColor;
                        Wait(Ob, 4f);
                    }
                }
            }

            // 조작자: 제어반 앞 → 홀짝 채팅 → 숫자 카드가 오면 입력·확인
            if (go && Active(Op) && Ready(Op) && _g.Near(Op, "panel", Reach))
            {
                if (st.calcFails != _opFailsSeen)
                {
                    // 방금 틀렸다 — 같은 카드를 다시 넣지 않고 홀짝을 다시 알린다
                    _opFailsSeen = st.calcFails;
                    _opConfirmedCard = st.numberCard;
                    _opChatClock = -99f;
                    Wait(Op, 1.5f);
                }
                else if (Clock - _opChatClock >= RepeatChatEvery && (st.numberCard < 0 || st.numberCard == _opConfirmedCard))
                {
                    Do(Op, Cmd.Make("Chat", 0, 0, Stage1Logic.Parity(st.calcResult) == 0 ? "결과 짝수야" : "결과 홀수야"));
                    _opChatClock = Clock;
                    Wait(Op, 1.5f);
                }
                else if (st.numberCard >= 0 && st.numberCard != _opConfirmedCard)
                {
                    string want = st.numberCard.ToString();
                    if (st.calcInput != want) { Do(Op, Cmd.Make("Digit", st.numberCard)); Wait(Op, 0.8f); }
                    else { _opConfirmedCard = st.numberCard; Do(Op, Cmd.Make("DigitConfirm")); Wait(Op, 2f); }
                }
            }

            // 통역: 벽보 앞 → 색 + 홀짝이 모이면 숫자 카드
            if (go && Active(In) && Ready(In) && _g.Near(In, "poster", Reach))
            {
                if (_inWaitSince < 0f) _inWaitSince = Clock;
                if (st.colorReport >= 0 && _parityHint >= 0)
                {
                    int card = Stage1Logic.Table1[_parityHint][st.colorReport];
                    if (_inCardSent != card || st.numberCard != card)
                    {
                        Do(In, Cmd.Make("Card", card));
                        _inCardSent = card;
                        Wait(In, 3f);
                    }
                }
                else if ((_unreadable || Clock - _inWaitSince > 8f) && Clock - _lastAsk >= AskEvery)
                {
                    DoSignal(In, "WHERE");
                    _lastAsk = Clock;
                    _unreadable = false;
                    Wait(In, 1f);
                }
            }
        }

        // ------------------------------------------------------------------ 이동 구간 + 관문 2

        private void Gate2(StageState st)
        {
            // 내비게이터: Ping 3 + GO → 배전함 앞 → 4라운드가 끝나면 판독
            if (Active(Na) && Ready(Na) && Clock - _phaseStart >= NavStartDelay)
            {
                if (!st.boardOpened && !_navBoardTried && _g.Near(Na, "board", Reach))
                {
                    Do(Na, Cmd.Make("OpenBoard"));
                    _navBoardTried = true;
                    Wait(Na, 1.2f);
                }
                else if (!_navPingedGate2)
                {
                    Do(Na, Cmd.Make("Ping", StageMaps.RoomMosaic));
                    DoSignal(Na, "GO");
                    _navPingedGate2 = true;
                    Wait(Na, 3f);
                }
                else if (st.round >= Stage1Logic.Rounds && !st.gate2Done && _g.Near(Na, "junction", Reach))
                {
                    if (Stage1Logic.PathEnd(st) == st.ladderSafe)
                    {
                        Do(Na, Cmd.Make("Judge", 1));
                        Wait(Na, 3f);
                    }
                    else
                    {
                        // 위험 → 아니오 몸짓 카드는 서버가 Judge 0 을 받아들이며 대신 발행한다(D-A5) — 봇이 따로 보내면 두 번 나온다
                        Do(Na, Cmd.Make("Judge", 0));
                        Wait(Na, 3f);
                    }
                }
            }

            if (st.round >= Stage1Logic.Rounds || st.gate2Done) return;
            bool go = Instructed(st, StageMaps.RoomMosaic);
            int key = RoundKey(st);

            // 관측자: 라운드가 바뀔 때마다 색상등 신고
            if (go && Active(Ob) && Ready(Ob) && _g.Near(Ob, "colorlamp", Reach))
            {
                if (_obRoundSent != key)
                {
                    Do(Ob, Cmd.Make("Color", st.lampColor2[st.round]));
                    _obRoundSent = key;
                    Wait(Ob, 3f);
                }
            }

            // 조작자: 라운드마다 점자 채팅 → 방향 카드가 오면 레버
            if (go && Active(Op) && Ready(Op) && _g.Near(Op, "lever", Reach))
            {
                if (st.dirCard >= 0)
                {
                    Do(Op, Cmd.Make("Lever", st.dirCard));
                    Wait(Op, 2f);
                }
                else if (_opDotsRound != key || Clock - _opChatClock >= RepeatChatEvery)
                {
                    Do(Op, Cmd.Make("Chat", 0, 0, Stage1Logic.DotsChat(st.brailleDigit[st.round])));
                    _opDotsRound = key;
                    _opChatClock = Clock;
                    Wait(Op, 1.5f);
                }
            }

            // 통역: 벽화 앞 → 이번 라운드의 색 + 점자 숫자가 모이면 방향 카드
            if (go && Active(In) && Ready(In) && _g.Near(In, "mural", Reach))
            {
                bool colorFresh = st.colorReport >= 0;   // 라운드·관문이 바뀌면 서버가 colorReport 를 지운다 — 남아 있으면 이번 라운드 신고다
                bool dotsFresh = _dotsDigit >= 0 && _dotsRound == key;
                if (colorFresh && dotsFresh && _inDirRound != key && st.dirCard < 0)
                {
                    int row = Stage1Logic.Row(_dotsDigit);
                    if (row >= 0)
                    {
                        Do(In, Cmd.Make("DirCard", Stage1Logic.Table2[row][st.colorReport]));
                        _inDirRound = key;
                        Wait(In, 3f);
                    }
                }
                else if ((_unreadable || (!dotsFresh && Clock - _roundStart > 8f)) && Clock - _lastAsk >= AskEvery)
                {
                    DoSignal(In, "WHERE");
                    _lastAsk = Clock;
                    _unreadable = false;
                    Wait(In, 1f);
                }
            }
        }
    }
}
