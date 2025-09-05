using BepInEx;
using BepInEx.Configuration;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using FrooxEngine.Undo;
using HarmonyLib;
using System.Reflection;

namespace DeleagateRefEditing;

[ResonitePlugin(PluginMetadata.GUID, PluginMetadata.NAME, PluginMetadata.VERSION, PluginMetadata.AUTHORS, PluginMetadata.REPOSITORY_URL)]
[BepInDependency(BepInExResoniteShim.PluginMetadata.GUID)]
public class Plugin : BasePlugin
{
    static readonly string FanyUiTag = "RetargetableDelegateEditor";
    static ConfigEntry<bool> uiGen, rightAllignNull;
    public override void Load()
    {
        uiGen = Config.Bind("options", "EnableUiGeneration", true, "when false disable generation of retargeting fields next to delegate editors");
        rightAllignNull = Config.Bind("options", "rightAllignNull", false, "when true moves the null button is on the right otherwise its between the fields");
        HarmonyInstance.PatchAll();
    }
    public override bool Unload() { try { HarmonyInstance.UnpatchSelf(); return true; } catch { return false; } }

    static readonly MethodInfo SetReference = AccessTools.Method(typeof(RefEditor), "SetReference");
    static readonly MethodInfo OpenWorkerInspectorButton = AccessTools.Method(typeof(RefEditor), "OpenWorkerInspectorButton");
    [HarmonyPatch(typeof(DelegateEditor), nameof(DelegateEditor.Setup))]
    class Patch
    {
        static void Postfix(DelegateEditor __instance, ISyncDelegate target)
        {
            if (!uiGen.Value) return;
            __instance.Slot.Tag = FanyUiTag;
            if (rightAllignNull.Value) __instance.Slot[0].Children.Last().OrderOffset = 10000;
            UIBuilder ui = new UIBuilder(__instance.Slot[0]);
            RadiantUI_Constants.SetupEditorStyle(ui);
            ui.Style.MinWidth = 24f;
            ui.Style.FlexibleWidth = 100f;
            Button refField = ui.Button();
            var refEdit = refField.Slot.AttachComponent<RefEditor>();
            refField.Pressed.Target = SetReference.CreateDelegate<ButtonEventHandler>(refEdit);
            Text textComp = refField.Slot.GetComponentInChildren<Text>();
            ((ISyncRef)refEdit.GetSyncMember("_textDrive")).Target = textComp.Content;
            ((ISyncRef)refEdit.GetSyncMember("_targetRef")).Target = target;
            ((ISyncRef)refEdit.GetSyncMember("_button")).Target = refField;

            ui.Style.FlexibleWidth = -1f;
            __instance.Slot[0][0].OrderOffset = -2;
            LocaleString text = "↑";
            var workerButton = ui.Button(in text);
            workerButton.Slot.OrderOffset = -1;
            workerButton.Pressed.Target = OpenWorkerInspectorButton.CreateDelegate<ButtonEventHandler>(refEdit);
        }
        [HarmonyPostfix]
        [HarmonyPatch("OnChanges")]
        static void OnChangesPostfix(DelegateEditor __instance, FieldDrive<string> ____textDrive, RelayRef<ISyncDelegate> ____targetDelegate)
        {
            if (__instance.Slot.Tag == FanyUiTag && ____textDrive.IsLinkValid && ____targetDelegate.Target
                is ISyncDelegate target && target.Target != null && target.Method != null)
            {
                ____textDrive.Target.Value = $"{target.MethodName} on {target.GetType().Name})";
            }
        }
    }


    [HarmonyPatch]
    class UndoPatch
    {
        static MethodBase TargetMethod() => typeof(SetReferenceExtensions).GetMethods().First(m => m.Name == nameof(SetReferenceExtensions.CreateUndoPoint) && !m.IsGenericMethod);
        static bool Prefix(ISyncRef reference) => reference.GetType().IsAssignableTo(typeof(SyncRef<>));
    }
}
