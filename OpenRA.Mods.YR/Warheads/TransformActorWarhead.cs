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
using System;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.YR.Warheads
{
    public class TransformActorWarhead : Warhead
    {
        [Desc("Which actor did you wish to transfrom to?")]
        public readonly string Actor = null;
        [Desc("Which sequence did you wish to play when tranforming an actor?")]
        public readonly string Sequence = null;
        public readonly string ExcludeActor = null;
        public readonly WAngle Facing = WAngle.FromFacing(128);
        [Desc("Types of damage that this warhead causes. Leave empty for no damage types.")]
        public readonly BitSet<DamageType> DamageTypes = default(BitSet<DamageType>);
        private Actor actor;
        private TypeDictionary typeDic;
        private string[] excludeActors;
        protected override void DoImpact(Target target, WarheadArgs args)
        {
            var firedBy = args.SourceActor;

            if (!string.IsNullOrEmpty(ExcludeActor))
            {
                excludeActors = ExcludeActor.Split(',');
            }
            else
            {
                excludeActors = Array.Empty<string>();
            }

            World w = firedBy.World;
            WPos targetPos = target.CenterPosition;
            var victimActors = w.FindActorsInCircle(targetPos, new WDist(1));
            foreach (Actor victimActor in victimActors)
            {
                if (!victimActor.IsDead &&
                    victimActor.TraitsImplementing<WithInfantryBody>().Count() > 0 &&
                    !excludeActors.Contains(victimActor.Info.Name))
                {
                    victimActor.Kill(firedBy, DamageTypes);

                    actor = firedBy;
                    CPos pos = victimActor.World.Map.CellContaining(victimActor.CenterPosition);

                    typeDic = new TypeDictionary()
                    {
                        new CenterPositionInit(targetPos),
                        new LocationInit(pos),
                        new OwnerInit(firedBy.Owner),
                        new FacingInit(Facing)
                    };
                    w.CreateActor(Actor, typeDic);
                }
            }
        }
    }
}
