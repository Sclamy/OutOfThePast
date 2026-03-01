using HarmonyLib;


namespace OutOfThePast.Patches.UIPatches
{
    /// <summary> Suppresses [Target] brackets on all action prompts</summary>
    internal static class SuppressAllTargetBrackets
    {
        [HarmonyPatch(typeof(ControlDisplayController), nameof(ControlDisplayController.SetControlText))]
        internal static class SuppressAllBrackets
        {
            /// <summary> Clears useContext (unconditionally), preventing any [Target] bracket from appearing </summary>
            [HarmonyPrefix]
            static void Prefix(ref bool useContext)
            {
                useContext = false;
            }
        }
    }
}
