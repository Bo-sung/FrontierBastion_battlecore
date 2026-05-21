namespace BattleSim.Core.State
{
    /// <summary>
    /// Which side of the battle owns an entity or command.
    /// API/DB string encoding ("side_a" | "side_b") is handled outside Core.
    /// PvE interpretation: SideA = local player, SideB = AI controller.
    /// </summary>
    public enum BattleSide
    {
        None  = 0,
        SideA = 1,
        SideB = 2,
    }
}
