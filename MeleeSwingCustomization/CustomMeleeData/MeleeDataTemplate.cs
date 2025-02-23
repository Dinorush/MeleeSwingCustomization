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
                AttackOffset = new(0, -0.111f, 0.483f),
                PushOffset = new(0, -0.005f, 0.041f)
            },
            new()
            {
                ArchetypeID = 0,
                Name = "Knife",
                AttackOffset = new(0, 0.248f, 0.076f),
                PushOffset = new(0, 0.0228f, 1.061f)
            },
            new()
            {
                ArchetypeID = 0,
                Name = "Spear",
                AttackOffset = new(0, 0.972f, -0.002f),
                PushOffset = new(0, -0.501f, .005f)
            },
            new()
            {
                ArchetypeID = 0,
                Name = "Bat",
                AttackOffset = new(0, 0.4047f, 0.0f),
                PushOffset = new(0, -0.0817f, 0f)
            },
            new()
            {
                ArchetypeID = 0,
                Name = "Improved Bat",
                AttackOffset = new(0, 0.55f, 0.0f),
            },
            new()
            {
                ArchetypeID = 0,
                Name = "Improved Spear",
                AttackOffset = new((new(0, 0.972f, -0.002f), new(0, -0.2f, 0))) { CapsuleDelay = 0.1f }
            }
        };
    }
}
