using Paradox.Game;

namespace Paradox.Client
{
    /// <summary>"지금 내 역할이 할 일" 한 줄 안내. 스테이지·단계·역할에 따라 달라진다.</summary>
    public static class StageGuide
    {
        private const int Op = 0, In = 1, Na = 2, Ob = 3;

        public static string Instruction(int slot)
        {
            var S = Session.State;
            var st = S.st;
            if (st.failed) return "실패… 호스트가 [다시 시도]를 누르면 재도전합니다.";
            if (st.cleared) return "클리어! 잠시 후 다음으로 넘어갑니다.";

            switch (st.stage)
            {
                case 1: return Stage1(st, slot);
                case 2: return Stage2(st, slot);
                case 3: return Stage3(st, slot);
                case 4: return Stage4(st, slot);
            }
            return "";
        }

        private static string Stage1(StageState st, int slot)
        {
            switch (st.step)
            {
                case 2: return "이동 중… 다음 지점까지 네 명이 함께 이동합니다.";
                case 3: return "쓰러진 집기가 길을 막았다! 네 명 모두 <b>SPACE 연타</b>. 한 명이라도 멈추면 진행도가 빨리 떨어집니다.";
                case 4: return "로비 도착. <b>각자 자기 색 원</b> 안으로! 네 명이 동시에 들어와야 셔터가 열립니다.";
            }
            switch (slot)
            {
                case Op: return "표지판은 <b>???</b>로 보입니다. 릴레이로 글자 <b>2개</b>가 도착하면 단어를 유추해 <b>같은 라벨의 레버</b>를 고르고 확정. 글자가 이상하면 <b>? (다시 요청)</b>.";
                case In: return $"당신만 표지판을 읽습니다: <b>{st.word}</b>. 왼쪽 글자 카드에서 글자를 순서대로 고른 뒤 <b>[보내기]</b> — 글자가 <b>내비게이터</b>에게 날아갑니다.";
                case Na: return "통역이 보낸 글자가 도착하면 <b>[그대로 전달]</b>로 조작자에게 넘기세요. 뜻은 몰라도 <b>모양 그대로, 순서대로</b>!";
                case Ob: return "<b>정전 경고등</b>을 지켜보세요. 글자가 전달 중일 때 경고등이 붉게 최고조로 깜빡이면 <b>⚠ 경고</b>! 놓치면 글자가 소실됩니다.";
            }
            return "";
        }

        private static string Stage2(StageState st, int slot)
        {
            if (st.step == 0)
            {
                switch (slot)
                {
                    case Op: return "내비게이터가 보낸 글자로 <b>펌프 모드</b>를 알아내 콘솔 버튼을 맞추고, 콘솔 앞에서 <b>E</b>로 붙잡아 방벽을 <b>60 이상</b> 유지. <b>물살 경고</b>가 오면 E로 놓고 피하세요.";
                    case Na: return $"표지판: 펌프 모드 <b>{st.word}</b>. 글자 카드를 고른 뒤 <b>[보내기]</b>로 조작자에게 알리세요. 그다음 미니맵에서 <b>물이 안 차는 통로</b>를 확인해 두세요.";
                    case In: return "<b>물살 미터</b>가 낮아질 때 <b>GO</b> 신호! 그리고 오른쪽 수문 핸들 앞에서 <b>E</b>로 당기세요. 관측자와 <b>1.5초 안</b>에 함께.";
                    case Ob: return "왼쪽 수문 핸들 앞에서 대기. 통역의 <b>GO</b> 신호가 오면 바로 <b>E</b>로 당기세요 (동기화 대기).";
                }
            }
            else
            {
                if (slot == Na) return "미니맵의 <b>파란 갈래</b>는 침수 중! ← → ↑ ↓ / GO / STOP 신호로 안내하고, 상부 통로의 자기 원으로.";
                return "내비게이터의 <b>방향 신호</b>를 따라 물이 안 차는 통로로! 상부 통로의 <b>자기 색 원</b> 안으로. 잘못 들어가면 시간이 깎입니다.";
            }
            return "";
        }

        private static string Stage3(StageState st, int slot)
        {
            switch (slot)
            {
                case Ob: return "<b>고글</b>로 구역 색이 보입니다. 경고문: <b>초록만 안전</b>. 보드에 초록 구역을 ✓ 표시하고, 정답 구역에서 <b>확인 패널(E)</b>.";
                case Na: return "<b>미니맵</b>에 붕괴(막다른) 구역이 보입니다. 보드에 <b>구조가 안전한</b> 구역을 ✓ 표시하고, 정답 구역에서 <b>확인 패널(E)</b>.";
                case In: return "<b>감지기</b> 수치는 언어와 무관합니다! 구역을 돌며 <b>50 이상</b>인 곳을 보드에 ✗(위험)으로 표시하세요.";
                case Op: return "친구들의 표시가 <b>✓✓이고 ✗가 없는</b> 구역으로 모두를 이끌고 각자 원 안으로. 노랑·빨강 구역은 시간이 깎입니다.";
            }
            return "";
        }

        private static string Stage4(StageState st, int slot)
        {
            if (slot == Op)
            {
                if (!st.devicesDone) return $"당신만 방송·지도를 봅니다. <b>채팅 불가!</b> 대상을 골라 ← → ↑ ↓로 친구를 장치까지 안내, 불 근처면 <b>⚠</b>. 암호 <b>{st.word}</b>는 글자 카드를 고른 뒤 <b>[보내기]</b>.";
                if (!st.codeDone) return $"동쪽 셔터로 모두를 안내하세요. 세 친구가 키패드에 암호 <b>{st.word}</b>를 <b>4초 안에 동시</b> 입력해야 합니다.";
                return "셔터가 열렸다! 전원 <b>자기 색 원</b> 안으로.";
            }
            string dev = Stage4Logic.DeviceNames[slot];
            if (!st.device[slot]) return $"연기로 지도도 고글도 안 보입니다. 조작자의 <b>방향 신호</b>를 따라 내 장치 <b>{dev}</b>를 찾아 <b>E</b>. ⚠가 오면 불 조심!";
            if (!st.devicesDone) return "내 장치는 끝! 다른 친구들이 장치를 켤 때까지 조작자의 신호를 따르세요.";
            if (!st.codeDone) return "동쪽 셔터 옆 <b>내 키패드</b> 앞에서, 조작자가 보낸 글자를 <b>같은 모양</b>으로 입력하세요. 세 명이 4초 안에!";
            return "셔터가 열렸다! 전원 <b>자기 색 원</b> 안으로.";
        }
    }
}
