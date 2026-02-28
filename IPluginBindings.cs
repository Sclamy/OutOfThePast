using SOD.Common.BepInEx.Configuration;

namespace OutOfThePast
{
    public interface IPluginBindings : IPatchToggleBindings, IAdjustPayphoneCallDelayBindings
    { }
    
    public interface IPatchToggleBindings
    {
        [Binding(true, "Enable Improved Payphone Call Delay", "PatchEnable.PayphoneCallDelay")]
        bool PatchEnablePayphoneCallDelay { get; set; }
        
        [Binding(true, "Enable Talking While Sitting", "PatchEnable.SitAndTalk")]
        bool PatchEnableSitAndTalk { get; set; }
    }
    
    public interface IAdjustPayphoneCallDelayBindings
    {
        [Binding(30, "Minimum delay (minutes) before Side Job phone rings", "PayphoneCallDelay.MinimumDelay")]
        int PayphoneCallDelayMinimumDelay { get; set; }
        
        [Binding(45, "Maximum delay (minutes) before Side Job phone rings", "PayphoneCallDelay.MaximumDelay")]
        int PayphoneCallDelayMaximumDelay { get; set; }
    }



    
        
}
