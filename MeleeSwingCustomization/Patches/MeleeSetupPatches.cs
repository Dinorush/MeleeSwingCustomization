using Gear;
using HarmonyLib;
using MSC.CustomMeleeData;
using MSC.Utils;
using System;

namespace MSC.Patches
{
    [HarmonyPatch(typeof(MeleeWeaponFirstPerson))]
    internal static class MeleeSetupPatches
    {
        private const string BatPrefab = "Assets/AssetPrefabs/Items/Melee/MeleeWeaponFirstPersonBat.prefab";
        private readonly static MeleeData ImprovedBatData = new()
        {
            AttackOffset = new(0, 0.55f, 0.0f)
        };

        [HarmonyPatch(nameof(MeleeWeaponFirstPerson.SetupMeleeAnimations))]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_MeleeSetup(MeleeWeaponFirstPerson __instance)
        {
            if (!MeleeDataManager.Current.RegisterMelee(__instance) && Configuration.ImproveBatHitbox)
            {
                var prefabs = __instance.ItemDataBlock.FirstPersonPrefabs;
                if (prefabs?.Count > 0 && prefabs[0] == BatPrefab)
                    __instance.ModelData.m_damageRefAttack.localPosition = ImprovedBatData.AttackOffset.Offset;
            }

            DebugUtil.DrawDebugSpheres(__instance);
        }

        private static MWS_AttackLight? _lightLeft;
        private static MWS_AttackLight? _lightRight;
        private static IntPtr _cachedPtr = IntPtr.Zero;
        [HarmonyPatch(nameof(MeleeWeaponFirstPerson.ChangeState))]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_MeleeChangeState(MeleeWeaponFirstPerson __instance, eMeleeWeaponState newState)
        {
            MeleeData? data = MeleeDataManager.Current.GetData(__instance.MeleeArchetypeData.persistentID);
            if (data == null) return;

            if (_cachedPtr != __instance.Pointer)
            {
                _cachedPtr = __instance.Pointer;
                _lightLeft = __instance.m_states[(int)eMeleeWeaponState.AttackMissLeft].TryCast<MWS_AttackLight>()!;
                _lightRight = __instance.m_states[(int)eMeleeWeaponState.AttackMissRight].TryCast<MWS_AttackLight>()!;
            }

            switch (newState)
            {
                case eMeleeWeaponState.AttackMissLeft:
                    _lightLeft!.m_wantedNormalSpeed = data.LightAttackSpeed;
                    _lightLeft.m_wantedChargeSpeed = data.LightAttackSpeed * 0.3f;
                    break;
                case eMeleeWeaponState.AttackMissRight:
                    _lightRight!.m_wantedNormalSpeed = data.LightAttackSpeed;
                    _lightRight.m_wantedChargeSpeed = data.LightAttackSpeed * 0.3f;
                    break;
                case eMeleeWeaponState.AttackHitLeft:
                case eMeleeWeaponState.AttackHitRight:
                    __instance.WeaponAnimator.speed = data.LightAttackSpeed;
                    break;
                case eMeleeWeaponState.AttackChargeReleaseLeft:
                case eMeleeWeaponState.AttackChargeReleaseRight:
                    __instance.WeaponAnimator.speed = data.ChargedAttackSpeed;
                    break;
                case eMeleeWeaponState.Push: // Push sets speed based on stamina
                    __instance.WeaponAnimator.speed *= data.PushSpeed;
                    break;
            }
        }
    }
}
