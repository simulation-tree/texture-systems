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
    public partial class TextureImportSystem : SystemBase, IListener<DataUpdate>
    {
        private readonly World world;
        private readonly Operation operation;
        private readonly Dictionary<long, CompiledImage> images;
        private readonly int requestType;
        private readonly int textureType;
        private readonly int pixelArrayType;

        public TextureImportSystem(Simulator simulator, World world) : base(simulator)
        {
            this.world = world;
            operation = new(world);
            images = new();

            Schema schema = world.Schema;
            requestType = schema.GetComponentType<IsTextureRequest>();
            textureType = schema.GetComponentType<IsTexture>();
            pixelArrayType = schema.GetArrayType<Pixel>();
        }

        public override void Dispose()
        {
            operation.Dispose();

            foreach (CompiledImage loadedImage in images.Values)
            {
                loadedImage.Dispose();
            }

            images.Dispose();
        }

        void IListener<DataUpdate>.Receive(ref DataUpdate message)
        {
            LoadDataOntoEntities(message.deltaTime);
            if (operation.TryPerform())
            {
                operation.Reset();
            }
        }

        private void LoadDataOntoEntities(double deltaTime)
        {
            ReadOnlySpan<Chunk> chunks = world.Chunks;
            for (int c = 0; c < chunks.Length; c++)
            {
                Chunk chunk = chunks[c];
                if (chunk.ComponentTypes.Contains(requestType))
                {
                    ReadOnlySpan<uint> entities = chunk.Entities;
                    ComponentEnumerator<IsTextureRequest> components = chunk.GetComponents<IsTextureRequest>(requestType);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        ref IsTextureRequest request = ref components[i];
                        uint texture = entities[i];
                        if (request.status == IsTextureRequest.Status.Submitted)
                        {
                            request.status = IsTextureRequest.Status.Loading;
                            Trace.WriteLine($"Started searching data for texture `{texture}` with address `{request.address}`");
                        }

                        if (request.status == IsTextureRequest.Status.Loading)
                        {
                            if (TryLoadTexture(texture, request))
                            {
                                Trace.WriteLine($"Texture `{texture}` has been loaded");
                                request.status = IsTextureRequest.Status.Loaded;
                            }
                            else
                            {
                                request.duration += deltaTime;
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
        private bool TryLoadTexture(uint textureEntity, IsTextureRequest request)
        {
            //todo: implement loading cubemaps

            long requestHash = request.address.GetLongHashCode();
            ref CompiledImage compiledImage = ref images.TryGetValue(requestHash, out bool contains);
            if (!contains)
            {
                LoadData message = new(request.address);
                simulator.Broadcast(ref message);
                if (message.TryConsume(out ByteReader data))
                {
                    //update pixels collection
                    using (Image<Rgba32> image = Image.Load<Rgba32>(data.GetBytes()))
                    {
                        int width = image.Width;
                        int height = image.Height;
                        compiledImage = ref images.Add(requestHash);
                        compiledImage = new(width, height);
                        Span<Pixel> pixels = compiledImage.Pixels;
                        if ((request.flags & IsTextureRequest.Flags.FlipY) != 0)
                        {
                            for (int p = 0; p < compiledImage.length; p++)
                            {
                                Texture.GetPosition(p, width, out int x, out int y);
                                Rgba32 pixel = image[x, height - y - 1]; //flip y
                                pixels[p] = new Pixel(pixel.R, pixel.G, pixel.B, pixel.A);
                            }
                        }
                        else
                        {
                            for (int p = 0; p < compiledImage.length; p++)
                            {
                                Texture.GetPosition(p, width, out int x, out int y);
                                Rgba32 pixel = image[x, y];
                                pixels[p] = new Pixel(pixel.R, pixel.G, pixel.B, pixel.A);
                            }
                        }
                    }

                    data.Dispose();
                }
                else
                {
                    Trace.TraceError($"Texture `{textureEntity}` could not be loaded");
                    return false;
                }
            }

            Trace.WriteLine($"Loading image data from `{request.address}` onto entity `{textureEntity}`");

            //update texture size data
            operation.SetSelectedEntity(textureEntity);
            world.TryGetComponent(textureEntity, textureType, out IsTexture texture);
            texture.width = compiledImage.width;
            texture.height = compiledImage.height;
            texture.version++;
            operation.AddOrSetComponent(texture, textureType);
            operation.CreateOrSetArray(compiledImage.Pixels, pixelArrayType);
            return true;
        }
    }
}