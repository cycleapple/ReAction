using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Hypostasis.Game.Structures;
using Lumina.Excel.Sheets;
using Action = Lumina.Excel.Sheets.Action;

namespace ReAction;

public static class PluginUI
{
    private static bool isVisible = false;
    private static int selectedStack = -1;
    private static int hotbar = 0;
    private static int hotbarSlot = 0;
    private static int commandType = 1;
    private static uint commandID = 0;

    public static bool IsVisible
    {
        get => isVisible;
        set => isVisible = value;
    }

    private static Configuration.ActionStack CurrentStack => 0 <= selectedStack && selectedStack < ReAction.Config.ActionStacks.Count ? ReAction.Config.ActionStacks[selectedStack] : null;

    public static void Draw()
    {
        if (!isVisible) return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(700, 600) * ImGuiHelpers.GlobalScale, new Vector2(9999));
        ImGui.Begin("ReAction 設定", ref isVisible);
        ImGuiEx.AddDonationHeader();

        if (ImGui.BeginTabBar("ReActionTabs"))
        {
            if (ImGui.BeginTabItem("技能堆疊"))
            {
                DrawStackList();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("其他設定"))
            {
                ImGui.BeginChild("OtherSettings");
                DrawOtherSettings();
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("自訂代名詞"))
            {
                DrawCustomPlaceholders();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("說明"))
            {
                DrawStackHelp();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private static void DrawStackList()
    {
        var currentStack = CurrentStack;
        var hasSelectedStack = currentStack != null;

        ImGui.PushFont(UiBuilder.IconFont);

        var buttonSize = ImGui.CalcTextSize(FontAwesomeIcon.SignOutAlt.ToIconString()) + ImGui.GetStyle().FramePadding * 2;

        if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString(), buttonSize))
        {
            ReAction.Config.ActionStacks.Add(new() { Name = "新堆疊" });
            ReAction.Config.Save();
        }

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.SignOutAlt.ToIconString(), buttonSize) && hasSelectedStack)
            ImGui.SetClipboardText(Configuration.ExportActionStack(CurrentStack));
        ImGui.PopFont();
        ImGuiEx.SetItemTooltip("將堆疊匯出至剪貼簿。");
        ImGui.PushFont(UiBuilder.IconFont);

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.SignInAlt.ToIconString(), buttonSize))
        {
            try
            {
                var stack = Configuration.ImportActionStack(ImGui.GetClipboardText());
                ReAction.Config.ActionStacks.Add(stack);
                ReAction.Config.Save();
            }
            catch (Exception e)
            {
                DalamudApi.PrintError($"無法從剪貼簿匯入堆疊！\n{e.Message}");
            }
        }
        ImGui.PopFont();
        ImGuiEx.SetItemTooltip("從剪貼簿匯入堆疊。");
        ImGui.PushFont(UiBuilder.IconFont);

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.ArrowUp.ToIconString(), buttonSize) && hasSelectedStack)
        {
            var preset = CurrentStack;
            ReAction.Config.ActionStacks.RemoveAt(selectedStack);

            selectedStack = Math.Max(selectedStack - 1, 0);

            ReAction.Config.ActionStacks.Insert(selectedStack, preset);
            ReAction.Config.Save();
        }

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.ArrowDown.ToIconString(), buttonSize) && hasSelectedStack)
        {
            var preset = CurrentStack;
            ReAction.Config.ActionStacks.RemoveAt(selectedStack);

            selectedStack = Math.Min(selectedStack + 1, ReAction.Config.ActionStacks.Count);

            ReAction.Config.ActionStacks.Insert(selectedStack, preset);
            ReAction.Config.Save();
        }

        ImGui.PopFont();

        ImGui.SameLine();

        if (ImGuiEx.DeleteConfirmationButton(buttonSize) && hasSelectedStack)
        {
            ReAction.Config.ActionStacks.RemoveAt(selectedStack);
            selectedStack = Math.Min(selectedStack, ReAction.Config.ActionStacks.Count - 1);
            currentStack = CurrentStack;
            hasSelectedStack = currentStack != null;
            ReAction.Config.Save();
        }

        var firstColumnWidth = 250 * ImGuiHelpers.GlobalScale;
        ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
        ImGui.BeginChild("ReActionPresetList", new Vector2(firstColumnWidth, ImGui.GetContentRegionAvail().Y / 2), true);
        ImGui.PopStyleColor();

        for (int i = 0; i < ReAction.Config.ActionStacks.Count; i++)
        {
            ImGui.PushID(i);

            var preset = ReAction.Config.ActionStacks[i];

            if (ImGui.Selectable(preset.Name, selectedStack == i))
                selectedStack = i;

            ImGui.PopID();
        }

        ImGui.EndChild();

        if (!hasSelectedStack) return;

        var lastCursorPos = ImGui.GetCursorPos();
        ImGui.SameLine();
        var nextLineCursorPos = ImGui.GetCursorPos();
        ImGui.SetCursorPos(lastCursorPos);

        ImGui.BeginChild("ReActionStackEditorMain", new Vector2(firstColumnWidth, ImGui.GetContentRegionAvail().Y), true);
        DrawStackEditorMain(currentStack);
        ImGui.EndChild();

        ImGui.SetCursorPos(nextLineCursorPos);
        ImGui.BeginChild("ReActionStackEditorLists", ImGui.GetContentRegionAvail(), false);
        DrawStackEditorLists(currentStack);
        ImGui.EndChild();
    }

    private static void DrawStackEditorMain(Configuration.ActionStack stack)
    {
        var save = false;

        save |= ImGui.InputText("名稱", ref stack.Name, 64);
        save |= ImGui.CheckboxFlags("##Shift", ref stack.ModifierKeys, 1u);
        ImGuiEx.SetItemTooltip("Shift 鍵");
        ImGui.SameLine();
        save |= ImGui.CheckboxFlags("##Ctrl", ref stack.ModifierKeys, 2u);
        ImGuiEx.SetItemTooltip("Ctrl 鍵");
        ImGui.SameLine();
        save |= ImGui.CheckboxFlags("##Alt", ref stack.ModifierKeys, 4u);
        ImGuiEx.SetItemTooltip("Alt 鍵");
        ImGui.SameLine();
        save |= ImGui.CheckboxFlags("##Exact", ref stack.ModifierKeys, 8u);
        ImGuiEx.SetItemTooltip("必須完全符合所選輔助鍵。例如勾選 Shift + Ctrl 時，只會符合按住 Shift + Ctrl，不會符合 Shift + Ctrl + Alt。");
        ImGui.SameLine();
        ImGui.TextUnformatted("輔助鍵");
        save |= ImGui.Checkbox("堆疊失敗時阻擋原技能", ref stack.BlockOriginal);
        save |= ImGui.Checkbox("超出距離時視為失敗", ref stack.CheckRange);
        save |= ImGui.Checkbox("冷卻中時視為失敗", ref stack.CheckCooldown);
        ImGuiEx.SetItemTooltip("若技能因冷卻而無法進入佇列，則判定失敗。" +
            "\n包括冷卻剩餘時間大於 0.5 秒，或距離上次使用未滿 0.5 秒（充能／GCD）。");

        if (save)
            ReAction.Config.Save();
    }

    private static void DrawStackEditorLists(Configuration.ActionStack stack)
    {
        DrawActionEditor(stack);
        DrawItemEditor(stack);
    }

    private static string FormatActionRow(Action a) => a.RowId switch
    {
                0 => "所有技能",
                1 => "所有攻擊技能",
                2 => "所有輔助技能",
        _ => $"[#{a.RowId} {a.ClassJob.ValueNullable?.Abbreviation}{(a.IsPvP ? " PVP" : string.Empty)}] {a.Name}"
    };

    private static readonly ImGuiEx.ExcelSheetComboOptions<Action> actionComboOptions = new()
    {
        FormatRow = FormatActionRow,
        FilteredSheet = DalamudApi.DataManager.GetExcelSheet<Action>().Take(3).Concat(ReAction.actionSheet.Select(kv => kv.Value))
    };

    private static readonly ImGuiEx.ExcelSheetPopupOptions<Action> actionPopupOptions = new()
    {
        FormatRow = FormatActionRow,
        FilteredSheet = actionComboOptions.FilteredSheet
    };

    private static void DrawActionEditor(Configuration.ActionStack stack)
    {
        var contentRegion = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("ReActionActionEditor", contentRegion with { Y = contentRegion.Y / 2 }, true);

        var buttonWidth = ImGui.GetContentRegionAvail().X / 2;
        var buttonIndent = 0f;
        for (int i = 0; i < stack.Actions.Count; i++)
        {
            using var _ = ImGuiEx.IDBlock.Begin(i);
            var action = stack.Actions[i];

            ImGui.Button("≡");
            if (ImGuiEx.IsItemDraggedDelta(action, ImGuiMouseButton.Left, ImGui.GetFrameHeightWithSpacing(), false, out var dt) && dt.Y != 0)
                stack.Actions.Shift(i, dt.Y);

            if (i == 0)
                buttonIndent = ImGui.GetItemRectSize().X + ImGui.GetStyle().ItemSpacing.X;

            ImGui.SameLine();

            ImGui.SetNextItemWidth(buttonWidth);
            if (ImGuiEx.ExcelSheetCombo("##Action", ref action.ID, actionComboOptions))
                ReAction.Config.Save();

            ImGui.SameLine();

        if (ImGui.Checkbox("使用變化後 ID", ref action.UseAdjustedID))
                ReAction.Config.Save();
            var detectedAdjustment = false;
            unsafe
            {
                if (!action.UseAdjustedID && (detectedAdjustment = Common.ActionManager->CS.GetAdjustedActionId(action.ID) != action.ID))
                    ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 0x2000FF30, ImGui.GetStyle().FrameRounding);
            }
            ImGuiEx.SetItemTooltip("讓技能也能符合其變化後的其他技能。" +
                "\n例如「疾風」可符合「天輝」、「出卡」可符合所有卡片、「診斷」可符合「均衡診斷」等。" +
                "\n會升級的技能建議啟用；若要相容部分連段插件則可停用。" +
                (detectedAdjustment ? "\n\n此技能目前因特性、連段或插件而發生變化，建議啟用此選項。" : string.Empty));

            ImGui.SameLine();

            if (!ImGuiEx.DeleteConfirmationButton()) continue;
            stack.Actions.RemoveAt(i);
            ReAction.Config.Save();
        }

        using (ImGuiEx.IndentBlock.Begin(buttonIndent))
        {
            ImGuiEx.FontButton(FontAwesomeIcon.Plus.ToIconString(), UiBuilder.IconFont, new Vector2(buttonWidth, 0));
            if (ImGuiEx.ExcelSheetPopup("ReActionAddSkillsPopup", out var row, actionPopupOptions))
            {
                stack.Actions.Add(new() { ID = row });
                ReAction.Config.Save();
            }
        }

        ImGui.EndChild();
    }

    private static string FormatOverrideActionRow(Action a) => a.RowId switch
    {
                0 => "相同技能",
        _ => $"[#{a.RowId} {a.ClassJob.ValueNullable?.Abbreviation}{(a.IsPvP ? " PVP" : string.Empty)}] {a.Name}"
    };

    private static readonly ImGuiEx.ExcelSheetComboOptions<Action> actionOverrideComboOptions = new()
    {
        FormatRow = FormatOverrideActionRow,
        FilteredSheet = DalamudApi.DataManager.GetExcelSheet<Action>().Take(1).Concat(ReAction.actionSheet.Select(kv => kv.Value))
    };

    private static void DrawItemEditor(Configuration.ActionStack stack)
    {
        ImGui.BeginChild("ReActionItemEditor", ImGui.GetContentRegionAvail(), true);

        var buttonWidth = ImGui.GetContentRegionAvail().X / 3;
        var buttonIndent = 0f;
        for (int i = 0; i < stack.Items.Count; i++)
        {
            using var _ = ImGuiEx.IDBlock.Begin(i);
            var item = stack.Items[i];

            ImGui.Button("≡");
            if (ImGuiEx.IsItemDraggedDelta(item, ImGuiMouseButton.Left, ImGui.GetFrameHeightWithSpacing(), false, out var dt) && dt.Y != 0)
                stack.Items.Shift(i, dt.Y);

            if (i == 0)
                buttonIndent = ImGui.GetItemRectSize().X + ImGui.GetStyle().ItemSpacing.X;

            ImGui.SameLine();

            ImGui.SetNextItemWidth(buttonWidth);
            if (DrawTargetTypeCombo("##TargetType", ref item.TargetID))
                ReAction.Config.Save();

            ImGui.SameLine();

            ImGui.SetNextItemWidth(buttonWidth);
            if (ImGuiEx.ExcelSheetCombo("##ActionOverride", ref item.ID, actionOverrideComboOptions))
                ReAction.Config.Save();

            ImGui.SameLine();

            if (!ImGuiEx.DeleteConfirmationButton()) continue;
            stack.Items.RemoveAt(i);
            ReAction.Config.Save();
        }

        using (ImGuiEx.IndentBlock.Begin(buttonIndent))
        {
            if (ImGuiEx.FontButton(FontAwesomeIcon.Plus.ToIconString(), UiBuilder.IconFont, new Vector2(buttonWidth, 0)))
            {
                stack.Items.Add(new());
                ReAction.Config.Save();
            }
        }

        ImGui.EndChild();
    }

    private static bool DrawTargetTypeCombo(string label, ref uint currentSelection)
    {
        if (!ImGui.BeginCombo(label, PronounManager.GetPronounName(currentSelection))) return false;

        var ret = false;
        foreach (var id in PronounManager.OrderedIDs)
        {
            if (!ImGui.Selectable(PronounManager.GetPronounName(id), id == currentSelection)) continue;
            currentSelection = id;
            ret = true;
            break;
        }

        ImGui.EndCombo();
        return ret;
    }

    private static void DrawOtherSettings()
    {
        var save = false;

        if (ImGuiEx.BeginGroupBox("技能", 0.5f))
        {
            save |= ImGui.Checkbox("啟用快捷欄按鍵連發", ref ReAction.Config.EnableTurboHotbars);
            ImGuiEx.SetItemTooltip("可持續按住快捷欄按鍵（不支援控制器）。\n警告：文字巨集可能會重複洗頻。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableTurboHotbars))
            {
                ImGuiEx.Prefix(false);
                save |= ImGui.DragInt("間隔", ref ReAction.Config.TurboHotbarInterval, 0.5f, 0, 1000, "%d 毫秒");

                ImGuiEx.Prefix(false);
                save |= ImGui.DragInt("開始延遲", ref ReAction.Config.InitialTurboHotbarInterval, 0.5f, 0, 1000, "%d 毫秒");

                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("非戰鬥狀態也啟用##Turbo", ref ReAction.Config.EnableTurboHotbarsOutOfCombat);
            }

            save |= ImGui.Checkbox("啟用立即放置地面目標", ref ReAction.Config.EnableInstantGroundTarget);
            ImGuiEx.SetItemTooltip("若堆疊未覆寫目標，地面指定技能會立即放置在目前游標位置。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableInstantGroundTarget))
            {
                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("排除其他地面指定技能", ref ReAction.Config.EnableBlockMiscInstantGroundTargets);
                ImGuiEx.SetItemTooltip("避免前一選項套用到放置召喚物等技能。");
            }

            save |= ImGui.Checkbox("啟用增強型自動面向目標", ref ReAction.Config.EnableEnhancedAutoFaceTarget);
            ImGuiEx.SetItemTooltip("不需要面向目標的技能（例如治療）將不再自動轉向目標。");

            save |= ImGui.Checkbox("引導與方向技能依鏡頭方向", ref ReAction.Config.EnableCameraRelativeDirectionals);
            ImGuiEx.SetItemTooltip("讓「武裝戍衛」或「蘇利耶之祥光」等引導與方向技能依照鏡頭朝向，而非角色朝向。");

            save |= ImGui.Checkbox("位移技能依鏡頭方向", ref ReAction.Config.EnableCameraRelativeDashes);
            ImGuiEx.SetItemTooltip("讓「前衝步」與「回避跳躍」等位移技能依照鏡頭朝向，而非角色朝向。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableCameraRelativeDashes))
            {
                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("排除後退型位移", ref ReAction.Config.EnableNormalBackwardDashes);
                ImGuiEx.SetItemTooltip("讓「回避跳躍」等後退型位移不套用前一選項。");
            }

            ImGuiEx.EndGroupBox();
        }

        ImGui.SameLine();

        if (ImGuiEx.BeginGroupBox("自動功能", 0.5f))
        {
            save |= ImGui.Checkbox("啟用自動下坐騎", ref ReAction.Config.EnableAutoDismount);
            ImGuiEx.SetItemTooltip("使用技能時會先自動下坐騎，再執行技能。");

            save |= ImGui.Checkbox("啟用自動中斷詠唱", ref ReAction.Config.EnableAutoCastCancel);
            ImGuiEx.SetItemTooltip("目標死亡時自動中斷詠唱。");

            save |= ImGui.Checkbox("啟用自動選取目標", ref ReAction.Config.EnableAutoTarget);
            ImGuiEx.SetItemTooltip("使用需要目標的攻擊且未指定目標時，自動選取最近的敵人。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableAutoTarget))
            {
                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("啟用自動切換目標", ref ReAction.Config.EnableAutoChangeTarget);
                ImGuiEx.SetItemTooltip("使用需要目標的攻擊但目前主要目標不適用時，改為選取最近的敵人。");
            }

            var _ = ReAction.Config.AutoFocusTargetID != 0;
            if (ImGui.Checkbox("啟用自動焦點目標", ref _))
            {
                ReAction.Config.AutoFocusTargetID = _ ? PronounManager.OrderedIDs.First() : 0;
                save = true;
            }
            ImGuiEx.SetItemTooltip("在可行時，自動將指定目標類型設為焦點目標。");

            using (ImGuiEx.DisabledBlock.Begin(!_))
            {
                ImGuiEx.Prefix(false);
                save |= DrawTargetTypeCombo("##AutoFocusTargetID", ref ReAction.Config.AutoFocusTargetID);

                ImGuiEx.Prefix(true);
                save |= ImGui.Checkbox("非戰鬥狀態也啟用##AutoFocusTarget", ref ReAction.Config.EnableAutoFocusTargetOutOfCombat);
            }

            save |= ImGui.Checkbox("啟用自動恢復焦點目標", ref ReAction.Config.EnableAutoRefocusTarget);
            ImGuiEx.SetItemTooltip("在任務中失去焦點目標時，嘗試重新將先前的目標設為焦點目標。");

            save |= ImGui.Checkbox("魔法也會啟動自動攻擊", ref ReAction.Config.EnableSpellAutoAttacks);
            ImGuiEx.SetItemTooltip("讓魔法（以及部分其他技能）像戰技一樣開始自動攻擊。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableSpellAutoAttacks))
            {
                ImGuiEx.Prefix(true);
                if (ImGui.Checkbox("非戰鬥狀態也啟用##SpellAutos", ref ReAction.Config.EnableSpellAutoAttacksOutOfCombat))
                {
                    if (ReAction.Config.EnableSpellAutoAttacksOutOfCombat)
                        Game.spellAutoAttackPatch.Enable();
                    else
                        Game.spellAutoAttackPatch.Disable();
                    save = true;
                }
                ImGuiEx.SetItemTooltip("警告：這可能會讓部分首領提前開戰！");
            }

            ImGuiEx.EndGroupBox();
        }

        if (ImGuiEx.BeginGroupBox("技能佇列", 0.5f))
        {
            if (ImGui.Checkbox("啟用地面指定技能佇列", ref ReAction.Config.EnableGroundTargetQueuing))
            {
                Game.queueGroundTargetsPatch.Toggle();
                save = true;
            }
            ImGuiEx.SetItemTooltip("地面指定技能會進入技能佇列，並像其他能力技一樣在可用時立即施放。");

            save |= ImGui.Checkbox("允許更多技能進入佇列", ref ReAction.Config.EnableQueuingMore);
            ImGuiEx.SetItemTooltip("允許所有道具及極限技進入技能佇列。");

            save |= ImGui.Checkbox("巨集一律使用佇列", ref ReAction.Config.EnableMacroQueue);
            ImGuiEx.SetItemTooltip("所有巨集都會以使用 /macroqueue 時的方式運作。");

            save |= ImGui.Checkbox("啟用佇列調整（測試版）", ref ReAction.Config.EnableQueueAdjustments);
            ImGuiEx.SetItemTooltip("調整遊戲處理技能佇列的方式。\n此功能仍在測試，如有異常請回報。");

            using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableQueueAdjustments))
            using (ImGuiEx.ItemWidthBlock.Begin(ImGui.CalcItemWidth() / 2))
            {
                ImGuiEx.Prefix(false);
                save |= ImGui.Checkbox("##Enable GCD Adjusted Threshold", ref ReAction.Config.EnableGCDAdjustedQueueThreshold);
                ImGuiEx.SetItemTooltip("依目前 GCD 調整臨界值。");

                ImGui.SameLine();
                save |= ImGui.SliderFloat("佇列臨界值", ref ReAction.Config.QueueThreshold, 0.1f, 2.5f, "%.1f");
                ImGuiEx.SetItemTooltip("Time remaining on an action's cooldown to allow the game\nto queue up the next one when pressed early. Default: 0.5." +
                    (ReAction.Config.EnableGCDAdjustedQueueThreshold ? $"\nGCD Adjusted Threshold: {ReAction.Config.QueueThreshold * ActionManager.GCDRecast / 2500f}" : string.Empty));

                ImGui.BeginGroup();
                ImGuiEx.Prefix(false);
                save |= ImGui.Checkbox("##Enable Requeuing", ref ReAction.Config.EnableRequeuing);
                using (ImGuiEx.DisabledBlock.Begin(!ReAction.Config.EnableRequeuing))
                {
                    ImGui.SameLine();
                save |= ImGui.SliderFloat("佇列鎖定臨界值", ref ReAction.Config.QueueLockThreshold, 0.1f, 2.5f, "%.1f");
                }
                ImGui.EndGroup();
                ImGuiEx.SetItemTooltip("啟用後，只要佇列中技能的冷卻尚未低於此數值，就允許重新排入佇列。");

                ImGuiEx.Prefix(false);
                save |= ImGui.SliderFloat("技能鎖定時間", ref ReAction.Config.QueueActionLockout, 0, 2.5f, "%.1f");
                ImGuiEx.SetItemTooltip("技能進入冷卻後未滿此時間時，阻止相同技能再次進入佇列。");

                ImGuiEx.Prefix(true);
            save |= ImGui.Checkbox("啟用 GCD 滑步詠唱佇列", ref ReAction.Config.EnableSlidecastQueuing);
            ImGuiEx.SetItemTooltip("在 GCD 詠唱最後 0.5 秒內允許將下一個 GCD 排入佇列。");
            }

            ImGuiEx.EndGroupBox();
        }

        ImGui.SameLine();

        if (ImGuiEx.BeginGroupBox("拆分連段", 0.5f))
        {
            save |= ImGui.Checkbox("拆分「火炎之紅」", ref ReAction.Config.EnableDecomboFireInRed);
            ImGuiEx.SetItemTooltip("移除「火炎之紅」連段。若要再次使用，需透過下方快捷欄功能放回快捷欄。\n疾風之綠 ID：34651\n流水之藍 ID：34652");

            save |= ImGui.Checkbox("拆分「高烈炎之紅」", ref ReAction.Config.EnableDecomboFire2InRed);
            ImGuiEx.SetItemTooltip("移除「高烈炎之紅」連段。若要再次使用，需透過下方快捷欄功能放回快捷欄。\n烈風之綠 ID：34657\n高壓水流之藍 ID：34658");

            save |= ImGui.Checkbox("拆分「冰結之青」", ref ReAction.Config.EnableDecomboBlizzardInCyan);
            ImGuiEx.SetItemTooltip("移除「冰結之青」連段。若要再次使用，需透過下方快捷欄功能放回快捷欄。\n飛石之黃 ID：34654\n閃雷之紫 ID：34655");

            save |= ImGui.Checkbox("拆分「高冰結之青」", ref ReAction.Config.EnableDecomboBlizzard2InCyan);
            ImGuiEx.SetItemTooltip("移除「高冰結之青」連段。若要再次使用，需透過下方快捷欄功能放回快捷欄。\n堅石之黃 ID：34660\n震雷之紫 ID：34661");

            save |= ImGui.Checkbox("拆分「禮儀之鈴」", ref ReAction.Config.EnableDecomboLiturgy);
            ImGuiEx.SetItemTooltip("移除「禮儀之鈴」連段。若要再次使用，需透過下方快捷欄功能放回快捷欄。\n禮儀之鈴（引爆）ID：28509");

            save |= ImGui.Checkbox("拆分「地星」", ref ReAction.Config.EnableDecomboEarthlyStar);
            ImGuiEx.SetItemTooltip("移除「地星」連段。若要再次使用，需透過下方快捷欄功能放回快捷欄。\n星體爆轟 ID：8324");

            ImGuiEx.EndGroupBox();
        }

        if (ImGuiEx.BeginGroupBox("其他", 0.5f))
        {
            save |= ImGui.Checkbox("啟用幀對齊", ref ReAction.Config.EnableFrameAlignment);
            ImGuiEx.SetItemTooltip("將遊戲幀與 GCD 及動作鎖定對齊。\n注意：任一計時結束時可能出現幾乎無法察覺的頓挫。");

            if (ImGui.Checkbox("允許小數等待時間（Fractionality）", ref ReAction.Config.EnableFractionality))
            {
                Game.waitSyntaxDecimalPatch.Toggle();
                Game.waitCommandDecimalPatch.Toggle();
                save = true;
            }
            ImGuiEx.SetItemTooltip("允許等待指令使用小數，並移除 60 秒上限（例如 <wait.0.5> 或 /wait 0.5）。");

            if (ImGui.Checkbox("允許指令使用不可配置技能", ref ReAction.Config.EnableUnassignableActions))
            {
                Game.allowUnassignableActionsPatch.Toggle();
                save = true;
            }
            ImGuiEx.SetItemTooltip("允許在「/ac」中使用一般無法直接配置的技能，例如「陰陽鬥氣斬」或「星體爆轟」。");

            save |= ImGui.Checkbox("允許指令使用玩家名稱", ref ReAction.Config.EnablePlayerNamesInCommands);
            ImGuiEx.SetItemTooltip("需要目標的指令可使用「名字 姓氏@伺服器」格式。");

            ImGuiEx.EndGroupBox();
        }

        ImGui.SameLine();

        if (ImGuiEx.BeginGroupBox("放置到快捷欄（滑鼠移入查看說明）", 0.5f, new ImGuiEx.GroupBoxOptions
        {
            HeaderTextAction = () => ImGuiEx.SetItemTooltip(
                "可將一般無法配置的各種項目放到快捷欄。" +
                "\n若不清楚用途，請勿操作。" +
                "\n用途範例：" +
                "\n\t將特定技能放到快捷欄，以搭配「拆分連段」功能；ID 可在各選項提示中查看。" +
                "\n\t將特定打瞌睡或坐下情感動作放到快捷欄（Emote，88 與 95）。" +
                "\n\t將貨幣（Item，1–99）放到快捷欄，不開啟貨幣選單也能查看持有數量。" +
                "\n\t恢復飛行坐騎隨機召喚（GeneralAction，24）。")
        }))
        {
            ImGui.Combo("快捷欄", ref hotbar, "1\02\03\04\05\06\07\08\09\010\0XHB 1\0XHB 2\0XHB 3\0XHB 4\0XHB 5\0XHB 6\0XHB 7\0XHB 8");
            ImGui.Combo("欄位", ref hotbarSlot, "1\02\03\04\05\06\07\08\09\010\011\012\013\014\015\016");
            var hotbarSlotType = Enum.GetName(typeof(RaptureHotbarModule.HotbarSlotType), commandType) ?? commandType.ToString();
            if (ImGui.BeginCombo("類型", hotbarSlotType))
            {
                for (int i = 1; i <= 32; i++)
                {
                    if (!ImGui.Selectable($"{Enum.GetName(typeof(RaptureHotbarModule.HotbarSlotType), i) ?? i.ToString()}##{i}", commandType == i)) continue;
                    commandType = i;
                }
                ImGui.EndCombo();
            }

            DrawHotbarIDInput((RaptureHotbarModule.HotbarSlotType)commandType);

            if (ImGui.Button("執行"))
                Game.SetHotbarSlot(hotbar, hotbarSlot, (byte)commandType, commandID);
            ImGuiEx.EndGroupBox();
        }

        if (save)
            ReAction.Config.Save();
    }

    public static void DrawHotbarIDInput(RaptureHotbarModule.HotbarSlotType slotType)
    {
        switch ((RaptureHotbarModule.HotbarSlotType)commandType)
        {
            case RaptureHotbarModule.HotbarSlotType.Action:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Action> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Item:
                const int hqID = 1_000_000;
                var _ = commandID >= hqID ? commandID - hqID : commandID;
                if (ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref _, new ImGuiEx.ExcelSheetComboOptions<Item> { FormatRow = r => $"[#{r.RowId}] {r.Name}" }))
                    commandID = commandID >= hqID ? _ + hqID : _;
                var hq = commandID >= hqID;
                    if (ImGui.Checkbox("高品質", ref hq))
                    commandID = hq ? commandID + hqID : commandID - hqID;
                break;
            case RaptureHotbarModule.HotbarSlotType.EventItem:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<EventItem> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Emote:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Emote> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Marker:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Marker> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.CraftAction:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<CraftAction> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.GeneralAction:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<GeneralAction> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.BuddyAction:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<BuddyAction> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.MainCommand:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<MainCommand> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Companion:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Companion> { FormatRow = r => $"[#{r.RowId}] {r.Singular}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.PetAction:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<PetAction> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Mount:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Mount> { FormatRow = r => $"[#{r.RowId}] {r.Singular}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.FieldMarker:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<FieldMarker> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Recipe:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Recipe> { FormatRow = r => $"[#{r.RowId}] {r.ItemResult.ValueNullable?.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.ChocoboRaceAbility:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<ChocoboRaceAbility> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.ChocoboRaceItem:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<ChocoboRaceItem> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.ExtraCommand:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<ExtraCommand> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.PvPQuickChat:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<QuickChat> { FormatRow = r => $"[#{r.RowId}] {r.NameAction}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.PvPCombo:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<ActionComboRoute> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.BgcArmyAction:
                // Sheet is BgcArmyAction, but it doesn't appear to be in Lumina
                var __ = (int)commandID;
                if (ImGui.Combo("ID", ref __, "[#0]\0[#1] Engage\0[#2] Disengage\0[#3] Re-engage\0[#4] Execute Limit Break\0[#5] Display Order Hotbar"))
                    commandID = (uint)__;
                break;
            case RaptureHotbarModule.HotbarSlotType.PerformanceInstrument:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Perform> { FormatRow = r => $"[#{r.RowId}] {r.Instrument}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.McGuffin:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<McGuffin> { FormatRow = r => $"[#{r.RowId}] {r.UIData.ValueNullable?.Name}" });
                break;
            case RaptureHotbarModule.HotbarSlotType.Ornament:
                ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<Ornament> { FormatRow = r => $"[#{r.RowId}] {r.Singular}" });
                break;
            // Doesn't appear to have a sheet
            //case HotbarSlotType.LostFindsItem:
            //    ImGuiEx.ExcelSheetCombo($"ID##{commandType}", ref commandID, new ImGuiEx.ExcelSheetComboOptions<> { FormatRow = r => $"[#{r.RowId}] {r.Name}" });
            //    break;
            default:
                var ___ = (int)commandID;
                if (ImGui.InputInt("ID", ref ___))
                    commandID = (uint)___;
                break;
        }
    }

    private static unsafe void DrawCustomPlaceholders()
    {
        if (!ImGui.BeginTable("CustomPronounInfoTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("名稱");
        ImGui.TableSetupColumn("代名詞");
        ImGui.TableSetupColumn("目前目標");
        ImGui.TableHeadersRow();

        foreach (var (placeholder, pronoun) in PronounManager.CustomPlaceholders)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGui.TextUnformatted(pronoun.Name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(placeholder);

            var p = pronoun.GetGameObject();
            if (p == null) continue;
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(p->NameString);
        }

        ImGui.EndTable();
    }

    private static void DrawStackHelp()
    {
        ImGui.Text("建立堆疊");
        ImGui.Indent();
        ImGui.TextWrapped("按一下左上角的＋按鈕即可建立新堆疊，接著便可新增技能與功能。");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("編輯堆疊");
        ImGui.Indent();
        ImGui.TextWrapped("從左上方清單選取堆疊後會顯示編輯區。左下方區域包含主要設定，可調整該堆疊本身的基本運作方式。");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("編輯堆疊技能");
        ImGui.Indent();
        ImGui.TextWrapped("右上方區域用於新增技能；按一下＋即可開啟搜尋視窗。新增要調整的技能後，可選擇是否使用「變化後 ID」。" +
            "啟用後，技能也會符合快捷欄上取代它的其他技能，例如因特性升級、狀態效果或其他連段插件而產生的變化。" +
            "若變化後的技能另有用途，可停用此選項。技能目前正被遊戲或插件變化時，選項會以綠色標示。");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("編輯堆疊功能");
        ImGui.Indent();
        ImGui.TextWrapped("右下方區域可設定目標清單，以擴充或取代遊戲原本的選取方式。使用技能時，插件會由上而下尋找第一個有效目標，" +
            "並在遊戲自身的目標優先系統之前執行。若堆疊未阻擋原技能，遊戲才會繼續處理；找到有效目標時，也會套用已設定的技能覆寫。");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("堆疊優先順序");
        ImGui.Indent();
        ImGui.TextWrapped("插件會由上而下尋找第一個包含目前技能、且輔助鍵符合的堆疊。若使用「所有技能」，可在其上方加入更高優先的覆寫堆疊。" +
            "堆疊也可以不包含任何功能，用來讓指定技能避開「所有技能」的變更並維持原本行為。");
        ImGui.Unindent();
    }
}
