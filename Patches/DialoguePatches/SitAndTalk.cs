using HarmonyLib;
using UnityEngine;
using SOD.Common;
using System;


namespace OutOfThePast.Patches.DialoguePatches
{
    /// <summary> Allows talking to NPCs while remaining seated </summary>
    internal static class SitAndTalk
    {
        // Normally, "Sitting" and "Talking" are both locked-in interactions that conflict
        //   For context, while in a locked-in interaction, the player can do *most* actions
        //     except those specifically blocked, which generally includes other locked-in interactions
        // When Talk claims locked-in, the game ends Sitting (clears usagePoint, player stands up)
        // This patch allows Talk to claim locked-in (needed for dialogue UI to function)
        //   but saves and immediately restores the sitting usagePoint to keep player physically seated
        // It also prevents the player transforms for "stand up" and "sit down" while transitioning into and out of SitAndTalk
        // When dialogue ends, we restore Sitting as the locked-in interaction
        // This preserves both dialogue functionality and sitting position/animation

        // State machine:
        //   isSitAndTalkActive  - dialogue is open while seated
        //   pendingRestoration  - dialogue ended, waiting for next Update to call SetLockedIn(chair)
        //   isRestoringSitting  - inside the deferred SetLockedIn(chair) call
        //   sittingRestored     - restoration complete, permanently blocking an unknown per-frame
        //                         IL2CPP caller that tries to SetLockedIn(null) after restoration
        //
        // sittingRestored stays active until:
        //   - player explicitly gets up (ActionController.Return / PullPlayerFromHidingPlace)
        //   - player starts a new conversation (TalkTo.Prefix clears it)
        //   - chair becomes invalid

        private static bool isSitAndTalkActive = false;
        private static bool pendingRestoration = false;
        private static bool isRestoringSitting = false;
        private static bool sittingRestored = false;
        private static bool userInitiatedReturn = false;
        private static int restorationFrame = -1;

        private static Interactable storedSittingInteractable = null;

        private static bool Debug => Plugin.Instance.Config.DebugSitAndTalk;

        private static void LogDebug(string msg)
        {
            if (Debug) Plugin.Log.LogInfo(msg);
        }

        private static string StateString =>
            $"active={isSitAndTalkActive}, pending={pendingRestoration}, restoring={isRestoringSitting}, restored={sittingRestored}, chair={(storedSittingInteractable != null ? storedSittingInteractable.name : "null")}";

        private static void LogStateSnapshot(string label)
        {
            if (!Debug) return;
            var ic = InteractionController.Instance;
            var p = Player.Instance;
            var lockedIn = ic?.lockedInInteraction;
            Plugin.Log.LogInfo($"[SitAndTalk] === {label} ===");
            Plugin.Log.LogInfo($"  lockedInInteraction={lockedIn?.name ?? "null"}, ref={ic?.lockedInInteractionRef}");
            Plugin.Log.LogInfo($"  hideInteractable(IC)={ic?.hideInteractable?.name ?? "null"}");
            Plugin.Log.LogInfo($"  hideInteractable(Player)={p?.hideInteractable?.name ?? "null"}, hideRef={p?.hideReference}");
            Plugin.Log.LogInfo($"  interactingWith={p?.interactingWith?.name ?? "null"}");
            Plugin.Log.LogInfo($"  isHiding={p?.isHiding}, transitionActive={p?.transitionActive}");
            Plugin.Log.LogInfo($"  exitTransition={p?.exitTransition?.name ?? "null"}");
            if (lockedIn != null && lockedIn.usagePoint != null)
            {
                Human defaultUser = null;
                lockedIn.usagePoint.users?.TryGetValue(Interactable.UsePointSlot.defaultSlot, out defaultUser);
                Plugin.Log.LogInfo($"  usagePoint.defaultUser={defaultUser?.name ?? "null"}");
            }
            Plugin.Log.LogInfo($"  OnReturnFromLockedIn null?={ic?.OnReturnFromLockedIn == null}");
        }

        /// <summary> Returns true if the stored chair still exists and can be used </summary>
        private static bool IsStoredChairValid()
        {
            if (storedSittingInteractable == null) return false;
            if (storedSittingInteractable.usagePoint == null) return false;
            return true;
        }

        /// <summary> Whether any protection flag is active (used by downstream blocks as safety net) </summary>
        private static bool AnyProtectionActive =>
            isSitAndTalkActive || pendingRestoration || isRestoringSitting || sittingRestored;

        /// <summary> Clear all state </summary>
        private static void ClearState()
        {
            LogDebug($"[SitAndTalk] ClearState called ({StateString})");
            storedSittingInteractable = null;
            isSitAndTalkActive = false;
            pendingRestoration = false;
            isRestoringSitting = false;
            sittingRestored = false;
            userInitiatedReturn = false;
            restorationFrame = Time.frameCount;
        }


        // ------------------------------------------------------------------------------------------------

        /// <summary> Overrides interaction options to [Talk, Inspect] when sitting and looking at conscious NPC </summary>
        [HarmonyPatch(typeof(Interactable), nameof(Interactable.UpdateCurrentActions))]
        internal static class PrioritizeTalkWhileSitting
        {
            [HarmonyPostfix]
            static void Postfix(Interactable __instance)
            {
                if (__instance == null) return;

                InteractionController ic = InteractionController.Instance;
                Player player = Player.Instance;
                if (ic == null || player == null) return;

                // Check if player is sitting (locked into a sitting interaction)
                if (ic.lockedInInteraction == null) return;
                if (ic.lockedInInteraction.usagePoint == null) return;

                // Don't add Talk if the NPC IS the locked-in interactable (e.g. handcuffing)
                if (__instance == ic.lockedInInteraction) return;

                // Check if looking at a conscious citizen
                if (__instance.isActor == null) return;
                if (__instance.isActor.isDead || __instance.isActor.isAsleep || __instance.isActor.isStunned) return;
                if (__instance.isActor.ai == null || __instance.isActor.ai.ko) return;

                // Find the Talk action in currentActions
                InteractablePreset.InteractionKey talkKey = InteractablePreset.InteractionKey.none;
                foreach (var kvp in __instance.currentActions)
                {
                    if (kvp.Value.currentAction?.action?.presetName == "TalkTo")
                    {
                        talkKey = kvp.Key;
                        break;
                    }
                }

                if (talkKey == InteractablePreset.InteractionKey.none) return;

                var talkAction = __instance.currentActions[talkKey];
                if (talkAction == null) return;

                // Force enable Talk
                talkAction.enabled = true;
                talkAction.display = true;

                // Find and enable Inspect action
                InteractablePreset.InteractionKey inspectKey = InteractablePreset.InteractionKey.none;
                foreach (var kvp in __instance.currentActions)
                {
                    if (kvp.Value.currentAction?.action?.presetName == "Inspect")
                    {
                        inspectKey = kvp.Key;
                        break;
                    }
                }

                if (inspectKey != InteractablePreset.InteractionKey.none)
                {
                    var inspectAction = __instance.currentActions[inspectKey];
                    if (inspectAction != null)
                    {
                        inspectAction.enabled = true;
                        inspectAction.display = true;
                    }
                }

                // Assign Talk to primary and Inspect to secondary
                var primaryKey = InteractablePreset.InteractionKey.primary;
                var secondaryKey = InteractablePreset.InteractionKey.secondary;

                __instance.currentActions[primaryKey] = talkAction;
                if (inspectKey != InteractablePreset.InteractionKey.none)
                {
                    __instance.currentActions[secondaryKey] = __instance.currentActions[inspectKey];
                }
            }
        }


        /// <summary> Saves chair binding before TalkTo, restores player's binding to the chair after TalkTo </summary>
        [HarmonyPatch(typeof(NewAIController), nameof(NewAIController.TalkTo), new Type[] { typeof(InteractionController.ConversationType) })]
        internal static class PreserveSittingDuringTalk
        {
            [HarmonyPrefix]
            static void Prefix(NewAIController __instance, InteractionController.ConversationType convoType)
            {
                // Clear any previous sit-and-talk state (stale flags or previous sittingRestored)
                if (AnyProtectionActive)
                {
                    LogDebug($"[SitAndTalk] TalkTo.Prefix clearing previous state ({StateString})");
                    ClearState();
                }

                // Check if player is currently sitting
                InteractionController ic = InteractionController.Instance;
                if (ic.lockedInInteraction == null) return;
                if (ic.lockedInInteraction.usagePoint == null) return;

                LogDebug($"[SitAndTalk] Entering sit-and-talk mode (convoType={convoType}, chair={ic.lockedInInteraction.name}, NPC={__instance.name})");
                LogStateSnapshot("BEFORE sit-and-talk");

                storedSittingInteractable = ic.lockedInInteraction;
                isSitAndTalkActive = true;
            }

            [HarmonyPostfix]
            static void Postfix(NewAIController __instance, InteractionController.ConversationType convoType)
            {
                if (!IsStoredChairValid())
                {
                    if (storedSittingInteractable != null) ClearState();
                    return;
                }
                if (isSitAndTalkActive && storedSittingInteractable != null)
                {
                    LogDebug($"[SitAndTalk] TalkTo.Postfix - restoring sitting state ({StateString})");

                    Player.Instance.SetInteracting(storedSittingInteractable);

                    if (storedSittingInteractable.usagePoint != null)
                    {
                        storedSittingInteractable.usagePoint.TrySetUser(Interactable.UsePointSlot.defaultSlot, Player.Instance);
                    }
                }
            }
        }


        /// <summary> Prevents chair's usagePoint from clearing during SitAndTalk </summary>
        [HarmonyPatch(typeof(Interactable.UsagePoint), nameof(Interactable.UsagePoint.TrySetUser))]
        internal static class PreventUsagePointClear
        {
            [HarmonyPrefix]
            static bool Prefix(Interactable.UsagePoint __instance, Interactable.UsePointSlot slot, Human newUser, string debug)
            {
                if (!IsStoredChairValid())
                {
                    if (storedSittingInteractable != null) ClearState();
                    return true;
                }

                // Protect our chair's usagePoint from being cleared during any protection phase,
                // or while the game thinks we're sitting (lockedInInteraction == our chair)
                bool protectingSitting = AnyProtectionActive ||
                    (InteractionController.Instance != null && InteractionController.Instance.lockedInInteraction == storedSittingInteractable);
                if (protectingSitting && newUser == null && storedSittingInteractable.usagePoint == __instance)
                {
                    return false;
                }

                return true;
            }
        }


        /// <summary> Marks ActionController.Return as user-initiated so SetLockedIn allows it through </summary>
        [HarmonyPatch(typeof(ActionController), nameof(ActionController.Return))]
        internal static class AllowUserInitiatedReturn
        {
            [HarmonyPrefix]
            static void Prefix()
            {
                if (sittingRestored)
                {
                    LogDebug("[SitAndTalk] ActionController.Return - setting userInitiatedReturn");
                    userInitiatedReturn = true;
                }
            }
        }


        /// <summary> Marks PullPlayerFromHidingPlace as user-initiated so SetLockedIn allows it through </summary>
        [HarmonyPatch(typeof(ActionController), nameof(ActionController.PullPlayerFromHidingPlace))]
        internal static class AllowPullFromHiding
        {
            [HarmonyPrefix]
            static void Prefix()
            {
                if (sittingRestored)
                {
                    LogDebug("[SitAndTalk] PullPlayerFromHidingPlace - setting userInitiatedReturn");
                    userInitiatedReturn = true;
                }
            }
        }


        /// <summary> Controls SetLockedIn access: redirects NPC clear, blocks mystery caller, allows user-initiated return </summary>
        [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.SetLockedInInteractionMode))]
        internal static class PreventClearingSitting
        {
            [HarmonyPrefix]
            static bool Prefix(Interactable val, InteractionController __instance)
            {
                // Input bleed guard: the click that ended the conversation can trigger the
                // chair's "Get Up" action on the same frame as restoration completes
                if (val == null && restorationFrame == Time.frameCount)
                {
                    LogDebug("[SitAndTalk] Blocking post-restoration input bleed (same frame as restoration)");
                    return false;
                }

                // Chair gone - allow and clean up
                if (!IsStoredChairValid())
                {
                    if (storedSittingInteractable != null) ClearState();
                    return true;
                }

                // Switching from chair to NPC: TalkTo set Player.interactingWith=NPC before SetLockedIn
                // SetLockedIn's "clear other actor" then incorrectly clears the NPC (we're switching TO them)
                // Temporarily point at the chair, so the clear uses chair.objectRef (no Actor) and skips clearing the NPC
                if (val != null && val.isActor != null && isSitAndTalkActive && __instance.lockedInInteraction == storedSittingInteractable)
                {
                    LogDebug("[SitAndTalk] Redirecting interacting to chair to prevent NPC clear");
                    Player.Instance.SetInteracting(storedSittingInteractable);
                }

                // User-initiated return during permanent protection - allow through
                if (val == null && userInitiatedReturn && sittingRestored)
                {
                    LogDebug("[SitAndTalk] User-initiated return, clearing state and allowing");
                    ClearState();
                    return true;
                }

                // Block SetLockedIn(null) during restoration phases and permanent protection
                // Note: NOT during isSitAndTalkActive - the dialogue exit flow needs SetLockedIn(null) to run
                if (val == null && (pendingRestoration || isRestoringSitting || sittingRestored))
                {
                    LogDebug($"[SitAndTalk] Blocking SetLockedIn(null) ({StateString})");
                    return false;
                }

                return true;
            }

            [HarmonyPostfix]
            static void Postfix(InteractionController __instance)
            {
                if (!IsStoredChairValid())
                {
                    if (storedSittingInteractable != null) ClearState();
                    return;
                }

                // After the deferred SetLockedIn(chair) call, verify the write persisted
                // and enter permanent protection
                if (isRestoringSitting && storedSittingInteractable != null)
                {
                    if (__instance.lockedInInteraction == storedSittingInteractable)
                    {
                        LogDebug($"[SitAndTalk] Deferred restoration succeeded, entering permanent protection ({StateString})");
                        isRestoringSitting = false;
                        sittingRestored = true;
                        LogStateSnapshot("AFTER restoration (protected)");
                    }
                    else
                    {
                        LogDebug($"[SitAndTalk] WARNING: Deferred restoration write failed ({StateString})");
                        LogStateSnapshot("FAILED deferred restoration");
                        ClearState();
                    }
                }
            }
        }


        /// <summary> Performs the deferred restoration on the next frame after dialogue ends </summary>
        [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.Update))]
        internal static class DeferredRestorationUpdate
        {
            [HarmonyPostfix]
            static void Postfix(InteractionController __instance)
            {
                if (!pendingRestoration) return;

                if (!IsStoredChairValid())
                {
                    ClearState();
                    return;
                }

                LogDebug($"[SitAndTalk] Performing deferred restoration ({StateString})");
                LogStateSnapshot("BEFORE deferred restoration");

                pendingRestoration = false;
                isRestoringSitting = true;

                __instance.SetLockedInInteractionMode(storedSittingInteractable, 0, false);

                // If isRestoringSitting is still true, the Postfix didn't transition to sittingRestored
                if (isRestoringSitting)
                {
                    LogDebug("[SitAndTalk] DeferredRestoration - Postfix didn't finalize, cleaning up");
                    ClearState();
                }
            }
        }


        /// <summary> Prevent player standing when starting SitAndTalk </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.TransformPlayerController))]
        internal static class SkipTransformWhileSitting
        {
            [HarmonyPrefix]
            static bool Prefix(Player __instance, PlayerTransitionPreset newEnterTransition, Interactable newInteractable)
            {
                if (!AnyProtectionActive) return true;

                if (!IsStoredChairValid())
                {
                    ClearState();
                    return true;
                }

                // Stale flag failsafe during isSitAndTalkActive only
                if (isSitAndTalkActive)
                {
                    InteractionController ic = InteractionController.Instance;
                    if (ic != null && ic.lockedInInteraction == null)
                    {
                        LogDebug($"[SitAndTalk] Stale transform block detected, clearing ({StateString})");
                        ClearState();
                        return true;
                    }
                }

                LogDebug($"[SitAndTalk] Skipping TransformPlayerController (transition={newEnterTransition?.name ?? "null"}, interactable={newInteractable?.name ?? "null"})");
                return false;
            }
        }


        /// <summary> Block OnReturnFromHide to keep hideInteractable, event subscription, and hiding state intact </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.OnReturnFromHide))]
        internal static class BlockOnReturnFromHide
        {
            [HarmonyPrefix]
            static bool Prefix()
            {
                if (!AnyProtectionActive) return true;

                if (!IsStoredChairValid())
                {
                    if (storedSittingInteractable != null) ClearState();
                    return true;
                }

                // Stale flag failsafe during isSitAndTalkActive only (during restoration phases,
                // lockedInInteraction is legitimately null)
                if (isSitAndTalkActive && !pendingRestoration && !isRestoringSitting && !sittingRestored)
                {
                    InteractionController ic = InteractionController.Instance;
                    if (ic != null && ic.lockedInInteraction == null)
                    {
                        LogDebug($"[SitAndTalk] Stale OnReturnFromHide block detected, clearing ({StateString})");
                        ClearState();
                        return true;
                    }
                }

                LogDebug($"[SitAndTalk] Blocking OnReturnFromHide ({StateString})");
                return false;
            }
        }


        /// <summary> Block ReturnFromTransform to prevent stand-up animation </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.ReturnFromTransform))]
        internal static class PreventReturnFromTransform
        {
            [HarmonyPrefix]
            static bool Prefix(Player __instance)
            {
                if (!AnyProtectionActive) return true;

                if (!IsStoredChairValid())
                {
                    if (storedSittingInteractable != null) ClearState();
                    return true;
                }

                LogDebug($"[SitAndTalk] Blocking ReturnFromTransform ({StateString})");
                return false;
            }
        }


        /// <summary> Restores chair as locked-in interaction when dialogue closes </summary>
        [HarmonyPatch(typeof(InteractionController), nameof(InteractionController.SetDialog))]
        internal static class RestoreSittingAfterDialogue
        {
            [HarmonyPostfix]
            static void Postfix(InteractionController __instance, bool val)
            {
                if (!IsStoredChairValid())
                {
                    if (storedSittingInteractable != null) ClearState();
                    return;
                }
                if (!val && isSitAndTalkActive && storedSittingInteractable != null)
                {
                    LogDebug($"[SitAndTalk] Dialogue ended, deferring restoration to next frame ({StateString})");
                    isSitAndTalkActive = false;
                    pendingRestoration = true;
                }
                else if (val && isSitAndTalkActive)
                {
                    LogDebug($"[SitAndTalk] Dialogue opened ({StateString})");
                }
            }
        }
    }
}
