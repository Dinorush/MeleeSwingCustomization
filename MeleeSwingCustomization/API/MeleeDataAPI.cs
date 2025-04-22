using MSC.CustomMeleeData;

namespace MSC.API
{
    public static class MeleeDataAPI
    {
        public static void AddData(string prefab, MeleeData data) => MeleeDataManager.Current.AddDataForPrefab(prefab, data);
    }
}
