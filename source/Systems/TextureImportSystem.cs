using Simulation;
using Simulation.Functions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Textures.Components;
using Unmanaged;
using Unmanaged.Collections;

namespace Textures.Systems
{
    public readonly struct TextureImportSystem : ISystem
    {
        private readonly ComponentQuery<IsTextureRequest> textureRequestsQuery;
        private readonly ComponentQuery<IsTexture> texturesQuery;
        private readonly UnmanagedDictionary<Entity, uint> textureVersions;
        private readonly UnmanagedList<Operation> operations;

        readonly unsafe InitializeFunction ISystem.Initialize => new(&Initialize);
        readonly unsafe IterateFunction ISystem.Update => new(&Update);
        readonly unsafe FinalizeFunction ISystem.Finalize => new(&Finalize);

        [UnmanagedCallersOnly]
        private static void Initialize(SystemContainer container, World world)
        {
        }

        [UnmanagedCallersOnly]
        private static void Update(SystemContainer container, World world, TimeSpan delta)
        {
            ref TextureImportSystem system = ref container.Read<TextureImportSystem>();
            system.Update(world);
        }

        [UnmanagedCallersOnly]
        private static void Finalize(SystemContainer container, World world)
        {
            if (container.World == world)
            {
                ref TextureImportSystem system = ref container.Read<TextureImportSystem>();
                system.Dispose();
            }
        }

        public TextureImportSystem()
        {
            textureRequestsQuery = new();
            texturesQuery = new();
            textureVersions = new();
            operations = new();
        }

        private void Dispose()
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
            World world = texture.GetWorld();
            if (!texture.ContainsArray<byte>())
            {
                return false;
            }

            //update pixels collection
            Debug.WriteLine($"Loading image data onto entity `{texture}`");
            USpan<byte> bytes = texture.GetArray<byte>();
            using (Image<Rgba32> image = Image.Load<Rgba32>(bytes.AsSystemSpan()))
            {
                uint width = (uint)image.Width;
                uint height = (uint)image.Height;
                uint pixelCount = width * height;
                using UnmanagedArray<Pixel> pixels = new(pixelCount);
                for (uint p = 0; p < pixelCount; p++)
                {
                    uint x = p % width;
                    uint y = p / width;
                    Rgba32 pixel = image[(int)x, (int)(height - y - 1)]; //flip y
                    pixels[p] = new Pixel(pixel.R, pixel.G, pixel.B, pixel.A);
                }

                //update texture size data
                Operation operation = new();
                operation.SelectEntity(texture);
                if (texture.TryGetComponent(out IsTexture component))
                {
                    component.width = width;
                    component.height = height;
                    component.version++;
                    operation.SetComponent(component);
                }
                else
                {
                    operation.AddComponent(new IsTexture(width, height));
                }

                //put list
                if (!texture.ContainsArray<Pixel>())
                {
                    operation.CreateArray<Pixel>(pixels.AsSpan());
                }
                else
                {
                    operation.ResizeArray<Pixel>(pixels.Length);
                    operation.SetArrayElements(0, pixels.AsSpan());
                }

                operations.Add(operation);
                Debug.WriteLine($"Finished loading image data onto entity `{texture}`");
                return true;
            }
        }
    }
}
