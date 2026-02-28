using BepInEx;
using SOD.Common;
using SOD.Common.BepInEx;
using System;
using System.Reflection;

using OutOfThePast.Patches.SideJobPatches;
using OutOfThePast.Patches.DialoguePatches;

namespace OutOfThePast;


[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
[BepInDependency("Venomaus.SOD.Common")]
public class Plugin : PluginController<Plugin, IPluginBindings>
{
    public const string PLUGIN_GUID = "Sclamy.OutOfThePast";
    public const string PLUGIN_NAME = "OutOfThePast";
    public const string PLUGIN_VERSION = "0.2.0";

    public override void Load()
    {
        PatchIfEnabled(typeof(AdjustPayphoneCallDelay), Config.PatchEnablePayphoneCallDelay);
        PatchIfEnabled(typeof(SitAndTalk), Config.PatchEnableSitAndTalk);

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
