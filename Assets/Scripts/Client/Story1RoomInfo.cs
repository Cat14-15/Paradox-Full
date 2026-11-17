using System.Collections;
using System.Collections.Generic;
using Paradox.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Paradox.Client
{
    /// <summary>
    /// 처음 들어가는 방의 설명 창(모달). 방마다 한 번만 뜨고, X · E · Enter · 클릭으로 닫는다. 떠 있는 동안 주인공은 멈춘다.
    /// 창은 씬에 놓여 있다(Story1FlowBuilder).
    /// </summary>
    public sealed class Story1RoomInfo : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { IsOpen = false; }

        public CanvasGroup group;
        public PixelText title, line1, line2, hint;

        private readonly HashSet<string> _seen = new HashSet<string>();
        private float _openedAt;
        private Coroutine _co;

        // 방 이름 → 설명 두 줄. 확실한 것만 적는다(퍼즐 위치·분위기).
        private static readonly Dictionary<string, string[]> Info = new Dictionary<string, string[]>
        {
            { "입구와 안내실", new[] { "정전된 박물관의 입구예요.", "안내실 벽의 퓨즈함으로 전원을 켜야 해요." } },
            { "입구 연결 통로", new[] { "입구와 전시실들을 잇는 통로예요.", "" } },
            { "중앙 홀", new[] { "박물관 한가운데의 넓은 홀이에요.", "바닥의 원은 마지막에 네 명이 함께 밟아요." } },
            { "2층 연결 복도", new[] { "위층과 아래층을 잇는 복도예요.", "길이 헷갈리면 내비게이터의 지도(M)를 봐요." } },
            { "회화 전시실", new[] { "그림이 걸린 어두운 전시실이에요.", "끊어진 전선 옆 물웅덩이를 건너야 해요." } },
            { "수장고", new[] { "유물을 보관하는 방이에요.", "유물 전시대에서 유물을 순서대로 놓아요." } },
            { "학예실", new[] { "전시를 관리하는 방이에요.", "배수 밸브 패널을 돌려 물을 빼야 해요." } },
            { "도서 열람실", new[] { "책장이 늘어선 조용한 열람실이에요.", "" } },
            { "도자기 전시실", new[] { "도자기가 진열된 전시실이에요.", "" } },
            { "역사 전시실", new[] { "오래된 자료가 전시된 방이에요.", "" } },
            { "자연사 전시실", new[] { "표본이 진열된 전시실이에요.", "" } },
            { "보존 작업실", new[] { "유물을 손보는 작업실이에요.", "" } },
        };

        private void Awake() { Close(); }
        private void OnEnable() { Story1Scene.RoomEntered += OnRoom; }
        private void OnDisable() { Story1Scene.RoomEntered -= OnRoom; }

        private void OnRoom(string roomName)
        {
            if (Time.timeSinceLevelLoad < 1.5f) { _seen.Add(roomName); return; }   // 시작한 방은 설명하지 않는다
            if (_seen.Contains(roomName) || !Info.ContainsKey(roomName)) return;
            _seen.Add(roomName);
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(ShowLater(roomName));
        }

        private IEnumerator ShowLater(string roomName)
        {
            yield return new WaitForSeconds(0.7f);   // 문을 지나 화면이 돌아온 뒤에
            while (Story1Scene.InputLocked) yield return null;   // 다른 창이 떠 있으면 끝날 때까지
            var d = Info[roomName];
            title.text = roomName; line1.text = d[0]; line2.text = d[1];
            hint.text = "X 닫기";
            IsOpen = true; _openedAt = Time.unscaledTime;
            group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true;
            Sfx.Play("ui_select", 0.8f);
        }

        private void Close()
        {
            IsOpen = false;
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Time.unscaledTime - _openedAt < 0.4f) return;
            var kb = Keyboard.current; var ms = Mouse.current;
            bool press = (kb != null && (kb.xKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                         || (ms != null && ms.leftButton.wasPressedThisFrame);
            if (press) { Close(); Sfx.Play("ui_back", 0.8f); }
        }
    }
}
