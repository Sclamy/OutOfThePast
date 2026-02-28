using HarmonyLib;
using UnityEngine;
using SOD.Common;
// using SOD.Common.Extensions;


namespace OutOfThePast.Patches.SideJobPatches
{
    /// <summary> Increases time between accepting a Side Job and the phone-call (3 minutes -> [30-45] minutes) </summary>
    [HarmonyPatch(typeof(SideJob), nameof(SideJob.ObjectiveStateLoop))]
    internal static class AdjustPayphoneCallDelay
    {
        // Allows player time to explore and navigate the city organically
        // Avoids immediate F1 -> Route -> Sprint
        // Encourages learning and engaging with the city
        // Encourages organic passage of time (so player isn't solving 10 crimes a night)
        
        // Check gooseChaseCallTime before possible update
        [HarmonyPrefix]
        static void Prefix(SideJob __instance, ref float __state) => __state = __instance.gooseChaseCallTime;

        
        // Check gooseChaseCallTime after possible update (value might be set now)
        // If it has changed, update it with our logic
        [HarmonyPostfix]
        static void Postfix(SideJob __instance, float __state)
        {
            if (__instance.gooseChaseCallTime == __state) return;  // No update
            
            // It's been changed - the goose chase is on!
            // Game code:
            // float num = 0.5f;
            // this.gooseChaseCallTime = (float) ((double) SessionData.Instance.gameTime + 0.08500000089406967
            //                             + (double) path.accessList.Count / 60.0 / 60.0 * (double) num);
            
            // This is essentially gameTime (in hours) + 5.1 minutes + [dist to target (in seconds) / 2]
            float minDelay = Plugin.Instance.Config.PayphoneCallDelayMinimumDelay;  // 30 minutes
            float maxDelay = Plugin.Instance.Config.PayphoneCallDelayMaximumDelay;  // 45 minutes
            
            float callDelay = Random.Range(minDelay, maxDelay);  // 30-45 minutes

            // Add to game time (converted to hours)
            __instance.gooseChaseCallTime = SessionData.Instance.gameTime + (callDelay / 60f);

        }
        
    }
}