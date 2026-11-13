using Paradox.Game;
using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 상호작용 판정(클라이언트 표시용). 어떤 오브젝트가 내 화면에서 발광하는지, E/클릭으로 어떤 팝업을 열 수 있는지 결정한다.
    /// 발광 규칙(명세 5-3): 담당자 && 같은 방 && 해당 미션 미완료. 서버는 별도로 Near(slot, id, 2.2)로 근접만 검사한다.
    /// </summary>
    public static class StageInteractions
    {
        /// <summary>E 키 상호작용 반경(월드 유닛).</summary>
        public const float Reach = 1.6f;

        /// <summary>마우스 클릭으로 오브젝트를 고를 때 허용하는 오브젝트 중심과의 거리.</summary>
        public const float ClickRadius = 0.9f;

        public struct Prompt
        {
            public string text;     // HUD 하단에 보여줄 안내(없으면 빈 문자열)
            public Cmd cmd;         // E를 눌렀을 때 서버로 보낼 명령(없으면 null)
            public string popupId;  // E를 눌렀을 때 열 팝업의 오브젝트 id(없으면 null) — StageScreen.Instance.OpenPopup(popupId)
        }

        /// <summary>발광 대상이 될 수 있는 담당 오브젝트 id(순서 = 관문 순).</summary>
        public static readonly string[] MissionProps = { "panel", "poster", "lamp", "board", "lever", "mural", "colorlamp", "junction" };

        /// <summary>오브젝트 한국어 이름(라벨·안내에 쓴다).</summary>
        public static string PropName(string id)
        {
            switch (id)
            {
                case "panel": return "비상 제어반";
                case "poster": return "코팅된 벽보";
                case "lamp": return "경고등 패널";
                case "board": return "침수 현황판";
                case "lever": return "방향 레버반";
                case "mural": return "나침반 벽화";
                case "colorlamp": return "색상 표시등";
                case "junction": return "배전함";
                case "fountain": return "분수";
                case "door3": return "모자이크방 문";
                default: return id;
            }
        }

        /// <summary>"[E] 제어반 조사" 같은 안내 문구의 동사 부분.</summary>
        public static string PromptText(string id)
        {
            switch (id)
            {
                case "panel": return "[E] 제어반 조사";
                case "poster": return "[E] 벽보 읽기";
                case "lamp": return "[E] 경고등 확인";
                case "board": return "[E] 침수 현황판 보기";
                case "lever": return "[E] 레버반 조사";
                case "mural": return "[E] 벽화 매뉴얼 읽기";
                case "colorlamp": return "[E] 색상등 확인";
                case "junction": return "[E] 배전함 판독";
                default: return "[E] " + PropName(id) + " 조사";
            }
        }

        /// <summary>담당 오브젝트가 아닌 소품인가(발광·팝업 없음).</summary>
        public static bool IsDecor(string id) => id == "fountain" || id == "door3" || id == "door3b" || id == "shutter" || (id != null && id.StartsWith("sign"));

        /// <summary>이 오브젝트가 맡은 미션이 이미 끝났는가(끝나면 발광이 꺼진다).</summary>
        public static bool MissionDone(StageState st, string id)
        {
            if (st == null) return true;
            switch (id)
            {
                case "panel":
                case "poster":
                case "lamp":
                    return st.gate1Done;
                case "board":
                    return st.step >= 2;              // 최종 집결부터는 현황판이 필요 없다
                case "lever":
                case "mural":
                case "colorlamp":
                case "junction":
                    return st.gate2Done;
                default:
                    return true;
            }
        }

        /// <summary>발광 규칙: prop.slot == 내 슬롯 && 내가 그 오브젝트의 방 안 && 미션 미완료.</summary>
        public static bool IsGlowing(MapProp p, int slot, Vector2 myPos)
        {
            if (p == null || !Session.InStage) return false;
            var map = Session.Map;
            var st = Session.St;
            if (map == null || st == null || st.cleared || st.failed) return false;
            if (p.slot != slot || IsDecor(p.id) || MissionDone(st, p.id)) return false;
            int propRoom = map.RoomAt(p.pos);
            int myRoom = map.RoomAt(myPos);
            return propRoom >= 0 && propRoom == myRoom;
        }

        /// <summary>지금 내 발치에서 할 수 있는 일. 발광 중인 담당 오브젝트가 Reach 이내면 팝업 열기 안내.</summary>
        public static Prompt Get(int slot, Vector2 pos)
        {
            var result = new Prompt { text = "", cmd = null, popupId = null };
            if (!Session.InStage || Session.Map == null) return result;
            var st = Session.St;
            var map = Session.Map;
            if (st.cleared || st.failed) return result;

            MapProp best = null;
            float bestD = float.MaxValue;
            foreach (var p in map.props)
            {
                if (!IsGlowing(p, slot, pos)) continue;
                float d = Vector2.Distance(pos, p.pos);
                if (d <= Reach && d < bestD) { bestD = d; best = p; }
            }
            if (best != null)
            {
                result.text = PromptText(best.id);
                result.popupId = best.id;
                return result;
            }

            // 최종 집결: 내 원까지의 안내(명령 없음)
            if (st.step == 2)
            {
                bool inside = Stage1Logic.InGather(slot, pos, st, Session.Clock);
                result.text = inside ? "내 원 안 — 원이 움직이니 따라가며 머무르세요" : "내 색 원을 찾아 안으로 들어가세요";
            }
            return result;
        }

        /// <summary>
        /// 마우스 클릭 판정. 월드 좌표 worldPoint가 발광 중인 담당 오브젝트의 ClickRadius 이내이고
        /// 내 위치가 Reach 이내면 그 오브젝트 id를 돌려준다(없으면 null).
        /// </summary>
        public static string ClickTarget(int slot, Vector2 myPos, Vector2 worldPoint)
        {
            var map = Session.Map;
            if (map == null || !Session.InStage) return null;
            string best = null;
            float bestD = float.MaxValue;
            foreach (var p in map.props)
            {
                if (!IsGlowing(p, slot, myPos)) continue;
                float dc = Vector2.Distance(worldPoint, p.pos);
                if (dc > ClickRadius) continue;
                if (Vector2.Distance(myPos, p.pos) > Reach) continue;
                if (dc < bestD) { bestD = dc; best = p.id; }
            }
            return best;
        }

        /// <summary>발광 중인 담당 오브젝트가 Reach 이내에 있는가(HUD 액션 열 "상호작용 E" 강조용).</summary>
        public static bool CanInteract(int slot, Vector2 pos) => Get(slot, pos).popupId != null;
    }
}
