using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.YR.Traits
{
    public class RepairsUnitsConditionalInfo : RepairsUnitsInfo
    {
        [Desc("Grant a condition when repairing")]
        public readonly string RepairingCondition;
        public override object Create(ActorInitializer init)
        {
            return new RepairsUnitsConditional(init, this);
        }
    }

    public class RepairsUnitsConditional : RepairsUnits, INotifyResupply
    {
        private readonly RepairsUnitsConditionalInfo info;
        private int conditionToken = Actor.InvalidConditionToken;
        public RepairsUnitsConditional(ActorInitializer init, RepairsUnitsConditionalInfo info)
            : base(info)
        {
            this.info = info;
        }

        protected override void Created(Actor self)
        {
            base.Created(self);
        }

        public void BeforeResupply(Actor host, Actor target, ResupplyType types)
        {
        }

        public void ResupplyTick(Actor host, Actor target, ResupplyType types)
        {
            if (types.HasFlag(ResupplyType.Repair))
            {
                if (conditionToken == Actor.InvalidConditionToken)
                {
                    conditionToken = host.GrantCondition(info.RepairingCondition);
                }
            }
            else if (types.HasFlag(ResupplyType.None))
            {
                if (conditionToken != Actor.InvalidConditionToken)
                {
                    conditionToken = host.RevokeCondition(conditionToken);
                }
            }
        }
    }
}
