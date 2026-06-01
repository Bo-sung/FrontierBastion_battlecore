using BattleSim.Core.Commands;

namespace BattleSim.Core.State
{
    public sealed class BattleSideSupportState
    {
        public BattleSide Side { get; private set; }
        public int ResourceLevel { get; private set; }
        public int PilotLevel { get; private set; }
        public BattleSupportTrack ActiveTrack { get; private set; }
        public int ActiveTargetLevel { get; private set; }
        public int RemainingTick { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsEnergyRegenPaused { get; private set; }
        public bool IsPilotDeployBlocked { get; private set; }

        public BattleSideSupportState(
            BattleSide side,
            int resourceLevel,
            int pilotLevel,
            BattleSupportTrack activeTrack,
            int activeTargetLevel,
            int remainingTick,
            bool isActive,
            bool isEnergyRegenPaused,
            bool isPilotDeployBlocked)
        {
            Side = side;
            ResourceLevel = resourceLevel;
            PilotLevel = pilotLevel;
            ActiveTrack = activeTrack;
            ActiveTargetLevel = activeTargetLevel;
            RemainingTick = remainingTick;
            IsActive = isActive;
            IsEnergyRegenPaused = isEnergyRegenPaused;
            IsPilotDeployBlocked = isPilotDeployBlocked;
        }
    }
}
