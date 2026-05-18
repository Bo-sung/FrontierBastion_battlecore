namespace BattleSim.Core.State
{
    /// <summary>
    /// Which side of the battle owns an entity.
    /// API/DB string encoding ("player" | "enemy") is handled outside Core.
    /// </summary>
    public enum OwnerSide
    {
        Player = 1,
        Enemy = 2,
    }
}
