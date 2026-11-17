using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 조작자 자리를 AI 가 맡았을 때(온라인에서 사람이 모자란 방): 사람 동료가 퍼즐 화면을 열고 자기 단서를 신호 카드로 보내면
    /// AI 조작자가 그 신호를 받아 퍼즐을 푼다. 사람인 동료(조작자 제외) 모두가 퍼즐을 연 뒤 신호를 한 번 이상 보내야 풀린다.
    /// </summary>
    public static class Story1BotOperator
    {
        private static int _puzzle = -1;
        private static float _openAt, _readyAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _puzzle = -1; _readyAt = -1f; }

        public static bool Active => !Story1Role.Solo && Story1Role.IsBot((int)GameRole.Operator) && Story1Role.Current != (int)GameRole.Operator;

        public static void Opened(int puzzleId) { _puzzle = puzzleId; _openAt = Time.time; _readyAt = -1f; }

        /// <summary>0 = 해당 없음, 1 = 신호를 기다리는 중(waiting 에 아직 안 보낸 역할 이름), 2 = 풀 차례.</summary>
        public static int Tick(int puzzleId, out string waiting)
        {
            waiting = "";
            if (!Active) return 0;
            if (_puzzle != puzzleId) Opened(puzzleId);
            int missing = 0;
            for (int r = 0; r < Roles.Count; r++)
            {
                if (r == (int)GameRole.Operator || Story1Role.IsBot(r)) continue;
                bool sent = false;
                foreach (var e in Story1Net.Feed) if (e.slot == r && e.card >= 0 && e.t >= _openAt) { sent = true; break; }
                if (!sent) { missing++; waiting += (waiting.Length > 0 ? ", " : "") + Roles.Name(r); }
            }
            if (missing > 0) { _readyAt = -1f; return 1; }
            if (_readyAt < 0f) _readyAt = Time.time + 1.5f;
            if (Time.time < _readyAt) return 1;
            // 사람 동료가 둘 이상이면 가장 앞 번호의 한 명만 풀이를 보낸다(같은 퍼즐이 두 번 세어지지 않게). 나머지는 그 결과를 받는다.
            int leader = -1;
            for (int r = 0; r < Roles.Count; r++) if (r != (int)GameRole.Operator && !Story1Role.IsBot(r)) { leader = r; break; }
            return leader == Story1Role.Current ? 2 : 1;
        }
    }
}
