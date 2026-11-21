using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// Story1 에서 "지금 내가 어느 역할로 보고 있는가". 온라인이면 내 슬롯(= 역할), 혼자 테스트할 때는 조작자로 시작해
    /// Tab 으로 네 역할을 돌려 보며 모든 단서를 확인할 수 있다(개발용).
    /// </summary>
    public static class Story1Role
    {
        private static int _solo = (int)GameRole.Operator;

        public static bool Solo => !(Session.Connected && Session.MySlot >= 0);
        public static int Current => Solo ? _solo : Session.MySlot;
        public static string Name => Roles.Name(Current);

        /// <summary>이 역할을 AI(봇)가 맡고 있는가. 에디터 단독 테스트는 "내가 보는 역할 말고 전부", 세션에서는 봇 슬롯.</summary>
        public static bool IsBot(int role)
        {
            if (!Roles.Valid(role)) return false;
            if (Solo) return role != Current;
            if (role == Session.MySlot) return false;
            var p = Session.State.players[role];
            return p != null && p.present && p.isBot;
        }

        /// <summary>AI 동료가 도와주는 중인가(최종 퍼즐 등에서 AI 가 대신 밟는 조건). 단독 테스트는 조작자 시점일 때만.</summary>
        public static bool Helped
        {
            get
            {
                if (Solo) return Current == (int)GameRole.Operator;
                for (int r = 0; r < Roles.Count; r++) if (IsBot(r)) return true;
                return false;
            }
        }

        public static void Cycle() { if (Solo) _solo = (_solo + 1) % Roles.Count; }
        public static void Reset() { _solo = (int)GameRole.Operator; }
        /// <summary>온라인이 끊겨 혼자 이어 갈 때, 하던 역할(슬롯)을 그대로 지킨다.</summary>
        public static void Keep(int slot) { if (Roles.Valid(slot)) _solo = slot; }
    }
}
