#region Copyright & License Information
/*
 * Modded by Boolbada of OP mod, from Engineer repair enter activity.
 *
 * Note: You can still use this without modifying the OpenRA engine itself by deleting
 * FindAndTransitionToNextState. I just deleted a few lines of "movement" recovery code so that
 * interceptors can enter moving carrier.
 * However, for better results, consider modding the engine, as in the following commit:
 * https://github.com/forcecore/OpenRA/commit/fd36f63e508b7ad28e7d320355b7d257654b33ee
 *
 * Also, interceptors sometimes try to land on ground level.
 * To mitigate that, I added LnadingDistance in Spawned trait.
 * However, that isn't perfect. For perfect results, Land.cs of the engine must be modified:
 * https://github.com/forcecore/OpenRA/commit/45970f57283150bc57ce86b8ce8a555018c6ca14
 * I couldn't make it independent as it relies on other stuff in Enter.cs too much.
 *
 * Copyright 2007-2017 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.YR.Traits;
using OpenRA.Traits;

/*
Requires base engine changes.
Since this inherits "Enter", you need to make several variables "protected".
*/

namespace OpenRA.Mods.YR.Activities
{
    internal class EnterCarrierMaster : Enter
    {
        private readonly Actor master; // remember the spawner.
        private readonly CarrierMaster spawnerMaster;

        public EnterCarrierMaster(Actor self, Actor master, CarrierMaster spawnerMaster, EnterBehaviour enterBehaviour, WDist closeEnoughDist)
            : base(self, Target.FromActor(master))
        {
            this.master = master;
            this.spawnerMaster = spawnerMaster;
        }

        protected override void OnEnterComplete(Actor self, Actor targetActor)
        {
            // Master got killed :(
            if (master.IsDead)
                return;

            // Load this thingy.
            // Issue attack move to the rally point.
            self.World.AddFrameEndTask(w =>
            {
                if (self.IsDead || master.IsDead)
                    return;

                spawnerMaster.PickupSlave(master, self);
                w.Remove(self);

                // Insta repair.
                if (spawnerMaster.Info.InstaRepair)
                {
                    var health = self.Trait<Health>();
                    self.InflictDamage(self, new Damage(-health.MaxHP));
                }
            });
        }

        protected override void OnFirstRun(Actor self)
        {
            base.OnFirstRun(self);
        }

        protected override void OnLastRun(Actor self)
        {
            base.OnLastRun(self);
        }

        protected override bool TryStartEnter(Actor self, Actor targetActor)
        {
            return base.TryStartEnter(self, targetActor);
        }

        protected override void TickInner(Actor self, in Target target, bool targetIsDeadOrHiddenActor)
        {
            base.TickInner(self, target, targetIsDeadOrHiddenActor);
        }
    }
}
