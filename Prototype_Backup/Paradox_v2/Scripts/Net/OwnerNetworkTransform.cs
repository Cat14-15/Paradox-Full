using Unity.Netcode.Components;

namespace Paradox.Net
{
    /// <summary>
    /// 소유자(클라이언트) 권한 트랜스폼. 기본 NetworkTransform은 서버 권한이라
    /// 각 클라이언트가 자기 캐릭터를 직접 움직이려면 소유자 권한으로 바꿔야 한다.
    /// </summary>
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
