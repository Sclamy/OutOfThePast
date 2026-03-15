using HarmonyLib;


namespace OutOfThePast.Patches.InteractionPatches
{
    /// <summary> Hides Pass Time actions while sitting unless the watch is drawn </summary>
    internal static class PassTimeRequiresWatch
    {
        /// <summary> After the chair rebuilds its action list, disable PassTime if the watch isn't drawn </summary>
        [HarmonyPatch(typeof(Interactable), nameof(Interactable.UpdateCurrentActions))]
        internal static class SuppressPassTimeActions
        {
            [HarmonyPostfix]
            static void Postfix(Interactable __instance)
            {
                if (!Plugin.Instance.Config.PatchExtraEnablePassTimeRequiresWatch) return;

                // Only applies to the interactable the player is currently sitting on
                var ic = InteractionController.Instance;
                if (ic == null) return;

                // Check both lockedInInteraction and hideInteractable to catch the
                // sit-down animation (hideInteractable is set before the transition completes)
                bool isTarget = (ic.lockedInInteraction != null && __instance == ic.lockedInInteraction)
                             || (ic.hideInteractable != null && __instance == ic.hideInteractable);
                if (!isTarget) return;

                if (IsWatchDrawn()) return;

                DisablePassTimeActions(__instance);
            }
        }

        /// <summary> When the player equips or unequips the watch, refresh the chair's actions so PassTime
        /// appears/disappears accordingly </summary>
        [HarmonyPatch(typeof(BioScreenController), nameof(BioScreenController.SelectSlot))]
        internal static class RefreshActionsOnSlotChange
        {
            [HarmonyPostfix]
            static void Postfix()
            {
                if (!Plugin.Instance.Config.PatchExtraEnablePassTimeRequiresWatch) return;

                var ic = InteractionController.Instance;
                if (ic == null || ic.lockedInInteraction == null) return;

                // Trigger a full action rebuild so SuppressPassTimeActions can re-evaluate
                ic.lockedInInteraction.UpdateCurrentActions();
                ic.UpdateInteractionText();
            }
        }

        private static bool IsWatchDrawn()
        {
            var bio = BioScreenController.Instance;
            if (bio == null) return false;

            var slot = bio.selectedSlot;
            if (slot == null) return false;

            return slot.isStatic == FirstPersonItemController.InventorySlot.StaticSlot.watch;
        }

        private static void DisablePassTimeActions(Interactable target)
        {
            foreach (var kvp in target.currentActions)
            {
                var action = kvp.Value.currentAction;
                if (action == null) continue;

                var preset = action.action;
                if (preset == null) continue;

                var presetName = preset.presetName;
                if (presetName == "PassTime" || presetName == "ActivateTimePass")
                    kvp.Value.enabled = false;
            }
        }
    }
}
