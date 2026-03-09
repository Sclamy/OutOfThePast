using HarmonyLib;
using UnityEngine;

namespace OutOfThePast.Patches.DecorPatches
{
    /// <summary>Corrects the wok being held upside-down by flipping its "heldEuler" rotation</summary>
    internal static class FixWokRotation
    {
        [HarmonyPatch(typeof(InteractableController), "Update")]
        internal static class PatchUpdate
        {
            [HarmonyPrefix]
            static void Prefix(InteractableController __instance)
            {
                if (!__instance.isCarriedByPlayer) return;
                if (!__instance.setHeldEuler) return;
                if (!__instance.interactable.preset.name.Equals("Wok")) return;

                // derive from PhysicsProfile, so repeated pickups don't accumulate
                var profile = __instance.interactable.preset.GetPhysicsProfile();
                if (profile == null) return;

                // Only correct X (the flip axis), preserve Y/Z for player rotation (Q/E)
                __instance.heldEuler = new Vector3(
                    profile.heldEuler.x + 180f,
                    __instance.heldEuler.y,
                    __instance.heldEuler.z);
            }
        }
    }
}
