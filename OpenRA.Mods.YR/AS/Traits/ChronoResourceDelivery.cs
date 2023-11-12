﻿#region Copyright & License Information
/*
 * Copyright 2007-2016 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Mods.AS.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
    [Desc("When returning to a refinery to deliver resources, this actor will teleport if possible.")]
    public class ChronoResourceDeliveryInfo : ConditionalTraitInfo, Requires<HarvesterInfo>
    {
        [Desc("The number of ticks between each check to see if we can teleport to the refinery.")]
        public readonly int CheckTeleportDelay = 10;

        [Desc("Image used for the teleport effects. Defaults to the actor's type.")]
        public readonly string Image = null;

        [Desc("Sequence used for the effect played where the harvester jumped from.")]
        [SequenceReference("Image")]
        public readonly string WarpInSequence = null;

        [Desc("Sequence used for the effect played where the harvester jumped to.")]
        [SequenceReference("Image")]
        public readonly string WarpOutSequence = null;

        [Desc("Palette to render the warp in/out sprites in.")]
        [PaletteReference]
        public readonly string Palette = "effect";

        [Desc("Sound played where the harvester jumped from.")]
        public readonly string WarpInSound = null;

        [Desc("Sound where the harvester jumped to.")]
        public readonly string WarpOutSound = null;

        [Desc("Does the sound play under shroud or fog.")]
        public readonly bool AudibleThroughFog = true;

        [Desc("Volume the WarpInSound and WarpOutSound played at.")]
        public readonly float SoundVolume = 1;

        [Desc("Should parasites be teleported along?")]
        public readonly bool ExposeInfectors = true;

        public override object Create(ActorInitializer init) { return new ChronoResourceDelivery(init.Self, this); }
    }

    public class ChronoResourceDelivery : ConditionalTrait<ChronoResourceDeliveryInfo>, INotifyHarvesterAction, ITick
    {
        private CPos? destination = null;
        private CPos harvestedField;
        private int ticksTillCheck = 0;

        public ChronoResourceDelivery(Actor self, ChronoResourceDeliveryInfo info)
            : base(info) { }

        void ITick.Tick(Actor self)
        {
            if (IsTraitDisabled || destination == null)
                return;

            if (ticksTillCheck <= 0)
            {
                ticksTillCheck = Info.CheckTeleportDelay;

                TeleportIfPossible(self);
            }
            else
                ticksTillCheck--;
        }

        public void MovingToResources(Actor self, CPos targetCell)
        {
            Reset();
        }

        public void MovingToRefinery(Actor self, Actor refineryActor)
        {
            var deliverypos = refineryActor.Location + refineryActor.Trait<IAcceptResources>().DeliveryOffset;

            if (destination != null && destination.Value != deliverypos)
                ticksTillCheck = 0;

            harvestedField = self.World.Map.CellContaining(self.CenterPosition);

            destination = deliverypos;
        }

        public void MovementCancelled(Actor self)
        {
            Reset();
        }

        public void Harvested(Actor self, string resource) { }
        public void Docked() { }
        public void Undocked() { }

        private void TeleportIfPossible(Actor self)
        {
            // We're already here; no need to interfere.
            if (self.Location == destination.Value)
            {
                Reset();
                return;
            }

            var pos = self.Trait<IPositionable>();
            if (pos.CanEnterCell(destination.Value))
            {
                self.QueueActivity(false, new ChronoResourceTeleport(destination.Value, Info, harvestedField));
                Reset();
            }
        }

        private void Reset()
        {
            ticksTillCheck = 0;
            destination = null;
        }
    }
}
