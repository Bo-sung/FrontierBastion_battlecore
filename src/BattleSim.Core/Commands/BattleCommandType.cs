namespace BattleSim.Core.Commands
{
    /// <summary>
    /// Player-input command kinds accepted by <see cref="Simulation.BattleSimulator"/>.
    /// API/DB string encoding is handled outside Core.
    /// </summary>
    public enum BattleCommandType
    {
        SpawnDroneSquad = 1,
        DeployPilot = 2,
        RecallPilot = 3,
    }
}
