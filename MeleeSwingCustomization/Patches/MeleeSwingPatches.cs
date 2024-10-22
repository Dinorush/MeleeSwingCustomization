using Gear;
using HarmonyLib;
using MSC.CustomMeleeData;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppGeneric = Il2CppSystem.Collections.Generic;
using GameData;

namespace MSC.Patches
{
    [HarmonyPatch(typeof(MWS_AttackSwingBase))]
    internal static class MeleeSwingPatches
    {
        private static Coroutine? _capsuleRoutine;

        [HarmonyPatch(nameof(MWS_AttackSwingBase.Enter))]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_MeleeEnter(MWS_AttackSwingBase __instance)
        {
            MeleeWeaponFirstPerson melee = __instance.m_weapon;
            MeleeData? data = MeleeDataManager.Current.GetData(melee.MeleeArchetypeData.persistentID);
            if (data == null || !data.AttackOffset.HasCapsule) return;

            _capsuleRoutine = CoroutineManager.StartCoroutine(CapsuleHitDetection(melee, __instance, data).WrapToIl2Cpp());
        }

        [HarmonyPatch(nameof(MWS_AttackSwingBase.Exit))]
        [HarmonyWrapSafe]
        [HarmonyPostfix]
        private static void Post_MeleeExit(MWS_AttackSwingBase __instance)
        {
            if (_capsuleRoutine != null)
            {
                CoroutineManager.StopCoroutine(_capsuleRoutine);
                _capsuleRoutine = null;
            }
        }

        private static RaycastHit s_rayHit;
        private static IEnumerator CapsuleHitDetection(MeleeWeaponFirstPerson melee, MWS_AttackSwingBase mws, MeleeData data)
        {
            MeleeAttackData attackData = mws.AttackData;
            float endTime = Clock.Time + attackData.m_damageEndTime;
            yield return new WaitForSeconds(attackData.m_damageStartTime);

            MeleeArchetypeDataBlock archBlock = melee.MeleeArchetypeData;
            FPSCamera camera = melee.Owner.FPSCamera;
            while (Clock.Time < endTime && mws != null && !mws.m_targetsFound && camera != null)
            {
                // Abort if we're looking directly at something
                if (Physics.Raycast(camera.Position, camera.Forward, out s_rayHit, archBlock.CameraDamageRayLength, LayerManager.MASK_MELEE_ATTACK_TARGETS_WITH_STATIC))
                {
                    IDamageable damageable = s_rayHit.collider.GetComponent<IDamageable>();
                    if (damageable == null || damageable.GetBaseDamagable().TempSearchID == DamageUtil.SearchID)
                    {
                        yield return null;
                        continue;
                    }
                }

                if (CapsuleCheckForHits(camera, archBlock, attackData, data, out var hits))
                {
                    mws.m_targetsFound = true;
                    melee.HitsForDamage = hits;
                    mws.OnAttackHit();
                }
                else
                    yield return null;
            }
            _capsuleRoutine = null;
        }

        private static bool CapsuleCheckForHits(FPSCamera camera, MeleeArchetypeDataBlock archBlock, MeleeAttackData attackData, MeleeData data, [MaybeNullWhen(false)] out Il2CppGeneric.List<MeleeWeaponDamageData> hits)
        {
            hits = null;
            Transform transform = attackData.m_damageRef.transform;
            Vector3 transformPos = transform.position;
            Vector3 cameraPos = camera.Position;
            float viewDot = Vector3.Dot(camera.Forward, (transformPos - cameraPos).normalized);
            if (viewDot <= 0 && !archBlock.CanHitMultipleEnemies) return false;

            OffsetData offsetData = data.AttackOffset;
            float radius = offsetData.CapsuleSize(archBlock, viewDot > 0.5f ? viewDot * (data.AttackSphereCenterMod - 1f) : 0f);
            (Vector3 start, Vector3 end) = offsetData.CapsuleOffsets(transform, archBlock);
            Collider[] colliders = Physics.OverlapCapsule(start, end, radius, LayerManager.MASK_MELEE_ATTACK_TARGETS);
            if (colliders.Length == 0) return false;

            DamageUtil.IncrementSearchID();
            uint searchID = DamageUtil.SearchID;

            List<(Collider, float sqrMagnitude)> sortList = new();
            foreach (var collider in colliders)
                sortList.Add((collider, (collider.transform.position - cameraPos).sqrMagnitude));
            sortList.Sort(SqrMagnitudeCompare);

            hits = new();
            foreach ((Collider collider, _) in sortList)
            {
                IDamageable? damageable = collider.GetComponent<IDamageable>();
                if (damageable != null && damageable.GetBaseDamagable().TempSearchID != searchID)
                {
                    damageable.GetBaseDamagable().TempSearchID = searchID;
                    MeleeWeaponDamageData meleeWeaponDamageData = new()
                    { 
                        damageGO = collider.gameObject,
                        hitPos = collider.transform.position,
                        hitNormal = (transformPos - collider.transform.position).normalized,
                        sourcePos = cameraPos,
                        damageTargetFound = true,
                    };
                    hits.Add(meleeWeaponDamageData);
                }
            }

            return hits.Count > 0;
        }

        private static int SqrMagnitudeCompare((Collider, float sqrMagnitude) x, (Collider, float sqrMagnitude) y)
        {
            if (x.sqrMagnitude == y.sqrMagnitude) return 0;
            return x.sqrMagnitude < y.sqrMagnitude ? -1 : 1;
        }
    }
}
