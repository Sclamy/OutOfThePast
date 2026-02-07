using BepInEx;
// using BepInEx.Logging;
// using BepInEx.Unity.IL2CPP;
using SOD.Common;
using SOD.Common.BepInEx;
using System.Reflection;

namespace OutOfThePast;


[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
[BepInDependency("Venomaus.SOD.Common")]
public class Plugin : PluginController<Plugin, IPluginBindings>
{
    public const string PLUGIN_GUID = "Sclamy.OutOfThePast";
    public const string PLUGIN_NAME = "OutOfThePast";
    public const string PLUGIN_VERSION = "0.0.1";

    public override void Load()
    {
        Harmony.PatchAll(Assembly.GetExecutingAssembly());
        Log.LogInfo($"Plugin {PLUGIN_GUID} v{PLUGIN_VERSION} is loaded!");
        
        // Logging Tutorial
        // Log.LogInfo("This is information");
        // Log.LogWarning("This is a warning");
        // Log.LogError("This is an error");
        
        // Config Tutorial
        // Log.LogInfo("SyncDiskPrice: " + Config.SyncDiskPrice);
        // Log.LogInfo("SomeTextConfig: " + Config.SomeTextConfig);
        // Log.LogInfo("SomePercentage: " + Config.SomePercentage);
        // Log.LogInfo("EnableSomething: " + Config.EnableSomething);
        
    }
}
