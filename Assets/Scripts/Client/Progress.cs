using UnityEngine;

namespace Paradox.Client
{
    /// <summary>
    /// 로컬 저장(PlayerPrefs). 키는 명세 1-3 그대로:
    /// stranger.prologueSeen(0/1), stranger.stage1Cleared(0/1), stranger.bestSeconds(float),
    /// stranger.myName(string), stranger.volume(0~1), stranger.fullscreen(0/1).
    /// </summary>
    public static class Progress
    {
        private const string KeyPrologue = "stranger.prologueSeen";
        private const string KeyCleared = "stranger.stage1Cleared";
        private const string KeyBest = "stranger.bestSeconds";
        private const string KeyName = "stranger.myName";
        private const string KeyVolume = "stranger.volume";
        private const string KeyFullscreen = "stranger.fullscreen";

        /// <summary>프롤로그를 끝까지 봤거나 건너뛴 적이 있는가.</summary>
        public static bool prologueSeen
        {
            get { return PlayerPrefs.GetInt(KeyPrologue, 0) == 1; }
            set { PlayerPrefs.SetInt(KeyPrologue, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Stage 1을 클리어한 적이 있는가.</summary>
        public static bool stage1Cleared
        {
            get { return PlayerPrefs.GetInt(KeyCleared, 0) == 1; }
            set { PlayerPrefs.SetInt(KeyCleared, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Stage 1 최고 기록(초). 0 이하 = 기록 없음.</summary>
        public static float bestSeconds
        {
            get { return PlayerPrefs.GetFloat(KeyBest, 0f); }
            set { PlayerPrefs.SetFloat(KeyBest, value); PlayerPrefs.Save(); }
        }

        /// <summary>내 기본 이름(설정 화면 / 이름 설정 화면에서 저장). 비어 있으면 역할 기본 이름을 쓴다.</summary>
        public static string myName
        {
            get { return PlayerPrefs.GetString(KeyName, ""); }
            set { PlayerPrefs.SetString(KeyName, value ?? ""); PlayerPrefs.Save(); }
        }

        /// <summary>음량 0~1 (값만 저장하고 AudioListener.volume 에 반영).</summary>
        public static float volume
        {
            get { return Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, 1f)); }
            set { PlayerPrefs.SetFloat(KeyVolume, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        public static bool fullscreen
        {
            get { return PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1; }
            set { PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>타이틀 "이어하기" 활성 조건.</summary>
        public static bool CanContinue => prologueSeen || stage1Cleared;

        /// <summary>Stage 1 클리어 기록. 더 빠른 기록이면 최고 기록을 갱신한다.</summary>
        public static void RecordClear(float seconds)
        {
            stage1Cleared = true;
            if (seconds > 0f && (bestSeconds <= 0f || seconds < bestSeconds)) bestSeconds = seconds;
        }

        /// <summary>저장된 음량을 실제 소리에 반영한다(앱 시작·설정 변경 시).</summary>
        public static void ApplyAudio()
        {
            AudioListener.volume = volume;
        }

        /// <summary>진행 기록 전부 지우기(설정 화면).</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KeyPrologue);
            PlayerPrefs.DeleteKey(KeyCleared);
            PlayerPrefs.DeleteKey(KeyBest);
            PlayerPrefs.Save();
        }
    }
}
