#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.AS.Activities
{
    public class FallDown : Activity
    {
        private readonly IPositionable pos;
        private readonly WVec fallVector;
        private readonly WPos dropPosition;
        private WPos currentPosition;
        private bool triggered = false;

        public FallDown(Actor self, WPos dropPosition, int fallRate, Actor ignoreActor = null)
        {
            pos = self.TraitOrDefault<IPositionable>();
            IsInterruptible = false;
            fallVector = new WVec(0, 0, fallRate);
            this.dropPosition = dropPosition;
        }

        private Activity FirstTick(Actor self)
        {
            triggered = true;

            // Place the actor and retrieve its visual position (CenterPosition)
            pos.SetPosition(self, dropPosition);
            currentPosition = self.CenterPosition;

            return this;
        }

        private Activity LastTick(Actor self)
        {
            var dat = self.World.Map.DistanceAboveTerrain(currentPosition);
            pos.SetPosition(self, currentPosition - new WVec(WDist.Zero, WDist.Zero, dat));

            return NextActivity;
        }

        public override bool Tick(Actor self)
        {
            // If this is the first tick
            if (!triggered)
                Queue(FirstTick(self));

            currentPosition -= fallVector;

            // If the unit has landed, this will be the last tick
            if (self.World.Map.DistanceAboveTerrain(currentPosition).Length <= 0)
                Queue(LastTick(self));

            pos.SetCenterPosition(self, currentPosition);

            return false;
        }
    }
}
