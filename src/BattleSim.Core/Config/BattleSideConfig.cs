using System;
using BattleSim.Core.FixedPoint;
using BattleSim.Core.State;

namespace BattleSim.Core.Config
{
    /// <summary>
    /// Energy economy and base HP for one side of a battle.
    /// </summary>
    public sealed class BattleSideConfig
    {
        public BattleSide Side { get; private set; }
        public Fp BaseInitialHp { get; private set; }
        public Fp InitialEnergy { get; private set; }
        public Fp MaxEnergy { get; private set; }
        public Fp EnergyRegenPerTick { get; private set; }

        public BattleSideConfig(
            BattleSide side,
            Fp baseInitialHp,
            Fp initialEnergy,
            Fp maxEnergy,
            Fp energyRegenPerTick)
        {
            if (side == BattleSide.None)
                throw new ArgumentException("side must be SideA or SideB.", "side");
            Side = side;
            BaseInitialHp = baseInitialHp;
            InitialEnergy = initialEnergy;
            MaxEnergy = maxEnergy;
            EnergyRegenPerTick = energyRegenPerTick;
        }
    }
}
