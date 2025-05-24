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
    public partial class TextureImportSystem : ISystem, IDisposable
    {
        private readonly Operation operation;
        private readonly Dictionary<long, LoadedImage> images;
        private readonly int requestType;
        private readonly int textureType;

        public TextureImportSystem(Simulator simulator)
        {
            operation = new();
            images = new();

            Schema schema = simulator.world.Schema;
            requestType = schema.GetComponentType<IsTextureRequest>();
            textureType = schema.GetComponentType<IsTexture>();
        }

        public void Dispose()
        {
            operation.Dispose();

            foreach (LoadedImage loadedImage in images.Values)
            {
                loadedImage.Dispose();
            }

            images.Dispose();
        }

        void ISystem.Update(Simulator simulator, double deltaTime)
        {
            LoadDataOntoEntities(simulator, deltaTime);
            if (operation.Count > 0)
            {
                operation.Perform(simulator.world);
                operation.Reset();
            }
        }

        private void LoadDataOntoEntities(Simulator simulator, double delta)
        {
            World world = simulator.world;
            foreach (Chunk chunk in world.Chunks)
            {
                if (chunk.Definition.ContainsComponent(requestType))
                {
                    ReadOnlySpan<uint> entities = chunk.Entities;
                    ComponentEnumerator<IsTextureRequest> components = chunk.GetComponents<IsTextureRequest>(requestType);
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
                            if (TryLoadTexture(texture, request, simulator))
                            {
                                Trace.WriteLine($"Texture `{texture}` has been loaded");
                                request.status = IsTextureRequest.Status.Loaded;
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

        /// <summary>
        /// Updates the entity with the latest pixel data using the <see cref="byte"/>
        /// collection on it.
        /// </summary>
        private bool TryLoadTexture(Entity texture, IsTextureRequest request, Simulator simulator)
        {
            //todo: implement loading cubemaps

            long requestHash = request.address.GetLongHashCode();
            if (!images.TryGetValue(requestHash, out LoadedImage loadedImage))
            {
                LoadData message = new(texture.world, request.address);
                simulator.Broadcast(ref message);
                if (message.TryConsume(out ByteReader data))
                {
                    //update pixels collection
                    using (Image<Rgba32> image = Image.Load<Rgba32>(data.GetBytes()))
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

                    data.Dispose();
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

            Trace.WriteLine($"Loading image data from `{request.address}` onto entity `{texture}`");

            //update texture size data
            operation.SetSelectedEntity(texture);
            texture.TryGetComponent(textureType, out IsTexture component);
            component.width = loadedImage.width;
            component.height = loadedImage.height;
            component.version++;
            operation.AddOrSetComponent(component);
            operation.CreateOrSetArray(loadedImage.Pixels);
            return true;
        }
    }
}