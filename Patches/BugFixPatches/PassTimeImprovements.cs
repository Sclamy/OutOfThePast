using HarmonyLib;


namespace OutOfThePast.Patches.BugFixPatches
{
    /// <summary>
    /// BugFix 1: Prevents camera jolt when using Pass Time / Set Alarm / Cancel while sitting.
    /// BugFix 2: Cancels alarm UI when switching to a non-watch item during set-alarm mode.
    /// </summary>
    internal static class PassTimeImprovements
    {
        // --- Bug 1: Camera jolt fix ---
        // PassTime/ActivateTimePass/CancelPassTime call Player.OnHide() which triggers
        // TransformPlayerController -> OnTransitionComplete -> fps.InitialiseController(true),
        // resetting the camera orientation. Since the player is already seated, we suppress
        // the OnHide call via a flag and apply the ref change ourselves afterward.
        // All other original logic (sounds, slot selection, delays) runs untouched.

        private static bool suppressOnHide = false;
        private static int pendingRef = -1;

        /// <summary> Skips OnHide when the suppressOnHide flag is set by one of the PassTime patches </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.OnHide))]
        internal static class SuppressOnHide
        {
            [HarmonyPrefix]
            static bool Prefix()
            {
                if (suppressOnHide)
                {
                    // Plugin.Log.LogInfo("[PassTime] Suppressed OnHide (pendingRef=" + pendingRef + ")");
                    return false;
                }
                return true;
            }
        }

        /// <summary> Arms the OnHide suppressor and queues ref=1 (pass-time action set) before PassTime runs </summary>
        [HarmonyPatch(typeof(ActionController), nameof(ActionController.PassTime))]
        internal static class PassTimeNoJolt
        {
            [HarmonyPrefix]
            static void Prefix()
            {
                var lockedIn = InteractionController.Instance?.lockedInInteraction;
                if (lockedIn != null)
                {
                    suppressOnHide = true;
                    pendingRef = 1;
                    // Plugin.Log.LogInfo("[PassTime] PassTime while sitting on: " + lockedIn.name);
                }
            }

            /// <summary> Applies the queued ref change after the original method has run </summary>
            [HarmonyPostfix]
            static void Postfix()
            {
                if (!suppressOnHide) return;
                ApplyPendingRef();
            }
        }

        /// <summary> Arms the OnHide suppressor and queues ref=0 (normal sitting) before ActivateTimePass runs </summary>
        [HarmonyPatch(typeof(ActionController), nameof(ActionController.ActivateTimePass))]
        internal static class ActivateTimePassNoJolt
        {
            [HarmonyPrefix]
            static void Prefix()
            {
                var lockedIn = InteractionController.Instance?.lockedInInteraction;
                if (lockedIn != null)
                {
                    suppressOnHide = true;
                    pendingRef = 0;
                    // Plugin.Log.LogInfo("[PassTime] ActivateTimePass while sitting on: " + lockedIn.name);
                }
            }

            /// <summary> Applies the queued ref change after the original method has run </summary>
            [HarmonyPostfix]
            static void Postfix()
            {
                if (!suppressOnHide) return;
                ApplyPendingRef();
            }
        }

        /// <summary> Arms the OnHide suppressor and queues ref=0 (normal sitting) before CancelPassTime runs </summary>
        [HarmonyPatch(typeof(ActionController), nameof(ActionController.CancelPassTime))]
        internal static class CancelPassTimeNoJolt
        {
            [HarmonyPrefix]
            static void Prefix()
            {
                var lockedIn = InteractionController.Instance?.lockedInInteraction;
                if (lockedIn != null)
                {
                    suppressOnHide = true;
                    pendingRef = 0;
                    // Plugin.Log.LogInfo("[PassTime] CancelPassTime while sitting on: " + lockedIn.name);
                }
            }

            /// <summary> Clears alarm state (normally handled by the transition) then applies the queued ref change </summary>
            [HarmonyPostfix]
            static void Postfix()
            {
                if (!suppressOnHide) return;
                Player.Instance.SetSettingAlarmMode(false);
                Player.Instance.setAlarmModeAfterDelay = 0f;
                ApplyPendingRef();
            }
        }

        /// <summary> Writes pendingRef to lockedInInteractionRef and hideReference, refreshes the action display, then clears both flags </summary>
        private static void ApplyPendingRef()
        {
            var ic = InteractionController.Instance;
            ic.lockedInInteractionRef = pendingRef;
            Player.Instance.hideReference = pendingRef;
            if (ic.lockedInInteraction != null)
            {
                ic.lockedInInteraction.UpdateCurrentActions();
                ic.UpdateInteractionText();
                // Plugin.Log.LogInfo("[PassTime] Applied ref=" + pendingRef + " to " + ic.lockedInInteraction.name);
            }
            // else Plugin.Log.LogWarning("[PassTime] lockedInInteraction was null when applying ref=" + pendingRef);
            suppressOnHide = false;
            pendingRef = -1;
        }


        // --- Bug 2: Alarm persists when switching items ---
        // When the set-alarm UI is active and the player equips a different item via hotkey
        //   or inventory, the alarm UI and sounds persist. Cancel alarm on non-watch selection.

        /// <summary> When a non-watch slot is selected during set-alarm mode, cancels the alarm and resets the action set to normal sitting </summary>
        [HarmonyPatch(typeof(BioScreenController), nameof(BioScreenController.SelectSlot))]
        internal static class CancelAlarmOnItemSwitch
        {
            [HarmonyPostfix]
            static void Postfix()
            {
                var selected = BioScreenController.Instance.selectedSlot;
                if (selected == null) return;
                if (selected.isStatic == FirstPersonItemController.InventorySlot.StaticSlot.watch) return;

                var player = Player.Instance;
                if (player.setAlarmMode || player.setAlarmModeAfterDelay > 0f)
                {
                    // Plugin.Log.LogInfo("[PassTime] Cancelling alarm on item switch to: " + selected.isStatic);
                    player.SetSettingAlarmMode(false);
                    player.setAlarmModeAfterDelay = 0f;

                    var ic = InteractionController.Instance;
                    if (ic?.lockedInInteraction != null)
                    {
                        ic.lockedInInteractionRef = 0;
                        player.hideReference = 0;
                        ic.lockedInInteraction.UpdateCurrentActions();
                        ic.UpdateInteractionText();
                        // Plugin.Log.LogInfo("[PassTime] Reset ref=0 on " + ic.lockedInInteraction.name);
                    }
                }
            }
        }
    }
}
