using System.Collections.Generic;
using Paradox.Roles;
using UnityEngine;

namespace Paradox.Stage
{
    /// <summary>
    /// 역할별로 세계에서 무엇이 보이는지를 확 다르게 만드는 매니저(강한 정보 비대칭).
    ///  - 레버: 조작자에게만 보임
    ///  - 출구(도착존): 내비게이터에게만 보임
    /// (표지판 코드=통역/현지인, 함정=관측자, 미니맵=내비게이터는 각 컴포넌트가 처리)
    ///
    /// 이렇게 하면 아무도 혼자 전체를 볼 수 없어, 서로 안내해야 풀린다.
    /// </summary>
    public class RoleViewManager : MonoBehaviour
    {
        private Renderer[] _leverRenderers;
        private Renderer[] _zoneRenderers;

        private void Start()
        {
            _leverRenderers = Collect(Object.FindObjectsByType<SymbolLever>(FindObjectsSortMode.None));
            _zoneRenderers = Collect(Object.FindObjectsByType<ArrivalZone>(FindObjectsSortMode.None));

            RoleService.Changed += Apply;
            Apply();
        }

        private void OnDestroy() => RoleService.Changed -= Apply;

        private static Renderer[] Collect(Component[] sources)
        {
            var list = new List<Renderer>();
            foreach (var s in sources)
                if (s != null) list.AddRange(s.GetComponentsInChildren<Renderer>(true));
            return list.ToArray();
        }

        private void Apply()
        {
            SetVisible(_leverRenderers, RoleService.LocalRole == GameRole.Operator);
            SetVisible(_zoneRenderers, RoleService.LocalRole == GameRole.Navigator);
        }

        private static void SetVisible(Renderer[] renderers, bool visible)
        {
            if (renderers == null) return;
            foreach (var r in renderers) if (r != null) r.enabled = visible;
        }
    }
}
