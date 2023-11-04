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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.YR.Traits
{
	public class GrantExternalConditionPowerExInfo : SupportPowerInfo
	{
		[FieldLoader.Require]
		[Desc("The condition to apply. Must be included in the target actor's ExternalConditions list.")]
		public readonly string Condition = null;

		[Desc("Duration of the condition (in ticks). Set to 0 for a permanent condition.")]
		public readonly int Duration = 0;

		[Desc("Cells - affects whole cells only")]
		public readonly int Range = 1;

		[Desc("Sound to instantly play at the targeted area.")]
		public readonly string OnFireSound = null;

		[SequenceReference]
		[Desc("Sequence to play for granting actor when activated.",
			"This requires the actor to have the WithSpriteBody trait or one of its derivatives.")]
		public readonly string Sequence = "active";

		[Desc("Cursor to display when there are no units to apply the condition in range.")]
		public readonly string BlockedCursor = "move-blocked";

		[Desc("Will this support power will cause low power?")]
		public readonly bool LowPower = false;

		[Desc("If cause low power, when will it end?")]
		public readonly int LowPowerDuration = 0;

		[Desc("Should this support power effect to all actors with external condition?")]
		public readonly bool EffectToAll = false;

		public override object Create(ActorInitializer init) { return new GrantExternalConditionPowerEx(init.Self, this); }
	}

	public class GrantExternalConditionPowerEx : SupportPower
	{
		private PowerManager powerMgr;
		readonly GrantExternalConditionPowerExInfo info;

		public GrantExternalConditionPowerEx(Actor self, GrantExternalConditionPowerExInfo info)
			: base(self, info)
		{
			this.info = info;
			powerMgr = self.Owner.PlayerActor.Trait<PowerManager>();
		}

		public override void SelectTarget(Actor self, string order, SupportPowerManager manager)
		{
			Game.Sound.PlayToPlayer(SoundType.World, manager.Self.Owner, Info.SelectTargetSound);
			self.World.OrderGenerator = new SelectConditionTarget(Self.World, order, manager, this);
		}

		public override void Activate(Actor self, Order order, SupportPowerManager manager)
		{
			base.Activate(self, order, manager);

			var wsb = self.TraitOrDefault<WithSpriteBody>();
			if (wsb != null && wsb.DefaultAnimation.HasSequence(info.Sequence))
				wsb.PlayCustomAnimation(self, info.Sequence);

			Game.Sound.Play(SoundType.World, info.OnFireSound, order.Target.CenterPosition);

			IEnumerable<Actor> actors = null;
			if (!info.EffectToAll)
			{
				actors = UnitsInRange(self.World.Map.CellContaining(order.Target.CenterPosition));
			}
			else
			{
				actors = self.World.Actors.Where(o => o.Owner == self.Owner);
			}
			foreach (var actor in actors)
			{
				var external = actor.TraitsImplementing<ExternalCondition>()
					.FirstOrDefault(t => t.Info.Condition == info.Condition && t.CanGrantCondition(self));

				if (external != null)
					external.GrantCondition(actor, self, info.Duration);
			}

			if (info.LowPower)
			{
				powerMgr.TriggerPowerOutage(info.LowPowerDuration);
			}
		}

		public IEnumerable<Actor> UnitsInRange(CPos xy)
		{
			var range = info.Range;
			var tiles = Self.World.Map.FindTilesInCircle(xy, range);
			var units = new List<Actor>();
			foreach (var t in tiles)
				units.AddRange(Self.World.ActorMap.GetActorsAt(t));

			return units.Distinct().Where(a =>
			{
				if (!a.Owner.IsAlliedWith(Self.Owner))
					return false;

				return a.TraitsImplementing<ExternalCondition>()
					.Any(t => t.Info.Condition == info.Condition && t.CanGrantCondition(Self));
			});
		}

		class SelectConditionTarget : IOrderGenerator
		{
			readonly GrantExternalConditionPowerEx power;
			readonly int range;
			readonly Sprite tile;
			readonly SupportPowerManager manager;
			readonly string order;
			readonly float validAlpha;

			public SelectConditionTarget(World world, string order, SupportPowerManager manager, GrantExternalConditionPowerEx power)
			{
				// Clear selection if using Left-Click Orders
				if (Game.Settings.Game.UseClassicMouseStyle)
					manager.Self.World.Selection.Clear();

				this.manager = manager;
				this.order = order;
				this.power = power;
				range = power.info.Range;
				var validSequence = world.Map.Rules.Sequences.GetSequence("overlay", "target-select");
				tile = validSequence.GetSprite(0);
				validAlpha = validSequence.GetAlpha(0);
			}

			public IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
			{
				world.CancelInputMode();
				if (mi.Button == MouseButton.Left && power.UnitsInRange(cell).Any())
					yield return new Order(order, manager.Self, Target.FromCell(world, cell), false) { SuppressVisualFeedback = true };
			}

			public void Tick(World world)
			{
				// Cancel the OG if we can't use the power
				if (!manager.Powers.ContainsKey(order))
					world.CancelInputMode();
			}

			public IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world)
			{
				return new List<IRenderable>();
			}

			public IEnumerable<IRenderable> Render(WorldRenderer wr, World world)
			{
				var xy = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
				var pal = wr.Palette(TileSet.TerrainPaletteInternalName);

				foreach (var t in world.Map.FindTilesInCircle(xy, range))
					yield return new SpriteRenderable(tile, wr.World.Map.CenterOfCell(t), WVec.Zero, -511, pal, 1f, 1f, float3.Ones, TintModifiers.IgnoreWorldTint, true);
			}

			public string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
			{
				return power.UnitsInRange(cell).Any() ? power.info.Cursor : power.info.BlockedCursor;
			}

			public void Deactivate()
			{
			}

			public bool HandleKeyPress(KeyInput e)
			{
				return true;
			}

			public IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world)
			{
				throw new System.NotImplementedException();
			}

			public void SelectionChanged(World world, IEnumerable<Actor> selected)
			{
				throw new System.NotImplementedException();
			}
		}
	}
}
