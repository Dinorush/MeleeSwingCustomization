using BepInEx.Unity.IL2CPP.Utils.Collections;
using Gear;
using MSC.CustomMeleeData;
using Player;
using System.Collections;
using UnityEngine;

namespace MSC.Utils
{
    internal static class DebugUtil
    {
        private static Coroutine? _drawCoroutine;

        public static void DrawDebugSpheres(MeleeWeaponFirstPerson? melee = null)
        {
            if (_drawCoroutine != null)
            {
                CoroutineManager.StopCoroutine(_drawCoroutine);
                _drawCoroutine = null;
            }

            if (!Configuration.DrawDebugHitbox) return;

            if (melee == null)
            {
                if (!PlayerBackpackManager.LocalBackpack.TryGetBackpackItem(InventorySlot.GearMelee, out var bpItem)) return;
                melee = bpItem.Instance.Cast<MeleeWeaponFirstPerson>();
            }

            MeleeData? meleeData = MeleeDataManager.Current.GetData(melee.MeleeArchetypeData.persistentID);
            _drawCoroutine = CoroutineManager.StartCoroutine(DrawSpheres(melee, meleeData).WrapToIl2Cpp());
        }

        private static IEnumerator DrawSpheres(MeleeWeaponFirstPerson melee, MeleeData? data)
        {
            while (melee != null)
            {
                yield return null;

                ItemEquippable? holder = PlayerManager.GetLocalPlayerAgent()?.FPItemHolder?.WieldedItem;
                if (holder != melee) continue;

                Vector3 normal = melee.ModelData.m_damageRefAttack.position;
                if (data?.AttackOffset.HasCapsule == true)
                {
                    (Vector3 start, Vector3 end) = data.AttackOffset.GetCapsuleOffsets(melee.ModelData.m_damageRefAttack, melee.MeleeArchetypeData);
                    DebugDraw3D.DrawSphere(start, Configuration.DebugSphereSize, start == normal ? ColorExt.Orange(0.4f) : ColorExt.Yellow(0.4f));
                    DebugDraw3D.DrawSphere(end, Configuration.DebugSphereSize, end == normal ? ColorExt.Orange(0.4f) : ColorExt.Yellow(0.4f));
                    if (start != normal && end != normal)
                        DebugDraw3D.DrawSphere(normal, Configuration.DebugSphereSize, ColorExt.Red(0.4f));
                }
                else
                    DebugDraw3D.DrawSphere(normal, Configuration.DebugSphereSize, ColorExt.Red(0.4f));
            }
            _drawCoroutine = null;
        }
    }
}
