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
            ComponentQuery<IsTextureRequest> requestQuery = new(world);
            foreach (var r in requestQuery)
            {
                ref IsTextureRequest request = ref r.component1;
                Entity texture = new(world, r.entity);
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
                        world.SetComponent(r.entity, dataRequest.BecomeLoaded());
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
        private readonly bool TryLoadTexture(Entity texture, IsTextureRequest request, Simulator simulator)
        {
            HandleDataRequest message = new(texture, request.address);
            if (simulator.TryHandleMessage(ref message))
            {
                if (message.loaded)
                {
                    //update pixels collection
                    Trace.WriteLine($"Loading image data onto entity `{texture}`");
                    Schema schema = texture.world.Schema;
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
                        Operation.SelectedEntity selectedEntity = operation.SelectEntity(texture);

                        if (texture.TryGetComponent(out IsTexture component))
                        {
                            selectedEntity.SetComponent(component.IncrementVersion(), schema);
                        }
                        else
                        {
                            selectedEntity.AddComponent(new IsTexture(0, width, height), schema);
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
