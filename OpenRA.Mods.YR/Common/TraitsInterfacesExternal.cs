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

using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
    [RequireExplicitImplementation]
    public interface IResourceLogicLayer
    {
        void UpdatePosition(CPos cell, string type, int density);
    }

    [RequireExplicitImplementation]
    public interface IRefineryResourceDelivered
    {
        void ResourceDelivered(Actor self, int amount);
    }

    [RequireExplicitImplementation]
    public interface IRemoveInfector
    {
        void RemoveInfector(Actor self, bool kill, AttackInfo e = null);
    }

    [RequireExplicitImplementation]
    public interface IPointDefense
    {
        bool Destroy(WPos position, Player attacker, string type);
    }
}
