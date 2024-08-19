using Data.Components;
using Simulation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Concurrent;
using System.Threading;
using Textures.Components;
using Textures.Events;
using Unmanaged.Collections;

namespace Textures.Systems
{
    public class TextureImportSystem : SystemBase
    {
        private readonly Query<IsTextureRequest> textureRequestsQuery;
        private readonly Query<IsTexture> texturesQuery;
        private readonly UnmanagedDictionary<eint, uint> textureVersions;
        private readonly ConcurrentQueue<Operation> operations;

        public TextureImportSystem(World world) : base(world)
        {
            textureRequestsQuery = new(world);
            texturesQuery = new(world);
            textureVersions = new();
            Subscribe<TextureUpdate>(Update);
            operations = new();
        }

        public override void Dispose()
        {
            while (operations.TryDequeue(out Operation operation))
            {
                operation.Dispose();
            }

            textureVersions.Dispose();
            texturesQuery.Dispose();
            textureRequestsQuery.Dispose();
            base.Dispose();
        }

        private void Update(TextureUpdate e)
        {
            textureRequestsQuery.Update();
            foreach (var r in textureRequestsQuery)
            {
                IsTextureRequest request = r.Component1;
                bool sourceChanged = false;
                eint textureEntity = r.entity;
                if (!textureVersions.ContainsKey(textureEntity))
                {
                    textureVersions.Add(textureEntity, default);
                    sourceChanged = true;
                }
                else
                {
                    sourceChanged = textureVersions[textureEntity] != request.version;
                }

                if (sourceChanged)
                {
                    textureVersions[textureEntity] = request.version;
                    //ThreadPool.QueueUserWorkItem(LoadImageDataOntoEntity, textureEntity, false);
                    LoadImageDataOntoEntity(textureEntity);
                }
            }

            PerformInstructions();
        }

        private void PerformInstructions()
        {
            while (operations.TryDequeue(out Operation operation))
            {
                world.Perform(operation);
                operation.Dispose();
            }
        }

        /// <summary>
        /// Updates the entity with the latest pixel data using the <see cref="byte"/>
        /// collection on it.
        /// </summary>
        private void LoadImageDataOntoEntity(eint entity)
        {
            while (!world.ContainsList<byte>(entity))
            {
                Console.WriteLine("hanging textures");
                Thread.Sleep(1);
            }

            //update pixels collection
            Console.WriteLine($"Loading image data onto entity `{entity}`");
            UnmanagedList<byte> bytes = world.GetList<byte>(entity);
            using (Image<Rgba32> image = Image.Load<Rgba32>(bytes.AsSpan()))
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
                operation.SelectEntity(entity);
                if (world.TryGetComponent(entity, out IsTexture component))
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
                if (!world.ContainsList<Pixel>(entity))
                {
                    operation.CreateList<Pixel>();
                }
                else
                {
                    operation.ClearList<Pixel>();
                }

                operation.AppendToList<Pixel>(pixels.AsSpan());
                operations.Enqueue(operation);
                Console.WriteLine($"Finished loading image data onto entity `{entity}`");
            }
        }
    }
}
