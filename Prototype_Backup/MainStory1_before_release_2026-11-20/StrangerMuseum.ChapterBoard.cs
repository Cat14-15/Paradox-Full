using Paradox.Client;
using Paradox.Game;
using UnityEngine;

public sealed partial class StrangerMuseum
{
    private static readonly Color Surface = new Color32(27, 38, 47, 255);
    private static readonly Color Bay = new Color32(37, 53, 62, 255);
    private static readonly Color Brass = new Color32(221, 180, 105, 255);
    private static readonly Color Active = new Color32(116, 202, 163, 255);
    private static readonly Color Fault = new Color32(181, 82, 68, 255);
    private static readonly Color Divider = new Color32(76, 95, 103, 255);
    private GUIStyle boardCenter;

    private static void FillBoard(Rect rect, Color tint)
    {
        Color previous = GUI.color;
        GUI.color = tint;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private void DrawSignalTray(float scale, float width)
    {
        int slot = Story1Role.Current;
        int receiver = SignalRecipient(slot);
        int sender = SignalSender(slot);
        bool sent = HasChapterSignal(slot), received = HasChapterSignal(sender);
        if (width >= 480 * scale)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(width * .48f));
            DrawSignalCard($"{Roles.DefaultName(receiver)}에게 보낼 단서", SignalFor(receiver), sent);
            GUILayout.EndVertical();
            GUILayout.Space(12 * scale);
            GUILayout.BeginVertical();
            DrawSignalCard($"{Roles.DefaultName(sender)}에게 받은 단서", received ? SignalFor(slot) : "수신 대기", received);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
        else
        {
            DrawSignalCard($"{Roles.DefaultName(receiver)}에게 보낼 단서", SignalFor(receiver), sent);
            DrawSignalCard($"{Roles.DefaultName(sender)}에게 받은 단서", received ? SignalFor(slot) : "수신 대기", received);
        }

        if (GUILayout.Button(sent ? "단서 다시 전송" : $"{Roles.DefaultName(receiver)}에게 전송", button, GUILayout.Height(34 * scale)))
            ShareChapterSignal();
    }

    private void DrawSignalCard(string heading, string text, bool ready)
    {
        var header = GUILayoutUtility.GetRect(0, inspectionDetail.lineHeight + 6, GUILayout.ExpandWidth(true));
        FillBoard(header, Bay);
        FillBoard(new Rect(header.x, header.y, 3, header.height), ready ? Active : Brass);
        GUI.Label(new Rect(header.x + 10, header.y, header.width - 12, header.height), heading, inspectionDetail);
        GUILayout.Label(text, inspectionBody);
    }

    private void DrawTeamReadiness(float scale)
    {
        GUILayout.Space(8 * scale);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < Roles.Count; i++)
        {
            bool ready = !HumanAt(i) || IsChapterReady(openedChapterIndex, i);
            var card = GUILayoutUtility.GetRect(1, 34 * scale, GUILayout.ExpandWidth(true));
            FillBoard(card, ready ? new Color32(37, 69, 63, 255) : Bay);
            FillBoard(new Rect(card.x, card.yMax - 3, card.width, 3), ready ? Active : Divider);
            GUI.Label(card, Roles.DefaultName(i), BoardCenter(scale, 15));
        }
        GUILayout.EndHorizontal();
        if (IsChapterReady(openedChapterIndex, Story1Role.Current) && !chapterCorrect)
            GUILayout.Label("합류 대기 중 · 팀원이 각자 조작을 마치면 진행됩니다.", inspectionDetail);
    }

    private GUIStyle BoardCenter(float scale, int size)
    {
        if (boardCenter == null)
            boardCenter = new GUIStyle(inspectionBody) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        boardCenter.fontSize = Mathf.RoundToInt(size * scale);
        return boardCenter;
    }

    private void DrawActiveMechanism(float scale)
    {
        bool canUse = ChapterCanAnswer && !chapterCorrect && chapterAnswerIndex < openedChapter.Answers.Length;
        if (!ChapterCanAnswer)
            GUILayout.Label(HasChapterSignal(Story1Role.Current) ? "동료의 단서를 수신하면 조작할 수 있습니다." : "먼저 나에게 보이는 단서를 팀에 보내세요.", inspectionDetail);

        bool enabled = GUI.enabled;
        GUI.enabled = enabled && canUse;
        switch (openedChapterIndex)
        {
            case 1: DrawSwitchBank(scale, true); break;
            case 2:
            case 11: DrawSwitchBank(scale, false); break;
            case 3: DrawRescueLift(scale); break;
            case 5:
            case 6: DrawAssemblyBoard(scale); break;
            case 7: DrawTramMap(scale); break;
            default: DrawExhibitCards(scale); break;
        }
        GUI.enabled = enabled;
    }

    private Color ChoiceStatus(int option)
    {
        if (chapterLastWrong == option) return Fault;
        return selectedChapterOptions.Contains(openedChapter.Options[option]) ? Active : Divider;
    }

    private void MechanismButton(Rect rect, int option, float scale, int size)
    {
        FillBoard(rect, Bay);
        if (GUI.Button(rect, openedChapter.Options[option], BoardCenter(scale, size)))
            ChooseChapterAnswer(option);
        FillBoard(new Rect(rect.x + 4, rect.yMax - 4, rect.width - 8, 3), ChoiceStatus(option));
    }

    private void DrawSwitchBank(float scale, bool fans)
    {
        var panel = GUILayoutUtility.GetRect(1, 117 * scale, GUILayout.ExpandWidth(true));
        FillBoard(panel, Surface);
        float unit = panel.width / 3;
        for (int i = 0; i < 3; i++)
        {
            var x = panel.x + i * unit;
            var lamp = new Rect(x + unit * .5f - 6 * scale, panel.y + 14 * scale, 12 * scale, 12 * scale);
            FillBoard(lamp, ChoiceStatus(i));
            var control = new Rect(x + 8 * scale, panel.y + 37 * scale, unit - 16 * scale, 60 * scale);
            MechanismButton(control, i, scale, fans ? 16 : 18);
        }
        if (!fans) DrawOrderSlots(scale);
    }

    private void DrawOrderSlots(float scale)
    {
        var row = GUILayoutUtility.GetRect(1, 36 * scale, GUILayout.ExpandWidth(true));
        float width = row.width / openedChapter.Answers.Length;
        for (int i = 0; i < openedChapter.Answers.Length; i++)
        {
            var slot = new Rect(row.x + width * i + 2, row.y + 2, width - 4, row.height - 4);
            bool filled = i < selectedChapterOptions.Count;
            FillBoard(slot, filled ? Bay : Surface);
            GUI.Label(slot, filled ? selectedChapterOptions[i] : $"{i + 1}", BoardCenter(scale, 15));
        }
    }

    private void DrawAssemblyBoard(float scale)
    {
        DrawOrderSlots(scale);
        DrawExhibitCards(scale);
    }

    private void DrawExhibitCards(float scale)
    {
        int columns = openedChapter.Options.Length == 4 ? 2 : Mathf.Min(3, openedChapter.Options.Length);
        int rows = (openedChapter.Options.Length + columns - 1) / columns;
        var board = GUILayoutUtility.GetRect(1, (68 * rows + 4) * scale, GUILayout.ExpandWidth(true));
        FillBoard(board, Surface);
        float width = board.width / columns;
        for (int i = 0; i < openedChapter.Options.Length; i++)
        {
            var card = new Rect(board.x + i % columns * width + 4 * scale, board.y + (4 + i / columns * 68) * scale,
                width - 8 * scale, 60 * scale);
            MechanismButton(card, i, scale, 17);
        }
    }

    private static readonly int[] RackLevels = { 4, 3, 2, 1 };

    private void DrawRescueLift(float scale)
    {
        var board = GUILayoutUtility.GetRect(1, 185 * scale, GUILayout.ExpandWidth(true));
        FillBoard(board, Surface);
        FillBoard(new Rect(board.x, board.y + 100 * scale, board.width, 34 * scale), new Color32(37, 69, 85, 255));
        float width = board.width / 4;
        for (int i = 0; i < 4; i++)
        {
            float x = board.x + i * width + 4 * scale;
            float y = board.y + (137 - 20 * RackLevels[i]) * scale;
            FillBoard(new Rect(x + width * .25f, y, width * .44f, board.y + 137 * scale - y), Bay);
            FillBoard(new Rect(x + 5 * scale, y, width - 17 * scale, 5 * scale), ChoiceStatus(i));
            MechanismButton(new Rect(x, board.y + 137 * scale, width - 8 * scale, 44 * scale), i, scale, 14);
        }
    }

    private static readonly Vector2[] TramStops =
    {
        new Vector2(.81f, .50f), new Vector2(.17f, .50f), new Vector2(.49f, .24f), new Vector2(.49f, .76f)
    };

    private void DrawTramMap(float scale)
    {
        var map = GUILayoutUtility.GetRect(1, 220 * scale, GUILayout.ExpandWidth(true));
        FillBoard(map, Surface);
        Vector2 PointFor(int i) => map.position + Vector2.Scale(map.size, TramStops[i]);
        DrawTrack(PointFor(1), PointFor(2), TracksColor(1, 2), scale);
        DrawTrack(PointFor(2), PointFor(0), TracksColor(2, 0), scale);
        DrawTrack(PointFor(1), PointFor(3), TracksColor(1, 3), scale);
        DrawTrack(PointFor(3), PointFor(0), TracksColor(3, 0), scale);
        float w = Mathf.Min(115 * scale, map.width * .28f);
        for (int i = 0; i < TramStops.Length; i++)
        {
            var p = PointFor(i);
            MechanismButton(new Rect(p.x - w / 2, p.y - 19 * scale, w, 38 * scale), i, scale, 16);
        }
        DrawOrderSlots(scale);
    }

    private Color TracksColor(int from, int to) =>
        selectedChapterOptions.Contains(openedChapter.Options[from]) && selectedChapterOptions.Contains(openedChapter.Options[to])
            ? Active : Divider;

    private static void DrawTrack(Vector2 from, Vector2 to, Color tint, float scale)
    {
        Vector2 change = to - from;
        Matrix4x4 previous = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(change.y, change.x) * Mathf.Rad2Deg, from);
        FillBoard(new Rect(from.x, from.y - 2 * scale, change.magnitude, 4 * scale), tint);
        GUI.matrix = previous;
    }
}
