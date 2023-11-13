﻿#region Copyright & License Information
/*
 * Modded by Cook Green of YR Mod
 * Modded from Cloak.cs but some changed.
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
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.YR.Traits
{
    public enum UnvarietyType
    {
        None,
        Move,
        Attack,
        Repair,
        Damage,
        Heal,
        SelfHeal,
        Dock
    }

    /// <summary>
    /// Means this actor can vary itself into another actor, if the target actor is null, the actor will be just invisible
    /// </summary>
    public class VarietyInfo : ConditionalTraitInfo
    {
        public readonly UnvarietyType UnvarietyOn = UnvarietyType.Attack | UnvarietyType.Dock;
        [Desc("Target actor, if it is null, the actor will just be invisible")]
        public readonly string Actor = null;
        [PaletteReference("IsPlayerPalette")]
        public readonly string Palette = "cloak";
        public readonly bool IsPlayerPalette = false;
        public readonly HashSet<string> VarietyTypes = new HashSet<string> { "Variety" };
        public readonly string VarietyCondition = null;

        [Desc("Measured in game ticks.")]
        public readonly int InitialDelay = 10;

        [Desc("Measured in game ticks.")]
        public readonly int VarietyDelay = 30;

        public readonly string VarietySound = null;
        public readonly string UnvarietySound = null;
        public override object Create(ActorInitializer init)
        {
            return new Variety(init, this);
        }
    }

    public class Variety : ConditionalTrait<VarietyInfo>, IRenderModifier, INotifyAttack, ITick, INotifyDamage,
        IVisibilityModifier, INotifyCreated, INotifyHarvesterAction, INotifyCenterPositionChanged
    {
        [Sync]
        private int remainingTime;
        private readonly VarietyInfo info;
        private int variedToken = Actor.InvalidConditionToken;
        private Actor varietiedActor;
        private readonly Actor self;
        private CPos? lastPos; // Last position
        private bool wasVaried; // Vary last time
        private bool firstTick = true; // Run this trait firstly
        private bool isDocking = false;
        private Variety[] otherVaried;

        public bool Varied { get { return !IsTraitDisabled && remainingTime <= 0; } }

        public Variety(ActorInitializer init, VarietyInfo info)
            : base(info)
        {
            self = init.Self;
            this.info = info;
            remainingTime = info.InitialDelay;
        }

        protected override void Created(Actor self)
        {
            otherVaried = self.TraitsImplementing<Variety>()
                .Where(c => c != this)
                .ToArray();
            if (Varied)
            {
                wasVaried = true;
                if (self != null && variedToken == Actor.InvalidConditionToken && !string.IsNullOrEmpty(Info.VarietyCondition))
                    variedToken = self.GrantCondition(Info.VarietyCondition);
            }

            base.Created(self);
        }

        public IEnumerable<IRenderable> ModifyRender(Actor self, WorldRenderer wr, IEnumerable<IRenderable> rr)
        {
            IEnumerable<IPalettedRenderable> r = rr.OfType<IPalettedRenderable>();
            if (remainingTime > 0 || IsTraitDisabled)
            {
                if (varietiedActor != null && varietiedActor.IsInWorld)
                {
                    self.World.Remove(varietiedActor);
                }

                return r;
            }

            if (Varied && IsVisible(self, self.World.RenderPlayer))
            {
                var palette = string.IsNullOrEmpty(Info.Palette) ? null : Info.IsPlayerPalette ? wr.Palette(Info.Palette + self.Owner.InternalName) : wr.Palette(Info.Palette);
                if (palette == null)
                    return r;
                else
                    return r.Select(a => a.IsDecoration ? a : a.WithPalette(palette));
            }
            else
            {
                if (!string.IsNullOrEmpty(info.Actor))
                {
                    return createActorAndRender(self.World, info.Actor, wr);
                }
                else
                {
                    return SpriteRenderable.None;
                }
            }
        }

        public bool IsVisible(Actor self, Player viewer)
        {
            if (!Varied || self.Owner.IsAlliedWith(viewer))
                return true;

            // maybe can use DetectCloak, but we don't want submarines detect varietied units, so...
            return self.World.ActorsWithTrait<DetectVariety>().Any(a => !a.Trait.IsTraitDisabled && a.Actor.Owner.IsAlliedWith(viewer)
                && Info.VarietyTypes.Overlaps(a.Trait.Info.CloakTypes)
                && (self.CenterPosition - a.Actor.CenterPosition).LengthSquared <= a.Trait.Info.Range.LengthSquared);
        }

        private IEnumerable<IRenderable> createActorAndRender(World world, string actor, WorldRenderer wr)
        {
            TypeDictionary dic = new TypeDictionary
            {
                new CenterPositionInit(self.CenterPosition),
                new LocationInit(self.Location),
                new OwnerInit(self.Owner),
                new FacingInit(WAngle.FromFacing(128))
            };
            varietiedActor = world.CreateActor(info.Actor, dic);
            if (!varietiedActor.IsInWorld)
            {
                self.World.AddFrameEndTask((w =>
                {
                    world.Add(varietiedActor);
                }));
            }

            return varietiedActor.Render(wr);
        }

        public IEnumerable<Rectangle> ModifyScreenBounds(Actor self, WorldRenderer wr, IEnumerable<Rectangle> bounds)
        {
            return bounds;
        }

        public void Attacking(Actor self, in Target target, Armament a, Barrel barrel)
        {
            Unvariety();
        }

        public void PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel) { }

        public void Tick(Actor self)
        {
            if (!IsTraitDisabled)
            {
                if (remainingTime > 0 && !isDocking)
                    remainingTime--;

                if (Info.UnvarietyOn.HasFlag(UnvarietyType.Move) && (lastPos == null || lastPos.Value != self.Location))
                {
                    Unvariety();
                    lastPos = self.Location;
                }
            }

            var isVaried = Varied;
            if (isVaried && !wasVaried)
            {
                if (self != null && variedToken == Actor.InvalidConditionToken && !string.IsNullOrEmpty(Info.VarietyCondition))
                    variedToken = self.GrantCondition(Info.VarietyCondition);

                // Sounds shouldn't play if the actor starts cloaked
                if (!(firstTick && Info.InitialDelay == 0) && !otherVaried.Any(a => a.Varied))
                    Game.Sound.Play(SoundType.World, Info.VarietySound, self.CenterPosition);
            }
            else if (!isVaried && wasVaried)
            {
                if (variedToken != Actor.InvalidConditionToken)
                    variedToken = self.RevokeCondition(variedToken);

                if (!(firstTick && Info.InitialDelay == 0) && !otherVaried.Any(a => a.Varied))
                    Game.Sound.Play(SoundType.World, Info.UnvarietySound, self.CenterPosition);
            }

            wasVaried = isVaried;
            firstTick = false;
        }

        public void Damaged(Actor self, AttackInfo e)
        {
            if (e.Damage.Value == 0)
                return;

            var type = e.Damage.Value < 0
                ? (e.Attacker == self ? UnvarietyType.SelfHeal : UnvarietyType.Heal)
                : UnvarietyType.Damage;
            if (Info.UnvarietyOn.HasFlag(type))
                Unvariety();
        }

        public void Unvariety() { Unvariety(Info.VarietyDelay); }

        public void Unvariety(int time)
        {
            if (varietiedActor != null && varietiedActor.IsInWorld)
            {
                varietiedActor.Kill(self);
            }

            remainingTime = Math.Max(remainingTime, time);
        }

        public void MovingToResources(Actor self, CPos targetCell)
        {
        }

        public void MovingToRefinery(Actor self, Actor refineryActor)
        {
        }

        public void MovementCancelled(Actor self) { }

        public void Harvested(Actor self, string resource) { }

        public void Docked()
        {
            if (Info.UnvarietyOn.HasFlag(UnvarietyType.Dock))
            {
                isDocking = true;
                Unvariety();
            }
        }

        public void Undocked()
        {
            isDocking = false;
        }

        public void CenterPositionChanged(Actor self, byte oldLayer, byte newLayer)
        {
        }
    }
}
