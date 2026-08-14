/// <summary>
/// Point d'accès central vers toutes les configurations du jeu (R3 — éclaté depuis l'ancien
/// HotelConfig monolithique en un ScriptableObject par catégorie).
///
/// Chaque catégorie s'auto-charge depuis Resources/Config/ — aucun câblage manuel requis, il
/// suffit que l'asset correspondant existe (voir chaque XConfig.Instance).
/// </summary>
public static class HotelConfig
{
    public static EconomyConfig      Economy      => EconomyConfig.Instance;
    public static SatisfactionConfig Satisfaction => SatisfactionConfig.Instance;
    public static ReceptionConfig    Reception    => ReceptionConfig.Instance;
    public static KitchenConfig      Kitchen      => KitchenConfig.Instance;
    public static DayNightConfig     DayNight     => DayNightConfig.Instance;
    public static SpawnConfig        Spawn        => SpawnConfig.Instance;
    public static PlayerConfig       Player       => PlayerConfig.Instance;
    public static PlacementConfig    Placement    => PlacementConfig.Instance;
    public static CameraConfig       Camera       => CameraConfig.Instance;
    public static DitherConfig       Dither       => DitherConfig.Instance;
    public static BlockConfig        Block        => BlockConfig.Instance;
    public static RoamConfig         Roam         => RoamConfig.Instance;
    public static EmployeeConfig     Employee     => EmployeeConfig.Instance;
    public static DebugConfig        Debug        => DebugConfig.Instance;
    public static HotelCatalog       Catalog      => HotelCatalog.Instance;
}
