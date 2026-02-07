using SOD.Common.BepInEx.Configuration;

namespace OutOfThePast
{
    public interface IPluginBindings : IAdjustPayphoneCallDelayBindings, IPlaceholderBindings
    { }
    
    public interface IAdjustPayphoneCallDelayBindings
    {
        [Binding(30, "Minimum delay before Side Job phone rings", "PayphoneCallDelay.MinimumDelay")]
        int PayphoneCallDelayMinimumDelay { get; set; }
        
        [Binding(45, "Maximum delay before Side Job phone rings", "PayphoneCallDelay.MaximumDelay")]
        int PayphoneCallDelayMaximumDelay { get; set; }
    }

    public interface IPlaceholderBindings
    {
        
    }
}
