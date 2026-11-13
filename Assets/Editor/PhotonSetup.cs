using System;
using UnityEditor;
using UnityEngine;
using Fusion.Photon.Realtime;

namespace Paradox.Editor
{
    /// <summary>
    /// Photon Fusion 앱 ID 를 PhotonAppSettings 에셋에 넣는다(배치 모드용).
    /// <c>-executeMethod Paradox.Editor.PhotonSetup.ApplyAppId</c> + 환경변수 <c>PHOTON_APPID_FUSION</c>.
    /// </summary>
    public static class PhotonSetup
    {
        public static void ApplyAppId()
        {
            string id = Environment.GetEnvironmentVariable("PHOTON_APPID_FUSION");
            if (string.IsNullOrWhiteSpace(id)) { Debug.LogError("[PhotonSetup] PHOTON_APPID_FUSION 환경변수가 비어 있음"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
            var s = PhotonAppSettings.Global;
            s.AppSettings.AppIdFusion = id.Trim();
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PhotonSetup] AppIdFusion 설정 완료 ({s.AppSettings.AppIdFusion.Substring(0, 8)}…) · asset={AssetDatabase.GetAssetPath(s)}");
        }
    }
}
