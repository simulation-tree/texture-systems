using Collections;
using Data.Components;
using Simulation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Diagnostics;
using Textures.Components;
using Unmanaged;
using Worlds;

namespace Textures.Systems
{
    public readonly partial struct TextureImportSystem : ISystem
    {
        private readonly Dictionary<Entity, uint> textureVersions;
        private readonly Stack<Operation> operations;

        private TextureImportSystem(Dictionary<Entity, uint> textureVersions, Stack<Operation> operations)
        {
            this.textureVersions = textureVersions;
            this.operations = operations;
        }

        void ISystem.Start(in SystemContainer systemContainer, in World world)
        {
            if (systemContainer.World == world)
            {
                Dictionary<Entity, uint> textureVersions = new();
                Stack<Operation> operations = new();
                systemContainer.Write(new TextureImportSystem(textureVersions, operations));
            }
        }

        void ISystem.Update(in SystemContainer systemContainer, in World world, in TimeSpan delta)
        {
            LoadDataOntoEntities(world);
            PerformInstructions(world);
        }

        void ISystem.Finish(in SystemContainer systemContainer, in World world)
        {
            if (systemContainer.World == world)
            {
                while (operations.TryPop(out Operation operation))
                {
                    operation.Dispose();
                }

                operations.Dispose();
                textureVersions.Dispose();
            }
        }

        private readonly void LoadDataOntoEntities(World world)
        {
            ComponentQuery<IsTextureRequest> requestQuery = new(world);
            foreach (var r in requestQuery)
            {
                ref IsTextureRequest request = ref r.component1;
                bool sourceChanged;
                Entity texture = new(world, r.entity);
                if (!textureVersions.ContainsKey(texture))
                {
                    sourceChanged = true;
                }
                else
                {
                    sourceChanged = textureVersions[texture] != request.version;
                }

                if (sourceChanged)
                {
                    if (TryLoad(texture))
                    {
                        textureVersions.AddOrSet(texture, request.version);
                    }
                }
            }
        }

        private readonly void PerformInstructions(World world)
        {
            while (operations.TryPop(out Operation operation))
            {
                world.Perform(operation);
                operation.Dispose();
            }
        }

        /// <summary>
        /// Updates the entity with the latest pixel data using the <see cref="byte"/>
        /// collection on it.
        /// </summary>
        private readonly bool TryLoad(Entity texture)
        {
            if (!texture.ContainsArray<BinaryData>())
            {
                return false;
            }

            //update pixels collection
            Trace.WriteLine($"Loading image data onto entity `{texture}`");
            USpan<byte> bytes = texture.GetArray<BinaryData>().As<byte>();
            Schema schema = texture.GetWorld().Schema;
            using (Image<Rgba32> image = Image.Load<Rgba32>(bytes))
            {
                uint width = (uint)image.Width;
                uint height = (uint)image.Height;
                uint pixelCount = width * height;
                using Array<Pixel> pixels = new(pixelCount);
                for (uint p = 0; p < pixelCount; p++)
                {
                    uint x = p % width;
                    uint y = p / width;
                    Rgba32 pixel = image[(int)x, (int)(height - y - 1)]; //flip y
                    pixels[p] = new Pixel(pixel.R, pixel.G, pixel.B, pixel.A);
                }

                //update texture size data
                Operation operation = new();
                Operation.SelectedEntity selectedEntity = operation.SelectEntity(texture);
                ref IsTexture component = ref texture.TryGetComponent<IsTexture>(out bool contains);
                if (contains)
                {
                    selectedEntity.SetComponent(new IsTexture(width, height, component.version + 1), schema);
                }
                else
                {
                    selectedEntity.AddComponent(new IsTexture(width, height), schema);
                }

                //put list
                if (!texture.ContainsArray<Pixel>())
                {
                    selectedEntity.CreateArray(pixels.AsSpan(), schema);
                }
                else
                {
                    selectedEntity.ResizeArray<Pixel>(pixels.Length, schema);
                    selectedEntity.SetArrayElements(0, pixels.AsSpan(), schema);
                }

                operations.Push(operation);
                Trace.WriteLine($"Finished loading image data onto entity `{texture}`");
                return true;
            }
        }
    }
}
