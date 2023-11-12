#region Copyright & License Information
/*
 * Copyright 2007-2017 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.YR.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.YR.Activities
{
    public class UnloadBunkerCargo : Activity
    {
        private readonly Actor self;
        private readonly BunkerCargo cargo;
        private readonly INotifyUnload[] notifiers;
        private readonly bool unloadAll;
        private readonly Aircraft aircraft;
        private readonly Mobile mobile;
        private readonly bool assignTargetOnFirstRun;
        private readonly WDist unloadRange;
        private Target destination;
        private bool takeOffAfterUnload;

        public UnloadBunkerCargo(Actor self, WDist unloadRange, bool unloadAll = true)
            : this(self, Target.Invalid, unloadRange, unloadAll)
        {
            assignTargetOnFirstRun = true;
        }

        public UnloadBunkerCargo(Actor self, Target destination, WDist unloadRange, bool unloadAll = true)
        {
            this.self = self;
            cargo = self.Trait<BunkerCargo>();
            notifiers = self.TraitsImplementing<INotifyUnload>().ToArray();
            this.unloadAll = unloadAll;
            aircraft = self.TraitOrDefault<Aircraft>();
            mobile = self.TraitOrDefault<Mobile>();
            this.destination = destination;
            this.unloadRange = unloadRange;
        }

        protected override void OnFirstRun(Actor self)
        {
            if (assignTargetOnFirstRun)
                destination = Target.FromCell(self.World, self.Location);

            // Move to the target destination
            if (aircraft != null)
            {
                // Queue the activity even if already landed in case self.Location != destination
                QueueChild(new Land(self, destination, unloadRange));
                takeOffAfterUnload = !aircraft.AtLandAltitude;
            }
            else if (mobile != null)
            {
                var cell = self.World.Map.Clamp(this.self.World.Map.CellContaining(destination.CenterPosition));
                QueueChild(new Move(self, cell, unloadRange));
            }

            QueueChild(new Wait(cargo.Info.BeforeUnloadDelay));
        }

        public (CPos Cell, SubCell SubCell)? ChooseExitSubCell(Actor passenger)
        {
            var pos = passenger.Trait<IPositionable>();

            return cargo.CurrentAdjacentCells
                .Shuffle(self.World.SharedRandom)
                .Select(c => (c, pos.GetAvailableSubCell(c)))
                .Cast<(CPos, SubCell SubCell)?>()
                .FirstOrDefault(s => s.Value.SubCell != SubCell.Invalid);
        }

        private IEnumerable<CPos> BlockedExitCells(Actor passenger)
        {
            var pos = passenger.Trait<IPositionable>();

            // Find the cells that are blocked by transient actors
            return cargo.CurrentAdjacentCells
                .Where(c => pos.CanEnterCell(c, null, BlockedByActor.All) != pos.CanEnterCell(c, null, BlockedByActor.None));
        }

        public override bool Tick(Actor self)
        {
            if (IsCanceling || cargo.IsEmpty(self))
                return true;

            if (cargo.CanUnload())
            {
                foreach (var inu in notifiers)
                    inu.Unloading(self);

                var actor = cargo.Peek(self);
                var spawn = self.CenterPosition;

                var exitSubCell = ChooseExitSubCell(actor);
                if (exitSubCell == null)
                {
                    self.NotifyBlocker(BlockedExitCells(actor));
                    QueueChild(new Wait(10));
                    return false;
                }

                cargo.Unload(self);
                self.World.AddFrameEndTask(w =>
                {
                    if (actor.Disposed)
                        return;

                    var move = actor.Trait<IMove>();
                    var pos = actor.Trait<IPositionable>();

                    actor.CancelActivity();
                    if (cargo.Info.WillDisappear)
                    {
                        w.Add(actor);
                    }

                    BunkerPassenger bunkerPassenger = actor.TraitOrDefault<BunkerPassenger>();
                    bunkerPassenger.RevokeCondition();
                });
            }

            if (!unloadAll || !cargo.CanUnload())
            {
                if (cargo.Info.AfterUnloadDelay > 0)
                    QueueChild(new Wait(cargo.Info.AfterUnloadDelay, false));

                if (takeOffAfterUnload)
                    QueueChild(new TakeOff(self));

                return true;
            }

            return false;
        }
    }
}
