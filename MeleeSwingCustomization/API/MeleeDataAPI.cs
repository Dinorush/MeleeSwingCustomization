using Gear;
using MSC.CustomMeleeData;

namespace MSC.API
{
    public static class MeleeDataAPI
    {
        public static void AddData(string prefab, MeleeData data) => MeleeDataManager.Current.AddDataForPrefab(prefab, data);

        public delegate bool TryGetMeleeData(MeleeWeaponFirstPerson melee, out MeleeData data);
        public static void AddInstanceData(string prefab, TryGetMeleeData callback) => MeleeDataManager.Current.AddInstanceDataForPrefab(prefab, callback);
    }
}
