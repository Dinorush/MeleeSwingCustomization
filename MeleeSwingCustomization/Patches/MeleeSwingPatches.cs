using Gear;
using HarmonyLib;
using MSC.CustomMeleeData;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Il2CppGeneric = Il2CppSystem.Collections.Generic;
using GameData;

namespace MSC.Patches
{
    [HarmonyPatch]
    internal static class MeleeSwingPatches
    {
        private static RaycastHit _rayHit;
        private static MeleeData _activeData = null!;
        private static float _lastCrosshairCheckTime = 0f;
        private static bool _lastCrosshairCheck = false;
        private const float CameraUpdateInterval = 0.033f;

        [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.OnWield))]
        [HarmonyPostfix]
        private static void Post_Wield(MeleeWeaponFirstPerson __instance)
        {
            MeleeData? data = MeleeDataManager.Current.GetData(__instance);
            if (data == null || (!data.AttackOffset.HasCapsule && !data.AttackOffset.HasEntity && !data.AttackOffset.HasEntityRay))
                _activeData = null!;
            else
                _activeData = data;
        }

        [HarmonyPatch(typeof(MeleeWeaponFirstPerson), nameof(MeleeWeaponFirstPerson.UpdateLocal))]
        [HarmonyWrapSafe]
        [HarmonyPrefix]
        private static bool Pre_Update(MeleeWeaponFirstPerson __instance)
        {
            if (_activeData == null) return true;

            __instance.UpdateInput();
            __instance.CurrentState.Update();
            switch (__instance.CurrentStateName)
            {
                case eMeleeWeaponState.AttackMissLeft:
                case eMeleeWeaponState.AttackMissRight:
                case eMeleeWeaponState.AttackChargeReleaseLeft:
                case eMeleeWeaponState.AttackChargeReleaseRight:
                    var swingState = __instance.CurrentState.Cast<MWS_AttackSwingBase>();
                    CustomHitDetection(__instance, swingState, _activeData);
                    break;
            }

            CrosshairUpdate(__instance);
            return false;
        }

        private static void CrosshairUpdate(MeleeWeaponFirstPerson melee)
        {
            var camera = melee.Owner.FPSCamera;
            bool hasEnemy = camera.CameraRayObject != null && camera.CameraRayDist <= melee.MeleeArchetypeData.CameraDamageRayLength && camera.CameraRayObject.layer == LayerManager.LAYER_ENEMY_DAMAGABLE;

            if (!hasEnemy)
            {
                if (Clock.Time - _lastCrosshairCheckTime > CameraUpdateInterval)
                {
                    float rayLen = melee.MeleeArchetypeData.CameraDamageRayLength + _activeData.AttackOffset.EntityRayLengthAdd;
                    _lastCrosshairCheckTime = Clock.Time;
                    _lastCrosshairCheck = Physics.Raycast(camera.m_camRay, out _rayHit, rayLen, LayerManager.MASK_ENEMY_DAMAGABLE);
                }
                hasEnemy = _lastCrosshairCheck;
            }

            if (hasEnemy)
            {
                if (!melee.m_lookAtEnemy)
                {
                    GuiManager.CrosshairLayer.ScaleToSize(melee.HipFireCrosshairSize * 0.6f);
                    GuiManager.CrosshairLayer.TriggerBlink(Color.white);
                    melee.m_lookAtEnemy = true;
                }
            }
            else if (melee.m_lookAtEnemy)
            {
                GuiManager.CrosshairLayer.ScaleToSize(melee.HipFireCrosshairSize);
                GuiManager.CrosshairLayer.ResetChargeUpColor();
                melee.m_lookAtEnemy = false;
            }
        }

        private static void CustomHitDetection(MeleeWeaponFirstPerson melee, MWS_AttackSwingBase mws, MeleeData data)
        {
            MeleeAttackData attackData = mws.AttackData;
            float damageStartDelay = attackData.m_damageStartTime;
            float elapsed = mws.m_elapsed;
            if (elapsed <= damageStartDelay || elapsed > attackData.m_damageEndTime || mws.m_targetsFound) return;

            MeleeArchetypeDataBlock archBlock = melee.MeleeArchetypeData;
            bool hasRay = data.AttackOffset.HasEntityRay;
            bool hasCapsule = data.AttackOffset.HasCapsule;
            bool hasEntity = data.AttackOffset.HasEntity;
            float capsuleStartDelay = hasCapsule ? damageStartDelay + data.AttackOffset.GetCapsuleDelay(melee.CurrentStateName) : 0f;
            float rayStartDelay = attackData.m_attackCamFwdHitTime;

            FPSCamera camera = melee.Owner.FPSCamera;
            // Abort if we're looking directly at something
            if (!archBlock.CanHitMultipleEnemies && Physics.Raycast(camera.Position, camera.Forward, out _rayHit, archBlock.CameraDamageRayLength, LayerManager.MASK_MELEE_ATTACK_TARGETS_WITH_STATIC))
                return;

            if (CheckForHits(hasRay && elapsed >= rayStartDelay, hasEntity, hasCapsule && elapsed >= capsuleStartDelay, camera, archBlock, attackData, data, out var hits))
            {
                mws.m_targetsFound = true;
                melee.HitsForDamage = hits;
                mws.OnAttackHit(); // Ends this coroutine via patch
            }
        }

        private static bool CheckForHits(bool hasRay, bool checkEntity, bool checkCapsule, FPSCamera camera, MeleeArchetypeDataBlock archBlock, MeleeAttackData attackData, MeleeData data, [MaybeNullWhen(false)] out Il2CppGeneric.List<MeleeWeaponDamageData> hits)
        {
            DamageUtil.IncrementSearchID();
            hits = new();
            bool stopOnHit = !archBlock.CanHitMultipleEnemies;
            if (hasRay && CheckForRaycastHit(camera, archBlock, data, hits) && stopOnHit)
                return true;
            if (checkEntity && CheckForHits_Inner(capsule: false, camera, archBlock, attackData, data, hits) && stopOnHit)
                return true;
            if (checkCapsule && CheckForHits_Inner(capsule: true, camera, archBlock, attackData, data, hits) && stopOnHit)
                return true;
            return hits.Count > 0;
        }

        private static bool CheckForRaycastHit(FPSCamera camera, MeleeArchetypeDataBlock archBlock, MeleeData data, Il2CppGeneric.List<MeleeWeaponDamageData> hits)
        {
            float rayLen = archBlock.CameraDamageRayLength + data.AttackOffset.EntityRayLengthAdd;
            if (Physics.Raycast(camera.Position, camera.Forward, out _rayHit, rayLen, LayerManager.MASK_ENEMY_DAMAGABLE))
            {
                hits.Add(new()
                {
                    damageGO = _rayHit.collider.gameObject,
                    damageComp = _rayHit.collider.GetComponent<IDamageable>(),
                    hitPos = _rayHit.point,
                    hitNormal = _rayHit.normal,
                    sourcePos = camera.Position
                });
                return true;
            }
            return false;
        }

        private static bool CheckForHits_Inner(bool capsule, FPSCamera camera, MeleeArchetypeDataBlock archBlock, MeleeAttackData attackData, MeleeData data, Il2CppGeneric.List<MeleeWeaponDamageData> hits)
        {
            Transform transform = attackData.m_damageRef.transform;
            Vector3 transformPos = transform.position;
            Vector3 cameraPos = camera.Position;
            float viewDot = Vector3.Dot(camera.Forward, (transformPos - cameraPos).normalized);
            if (viewDot <= 0 && !archBlock.CanHitMultipleEnemies) return false;

            float dotScale = viewDot > 0.5f ? 1f + viewDot * (data.AttackSphereCenterMod - 1f) : 1f;
            OffsetData offsetData = data.AttackOffset;
            float radius;
            Collider[] colliders;
            if (capsule)
            {
                radius = offsetData.GetCapsuleSize(archBlock, dotScale);
                (Vector3 start, Vector3 end) = offsetData.GetCapsuleOffsets(transform, archBlock);
                colliders = Physics.OverlapCapsule(start, end, radius, LayerManager.MASK_MELEE_ATTACK_TARGETS);
            }
            else
            {
                radius = offsetData.GetEntitySize(archBlock, dotScale);
                colliders = Physics.OverlapSphere(offsetData.GetEntityOffset(transform), radius, LayerManager.MASK_MELEE_ATTACK_TARGETS);
            }

            if (colliders.Length == 0) return false;

            uint searchID = DamageUtil.SearchID;
            List<(Collider, float sqrMagnitude)> sortList = new();
            foreach (var collider in colliders)
                sortList.Add((collider, (collider.transform.position - cameraPos).sqrMagnitude));
            sortList.Sort(SqrMagnitudeCompare);

            bool gotHit = false;
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
                    gotHit = true;
                }
            }

            return gotHit;
        }

        private static int SqrMagnitudeCompare((Collider, float sqrMagnitude) x, (Collider, float sqrMagnitude) y)
        {
            if (x.sqrMagnitude == y.sqrMagnitude) return 0;
            return x.sqrMagnitude < y.sqrMagnitude ? -1 : 1;
        }
    }
}
