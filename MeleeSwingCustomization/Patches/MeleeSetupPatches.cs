using Gear;
using HarmonyLib;
using MSC.CustomMeleeData;
using MSC.Utils;

namespace MSC.Patches
{
    [HarmonyPatch(typeof(MeleeWeaponFirstPerson))]
    internal static class MeleeSetupPatches
    {
        [HarmonyPatch(nameof(MeleeWeaponFirstPerson.SetupMeleeAnimations))]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_MeleeSetup(MeleeWeaponFirstPerson __instance)
        {
            MeleeDataManager.Current.RegisterMelee(__instance);
            DebugUtil.DrawDebugSpheres(__instance);
        }
    }
}
