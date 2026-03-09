using HarmonyLib;
using UnityEngine;

namespace OutOfThePast.Patches.DecorPatches
{
    /// <summary>Increases how far decor items are held from the camera during apartment placement</summary>
    internal static class ExtendedDecorCarryDistance
    {
        // Prefix inflates carryDistance, so that the original Update positions items further out.
        // The higher minimum clamp can push items past surfaces, so the Postfix raycasts
        //   camera -> item and pulls them back in front of any intervening surface.
        // carryDistance is restored in the Postfix so non-decor carry is unaffected.

        // The Postfix correction doesn't persist to the next frame (something overwrites
        //   transform.position back to the uncorrected value between frames). So the DropThis
        //   Prefix redoes the same correction at the exact moment of placement, ensuring the
        //   new rb created by SetPhysics inherits the corrected position.

        private static float originalCarryDistance;
        private static bool isActive;       // true between Prefix and Postfix of a decor carry frame
        private static bool isDecorCarry;   // true while any decor item is being carried

        [HarmonyPatch(typeof(InteractableController), "Update")]
        internal static class PatchUpdate
        {
            [HarmonyPrefix]
            static void Prefix(InteractableController __instance)
            {
                if (!__instance.isCarriedByPlayer) return;
                // Inventory items (weapons, tools) keep normal carry distance
                if (__instance.interactable.preset.isInventoryItem) return;

                // Save and inflate for the original's positioning math
                originalCarryDistance = GameplayControls.Instance.carryDistance;
                GameplayControls.Instance.carryDistance = Plugin.Instance.Config.DecorPlaceDistance;
                isActive = true;
                isDecorCarry = true;
            }

            [HarmonyPostfix]
            static void Postfix(InteractableController __instance)
            {
                if (!isActive) return;
                isActive = false;

                GameplayControls.Instance.carryDistance = originalCarryDistance;

                // Wall/ceiling modes use snap logic that doesn't clip
                if (__instance.interactable.preset.apartmentPlacementMode
                    != InteractablePreset.ApartmentPlacementMode.physics)
                    return;

                CorrectSurfaceClipping(__instance);
            }
        }

        [HarmonyPatch(typeof(InteractableController), nameof(InteractableController.DropThis))]
        internal static class PatchDropThis
        {
            [HarmonyPrefix]
            static void Prefix(InteractableController __instance)
            {
                if (!isDecorCarry) return;
                isDecorCarry = false;

                if (__instance.interactable.preset.apartmentPlacementMode
                    != InteractablePreset.ApartmentPlacementMode.physics)
                    return;

                // Redo the correction - our Postfix correction doesn't persist between frames
                CorrectSurfaceClipping(__instance);

                // Fix non-convex MeshColliders before SetPhysics adds a Rigidbody,
                // which would log a Unity error for the unsupported combination
                FixNonConvexMeshColliders(__instance);
            }
        }

        // SetPhysics adds a non-kinematic Rigidbody, which Unity rejects on GameObjects
        // with non-convex MeshColliders. Mark them convex to prevent the error log.
        private static void FixNonConvexMeshColliders(InteractableController ic)
        {
            var target = (UnityEngine.Object)ic.alternativePhysicsParent != (UnityEngine.Object)null
                ? ic.alternativePhysicsParent.gameObject
                : ic.gameObject;
            foreach (var mc in target.GetComponentsInChildren<MeshCollider>())
            {
                if (!mc.convex)
                    mc.convex = true;
            }
        }

        // Raycast camera->item; if a surface lies between them, pull the item back in front
        private static void CorrectSurfaceClipping(InteractableController ic)
        {
            var cam = Player.Instance.cam.transform;
            bool hasAltParent = (UnityEngine.Object)ic.alternativePhysicsParent
                != (UnityEngine.Object)null;
            var itemPos = hasAltParent
                ? ic.alternativePhysicsParent.transform.position
                : ic.transform.position;

            // Direction and distance from camera to item's current (uncorrected) position
            var toItem = itemPos - cam.position;
            float distToItem = toItem.magnitude;
            if (distToItem < 0.01f) return;
            var dir = toItem / distToItem;

            // The carried item's collider may still be active (e.g. MeshColliders on
            // ashtrays). If the first hit is the carried item itself, skip past it.
            var itemTransform = hasAltParent
                ? (Transform)ic.alternativePhysicsParent
                : ic.transform;
            int layerMask = Toolbox.Instance.heldObjectsObjectsLayerMask;

            RaycastHit hit;
            if (!Physics.Raycast(cam.position, dir, out hit, distToItem, layerMask))
                return; // no surface between camera and item

            float hitDist = hit.distance;
            if (hit.transform.IsChildOf(itemTransform))
            {
                // Hit the carried item itself - retry from just past it
                float skipDist = hit.distance + 0.01f;
                if (skipDist >= distToItem) return;
                if (!Physics.Raycast(cam.position + dir * skipDist, dir, out hit,
                    distToItem - skipDist, layerMask))
                    return;
                if (hit.transform.IsChildOf(itemTransform)) return;
                
                // Adjust distance to be relative to camera, not the retry origin
                hitDist = skipDist + hit.distance;
            }

            // How far the mesh extends from its center toward the camera
            float meshRadius = 0.25f;
            if (ic.meshes.Count > 0)
                meshRadius = Vector3.Distance(
                    ic.meshes[0].bounds.center,
                    ic.meshes[0].bounds.ClosestPoint(cam.position));

            // Pull so the item's far edge lands at the surface
            float pullAmount = distToItem - hitDist + meshRadius;

            // Don't pull closer than the mesh edge + small gap from camera
            float minDistFromCam = meshRadius + 0.1f;
            float maxPull = Mathf.Max(0f, distToItem - minDistFromCam);
            pullAmount = Mathf.Min(pullAmount, maxPull);
            if (pullAmount <= 0f) return;

            var correctedPos = itemPos - dir * pullAmount;

            // Uncomment for object-specific debugging:
            // Plugin.Log.LogInfo($"[DecorCarry] correction: pull={pullAmount:F3}," +
            //     $" hitDist={hitDist:F3}, itemDist={distToItem:F3}, hit={hit.collider.name}");

            if (hasAltParent)
                ic.alternativePhysicsParent.transform.position = correctedPos;
            else
                ic.transform.position = correctedPos;
        }
    }
}
