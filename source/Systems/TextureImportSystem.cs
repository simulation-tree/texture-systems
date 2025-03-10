using Collections.Generic;
using Data.Messages;
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
        private readonly Stack<Operation> operations;
        private readonly Dictionary<long, LoadedImage> images;

        private TextureImportSystem(Stack<Operation> operations)
        {
            this.operations = operations;
            images = new();
        }

        void ISystem.Start(in SystemContainer systemContainer, in World world)
        {
            if (systemContainer.World == world)
            {
                systemContainer.Write(new TextureImportSystem(new()));
            }
        }

        void ISystem.Update(in SystemContainer systemContainer, in World world, in TimeSpan delta)
        {
            LoadDataOntoEntities(world, systemContainer.simulator, delta);
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

                foreach (LoadedImage image in images.Values)
                {
                    image.Dispose();
                }

                images.Dispose();
            }
        }

        private readonly void LoadDataOntoEntities(World world, Simulator simulator, TimeSpan delta)
        {
            ComponentType componentType = world.Schema.GetComponentType<IsTextureRequest>();
            foreach (Chunk chunk in world.Chunks)
            {
                if (chunk.Definition.ContainsComponent(componentType))
                {
                    ReadOnlySpan<uint> entities = chunk.Entities;
                    Span<IsTextureRequest> components = chunk.GetComponents<IsTextureRequest>(componentType);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        ref IsTextureRequest request = ref components[i];
                        Entity texture = new(world, entities[i]);
                        if (request.status == IsTextureRequest.Status.Submitted)
                        {
                            request.status = IsTextureRequest.Status.Loading;
                            Trace.WriteLine($"Started searching data for texture `{texture}` with address `{request.address}`");
                        }

                        if (request.status == IsTextureRequest.Status.Loading)
                        {
                            IsTextureRequest dataRequest = request;
                            if (TryLoadTexture(texture, dataRequest, simulator))
                            {
                                Trace.WriteLine($"Texture `{texture}` has been loaded");

                                //todo: being done this way because reference to the request may have shifted
                                texture.SetComponent(dataRequest.BecomeLoaded());
                            }
                            else
                            {
                                request.duration += delta;
                                if (request.duration >= request.timeout)
                                {
                                    Trace.TraceError($"Texture `{texture}` could not be loaded");
                                    request.status = IsTextureRequest.Status.NotFound;
                                }
                            }
                        }
                    }
                }
            }
        }

        private readonly void PerformInstructions(World world)
        {
            while (operations.TryPop(out Operation operation))
            {
                operation.Perform(world);
                operation.Dispose();
            }
        }

        /// <summary>
        /// Updates the entity with the latest pixel data using the <see cref="byte"/>
        /// collection on it.
        /// </summary>
        private readonly bool TryLoadTexture(Entity texture, IsTextureRequest request, Simulator simulator)
        {
            //todo: implement loading cubemaps

            long requestHash = request.address.GetLongHashCode();
            if (!images.TryGetValue(requestHash, out LoadedImage loadedImage))
            {
                LoadData message = new(texture.world, request.address);
                if (simulator.TryHandleMessage(ref message) != default)
                {
                    if (message.TryGetBytes(out ReadOnlySpan<byte> data))
                    {
                        //update pixels collection
                        using (Image<Rgba32> image = Image.Load<Rgba32>(data))
                        {
                            int width = image.Width;
                            int height = image.Height;
                            loadedImage = new(width, height);
                            for (int p = 0; p < loadedImage.length; p++)
                            {
                                int x = p % width;
                                int y = p / width;
                                Rgba32 pixel = image[x, height - y - 1]; //flip y
                                loadedImage[p] = new Pixel(pixel.R, pixel.G, pixel.B, pixel.A);
                            }
                        }

                        message.Dispose();
                        images.Add(requestHash, loadedImage);
                    }
                    else
                    {
                        Trace.TraceError($"Texture `{texture}` could not be loaded");
                        return false;
                    }
                }
                else
                {
                    Trace.TraceError($"Texture `{texture}` could not be loaded, no message handlers");
                    return false;
                }
            }

            Trace.WriteLine($"Loading image data from `{request.address}` onto entity `{texture}`");

            //update texture size data
            Operation operation = new();
            operation.SelectEntity(texture);
            texture.TryGetComponent(out IsTexture component);
            operation.AddOrSetComponent(component.IncrementVersion(loadedImage.width, loadedImage.height));
            operation.CreateOrSetArray(loadedImage.Pixels);
            operations.Push(operation);
            return true;
        }
    }
}