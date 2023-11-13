#region Copyright & License Information
/*
 * Modded by Cook Green of YR Mod
 *
 * Modded by Boolbada of OP Mod.
 * Modded from cargo.cs but a lot changed.
 *
 * Copyright 2007-2017 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;
using OpenRA.Mods.YR.Activities;

/*
 * Works without base engine modification?
 */

namespace OpenRA.Mods.YR.Traits
{
    [Desc("This actor can spawn missile actors.")]
    public class MissileSpawnerMasterInfo : BaseSpawnerMasterInfo
    {
        [GrantedConditionReference]
        [Desc("The condition to grant to self right after launching a spawned unit. (Used by V3 to make immobile.)")]
        public readonly string LaunchingCondition = null;

        [Desc("After this many ticks, we remove the condition.")]
        public readonly int LaunchingTicks = 15;

        [GrantedConditionReference]
        [Desc("The condition to grant to self while spawned units are loaded.",
            "Condition can stack with multiple spawns.")]
        public readonly string LoadedCondition = null;

        [Desc("Conditions to grant when specified actors are contained inside the transport.",
            "A dictionary of [actor id]: [condition].")]
        public readonly Dictionary<string, string> SpawnContainConditions = new Dictionary<string, string>();

        [GrantedConditionReference]
        public IEnumerable<string> LinterSpawnContainConditions { get { return SpawnContainConditions.Values; } }

        public override object Create(ActorInitializer init) { return new MissileSpawnerMaster(init, this); }
    }

    public class MissileSpawnerMaster : BaseSpawnerMaster, ITick, INotifyAttack
    {
        public new MissileSpawnerMasterInfo Info { get; private set; }

        private int loadedConditionToken = Actor.InvalidConditionToken;

        //// Stack<int> loadedTokens = new Stack<int>();

        private int respawnTicks = 0;

        public MissileSpawnerMaster(ActorInitializer init, MissileSpawnerMasterInfo info)
            : base(init, info)
        {
            Info = info;
        }

        protected override void Created(Actor self)
        {
            base.Created(self);

            if (!string.IsNullOrEmpty(Info.LoadedCondition) &&
                loadedConditionToken == Actor.InvalidConditionToken)
            {
                loadedConditionToken = self.GrantCondition(Info.LoadedCondition);
            }
        }

        public override void OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
        {
            // Do nothing, because missiles can't be captured or mind controlled.
            return;
        }

        void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel) { }

        // The rate of fire of the dummy weapon determines the launch cycle as each shot
        // invokes Attacking()
        void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
        {
            if (IsTraitDisabled)
                return;

            if (a.Info.Name != Info.SpawnerArmamentName)
                return;

            // Issue retarget order for already launched ones
            foreach (var slave in SlaveEntries)
                if (slave.IsValid)
                    slave.SpawnerSlave.Attack(slave.Actor, target);

            var se = GetLaunchable();
            if (se == null)
                return;

            // Program the trajectory.
            var sbm = se.Actor.Trait<ShootableBallisticMissile>();
            sbm.Target = Target.FromPos(target.CenterPosition);

            SpawnIntoWorld(self, se.Actor, self.CenterPosition);

            // Queue attack order, too.
            self.World.AddFrameEndTask(w =>
            {
                se.Actor.QueueActivity(new ShootableBallisticMissileFly(se.Actor, sbm.Target, sbm));

                // invalidate the slave entry so that slave will regen.
                se.Actor = null;
            });

            // Set clock so that regen happens.
            if (respawnTicks <= 0) // Don't interrupt an already running timer!
                respawnTicks = Info.RespawnTicks;
        }

        private BaseSpawnerSlaveEntry GetLaunchable()
        {
            foreach (var se in SlaveEntries)
                if (se.IsValid)
                    return se;

            return null;
        }

        public void Tick(Actor self)
        {
            if (respawnTicks > 0)
            {
                respawnTicks--;

                // Time to respawn someting.
                if (respawnTicks <= 0)
                {
                    Replenish(self, SlaveEntries);

                    if (!string.IsNullOrEmpty(Info.LoadedCondition) &&
                        loadedConditionToken == Actor.InvalidConditionToken)
                    {
                        loadedConditionToken = self.GrantCondition(Info.LoadedCondition);
                    }

                    // If there's something left to spawn, restart the timer.
                    if (SelectEntryToSpawn(SlaveEntries) != null)
                        respawnTicks = Info.RespawnTicks;
                }
                else
                {
                    if (loadedConditionToken != Actor.InvalidConditionToken)
                    {
                        loadedConditionToken = self.RevokeCondition(loadedConditionToken);
                    }
                }
            }
        }
    }
}
