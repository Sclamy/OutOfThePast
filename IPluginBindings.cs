using SOD.Common.BepInEx.Configuration;

namespace OutOfThePast
{
    public interface IPluginBindings
    {
        // Binds in the config file as: Prices.SyncDiskPrice
        // [Binding(500, "The price for the sync disk.", "Prices.SyncDiskPrice")]
        // int SyncDiskPrice { get; set; }
        
    }
}
