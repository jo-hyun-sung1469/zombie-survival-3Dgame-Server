namespace zombie_servival_3Dgame_Server.Firearm.Configuration;

public static class DefaultFirearmCatalog
{
    // Add or edit weapons here. Startup sync will upsert by Name.
    public static IReadOnlyList<FirearmCatalogItem> Items { get; } =
    [
        new FirearmCatalogItem
        {
            Name = "Shotgun",
            DisplayName = "Shotgun",
            Category = "Shotgun",
            GachaProbability = 0.35,
            Damage = 55,
            FireRate = 1.1,
            MagazineSize = 8,
            ReloadTimeSeconds = 2.7,
            RangeMeters = 18,
            CriticalMultiplier = 1.4
        },
        new FirearmCatalogItem
        {
            Name = "SMG",
            DisplayName = "SMG",
            Category = "SubmachineGun",
            GachaProbability = 0.25,
            Damage = 18,
            FireRate = 11.5,
            MagazineSize = 32,
            ReloadTimeSeconds = 1.8,
            RangeMeters = 24,
            CriticalMultiplier = 1.3
        },
        new FirearmCatalogItem
        {
            Name = "AssaultRifle",
            DisplayName = "Assault Rifle",
            Category = "Rifle",
            GachaProbability = 0.2,
            Damage = 28,
            FireRate = 8.5,
            MagazineSize = 30,
            ReloadTimeSeconds = 2.1,
            RangeMeters = 40,
            CriticalMultiplier = 1.5
        },
        new FirearmCatalogItem
        {
            Name = "SniperRifle",
            DisplayName = "Sniper Rifle",
            Category = "Sniper",
            GachaProbability = 0.12,
            Damage = 95,
            FireRate = 0.8,
            MagazineSize = 5,
            ReloadTimeSeconds = 2.9,
            RangeMeters = 85,
            CriticalMultiplier = 2.2
        },
        new FirearmCatalogItem
        {
            Name = "RocketLauncher",
            DisplayName = "Rocket Launcher",
            Category = "Launcher",
            GachaProbability = 0.08,
            Damage = 180,
            FireRate = 0.35,
            MagazineSize = 1,
            ReloadTimeSeconds = 3.6,
            RangeMeters = 55,
            CriticalMultiplier = 1.0
        }
    ];
}
