using System.Linq;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.YR.Traits
{
    public class ExploreMapSupportPowerInfo : SupportPowerInfo, IRulesetLoaded
    {
        [Desc("Radius of the explore map support power")]
        public readonly int Radius = 6;

        [Desc("Image used by playing the sequence")]
        public readonly string Image = null;

        [Desc("Sequence played when explore specific destination")]
        public readonly string Sequence = null;

        [Desc("Platte which applied to the sequence")]
        [PaletteReference]
        public readonly string Platte = null;

        public override object Create(ActorInitializer init)
        {
            return new ExploreMapSupportPower(init.Self, this);
        }
    }

    public class ExploreMapSupportPower : SupportPower
    {
        private ExploreMapSupportPowerInfo info;
        private Shroud.SourceType type = Shroud.SourceType.Visibility;
        public ExploreMapSupportPower(Actor self, ExploreMapSupportPowerInfo info)
            : base(self, info)
        {
            this.info = info;
        }

        public override void Activate(Actor self, Order order, SupportPowerManager manager)
        {
            base.Activate(self, order, manager);

            self.World.AddFrameEndTask(w =>
            {
                Shroud shroud = self.Owner.Shroud;
                WPos destPosition = order.Target.CenterPosition;
                var cells = Shroud.ProjectedCellsInRange(self.World.Map, self.World.Map.CellContaining(destPosition), WDist.FromCells(info.Radius));
                try
                {
                    shroud.AddSource(this, type, cells.ToArray());
                }
                catch
                {
                    shroud.RemoveSource(this);
                    shroud.AddSource(this, type, cells.ToArray());
                }

                shroud.ExploreProjectedCells(cells);

                if (!string.IsNullOrEmpty(info.Sequence))
                {
                    string palette = null;
                    if (info.Platte == "player")
                    {
                        palette = "player" + self.Owner.InternalName;
                    }
                    else
                    {
                        palette = info.Platte;
                    }

                    self.World.Add(new SpriteEffect(destPosition, self.World, info.Image, info.Sequence, palette));
                }
            });
        }
    }
}
