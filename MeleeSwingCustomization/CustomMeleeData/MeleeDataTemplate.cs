namespace MSC.CustomMeleeData
{
    internal static class MeleeDataTemplate
    {
        public static MeleeData[] Template = new MeleeData[]
        {
            new()
            {
                ArchetypeID = 0,
                Name = "Sledgehammer",
                AttackOffset = new() {x=0, y=-0.111f, z=0.483f},
                PushOffset = new() {x=0, y=-0.005f, z=0.041f}
            },
            new()
            {
                ArchetypeID = 0,
                Name = "Knife",
                AttackOffset = new() {x=0, y=0.248f, z=0.076f},
                PushOffset = new() {x=0, y=0.0228f, z=1.061f}
            },
            new()
            {
                ArchetypeID = 0,
                Name = "Spear",
                AttackOffset = new() {x=0, y=0.972f, z=-0.002f},
                PushOffset = new() {x=0, y=-0.501f, z=.005f}
            },
            new()
            {
                ArchetypeID = 0,
                Name = "Bat",
                AttackOffset = new() {x=0, y=0.4047f, z=0.0f},
                PushOffset = new() {x=0, y=-0.0817f, z=0f}
            },
            new()
            {
                ArchetypeID = 4,
                Name = "Improved Bat",
                AttackOffset = new() {x=0, y=0.55f, z=0.0f},
                PushOffset = null
            }
        };
    }
}
