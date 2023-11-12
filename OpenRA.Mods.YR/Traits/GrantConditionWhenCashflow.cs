﻿#region Copyright & License Information
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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.YR.Traits
{
    public class GrantConditionWhenCashflowInfo : ConditionalTraitInfo
    {
        [FieldLoader.Require]
        [GrantedConditionReference]
        [Desc("Condition will be granted")]
        public readonly string LowCashCondition = null;

        [FieldLoader.Require]
        [GrantedConditionReference]
        [Desc("Condition will be granted")]
        public readonly string NormalCashCondition = null;

        [Desc("Condition will be granted when player cash lower or equal this number")]
        public int CashAmount = 0;

        public override object Create(ActorInitializer init)
        {
            return new GrantConditionWhenCashflow(init, this);
        }
    }

    public class GrantConditionWhenCashflow : ConditionalTrait<GrantConditionWhenCashflowInfo>, INotifyOwnerChanged, ITick
    {
        private int lowCashConditionToken = Actor.InvalidConditionToken;
        private int normalCashConditionToken = Actor.InvalidConditionToken;
        private readonly Actor self;
        private readonly GrantConditionWhenCashflowInfo info;
        private PlayerResources resources;

        public GrantConditionWhenCashflow(ActorInitializer init, GrantConditionWhenCashflowInfo info)
            : base(info)
        {
            self = init.Self;
            this.info = info;
        }

        protected override void Created(Actor self)
        {
            resources = self.Owner.PlayerActor.Trait<PlayerResources>();
            CheckCashCondition();
        }

        public void OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
        {
            resources = newOwner.PlayerActor.Trait<PlayerResources>();
        }

        public void Tick(Actor self)
        {
            CheckCashCondition();
        }

        private void CheckCashCondition()
        {
            if (resources.Cash <= info.CashAmount)
            {
                if (lowCashConditionToken == Actor.InvalidConditionToken)
                {
                    lowCashConditionToken = self.GrantCondition(info.LowCashCondition);
                }

                if (normalCashConditionToken != Actor.InvalidConditionToken)
                {
                    normalCashConditionToken = self.RevokeCondition(normalCashConditionToken);
                }
            }
            else
            {
                if (lowCashConditionToken != Actor.InvalidConditionToken)
                {
                    lowCashConditionToken = self.RevokeCondition(lowCashConditionToken);
                }

                if (normalCashConditionToken == Actor.InvalidConditionToken)
                {
                    normalCashConditionToken = self.GrantCondition(info.NormalCashCondition);
                }
            }
        }
    }
}
