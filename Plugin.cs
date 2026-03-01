using BepInEx;
using SOD.Common;
using SOD.Common.BepInEx;
using System;
using System.Reflection;

using OutOfThePast.Patches.SideJobPatches;
using OutOfThePast.Patches.DialoguePatches;
using OutOfThePast.Patches.BugFixPatches;
using OutOfThePast.Patches.UIPatches;

namespace OutOfThePast;


[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
[BepInDependency("Venomaus.SOD.Common")]
public class Plugin : PluginController<Plugin, IPluginBindings>
{
    public const string PLUGIN_GUID = "Sclamy.OutOfThePast";
    public const string PLUGIN_NAME = "OutOfThePast";
    public const string PLUGIN_VERSION = "0.3.0";

    public override void Load()
    {
        PatchIfEnabled(typeof(AdjustPayphoneCallDelay), Config.PatchEnablePayphoneCallDelay);
        PatchIfEnabled(typeof(SitAndTalk), Config.PatchEnableSitAndTalk);
        PatchIfEnabled(typeof(PassTimeImprovements), Config.PatchEnablePassTimeBugFixes);
        PatchIfEnabled(typeof(SuppressAllTargetBrackets), Config.SuppressAllTargetBrackets);

        Log.LogInfo($"Plugin {PLUGIN_GUID} v{PLUGIN_VERSION} is loaded!");
    }

    private void PatchIfEnabled(Type patchType, bool enabled)
    {
        if (!enabled) return;
        Harmony.CreateClassProcessor(patchType).Patch();
        foreach (var type in patchType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            Harmony.CreateClassProcessor(type).Patch();
    }
}
