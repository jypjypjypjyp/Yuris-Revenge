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

using System;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.YR.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.YR.Orders
{
    public class EnterBunkersTargeter : EnterAlliedActorTargeter<BunkerCargoInfo>
    {
        private readonly Func<Actor, TargetModifiers, bool> canTarget;

        public EnterBunkersTargeter(string order, int priority,
            string enterCursor, string enterBlockedCursor,
            Func<Actor, TargetModifiers, bool> canTarget,
            Func<Actor, bool> useEnterCursor)
            : base(order, priority, enterCursor, enterBlockedCursor, canTarget, useEnterCursor)
        {
            this.canTarget = canTarget;
        }

        public override bool CanTargetActor(Actor self, Actor target, TargetModifiers modifiers, ref string cursor)
        {
            if (target.Owner.InternalName == "Neutral" && target.Info.HasTraitInfo<BunkerCargoInfo>() && canTarget(target, modifiers))
                return true;

            return base.CanTargetActor(self, target, modifiers, ref cursor);
        }
    }
}
