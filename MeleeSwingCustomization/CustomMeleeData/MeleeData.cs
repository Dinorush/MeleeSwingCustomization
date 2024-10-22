using UnityEngine;

namespace MSC.CustomMeleeData
{
    public sealed class MeleeData
    {
        public uint ArchetypeID { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public OffsetData AttackOffset { get; set; } = new();
        public Vector3? PushOffset { get; set; } = null;
        public float LightAttackSpeed { get; set; } = 1f;
        public float ChargedAttackSpeed { get; set; } = 1f;
        public float PushSpeed { get; set; } = 1f;
        public float AttackSphereCenterMod { get; set; } = 1.9f;
    }
}
