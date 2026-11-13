using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>E 키 상호작용의 문맥 판단: 지금 내 캐릭터가 서 있는 곳에서 할 수 있는 일과 그 명령.</summary>
    public static class StageInteractions
    {
        public const float Reach = 1.9f;

        public struct Prompt
        {
            public string text;   // HUD에 표시할 안내(없으면 빈 문자열)
            public Cmd cmd;       // E를 눌렀을 때 보낼 명령(없으면 null)
        }

        private static bool Near(MapData map, Vector2 pos, string propId, float r = Reach)
        {
            var p = map.Prop(propId);
            return p != null && Vector2.Distance(pos, p.pos) <= r;
        }

        public static Prompt Get(int slot, Vector2 pos)
        {
            var result = new Prompt { text = "", cmd = null };
            if (!Session.InStage || Session.Map == null) return result;
            var st = Session.St;
            var map = Session.Map;
            if (st.cleared || st.failed) return result;
            const int Op = 0, In = 1, Na = 2, Ob = 3;

            switch (st.stage)
            {
                case 1:
                    if (st.step == 0 && slot == Op)
                    {
                        for (int side = 0; side < 2; side++)
                        {
                            string id = (side == 0 ? "leverL" : "leverR") + st.fork;
                            if (Near(map, pos, id))
                            {
                                if (Stage1Logic.CanPullLever(st))
                                {
                                    result.text = $"E: [{Stage1Logic.LeverLabel(st, st.fork, side)}] 레버 당기기";
                                    result.cmd = Cmd.Make("Lever", side);
                                }
                                else result.text = st.signalCut ? "신호 차단 중 — 잠시 후" : $"[{Stage1Logic.LeverLabel(st, st.fork, side)}] 레버 — 글자 2개가 도착하면 당길 수 있어요";
                                return result;
                            }
                        }
                    }
                    if (st.step == 3) result.text = "SPACE 연타! 네 명이 함께";
                    break;

                case 2:
                    if (st.step == 0)
                    {
                        if (slot == Op && Near(map, pos, "console", 2.0f))
                        {
                            result.text = st.holding ? "E: 콘솔 놓기 (물살 피하기)" : "E: 콘솔 붙잡기 (방벽 사수)";
                            result.cmd = Cmd.Make("Hold", st.holding ? 0 : 1);
                            return result;
                        }
                        if (slot == Ob && Near(map, pos, "wheelA", 2.0f)) { result.text = "E: 수문 A 당기기 (GO 신호에 맞춰)"; result.cmd = Cmd.Make("Pull"); return result; }
                        if (slot == In && Near(map, pos, "wheelB", 2.0f)) { result.text = "E: 수문 B 당기기 (물살이 약할 때)"; result.cmd = Cmd.Make("Pull"); return result; }
                    }
                    break;

                case 3:
                    if (slot == Ob || slot == Na)
                    {
                        int sector = map.SectorAt(pos);
                        if (sector >= 0 && sector != StageMaps.Stage3StartSector)
                        {
                            string id = (slot == Ob ? "confObs" : "confNav") + sector;
                            if (Near(map, pos, id))
                            {
                                bool done = slot == Ob ? st.confirmObs && st.confirmObsSector == sector : st.confirmNav && st.confirmNavSector == sector;
                                result.text = done ? "확인됨 — 전원이 원 안에 들어오면 문이 열린다" : $"E: {StageMaps.SectorName(sector)} 구역 확인 패널 누르기";
                                if (!done) result.cmd = Cmd.Make("Confirm");
                                return result;
                            }
                        }
                    }
                    break;

                case 4:
                    if (slot >= 1 && slot <= 3)
                    {
                        if (!st.device[slot] && Near(map, pos, "dev" + slot, 2.0f))
                        {
                            result.text = $"E: {Stage4Logic.DeviceNames[slot]} 작동";
                            result.cmd = Cmd.Make("Operate");
                            return result;
                        }
                        if (!st.codeDone && Near(map, pos, "pad" + slot, 2.0f))
                        {
                            result.text = "키패드 — 왼쪽 글자 카드로 암호 입력";
                            return result;
                        }
                    }
                    break;
            }
            return result;
        }

        public static bool NearKeypad(int slot, Vector2 pos)
        {
            var map = Session.Map;
            if (map == null || slot < 1 || slot > 3) return false;
            return Near(map, pos, "pad" + slot, 2.0f);
        }
    }
}
