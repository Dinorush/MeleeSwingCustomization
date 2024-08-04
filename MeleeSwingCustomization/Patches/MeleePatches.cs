using Gear;
using HarmonyLib;
using MSC.CustomMeleeData;

namespace MSC.Patches
{
    [HarmonyPatch]
    internal static class MeleePatches
    {
        private const string BatPrefab = "Assets/AssetPrefabs/Items/Melee/MeleeWeaponFirstPersonBat.prefab";
        private readonly static MeleeData ImprovedBatData = new()
        {
            AttackOffset = new() { x = 0, y = 0.55f, z = 0.0f }
        };

        [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.SetupMeleeAnimations))]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_MeleeSetup(MeleeWeaponFirstPerson __instance)
        {
            if (!MeleeDataManager.Current.RegisterMelee(__instance) && Configuration.ImproveBatHitbox)
            {
                var prefabs = __instance.ItemDataBlock.FirstPersonPrefabs;
                if (prefabs?.Count > 0 && prefabs[0] == BatPrefab)
                    __instance.ModelData.m_damageRefAttack.localPosition = ImprovedBatData.AttackOffset!.Value;
            }
        }

        private static MWS_AttackLight? _lightLeft;
        private static MWS_AttackLight? _lightRight;
        [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.ChangeState))]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_MeleeChangeState(MeleeWeaponFirstPerson __instance, eMeleeWeaponState newState)
        {
            MeleeData? data = MeleeDataManager.Current.GetData(__instance.MeleeArchetypeData.persistentID);
            if (data == null) return;

            switch (newState)
            {
                case eMeleeWeaponState.AttackMissLeft:
                    if (_lightLeft == null)
                        _lightLeft = __instance.m_states[(int)eMeleeWeaponState.AttackMissLeft].TryCast<MWS_AttackLight>()!;
                    _lightLeft.m_wantedNormalSpeed = data.LightAttackSpeed;
                    _lightLeft.m_wantedChargeSpeed = data.LightAttackSpeed * 0.3f;
                    break;
                case eMeleeWeaponState.AttackMissRight:
                    if (_lightRight == null)
                        _lightRight = __instance.m_states[(int)eMeleeWeaponState.AttackMissRight].TryCast<MWS_AttackLight>()!;
                    _lightRight.m_wantedNormalSpeed = data.LightAttackSpeed;
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
