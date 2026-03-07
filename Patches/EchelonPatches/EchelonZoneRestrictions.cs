using HarmonyLib;

namespace OutOfThePast.Patches.EchelonPatches
{
    /// <summary> Restricts echelon zone apartments and side jobs to players with the echelon perk </summary>
    internal static class EchelonZoneRestrictions
    {
        // The game has an unused field MotivePreset.disallowEchelonHome, which is false on every
        // motive. It was partially wired into SideJobController's poster selection (random-citizen
        // path only, not the acquaintance-connection path). It appears buggy: placed under
        // [Header("Purpetrator")] alongside perpetrator fields, but checks the poster's home in
        // code. Our patch supersedes this dead code entirely, filtering all echelon-zone client
        // jobs based on the player's current perk status.
        
        // Also, why is it "purp" instead of "perp"? Strange...

        // Job ID flagged for cleanup between PostJob (Prefix) and JobCreationCheck (Postfix).
        private static int pendingCancelJobId = -1;

        /// <summary> True if the address is in an echelon zone and the player lacks the echelon perk </summary>
        private static bool IsEchelonRestricted(NewAddress address)
        {
            return Game.Instance.allowEchelons
                && (UnityEngine.Object)address != (UnityEngine.Object)null
                && (UnityEngine.Object)address.floor != (UnityEngine.Object)null
                && address.floor.isEchelons
                && (double)UpgradeEffectController.Instance.GetUpgradeEffect(
                       SyncDiskPreset.Effect.allowedInEchelons) <= 0.0;
        }

        /// <summary> Shows an echelon-specific popup when trying to buy an echelon-zone apartment </summary>
        [HarmonyPatch(typeof(ApartmentSalesController), nameof(ApartmentSalesController.OnPurchaseButton))]
        internal static class BlockEchelonApartmentPurchase
        {
            [HarmonyPrefix]
            static bool Prefix(ApartmentSalesController __instance)
            {
                // Let original handle insufficient funds
                if (GameplayController.Instance.money < __instance.parentWindow.passedInteractable.forSale.GetPrice(false))
                    return true;

                // Mirror the original's apartment perk check
                int num = UnityEngine.Mathf.RoundToInt(
                    UpgradeEffectController.Instance.GetUpgradeEffect(SyncDiskPreset.Effect.allowApartmentPurchases));
                if (!Game.Instance.allowSocialCreditPerks)
                    num = 1; // social credit perks disabled, treat as having the perk

                // Mirror the original's intro chapter check
                if ((UnityEngine.Object)ChapterController.Instance != (UnityEngine.Object)null
                    && (UnityEngine.Object)ChapterController.Instance.chapterScript != (UnityEngine.Object)null
                    && !Game.Instance.sandboxMode)
                {
                    var chapterScript = ChapterController.Instance.chapterScript as ChapterIntro;
                    if ((UnityEngine.Object)chapterScript != (UnityEngine.Object)null && !chapterScript.completed)
                        num = -1; // intro not done, override perk status
                }

                // num <= 0 means player lacks apartment perk or hasn't finished intro;
                // let the original show its own message for those cases
                if (num <= 0)
                    return true;

                var forSale = __instance.parentWindow?.passedInteractable?.forSale;
                if (forSale == null) return true;
                if (!IsEchelonRestricted(forSale.thisAsAddress)) return true;

                // Reuse "LevelUpBeforeApartment" for its title ("Low Social Credit Level"),
                // but override the body to mention echelon zones
                PopupMessageController.Instance.PopupMessage(
                    "LevelUpBeforeApartment",
                    LButton: "Confirm",
                    mainTextPreWrittenOverride:
                        "You must level up your social credit before you are allowed to purchase an apartment in an Echelon Zone...");
                return false;
            }
        }

        /// <summary> Blocks posting of side jobs whose client lives in an echelon zone </summary>
        [HarmonyPatch(typeof(SideJob), nameof(SideJob.PostJob))]
        internal static class PreventEchelonJobPosting
        {
            [HarmonyPrefix]
            static bool Prefix(SideJob __instance)
            {
                if ((UnityEngine.Object)__instance.poster == (UnityEngine.Object)null) return true;
                if (!IsEchelonRestricted(__instance.poster.home)) return true;

                // Flag this job for cleanup after JobCreationCheck re-adds it to activeJobs
                pendingCancelJobId = __instance.jobID;
                return false;
            }
        }

        /// <summary> Ends flagged echelon jobs after JobCreationCheck, freeing the slot and exemptions </summary>
        [HarmonyPatch(typeof(SideJobController), nameof(SideJobController.JobCreationCheck))]
        internal static class CleanupBlockedEchelonJob
        {
            [HarmonyPostfix]
            static void Postfix()
            {
                if (pendingCancelJobId < 0) return;

                // SetJobState(ended) removes from activeJobs and clears poster/perp exemptions
                if (SideJobController.Instance.allJobsDictionary.ContainsKey(pendingCancelJobId))
                {
                    var job = SideJobController.Instance.allJobsDictionary[pendingCancelJobId];
                    job.SetJobState(SideJob.JobState.ended);
                }
                pendingCancelJobId = -1;
            }
        }
    }
}
