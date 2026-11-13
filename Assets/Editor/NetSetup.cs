using System.IO;
using Fusion;
using UnityEditor;
using UnityEngine;
using Paradox.Net;

namespace Paradox.Editor
{
    /// <summary>
    /// Fusion 네트워크 프리팹(Session / Avatar)을 Assets/Resources/NetPrefabs 에 만든다.
    /// 코드로 만든 템플릿은 Fusion 이 모르는 프리팹이라 에셋이 필요하다. 임포트 후처리가 NetworkObject 를 굽고 "FusionPrefab" 라벨을 붙여
    /// NetworkProjectConfig 의 프리팹 테이블에 등록한다.
    /// 배치 실행: <c>-executeMethod Paradox.Editor.NetSetup.CreatePrefabs</c>
    /// </summary>
    public static class NetSetup
    {
        public const string Dir = "Assets/Resources/NetPrefabs";

        [MenuItem("Tools/STRANGER/Create Net Prefabs")]
        public static void CreatePrefabs()
        {
            Directory.CreateDirectory(Dir);
            AssetDatabase.Refresh();

            var session = new GameObject("Session");
            session.AddComponent<NetworkObject>();
            session.AddComponent<SessionNet>();
            Save(session, Dir + "/Session.prefab");

            var avatar = new GameObject("Avatar");
            avatar.AddComponent<NetworkObject>();
            var nt = avatar.AddComponent<NetworkTransform>();
            nt.SyncScale = false;
            nt.SyncParent = false;
            nt.ConfigFlags = NetworkTransform.NetworkTransformFlags.DisableSharedModeInterpolation;   // 이동은 Update() 에서 한다
            avatar.AddComponent<AvatarNet>();
            Save(avatar, Dir + "/Avatar.prefab");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Fusion.Editor.NetworkProjectConfigUtilities.RebuildPrefabTable();

            foreach (var n in new[] { "Session", "Avatar" })
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{n}.prefab");
                var no = go != null ? go.GetComponent<NetworkObject>() : null;
                Debug.Log($"[NetSetup] {n}: prefab={(go != null)} networkObject={(no != null)} flags={(no != null ? no.Flags.ToString() : "-")} labels={string.Join(",", AssetDatabase.GetLabels(go))}");
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Save(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
