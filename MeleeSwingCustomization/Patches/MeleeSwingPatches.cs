using Gear;
using HarmonyLib;
using MSC.CustomMeleeData;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppGeneric = Il2CppSystem.Collections.Generic;

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

            _capsuleRoutine = CoroutineManager.StartCoroutine(CapsuleHitDetection(melee, __instance, data.AttackOffset).WrapToIl2Cpp());
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

        private static IEnumerator CapsuleHitDetection(MeleeWeaponFirstPerson melee, MWS_AttackSwingBase mws, OffsetData offsetData)
        {
            MeleeAttackData data = mws.AttackData;
            float endTime = Clock.Time + data.m_damageEndTime;
            yield return new WaitForSeconds(data.m_damageStartTime);

            while (Clock.Time < endTime && mws != null && !mws.m_targetsFound)
            {
                if (CapsuleCheckForHits(melee, mws.AttackData, offsetData, out var hits))
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

        private static bool CapsuleCheckForHits(MeleeWeaponFirstPerson melee, MeleeAttackData data, OffsetData offsetData, [MaybeNullWhen(false)] out Il2CppGeneric.List<MeleeWeaponDamageData> hits)
        {
            hits = null;
            Transform transform = data.m_damageRef.transform;
            Vector3 transformPos = transform.position;
            Vector3 cameraPos = melee.Owner.FPSCamera.Position;
            float viewDot = Vector3.Dot(melee.Owner.FPSCamera.Forward, (cameraPos - transformPos).normalized);

            float radius = offsetData.CapsuleSize(melee.MeleeArchetypeData, viewDot);
            (Vector3 start, Vector3 end) = offsetData.CapsuleOffsets(transform, melee.MeleeArchetypeData);
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
