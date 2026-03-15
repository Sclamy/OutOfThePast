using SOD.Common.BepInEx.Configuration;

namespace OutOfThePast
{
    public interface IPluginBindings : IPatchToggleBindings, IDebugBindings, IAdjustPayphoneCallDelayBindings,
        IDecorBindings
    { }
    
    public interface IPatchToggleBindings
    {
        [Binding(true, "Enable Improved Payphone Call Delay", "_PatchEnable.PayphoneCallDelay")]
        bool PatchEnablePayphoneCallDelay { get; set; }
        
        [Binding(true, "Enable Talking While Sitting", "_PatchEnable.SitAndTalk")]
        bool PatchEnableSitAndTalk { get; set; }

        [Binding(true, "Enable Pass Time BugFixes", "_PatchEnable.PassTimeBugFixes")]
        bool PatchEnablePassTimeBugFixes { get; set; }
        
        [Binding(true, "Suppress [Target] brackets on all action prompts", "_PatchEnable.SuppressAllTargetBrackets")]
        bool PatchEnableSuppressAllTargetBrackets { get; set; }

        [Binding(true, "Enable Echelon Zone Restrictions", "_PatchEnable.EchelonZoneRestrictions")]
        bool PatchEnableEchelonZoneRestrictions { get; set; }

        [Binding(true, "Enable Extended Decor Placement", "_PatchEnable.ExtendDecorPlacement")]
        bool PatchEnableExtendDecorPlacement { get; set; }

        [Binding(true, "Enable Place Cigarette Butt in Ashtray", "_PatchEnable.PlaceCigaretteButtInAshtray")]
        bool PatchEnablePlaceCigaretteButtInAshtray { get; set; }

        [Binding(true, "Fix Wok held upside-down", "_PatchEnable.FixWokRotation")]
        bool PatchEnableFixWokRotation { get; set; }
    }


    public interface IDebugBindings
    {
        [Binding(false, "Enable SitAndTalk Debug Console", "|Debug.SitAndTalk")]
        bool DebugSitAndTalk { get; set; }
    }
    
    public interface IAdjustPayphoneCallDelayBindings
    {
        [Binding(30, "Minimum delay (minutes) before Side Job phone rings", "PayphoneCallDelay.MinimumDelay")]
        int PayphoneCallDelayMinimumDelay { get; set; }

        [Binding(45, "Maximum delay (minutes) before Side Job phone rings", "PayphoneCallDelay.MaximumDelay")]
        int PayphoneCallDelayMaximumDelay { get; set; }
    }

    public interface IDecorBindings
    {
        [Binding(0.9f, "How far decor items are held from the camera during placement", "Decor.DecorPlaceDistance")]
        float DecorPlaceDistance { get; set; }
    }
    
}
