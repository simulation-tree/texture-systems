using Collections;
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

        private TextureImportSystem(Stack<Operation> operations)
        {
            this.operations = operations;
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
            }
        }

        private readonly void LoadDataOntoEntities(World world, Simulator simulator, TimeSpan delta)
        {
            ComponentType componentType = world.Schema.GetComponent<IsTextureRequest>();
            foreach (Chunk chunk in world.Chunks)
            {
                if (chunk.Definition.Contains(componentType))
                {
                    USpan<uint> entities = chunk.Entities;
                    USpan<IsTextureRequest> components = chunk.GetComponents<IsTextureRequest>(componentType);
                    for (uint i = 0; i < entities.Length; i++)
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
            HandleDataRequest message = new(texture, request.address);
            if (simulator.TryHandleMessage(ref message))
            {
                if (message.loaded)
                {
                    //update pixels collection
                    Trace.WriteLine($"Loading image data onto entity `{texture}`");
                    USpan<byte> binaryData = message.Bytes;
                    using (Image<Rgba32> image = Image.Load<Rgba32>(binaryData))
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
                        operation.SelectEntity(texture);

                        texture.TryGetComponent(out IsTexture component);
                        operation.AddOrSetComponent(new IsTexture(component.version + 1, width, height));

                        //put list
                        if (!texture.ContainsArray<Pixel>())
                        {
                            operation.CreateArray(pixels.AsSpan());
                        }
                        else
                        {
                            operation.ResizeArray<Pixel>(pixels.Length);
                            operation.SetArrayElements(0, pixels.AsSpan());
                        }

                        operations.Push(operation);
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
    }
}
