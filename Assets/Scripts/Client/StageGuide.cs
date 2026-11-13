using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// "지금 할 일" 한 줄 안내(순수 함수). 역할 × 단계 × 상황에 따라 바뀐다.
    /// 관문 1 동안은 기획서 온보딩 표의 문구를 그대로 쓴다(튜토리얼 성격). 이후는 짧은 방향 안내만.
    /// </summary>
    public static class StageGuide
    {
        private const int Op = 0, In = 1, Na = 2, Ob = 3;

        /// <summary>A3: 핑 없이 이만큼 지나면 기다리는 세 명에게도 방 이름을 알려준다(서버의 pingNudged 토스트와 같은 시각).</summary>
        public const float PingNudgeAfter = 40f;

        // 관문 2 리셋(gate2Attempts 증가) 직후 안내용 — 순수 함수지만 "직후"를 알려면 마지막 시도 수를 기억해야 한다
        private static int _seenAttempts;
        private static float _resetAt = -99f;
        private const float ResetHintFor = 12f;

        public static string Instruction(int slot)
        {
            var st = Session.St;
            if (st == null) return "";
            if (st.failed) return "물이 차올랐다… " + HostRetryHint();
            if (st.cleared) return "클리어! 잠시 후 다음으로 넘어가요.";
            if (!Roles.Valid(slot)) return "";

            switch (st.step)
            {
                case 0: return Gate1(st, slot);
                case 1: return Move(st, slot);
                default: return "· 내 색 원을 따라가 안에 머물기 — 네 명이 동시에 들어가야 해요";
            }
        }

        /// <summary>
        /// B5: 실패 화면·안내의 "다시 시도" 문구. 호스트 자신에게는 "친구들이 기다려요 — [다시 시도]",
        /// 나머지에게는 호스트의 이름·역할(모르면 "방장")로 누가 눌러야 하는지 알려준다.
        /// </summary>
        public static string HostRetryHint()
        {
            if (Session.IsHost) return "친구들이 기다려요 — [다시 시도]를 누르면 마지막 관문 앞에서 다시 시작해요.";
            return $"{HostLabel()}이(가) [다시 시도]를 누르면 마지막 관문 앞에서 다시 시작해요.";
        }

        /// <summary>호스트 표기: "이름(역할)" — 슬롯을 모르면 "방장".</summary>
        public static string HostLabel()
        {
            var s = Session.State;
            int h = s != null ? s.hostSlot : -1;
            if (!Roles.Valid(h) || !s.players[h].present) return "방장";
            string name = Session.NameOf(h);
            if (string.IsNullOrEmpty(name)) name = "방장";
            return $"{name}({Roles.Name(h)})";
        }

        /// <summary>A3: 핑 없이 40초가 지났는가 — 서버 플래그(pingNudged, 토스트와 같은 시각)를 우선하고 클록으로 보강한다.</summary>
        private static bool PingNudged(StageState st)
            => st.lastPingRoom < 0 && (st.pingNudged || (st.timerRunning && Session.Clock - st.startClock >= PingNudgeAfter));

        /// <summary>B2: 통역이 보낸 숫자 카드가 방금 틀린 그 숫자인가(서버가 lastWrongDigit 을 카드 갱신 시 −1 로 되돌린다).</summary>
        private static bool WrongCard(StageState st)
            => st.calcFails > 0 && st.numberCard >= 0 && st.lastWrongDigit == st.numberCard;

        /// <summary>관문 2 리셋 직후인가(gate2Attempts 가 늘어난 뒤 ResetHintFor 초, 아직 1라운드 진행 전).</summary>
        private static bool JustReset(StageState st)
        {
            if (st.gate2Attempts != _seenAttempts)
            {
                if (st.gate2Attempts > _seenAttempts) _resetAt = Time.time;
                _seenAttempts = st.gate2Attempts;
            }
            return st.gate2Attempts > 0 && st.round == 0 && Time.time - _resetAt < ResetHintFor;
        }

        private static int MyRoom(int slot)
        {
            var map = Session.Map;
            var p = Session.PosOf(slot);
            if (map == null || p == null) return -1;
            return map.RoomAt(p.Value);
        }

        private static bool PopupOpen => StageScreen.Instance != null && StageScreen.Instance.IsPopupOpen;

        // ---------------------------------------------------------------- 관문 1(온보딩 표 문구)

        /// <summary>담당 오브젝트에 다가가라는 공통 힌트 — 실제 조작 키(E)와 클릭을 함께 안내한다.</summary>
        private const string Approach = "이 오브젝트는 당신에게만 빛나요. 가까이 가서 E를 누르거나 클릭해 보세요.";

        /// <summary>
        /// A3: 우선순위 = 팝업 열림 > 내 방 안(담당 오브젝트가 빛나는 중) > 핑 여부.
        /// 핑 전에 방에 들어가도 "이 오브젝트는 당신에게만 빛나요…"가 나오고, 핑 없이 40초가 지나면 세 명에게 방 이름을 알려준다.
        /// </summary>
        private static string Gate1(StageState st, int slot)
        {
            bool pinged = st.lastPingRoom >= 0;
            bool nudged = PingNudged(st);
            if (slot == Na)
            {
                if (!pinged)
                {
                    if (nudged) return "친구들이 기다려요 — [여기로!] 버튼으로 안내데스크를 알려주세요.";
                    if (st.boardOpened) return "[여기로!] 버튼으로 안내데스크로 친구들을 보내요.";
                    return "지도를 확인해서 미션이 있는 방을 찾고, [여기로!] 버튼으로 친구들에게 알려주세요.";
                }
                if (!st.boardOpened) return "친구들을 보냈어요. 관리실의 침수 현황판(내 담당)도 확인해 두세요.";
                return "침수 현황판을 확인했어요. 관문 1이 풀릴 때까지 도넛으로 팀을 도와주세요.";
            }

            // B2: 방금 틀린 숫자가 그대로 카드로 와 있으면(홀짝 전달 착오) 팝업 안팎 어디서든 먼저 알려준다
            if (slot == Op && WrongCard(st) && (PopupOpen || MyRoom(slot) == 1))
                return "그 숫자는 틀렸어요 — 홀짝을 다시 확인하고 통역에게 말하세요.";

            // 1) 팝업이 열려 있으면 그 안에서 할 일
            if (PopupOpen)
            {
                switch (slot)
                {
                    case Ob: return "지금 켜진 색을 확인하고, 색상 버튼으로 통역에게 알려주세요.";
                    case Op: return "결과가 홀수인지 짝수인지 확인해서 통역에게 알려주세요. (이 채팅은 통역만 읽을 수 있어요)";
                    case In: return "매뉴얼을 보고 정답 숫자를 찾아 조작자에게 카드로 전달하세요. 당신만 모두의 채팅을 읽고 쓸 수 있어요.";
                }
                return "";
            }

            // 2) 내 방 안(담당 오브젝트가 빛나는 중) — 핑 전이어도 조작을 안내한다
            int room = MyRoom(slot);
            if (room == 1)
            {
                switch (slot)
                {
                    case Ob:
                        // C3: "색이 바뀌면 다시 신고해요"는 관문 1에서는 거짓 — 뺀다
                        if (st.colorReport >= 0) return "신고 완료! 통역이 숫자를 찾을 때까지 기다려요.";
                        return Approach;
                    case Op:
                        if (st.numberCard >= 0) return $"통역이 보낸 숫자 {st.numberCard} — 제어반에 입력하고 확인!";
                        return Approach;
                    case In:
                        if (st.numberCard >= 0) return "카드를 보냈어요. 조작자가 입력하면 문이 열려요. 틀렸다면 다시 보내요.";
                        return Approach;
                }
                return "";
            }

            // 3) 방 밖: 핑을 기다리거나 따라간다
            if (!pinged) return nudged ? "안내데스크 쪽으로 가 보세요. 방에 들어가면 내 담당 오브젝트가 빛나요." : "내비게이터의 안내를 기다려주세요.";
            return "안내데스크로 이동하세요. 방에 들어가면 내 담당 오브젝트가 빛나요.";
        }

        // ---------------------------------------------------------------- 이동 구간 + 관문 2

        /// <summary>A1: 관문 1 뒤에는 튜토리얼 어조 대신 "· " 접두의 짧은 한 줄 지시(이동 구간 → 관문 2 → 판독).</summary>
        private static string Move(StageState st, int slot)
        {
            int room = MyRoom(slot);
            bool reset = JustReset(st);
            int r = Mathf.Clamp(st.round + 1, 1, 4);
            if (slot == Na)
            {
                if (room != 3) return st.boardOpened
                    ? "· 팀을 나침반 모자이크방으로 안내 — 배전함은 내 담당"
                    : "· 관리실 현황판 확인 → 팀을 나침반 모자이크방으로";
                if (st.round >= 4) return "· 배전함 판독 — 안전/위험";
                return $"· 배전함 앞에서 대기 (라운드 {r}/4) — 위험 구역에 친구가 있으면 도넛";
            }
            if (room != 3) return "· 내비게이터를 따라 나침반 모자이크방으로 (문이 열렸어요)";
            if (st.round >= 4) return "· 4라운드 완료 — 내비게이터 판독 기다리기";
            switch (slot)
            {
                case Op:
                    if (st.dirCard >= 0) return $"· 방향 카드 {Comms.DirGlyph(st.dirCard)} — 레버를 그 방향으로";
                    if (reset) return "· 새 패턴 — 점자를 다시 보내세요";
                    return "· 점자 번호를 채팅 → 통역 카드 기다리기 → 레버";
                case In:
                    return "· 조작자 채팅 + 관측자 색 → 벽화 표 → 방향 카드";
                case Ob:
                    if (reset && st.colorReport < 0) return "· 색을 다시 신고";
                    if (st.colorReport < 0) return $"· 라운드 {r} 색 신고 — 라운드가 바뀌면(색이 같아도) 다시 신고";
                    return $"· 라운드 {r} 신고 완료 ✓ — 라운드가 바뀌면(색이 같아도) 다시 신고";
            }
            return "";
        }
    }
}
