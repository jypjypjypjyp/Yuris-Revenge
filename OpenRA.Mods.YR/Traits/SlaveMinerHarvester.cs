#region Copyright & License Information
/*
 * Modded by Boolbada of OP Mod.
 * Modded from cargo.cs but a lot changed.
 * 
 * Modded by Cook Green of YR Mod
 * 
 * Copyright 2007-2017 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.YR.Activities;
using OpenRA.Traits;

/*
Sort of works without engine mod if you get docking right.
If you want "legit" OP Mod docking behavior where the slaves dock any cells near the Master,
then you need to modify harvester logics, which is the very core of the engine!
*/

namespace OpenRA.Mods.YR.Traits
{
    public enum MiningState
    {
        Scan, // Scanning ore
        Moving, // Moving to the best location
        TryDeploy, // Try to deploy
        Deploying, // Playing deploy animation.
        Mining, // Slaves are mining. We get kicked sometimes to move closer to ore.
        Packaging, // Check if there's ore field is close enough.
        Undeploy,// Ready to transform
    }

    public class SpawnerHarvestResourceInfo : BaseSpawnerMasterInfo
    {
        [Desc("Which resources it can harvest. Make sure slaves can mine these too!")]
        public readonly HashSet<string> Resources = new HashSet<string>();
    }

    [Desc("This actor is a harvester that uses its spawns to indirectly harvest resources. i.e., Slave Miner.")]
    public class SlaveMinerHarvesterInfo : SpawnerHarvestResourceInfo, Requires<IOccupySpaceInfo>, Requires<GrantConditionOnDeployInfo>
    {
        [VoiceReference] public readonly string HarvestVoice = "Action";

        [Desc("Automatically search for resources on creation?")]
        public readonly bool SearchOnCreation = true;

        [Desc("When deployed, use this scan radius.")]
        public readonly int ShortScanRadius = 8;

        [Desc("Look this far when Searching for Ore (in Cells)")]
        public readonly int LongScanRadius = 24;

        [Desc("Look this far when trying to find a deployable position from the target resource patch")]
        public readonly int DeployScanRadius = 8; // 8 * 8 * 3 should be enough candidates, seriously.

        [Desc("If no resource within range at each kick, move.")]
        public readonly int KickScanRadius = 5;

        [Desc("If the SlaveMiner is idle for this long, he'll try to look for ore again at SlaveMinerShortScan range to find ore and wake up (in ticks)")]
        public readonly int KickDelay = 301;

        [Desc("Play this sound when the slave is freed")]
        public readonly string FreeSound = null;

        public override object Create(ActorInitializer init) { return new SlaveMinerHarvester(init, this); }
    }

    public class SlaveMinerHarvester : BaseSpawnerMaster,
        ITick, IIssueOrder, IResolveOrder, IOrderVoice, INotifyDeployComplete, INotifyTransform
    {
        private const string orderID = "SlaveMinerHarvest";
        readonly SlaveMinerHarvesterInfo info;
        readonly Actor self;
        readonly IResourceLayer resLayer;
        readonly Mobile mobile;

        // Because activities don't remember states, we remember states here for them.
        public CPos? LastOrderLocation = null;
        public MiningState MiningState = MiningState.Scan;

        public IEnumerable<IOrderTargeter> Orders
        {
            get { yield return new SlaveMinerHarvestOrderTargeter<SlaveMinerHarvesterInfo>(orderID); }
        }

        int respawnTicks; // allowed to spawn a new slave when <= 0.
        int kickTicks;
        bool allowKicks = true; // allow kicks?

        public SlaveMinerHarvester(ActorInitializer init, SlaveMinerHarvesterInfo info)
            : base(init, info)
        {
            self = init.Self;
            this.info = info;

            mobile = self.Trait<Mobile>();
            resLayer = self.World.WorldActor.Trait<ResourceLayer>();

            kickTicks = info.KickDelay;
        }

        // Modify Harvester trait's states to do the mining.
        void AssignTargetForSpawned(Actor slave, CPos targetLocation)
        {
            var harvest = slave.Trait<Harvester>();

            // set target spot to mine
            slave.QueueActivity(new FindAndDeliverResources(slave, targetLocation));
        }

        // Launch a freshly created slave that isn't in world to the world.
        void Launch(Actor self, BaseSpawnerSlaveEntry se, CPos targetLocation)
        {
            var slave = se.Actor;

            SpawnIntoWorld(self, slave, self.CenterPosition);

            self.World.AddFrameEndTask(w =>
            {
                // Move into world, if not. Ground units get stuck without this.
                if (info.SpawnIsGroundUnit)
                {
                    var mv = se.Actor.Trait<IMove>().MoveToTarget(slave, Target.FromPos(self.CenterPosition));
                    if (mv != null)
                        slave.QueueActivity(mv);
                }

                AssignTargetForSpawned(slave, targetLocation);
            });
        }

        public override void OnSlaveKilled(Actor self, Actor slave)
        {
            if (respawnTicks <= 0)
                respawnTicks = Info.RespawnTicks;
        }

        public void Tick(Actor self)
        {
            respawnTicks--;
            if (respawnTicks > 0)
                return;

            if (MiningState != MiningState.Mining)
                return;

            Replenish(self, SlaveEntries);

            CPos destination = LastOrderLocation.HasValue ? LastOrderLocation.Value : self.Location;

            // Launch whatever we can.
            bool hasInvalidEntry = false;
            foreach (var se in SlaveEntries)
            {
                if (!se.IsValid)
                {
                    hasInvalidEntry = true;
                }
                else if (!se.Actor.IsInWorld)
                {
                    Launch(self, se, destination);
                }
            }

            if (hasInvalidEntry)
            {
                respawnTicks = info.RespawnTicks;
            }
        }

        public Order IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
        {
            if (order.OrderID == orderID)
                return new Order(order.OrderID, self, target, queued);
            return null;
        }

        CPos ResolveHarvestLocation(Actor self, Order order)
        {
            if (self.World.Map.CellContaining(order.Target.CenterPosition) == CPos.Zero)
                return self.Location;

            var loc = self.World.Map.CellContaining(order.Target.CenterPosition);

            var territory = self.World.WorldActor.TraitOrDefault<ResourceClaimLayer>();
            if (territory != null)
            {
                // Find the nearest claimable cell to the order location (useful for group-select harvest):
                return mobile.NearestCell(loc, p => mobile.CanEnterCell(p), 1, 6);
            }

            // Find the nearest cell to the order location (useful for group-select harvest):
            return mobile.NearestCell(loc, p => mobile.CanEnterCell(p), 1, 6);
        }

        void HandleSpawnerHarvest(Actor self, Order order)
        {
            allowKicks = true;

            // state == Deploying implies order string of SpawnerHarvestDeploying
            // and must not cancel deploy activity!
            if (MiningState != MiningState.Deploying)
            {
                self.CancelActivity();
            }

            MiningState = MiningState.Scan;

            LastOrderLocation = ResolveHarvestLocation(self, order);
            self.QueueActivity(new SlaveMinerHarvesterHarvest(self));

            // self.SetTargetLine(Target.FromCell(self.World, LastOrderLocation.Value), Color.Red);

            // Assign new targets for slaves too.
            foreach (var se in SlaveEntries)
            {
                if (se.IsValid && se.Actor.IsInWorld)
                {
                    AssignTargetForSpawned(se.Actor, LastOrderLocation.Value);
                }
            }
        }

        public void ResolveOrder(Actor self, Order order)
        {
            if (order.OrderString == orderID)
                HandleSpawnerHarvest(self, order);
            else if (order.OrderString == "Stop" || order.OrderString == "Move")
            {
                // Disable "smart idle"
                allowKicks = false;
                MiningState = MiningState.Scan;
            }
        }

        public string VoicePhraseForOrder(Actor self, Order order)
        {
            return order.OrderString == orderID ? info.HarvestVoice : null;
        }

        public void TickIdle(Actor self)
        {
            // wake up on idle for long (to find new resource patch. i.e., kick)
            if (allowKicks && self.IsIdle)
                kickTicks--;
            else
                kickTicks = info.KickDelay;

            if (kickTicks <= 0)
            {
                kickTicks = info.KickDelay;
                MiningState = MiningState.Packaging;
                self.QueueActivity(new SlaveMinerHarvesterHarvest(self));
            }
        }

        void INotifyDeployComplete.FinishedDeploy(Actor self)
        {
            allowKicks = true;

            // rescan from where we are
            MiningState = MiningState.Scan;

            // Tell harvesters to unload and restart mining.
            foreach (var se in SlaveEntries)
            {
                if (!se.IsValid || !se.Actor.IsInWorld)
                    continue;

                var s = se.Actor;
                se.SpawnerSlave.Stop(s);
                AssignTargetForSpawned(s, self.Location);
                s.QueueActivity(new FindAndDeliverResources(s));
            }
        }

        void INotifyDeployComplete.FinishedUndeploy(Actor self)
        {
            allowKicks = false;

            // Interrupt harvesters and order them to follow me.
            foreach (var se in SlaveEntries)
            {
                se.SpawnerSlave.Stop(se.Actor);
                se.Actor.QueueActivity(new Follow(se.Actor, Target.FromActor(self), WDist.FromCells(1), WDist.FromCells(3), null));
            }
        }

        public bool CanHarvestCell(Actor self, CPos cell)
        {
            // Resources only exist in the ground layer
            if (cell.Layer != 0)
                return false;

            var resType = resLayer.GetResource(cell).Type;
            if (resType == null)
                return false;

            // Can the harvester collect this kind of resource?
            return info.Resources.Contains(resType.Info.Type);
        }

        void INotifyTransform.BeforeTransform(Actor self)
        {
        }

        void INotifyTransform.OnTransform(Actor self)
        {
        }

        void INotifyTransform.AfterTransform(Actor toActor)
        {
            // When transform complete, assign the slaves to the transform actor
            SlaveMinerMaster refineryMaster = toActor.Trait<SlaveMinerMaster>();
            foreach (var se in SlaveEntries)
            {
                se.SpawnerSlave.LinkMaster(se.Actor, toActor, refineryMaster);
                se.SpawnerSlave.Stop(se.Actor);
                if (!se.Actor.IsDead)
                    se.Actor.QueueActivity(new FindAndDeliverResources(se.Actor));
            }
            refineryMaster.AssignSlavesToMaster(SlaveEntries);
            toActor.QueueActivity(new SlaveMinerMasterHarvest(toActor));
        }

        public override void Killed(Actor self, AttackInfo e)
        {
            base.Killed(self, e);

            if (!string.IsNullOrEmpty(info.FreeSound))
            {
                Game.Sound.Play(SoundType.World, info.FreeSound, self.CenterPosition);
            }
        }
    }

    class SlaveMinerHarvestOrderTargeter<T> : IOrderTargeter where T : SpawnerHarvestResourceInfo
    {
        private string orderID;
        public SlaveMinerHarvestOrderTargeter(string orderID)
        {
            this.orderID = orderID;
        }

        public string OrderID { get { return orderID; } }
        public int OrderPriority { get { return 10; } }
        public bool IsQueued { get; protected set; }
        public bool TargetOverridesSelection(TargetModifiers modifiers) { return true; }

        public bool CanTarget(Actor self, in Target target, List<Actor> othersAtTarget, ref TargetModifiers modifiers, ref string cursor)
        {
            if (target.Type != TargetType.Terrain)
                return false;

            if (modifiers.HasModifier(TargetModifiers.ForceMove))
                return false;

            var location = self.World.Map.CellContaining(target.CenterPosition);

            // Don't leak info about resources under the shroud
            if (!self.Owner.Shroud.IsExplored(location))
                return false;

            var res = self.World.WorldActor.Trait<ResourceRenderer>().GetRenderedResourceType(location);
            var info = self.Info.TraitInfo<T>();
            if (res == null || !info.Resources.Contains(res.Info.Type))
                return false;

            cursor = "harvest";
            IsQueued = modifiers.HasModifier(TargetModifiers.ForceQueue);

            return true;
        }

        public bool TargetOverridesSelection(Actor self, Target target, List<Actor> actorsAt, CPos xy, TargetModifiers modifiers)
        {
            return true;
        }
    }
}
