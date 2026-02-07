using HarmonyLib;
using UnityEngine;
using SOD.Common;
// using SOD.Common.Extensions;


namespace OutOfThePast.Patches
{
    internal static class SideJobPatches
    {
        /*
        [HarmonyPatch(typeof(FirstPersonItemController), "Update")] // runs after a bunch of data is loaded
        internal static class PatchUpdate
        {
            [HarmonyPostfix]
            static void Postfix(FirstPersonItemController __instance)
            {
            }
            private static void PatchSpecificThing()
            {
            }
        }
        */
        
        
        /// <summary> Increases time between accepting a Side Job and the phone-call (3 minutes -> [30-45] minutes) </summary>
        public static void AdjustPayphoneCallDelay()
        {
            // Allows player time to explore and navigate the city organically
            // Avoids immediate F1 -> Route -> Sprint
            // Encourages learning and engaging with the city
            // Encourages organic passage of time (so player isn't solving 10 crimes a night)
            
            
            
        }


    }
}