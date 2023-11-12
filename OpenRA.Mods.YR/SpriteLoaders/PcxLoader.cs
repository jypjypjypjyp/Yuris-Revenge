using System.IO;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.YR.SpriteLoaders
{
    public class PcxLoader : ISpriteLoader
    {
        class PcxFrame : ISpriteFrame
        {
            public Size Size { get; set; }
            public Size FrameSize { get; set; }
            public float2 Offset { get; set; }
            public byte[] Data { get; set; }
            public bool DisableExportPadding { get { return false; } }

            public SpriteFrameType Type
            {
                get
                {
                    return SpriteFrameType.Bgra32;
                }
            }
        }

        public bool TryParseSprite(Stream s, string filename, out ISpriteFrame[] frames, out TypeDictionary metadata)
        {
            frames = new ISpriteFrame[1];
            metadata = new TypeDictionary();

            return true;
        }
    }
}
