#region Copyright & License Information
/*
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
using System.Collections.ObjectModel;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
    internal class ImmobileInfo : TraitInfo, IOccupySpaceInfo
    {
        public readonly bool OccupiesSpace = true;
        public override object Create(ActorInitializer init) { return new Immobile(init, this); }

        public IReadOnlyDictionary<CPos, SubCell> OccupiedCells(ActorInfo info, CPos location, SubCell subCell = SubCell.Any)
        {
            var occupied = OccupiesSpace ? new Dictionary<CPos, SubCell>() { { location, SubCell.FullCell } } :
                new Dictionary<CPos, SubCell>();

            return new ReadOnlyDictionary<CPos, SubCell>(occupied);
        }

        bool IOccupySpaceInfo.SharesCell { get { return false; } }
    }

    internal class Immobile : IOccupySpace, ISync, INotifyAddedToWorld, INotifyRemovedFromWorld
    {
        [Sync]
        private readonly CPos location;

        [Sync]
        private readonly WPos position;
        private readonly (CPos, SubCell)[] occupied;

        public Immobile(ActorInitializer init, ImmobileInfo info)
        {
            location = init.GetValue<LocationInit, CPos>();
            position = init.World.Map.CenterOfCell(location);

            if (info.OccupiesSpace)
                occupied = new[] { (TopLeft, SubCell.FullCell) };
            else
                occupied = Array.Empty<(CPos, SubCell)>();
        }

        public CPos TopLeft { get { return location; } }
        public WPos CenterPosition { get { return position; } }
        public (CPos, SubCell)[] OccupiedCells() { return occupied; }

        void INotifyAddedToWorld.AddedToWorld(Actor self)
        {
            self.World.AddToMaps(self, this);
        }

        void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
        {
            self.World.RemoveFromMaps(self, this);
        }
    }
}
