using HarmonyLib;
// ------------------------------
// Original patch by @piepieonline
// ------------------------------

namespace OutOfThePast.Patches.UIPatches
{
    /// <summary> Prevents the game from pausing when opening the case board or notebook </summary>
    [HarmonyPatch(typeof(InputController), nameof(InputController.Update))]
    internal static class NoPauseCaseBoard
    {
        // The game pauses when opening the case board because InputController.Update checks:
        //   if (!Player.Instance.autoTravelActive || Game.Instance.autoTravelPause)
        //       SessionData.Instance.PauseGame(true);
        // During auto-travel this check is skipped, and the case board opens without pausing
        // via SetDesktopMode instead - which is exactly the behavior we want all the time
        //
        // Since we can't transpile (IL2CPP), we temporarily set autoTravelActive = true
        // for the duration of Update so the game takes the SetDesktopMode path naturally
        // This is safe because the CaseBoard/Notebook handlers all return early,
        // and the original value is restored in the Postfix

        private static bool wasAutoTraveling;
        private static bool didOverride;

        [HarmonyPrefix]
        static void Prefix(InputController __instance)
        {
            didOverride = false;
            if (!Plugin.Instance.Config.PatchEnableNoPauseCaseBoard) return;
            if (__instance.player == null) return;

            if (__instance.player.GetButtonDown("CaseBoard") || __instance.player.GetButtonDown("Notebook"))
            {
                wasAutoTraveling = Player.Instance.autoTravelActive;
                Player.Instance.autoTravelActive = true;
                didOverride = true;
            }
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            if (didOverride)
            {
                Player.Instance.autoTravelActive = wasAutoTraveling;
                didOverride = false;
            }
        }
    }
}
