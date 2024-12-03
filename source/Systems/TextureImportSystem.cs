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
        private readonly ComponentQuery<IsTextureRequest> textureRequestsQuery;
        private readonly ComponentQuery<IsTexture> texturesQuery;
        private readonly Dictionary<Entity, uint> textureVersions;
        private readonly List<Operation> operations;

        void ISystem.Start(in SystemContainer systemContainer, in World world)
        {
        }

        void ISystem.Update(in SystemContainer systemContainer, in World world, in TimeSpan delta)
        {
            Update(world);
        }

        void ISystem.Finish(in SystemContainer systemContainer, in World world)
        {
            if (systemContainer.World == world)
            {
                CleanUp();
            }
        }

        public TextureImportSystem()
        {
            textureRequestsQuery = new();
            texturesQuery = new();
            textureVersions = new();
            operations = new();
        }

        private void CleanUp()
        {
            while (operations.Count > 0)
            {
                Operation operation = operations.RemoveAt(0);
                operation.Dispose();
            }

            operations.Dispose();
            textureVersions.Dispose();
            texturesQuery.Dispose();
            textureRequestsQuery.Dispose();
        }

        private void Update(World world)
        {
            textureRequestsQuery.Update(world);
            foreach (var r in textureRequestsQuery)
            {
                IsTextureRequest request = r.Component1;
                bool sourceChanged = false;
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
                    if (TryLoadImageDataOntoEntity(texture))
                    {
                        textureVersions.AddOrSet(texture, request.version);
                    }
                }
            }

            PerformInstructions(world);
        }

        private void PerformInstructions(World world)
        {
            while (operations.Count > 0)
            {
                Operation operation = operations.RemoveAt(0);
                world.Perform(operation);
                operation.Dispose();
            }
        }

        /// <summary>
        /// Updates the entity with the latest pixel data using the <see cref="byte"/>
        /// collection on it.
        /// </summary>
        private bool TryLoadImageDataOntoEntity(Entity texture)
        {
            if (!texture.ContainsArray<BinaryData>())
            {
                return false;
            }

            //update pixels collection
            Trace.WriteLine($"Loading image data onto entity `{texture}`");
            USpan<byte> bytes = texture.GetArray<BinaryData>().As<byte>();
            using (Image<Rgba32> image = Image.Load<Rgba32>(bytes.AsSystemSpan()))
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
                if (texture.TryGetComponent(out IsTexture component))
                {
                    component.width = width;
                    component.height = height;
                    component.version++;
                    selectedEntity.SetComponent(component);
                }
                else
                {
                    selectedEntity.AddComponent(new IsTexture(width, height));
                }

                //put list
                if (!texture.ContainsArray<Pixel>())
                {
                    selectedEntity.CreateArray(pixels.AsSpan());
                }
                else
                {
                    selectedEntity.ResizeArray<Pixel>(pixels.Length);
                    selectedEntity.SetArrayElements(0, pixels.AsSpan());
                }

                operations.Add(operation);
                Trace.WriteLine($"Finished loading image data onto entity `{texture}`");
                return true;
            }
        }
    }
}
