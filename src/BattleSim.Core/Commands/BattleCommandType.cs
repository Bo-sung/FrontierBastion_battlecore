namespace BattleSim.Core.Commands
{
    /// <summary>
    /// Command kinds accepted by <see cref="Simulation.BattleSimulator"/>.
    /// Both SideA and SideB use the same command types.
    /// API/DB string encoding is handled outside Core.
    /// </summary>
    public enum BattleCommandType
    {
        SpawnDroneSquad = 1,
        DeployPilot = 2,
        RecallPilot = 3,
        StartSupportUpgrade = 4,
    }
}
