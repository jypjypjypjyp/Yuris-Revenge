#region Copyright & License Information
/*
 * Written by Cook Green of YR Mod
 * Follows GPLv3 License as the OpenRA engine:
 * 
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.YR.Activities;
using OpenRA.Mods.YR.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.YR.Traits
{
    [Desc("This actor can enter Cargo actors.")]
    public class BunkerPassengerInfo : ConditionalTraitInfo
    {
        public readonly string CargoType = null;
        public readonly int Weight = 1;
        [Desc("Grant a condition when actor is bunkered")]
        public readonly string GrantBunkerCondition = null;

        [Desc("Number of retries using alternate transports.")]
        public readonly int MaxAlternateTransportAttempts = 1;

        [Desc("Range from self for looking for an alternate transport (default: 5.5 cells).")]
        public readonly WDist AlternateTransportScanRange = WDist.FromCells(11) / 2;

        [VoiceReference]
        public readonly string Voice = "Action";

        [Desc("Whose actors can accept this actor?")]
        public readonly string[] Accepter = null;

        [Desc("The weapon will be used when enter a bunker")]
        public readonly string[] Armaments = { "primary" };

        [CursorReference]
        [Desc("Cursor to display when able to be repaired at target actor.")]
        public readonly string EnterCursor = "enter";

        [CursorReference]
        [Desc("Cursor to display when unable to be repaired at target actor.")]
        public readonly string EnterBlockedCursor = "enter-blocked";

        public override object Create(ActorInitializer init) { return new BunkerPassenger(init, this); }
    }

    public class BunkerPassenger : ConditionalTrait<BunkerPassengerInfo>, IIssueOrder, IResolveOrder, IOrderVoice, INotifyRemovedFromWorld
    {
        public readonly BunkerPassengerInfo info;
        private readonly Actor self;
        private int bunkeredCondToken;
        public BunkerPassenger(ActorInitializer init, BunkerPassengerInfo info)
            : base(info)
        {
            this.info = info;
            self = init.Self;
            Func<Actor, TargetModifiers, bool> canTarget = IsCorrectCargoType;
            Func<Actor, bool> useEnterCursor = CanEnter;
            Orders = new EnterAlliedActorTargeter<BunkerCargoInfo>[]
            {
                new EnterBunkerTargeter("EnterBunker", 5, "enter", "enter", canTarget, useEnterCursor),
                new EnterBunkersTargeter("EnterBunkers", 5, "enter", "enter", canTarget, useEnterCursor)
            };
        }

        protected override void Created(Actor self)
        {
            base.Created(self);
        }

        public void GrantCondition()
        {
            bunkeredCondToken = self.GrantCondition(info.GrantBunkerCondition);
        }

        public void RevokeCondition()
        {
            if (bunkeredCondToken == Actor.InvalidConditionToken)
            {
                return;
            }

            if (!self.TokenValid(bunkeredCondToken))
            {
                return;
            }

            bunkeredCondToken = self.RevokeCondition(bunkeredCondToken);
        }

        public Actor Transport;
        public BunkerCargo ReservedCargo { get; private set; }

        public IEnumerable<IOrderTargeter> Orders { get; private set; }

        public Order IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
        {
            if (order.OrderID == "EnterBunker" || order.OrderID == "EnterBunkers")
                return new Order(order.OrderID, self, target, queued);

            return null;
        }

        private bool IsCorrectCargoType(Actor target, TargetModifiers modifiers)
        {
            var ci = target.Info.TraitInfo<BunkerCargoInfo>();

            bool canAccept = false;

            // this actor are not welcomed by the target actor
            for (int i = 0; i < info.Accepter.Length; i++)
            {
                if (info.Accepter[i] == target.Info.Name ||
                    info.Accepter[i] == ci.GrantAccepter)
                {
                    canAccept = true;
                    break;
                }
            }

            if (!canAccept)
            {
                return false;
            }

            return ci.Types.Contains(Info.CargoType);
        }

        private bool CanEnter(BunkerCargo cargo)
        {
            return cargo != null && cargo.HasSpace(Info.Weight);
        }

        private bool CanEnter(Actor target)
        {
            return CanEnter(target.TraitOrDefault<BunkerCargo>());
        }

        public string VoicePhraseForOrder(Actor self, Order order)
        {
            if (order.OrderString != "EnterBunker" && order.OrderString != "EnterBunkers")
                return null;

            if (order.Target.Type != TargetType.Actor || !CanEnter(order.Target.Actor))
                return null;

            return Info.Voice;
        }

        public void ResolveOrder(Actor self, Order order)
        {
            if (order.OrderString != "EnterBunker" && order.OrderString != "EnterBunkers")
                return;

            // Enter orders are only valid for own/allied actors,
            // which are guaranteed to never be frozen.
            if (order.Target.Type != TargetType.Actor)
                return;

            var targetActor = order.Target.Actor;
            if (!CanEnter(targetActor))
                return;

            if (!IsCorrectCargoType(targetActor, TargetModifiers.None))
                return;

            if (!order.Queued)
                self.CancelActivity();

            BunkerCargo cargo = targetActor.TraitOrDefault<BunkerCargo>();

            var transports = order.OrderString == "EnterBunkers";

            self.QueueActivity(new EnterBunker(self, targetActor, targetActor.CenterPosition, cargo.Info.WillDisappear, transports ? Info.MaxAlternateTransportAttempts : 0, !transports));
        }

        public bool Reserve(Actor self, BunkerCargo cargo)
        {
            Unreserve(self);
            if (!cargo.ReserveSpace(self))
                return false;
            ReservedCargo = cargo;
            return true;
        }

        void INotifyRemovedFromWorld.RemovedFromWorld(Actor self) { Unreserve(self); }

        public void Unreserve(Actor self)
        {
            if (ReservedCargo == null)
                return;
            ReservedCargo.UnreserveSpace(self);
            ReservedCargo = null;
        }
    }
}
