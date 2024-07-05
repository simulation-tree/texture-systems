using Simulation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Textures.Components;
using Textures.Events;
using Unmanaged.Collections;

namespace Textures.Systems
{
    public class TextureImportSystem : SystemBase
    {
        private readonly Query<IsTexture> textureQuery;

        public TextureImportSystem(World world) : base(world)
        {
            textureQuery = new(world);
            Subscribe<TextureUpdate>(Update);
        }

        public override void Dispose()
        {
            textureQuery.Dispose();
            base.Dispose();
        }

        private void Update(TextureUpdate e)
        {
            textureQuery.Fill();
            foreach (Query<IsTexture>.Result result in textureQuery)
            {
                ref IsTexture texture = ref result.Component1;
                if (texture.changed)
                {
                    texture.changed = false;
                    Update(result.entity);
                }
            }
        }

        /// <summary>
        /// Updates the entity with the latest pixel data using the <see cref="byte"/>
        /// collection on it.
        /// </summary>
        private void Update(EntityID entity)
        {
            if (!world.ContainsCollection<Pixel>(entity))
            {
                world.CreateCollection<Pixel>(entity);
            }

            UnmanagedList<Pixel> pixels = world.GetCollection<Pixel>(entity);
            pixels.Clear();

            //update pixels collection
            UnmanagedList<byte> bytes = world.GetCollection<byte>(entity);
            using (Image<Rgba32> image = Image.Load<Rgba32>(bytes.AsSpan()))
            {
                uint width = (uint)image.Width;
                uint height = (uint)image.Height;
                uint pixelCount = width * height;
                pixels.AddDefault(pixelCount);
                for (uint p = 0; p < pixelCount; p++)
                {
                    uint x = p % width;
                    uint y = p / width;
                    Rgba32 pixel = image[(int)x, (int)(height - y - 1)];
                    pixels[p] = new Pixel(pixel.R, pixel.G, pixel.B, pixel.A);
                }

                //update texture size data
                if (!world.ContainsComponent<TextureSize>(entity))
                {
                    world.AddComponent(entity, new TextureSize(width, height));
                }
                else
                {
                    world.SetComponent(entity, new TextureSize(width, height));
                }
            }
        }
    }
}
