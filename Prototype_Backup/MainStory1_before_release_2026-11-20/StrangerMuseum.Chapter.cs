using System.Collections.Generic;
using Paradox.Client;
using Paradox.Core;
using Paradox.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class StrangerMuseum
{
    private const int ChapterClearEvent = 15;
    private const int ChapterSignalEvent = 16;
    private const int ChapterReadyEvent = 17;
    private static readonly int[] ValveAnswers = { 1, 2, 0, 1 };
    private static readonly string[][] ValveSymbols =
    {
        new[] { "○", "△", "□" },
        new[] { "△", "□", "○" },
        new[] { "□", "○", "△" },
        new[] { "△", "○", "□" }
    };
    private readonly Dictionary<int, int> chapterSignalMasks = new Dictionary<int, int>();
    private readonly Dictionary<int, int> chapterReadyMasks = new Dictionary<int, int>();
    private static readonly ChapterTask[] ChapterTasks =
    {
        new ChapterTask("B1", "maintenance", "maint-pipe-header-1", "물길 막기",
            "동료가 보여 준 번호를 내 밸브에 맞춰요.", "", "내 밸브 번호가 적힌 받은 표시를 확인해요.",
            new[] { "← 1", "↑ 2", "→ 3" }, new[] { 1 },
            "네 밸브를 맞췄어요. 물소리가 멎었어요."),
        new ChapterTask("B1", "safety", "safety-console", "환풍기 켜기",
            "망가진 팬은 쉬게 하고, 나머지 하나를 켜요.", "○ ✕   □ ✕", "✕가 없는 모양을 골라요.",
            new[] { "○  동그라미", "△  세모", "□  네모" }, new[] { 1 },
            "바람이 돌아왔어요. 이제 전기실로 갈 수 있어요.", "△ ✕   □ ✕", new[] { 0 }),
        new ChapterTask("B1", "power", "power-switchgear", "불 켜기",
            "세 버튼을 한 번씩 눌러 전원을 복구하세요.", "□가 먼저, ○가 마지막", "첫 칸과 마지막 칸을 채우면 가운데에 버튼 하나가 남습니다.",
            new[] { "○  동그라미", "△  세모", "□  네모" }, new[] { 2, 1, 0 },
            "전원이 돌아왔다. 이제 지하 수장고를 확인할 차례다.", "○가 먼저, □가 마지막", new[] { 0, 1, 2 }),
        new ChapterTask("B1", "rescue", "rescue-table-c", "유물 상자 구하기",
            "물 위에 있고, 상자의 무게를 버틸 수 있는 받침을 찾으세요.", "물 2칸 · 상자 4kg", "2칸보다 높고, 하중이 4kg 이상이어야 합니다.",
            new[] { "A · 높이 4칸 / 하중 2kg", "B · 높이 3칸 / 하중 5kg", "C · 높이 2칸 / 하중 6kg", "D · 높이 1칸 / 하중 7kg" }, new[] { 1 },
            "상자를 구했어요. 안에서 전시실 점검표를 찾았어요."),
        new ChapterTask("1F", "life", "life-case-nw", "항아리 이름표 붙이기",
            "항아리 이름표에는 몸 모양과 입 크기가 모두 맞아야 해요.", "몸 ○   입 작음", "둥글고 입이 작은 그림을 찾아요.",
            new[] { "○  둥근 몸 · 작은 입", "○  둥근 몸 · 큰 입", "□  네모 몸 · 작은 입" }, new[] { 0 },
            "항아리 이름표를 찾았어요."),
        new ChapterTask("1F", "time", "time-table-center", "연표 이어 붙이기",
            "시간이 흐른 순서대로 두 장을 붙여요.", "1910 · 1898   /   1946은 예비 카드", "두 숫자 중 작은 해부터 골라요.",
            new[] { "1910년", "1898년", "1946년" }, new[] { 1, 0 },
            "떨어진 연표를 이었어요."),
        new ChapterTask("1F", "craft", "craft-table-center", "베틀 무늬 잇기",
            "같은 세 칸짜리 무늬가 반복됩니다. 빈칸 두 개를 왼쪽부터 채우세요.", "★ ☾ ○ | ★ ? ?", "앞의 세 칸과 뒤의 세 칸을 비교하세요.",
            new[] { "★  별", "○  해", "☾  달" }, new[] { 2, 1 },
            "천에 끊겼던 무늬가 이어졌다.", "☾ ★ ○ | ☾ ? ?", new[] { 0, 1 }),
        new ChapterTask("1F", "city", "city-table-center", "전차 길 찾기",
            "시장에서 출발해 열린 선로만 연결하세요.", "○–△  열림 / △–□  열림 / ◇–□  닫힘", "시장에서 박물관까지 두 구간이 모두 열려 있어야 합니다.",
            new[] { "□  박물관", "○  시장", "△  학교", "◇  항구" }, new[] { 1, 2, 0 },
            "전차가 박물관에 도착했어요.", "○–◇  열림 / ◇–□  열림 / △–□  닫힘", new[] { 1, 3, 0 }),
        new ChapterTask("2F", "archive", "archive-table", "기록 조각 맞추기",
            "찢어진 자리와 테두리가 맞는 조각을 찾아요.", "모서리 4개 · 곧은 변", "모서리가 네 개인 조각을 세어 봐요.",
            new[] { "○  둥근 조각", "△  뾰족한 조각", "□  네모 조각" }, new[] { 2 },
            "기록을 이었어요. 다음 유물은 보존실에 있어요."),
        new ChapterTask("2F", "preserve", "preserve-bench-r", "젖은 유물 받치기",
            "유물이 흔들리지 않도록 받침을 골라요.", "부드럽게   ·   마르게", "두 조건을 모두 만족하는 받침을 골라요.",
            new[] { "마른 천", "마른 돌", "젖은 천" }, new[] { 0 },
            "유물을 천 위에 안전하게 올렸어요."),
        new ChapterTask("2F", "reading", "reading-table", "자료 제자리에 놓기",
            "글자가 달라도 그림의 뜻은 같아요. 기록을 같은 주제에 놓아요.", "≋   +   돛", "물결과 돛이 함께 나오는 칸을 찾아요.",
            new[] { "△  전통 옷", "○  배와 항해", "□  도시의 교통" }, new[] { 1 },
            "배에 관한 자료를 찾아냈어요."),
        new ChapterTask("2F", "special", "special-table", "배의 신호 불 켜기",
            "배가 항구를 찾게 불빛을 두 번 보내요.", "낮의 불빛 → 물의 흔들림", "낮의 불빛은 해예요. 물의 흔들림은 파도예요.",
            new[] { "≋  파도", "★  별", "○  해" }, new[] { 2, 0 },
            "신호 불이 동상까지 닿았어요.", "물의 흔들림 → 낮의 불빛", new[] { 0, 2 }),
        new ChapterTask("2F", "atrium", "atrium-statue", "점검 신호 보내기",
            "동상 옆에서 모든 방의 점검이 끝났다는 신호를 보내요.", "", "완료 표시를 눌러요.",
            new[] { "✓  완료" }, new[] { 0 },
            "안내 데스크에 신호가 도착했어요."),
        new ChapterTask("1F", "hall", "hall-reception", "출구 열기",
            "네 친구가 안내 데스크에서 출구를 열어요.", "", "출구 열기 버튼을 눌러요.",
            new[] { "출구 열기" }, new[] { 0 },
            "문이 열렸다. 하린이 아까 그린 물방울을 가리키자, 이안이 엄지를 들었다. 린은 종이를 접어 챙겼고, 카이는 문을 잡아 주었다. 네 사람은 한 명씩 밖으로 나왔다.")
    };

    private readonly HashSet<int> chapterCleared = new HashSet<int>();
    private int chapterIndex;
    private int chapterAnswerIndex;
    private ChapterTask openedChapter;
    private int openedChapterIndex;
    private string chapterFeedback;
    private bool chapterCorrect;
    private bool showChapterHint;
    private int valveDial;
    private int chapterLastWrong = -1;
    private Texture2D valveFace;
    private readonly List<string> selectedChapterOptions = new List<string>();
    private float chapterStart;

    public string ChapterObjective => chapterIndex < ChapterTasks.Length ? ChapterTasks[chapterIndex].Title : "모두 무사히 탈출했어요!";
    public string ChapterLocation => chapterIndex < ChapterTasks.Length
        ? museum.floors[ChapterTasks[chapterIndex].Floor].scenes[ChapterTasks[chapterIndex].Room].label : "";
    public string ChapterProgress => $"{Mathf.Min(chapterIndex, ChapterTasks.Length)} / {ChapterTasks.Length}";
    public bool ChapterComplete => chapterIndex == ChapterTasks.Length;
    public bool ChapterPanelOpen => openedChapter != null;

    private ChapterTask CurrentChapter => guideObjectives && !ChapterComplete ? ChapterTasks[chapterIndex] : null;

    private Exhibit CurrentChapterExhibit()
    {
        ChapterTask task = CurrentChapter;
        return task != null && task.Floor == curFloorId && task.Room == roomId
            ? System.Array.Find(floor.objects, item => item.id == task.Exhibit) : null;
    }

    private Exhibit NearbyChapterExhibit()
    {
        Exhibit target = CurrentChapterExhibit();
        return target != null && target.interact != null && Vector2.Distance(position, Point(target.interact)) <= museum.player.inspectRadius
            ? target : null;
    }

    private bool TryOpenChapter()
    {
        if (NearbyChapterExhibit() == null)
            return false;

        openedChapter = CurrentChapter;
        MuseumChat.PuzzleActive = true;
        openedChapterIndex = chapterIndex;
        int sender = SignalSender(Story1Role.Current);
        if (openedChapterIndex < 12 && !HumanAt(sender)) MarkChapterSignal(openedChapterIndex, sender);
        scroll = Vector2.zero;
        chapterAnswerIndex = IsChapterReady(openedChapterIndex, Story1Role.Current) ? openedChapter.Answers.Length : 0;
        chapterLastWrong = -1;
        chapterFeedback = null;
        chapterCorrect = false;
        showChapterHint = false;
        valveDial = 0;
        selectedChapterOptions.Clear();
        moveInput = Vector2.zero;
        Sfx.Play("book_open", .6f);
        return true;
    }

    private bool ChapterDoorLocked(Portal portal)
    {
        if (!guideObjectives) return MuseumPuzzles.DoorLocked(portal.name);
        if (portal.targetFloor != "B1") return false;
        return (portal.targetScene == "safety" && chapterIndex < 1)
            || (portal.targetScene == "power" && chapterIndex < 2)
            || (portal.targetScene == "rescue" && chapterIndex < 3);
    }

    private static int SignalSender(int slot) => (slot + Roles.Count - 1) % Roles.Count;
    private static int SignalRecipient(int slot) => (slot + 1) % Roles.Count;

    private static bool HumanAt(int slot)
    {
        if (slot == Story1Role.Current) return true;
        if (!Story1Net.Online) return false;
        var player = Session.State.players[slot];
        return player != null && player.present && !player.isBot;
    }

    private bool HasChapterSignal(int slot) =>
        chapterSignalMasks.TryGetValue(openedChapterIndex, out int mask) && (mask & (1 << slot)) != 0;

    private void MarkChapterSignal(int index, int slot)
    {
        chapterSignalMasks.TryGetValue(index, out int mask);
        chapterSignalMasks[index] = mask | (1 << slot);
    }

    private void ShareChapterSignal()
    {
        int index = openedChapterIndex;
        int slot = Story1Role.Current;
        MarkChapterSignal(index, slot);
        int sender = SignalSender(slot);
        if (!HumanAt(sender)) MarkChapterSignal(index, sender);
        if (Story1Net.Online) Story1Net.Send(ChapterSignalEvent, index);
        Sfx.Play("ui_select", .5f);
    }

    private string SignalFor(int recipient)
    {
        if (openedChapterIndex == 0)
            return $"{recipient + 1}번 밸브 · {ValveSymbols[recipient][ValveAnswers[recipient]]}";
        return openedChapter.SignalFor(recipient);
    }

    private bool ChapterCanAnswer => openedChapterIndex >= 12 ||
        (HasChapterSignal(Story1Role.Current) && HasChapterSignal(SignalSender(Story1Role.Current)));

    private static bool IsTeamOperation(int index) => index == 0 || index == 2 || index == 13;

    private bool IsChapterReady(int index, int slot) =>
        chapterReadyMasks.TryGetValue(index, out int mask) && (mask & (1 << slot)) != 0;

    private void MarkChapterReady(int index, int slot)
    {
        if (!IsTeamOperation(index) || index < chapterIndex)
            return;
        chapterReadyMasks.TryGetValue(index, out int mask);
        chapterReadyMasks[index] = mask | (1 << slot);
        TryFinishTeamOperation();
    }

    private void TryFinishTeamOperation()
    {
        if (!IsTeamOperation(chapterIndex) || !chapterReadyMasks.ContainsKey(chapterIndex))
            return;

        for (int i = 0; i < Roles.Count; i++)
            if (HumanAt(i) && !IsChapterReady(chapterIndex, i))
                return;

        ApplyChapterClear(chapterIndex);
    }

    private void ChooseChapterAnswer(int option)
    {
        if (openedChapter == null || chapterCorrect || !ChapterCanAnswer || chapterAnswerIndex >= openedChapter.Answers.Length)
            return;

        int expected = openedChapterIndex == 0 ? ValveAnswers[Story1Role.Current] : openedChapter.AnswersFor(Story1Role.Current)[chapterAnswerIndex];
        if (option != expected)
        {
            chapterLastWrong = option;
            chapterFeedback = "단서와 맞지 않습니다. 다시 시도하세요.";
            return;
        }

        chapterLastWrong = -1;
        selectedChapterOptions.Add(openedChapter.Options[option]);
        chapterAnswerIndex++;
        if (chapterAnswerIndex < openedChapter.Answers.Length)
        {
            chapterFeedback = null;
            Sfx.Play("ui_select", .5f);
            return;
        }

        int index = openedChapterIndex;
        if (IsTeamOperation(index))
        {
            MarkChapterReady(index, Story1Role.Current);
            if (Story1Net.Online) Story1Net.Send(ChapterReadyEvent, index);
            return;
        }

        ApplyChapterClear(index);
        if (Story1Net.Online) Story1Net.Send(ChapterClearEvent, index);
    }

    private void ApplyChapterClear(int index)
    {
        if (index < 0 || index >= ChapterTasks.Length || !chapterCleared.Add(index))
            return;

        ChapterTask completed = ChapterTasks[index];
        if (openedChapter == completed)
        {
            chapterCorrect = true;
            chapterFeedback = completed.Success;
        }
        else if (index == chapterIndex)
        {
            MuseumStory.Think(completed.Success);
        }

        while (chapterCleared.Contains(chapterIndex))
            chapterIndex++;

        if (chapterIndex >= 3) MuseumStory.RestorePower();
        routedStep = -1;
        if (ChapterComplete)
        {
            openedChapter = ChapterTasks[ChapterTasks.Length - 1];
            MuseumChat.PuzzleActive = true;
            chapterCorrect = true;
            chapterFeedback = openedChapter.Success;
            scroll = Vector2.zero;
            Progress.stage1Cleared = true;
            float elapsed = Time.time - chapterStart;
            if (Progress.bestSeconds <= 0f || elapsed < Progress.bestSeconds)
                Progress.bestSeconds = elapsed;
        }

        Sfx.Play(ChapterComplete ? "power_on" : "ui_select", .75f);
    }

    private void ReceiveChapterClear(GameEvent e)
    {
        if (!Roles.Valid(e.slot)) return;
        if (e.a == ChapterClearEvent && e.b > 0 && !IsTeamOperation(e.b)) ApplyChapterClear(e.b);
        else if (e.a == ChapterSignalEvent && e.b >= 0 && e.b < ChapterTasks.Length)
            MarkChapterSignal(e.b, e.slot);
        else if (e.a == ChapterReadyEvent) MarkChapterReady(e.b, e.slot);
    }

    private void CloseChapter()
    {
        if (openedChapter == null)
            return;

        openedChapter = null;
        MuseumChat.PuzzleActive = false;
        chapterFeedback = null;
        moveInput = Vector2.zero;
    }

    private void DrawChapterObjective()
    {
        float scale = Mathf.Clamp(Screen.height / 720f, .8f, 1.3f);
        float margin = 12 * scale;
        float width = Mathf.Min(330 * scale, Screen.width - 2 * margin);
        float height = 66 * scale;
        var panel = new Rect(margin, Screen.height - height - margin, width, height);
        Color previous = GUI.color;
        GUI.color = new Color32(31, 24, 20, 246);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = previous;
        promptLabel.fontSize = Mathf.RoundToInt(19 * scale);
        GUI.Label(new Rect(panel.x + margin, panel.y + 2, width - margin * 2, 30 * scale),
            ChapterComplete ? ChapterObjective : $"{ChapterProgress}  ·  {ChapterObjective}", promptLabel);
        promptLabel.fontSize = Mathf.RoundToInt(16 * scale);
        GUI.Label(new Rect(panel.x + margin, panel.y + 31 * scale, width - margin * 2, 29 * scale),
            ChapterLocation, promptLabel);
    }

    private void DrawChapter()
    {
        float scale = Mathf.Clamp(Screen.height / 720f, .65f, 1.35f);
        float width = Mathf.Min(720 * scale, Screen.width - 24);
        float height = Mathf.Min(690 * scale, Screen.height - 24);
        float padding = 18 * scale;
        var panel = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
        if (MuseumChat.ChatRect(out Rect chat))
        {
            float leftWidth = chat.xMin - 24;
            if (leftWidth >= 380 * scale)
            {
                panel.width = Mathf.Min(width, leftWidth);
                panel.x = 12 + (leftWidth - panel.width) / 2;
            }
            else
            {
                panel.height = Mathf.Min(height, Mathf.Max(0, chat.yMin - 24));
                panel.y = 12;
            }
        }
        if (panel.height < 120 || panel.width < 120)
            return;
        GUI.Box(panel, GUIContent.none);
        Color previous = GUI.color;
        GUI.color = new Color32(17, 23, 29, 252);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color32(209, 170, 101, 255);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 3 * scale), Texture2D.whiteTexture);
        GUI.color = previous;

        inspectionTitle.fontSize = Mathf.RoundToInt(30 * scale);
        inspectionBody.fontSize = Mathf.RoundToInt(24 * scale);
        inspectionDetail.fontSize = Mathf.RoundToInt(21 * scale);
        button.fontSize = Mathf.RoundToInt(22 * scale);

        GUILayout.BeginArea(new Rect(panel.x + padding, panel.y + padding, panel.width - 2 * padding, panel.height - 2 * padding));
        scroll = GUILayout.BeginScrollView(scroll);
        string title = ChapterComplete ? "박물관 탈출 성공!" :
            openedChapterIndex == 0 ? $"급수 제어판  /  {Story1Role.Current + 1}번 밸브" : openedChapter.Title;
        GUILayout.Label(title, inspectionTitle);
        GUILayout.Space(10 * scale);
        if (chapterCorrect || openedChapterIndex != 0)
            GUILayout.Label(chapterCorrect ? chapterFeedback : openedChapter.Clue, inspectionBody);
        if (!chapterCorrect)
        {
            if (openedChapterIndex < 12)
                DrawSignalTray(scale, panel.width - padding * 2);

            GUILayout.Space(10 * scale);
            if (openedChapterIndex == 0)
            {
                GUILayout.Label("받은 기호를 찾아 바늘을 맞추세요.", inspectionBody);
                DrawValveDial(scale);
            }
            else
            {
                if (ChapterCanAnswer && openedChapterIndex < 12 &&
                    GUILayout.Button(showChapterHint ? "힌트 닫기" : "힌트 보기", button, GUILayout.Height(32 * scale)))
                    showChapterHint = !showChapterHint;
                if (showChapterHint)
                    GUILayout.Label(openedChapter.Hint, inspectionDetail);

                DrawActiveMechanism(scale);
            }

            if (IsTeamOperation(openedChapterIndex))
                DrawTeamReadiness(scale);

            if (!string.IsNullOrEmpty(chapterFeedback))
                GUILayout.Label(chapterFeedback, inspectionDetail);
        }
        GUILayout.EndScrollView();
        if (ChapterComplete && GUILayout.Button("로비로 돌아가기", button, GUILayout.Height(42 * scale)))
        {
            Bgm.Stop();
            SceneManager.LoadScene("Lobby");
        }
        else if (!ChapterComplete && GUILayout.Button(chapterCorrect ? "계속하기" : "닫기 · Esc", button, GUILayout.Height(42 * scale)))
        {
            CloseChapter();
        }
        GUILayout.EndArea();
    }

    private void DrawValveDial(float scale)
    {
        var dial = GUILayoutUtility.GetRect(1, 174 * scale, GUILayout.ExpandWidth(true));
        float radius = 54 * scale;
        var center = new Vector2(dial.center.x, dial.center.y + 14 * scale);
        if (valveFace == null)
            valveFace = CreateValveFace();
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), valveFace);
        for (int i = 0; i < 3; i++)
        {
            float angle = (i - 1) * 55f * Mathf.Deg2Rad;
            Vector2 tick = center + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * (radius + 13 * scale);
            GUI.Label(new Rect(tick.x - 10 * scale, tick.y - 13 * scale, 24 * scale, 28 * scale),
                ValveSymbols[Story1Role.Current][i], inspectionBody);
        }

        Color color = GUI.color;
        Matrix4x4 matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot((valveDial - 1) * 55f, center);
        GUI.color = new Color32(242, 192, 100, 255);
        GUI.DrawTexture(new Rect(center.x - 5 * scale, center.y - 44 * scale, 10 * scale, 50 * scale), Texture2D.whiteTexture);
        GUI.matrix = matrix;
        GUI.DrawTexture(new Rect(center.x - 10 * scale, center.y - 10 * scale, 20 * scale, 20 * scale), Texture2D.whiteTexture);
        GUI.color = color;

        if (chapterAnswerIndex > 0) return;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀ 돌리기", button, GUILayout.Height(38 * scale))) valveDial = (valveDial + 2) % 3;
        if (GUILayout.Button("돌리기 ▶", button, GUILayout.Height(38 * scale))) valveDial = (valveDial + 1) % 3;
        GUILayout.EndHorizontal();
        if (ChapterCanAnswer)
        {
            if (GUILayout.Button($"{ValveSymbols[Story1Role.Current][valveDial]} 위치 적용", button, GUILayout.Height(40 * scale)))
                ChooseChapterAnswer(valveDial);
        }
        else
        {
            GUILayout.Label(HasChapterSignal(Story1Role.Current) ? "동료의 단서를 기다리는 중" : "단서를 보내면 밸브를 적용할 수 있습니다.", inspectionDetail);
        }
    }

    private static Texture2D CreateValveFace()
    {
        const int size = 48;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - (size - 1) / 2f, dy = y - (size - 1) / 2f;
            float distance = dx * dx + dy * dy;
            if (distance > 23 * 23) continue;
            pixels[y * size + x] = distance > 21 * 21 ? new Color32(32, 38, 36, 255)
                : distance > 17 * 17 ? new Color32(148, 135, 106, 255)
                : new Color32(52, 67, 66, 255);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private sealed class ChapterTask
    {
        public readonly string Floor, Room, Exhibit, Title, Clue, Signal, Hint, Success;
        public readonly string[] Options;
        public readonly int[] Answers;
        private readonly string alternateSignal;
        private readonly int[] alternateAnswers;

        public string SignalFor(int role) => (role & 1) == 1 && alternateSignal != null ? alternateSignal : Signal;
        public int[] AnswersFor(int role) => (role & 1) == 1 && alternateAnswers != null ? alternateAnswers : Answers;

        public ChapterTask(string floor, string room, string exhibit, string title, string clue, string signal, string hint,
            string[] options, int[] answers, string success, string alternateSignal = null, int[] alternateAnswers = null)
        {
            Floor = floor;
            Room = room;
            Exhibit = exhibit;
            Title = title;
            Clue = clue;
            Signal = signal;
            Hint = hint;
            Options = options;
            Answers = answers;
            Success = success;
            this.alternateSignal = alternateSignal;
            this.alternateAnswers = alternateAnswers;
        }
    }
}

