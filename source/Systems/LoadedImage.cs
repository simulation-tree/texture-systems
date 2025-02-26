using Collections.Generic;
using System;
using Unmanaged;

namespace Textures.Systems
{
    public readonly struct LoadedImage : IDisposable
    {
        public readonly uint width;
        public readonly uint height;
        public readonly uint length;
        private readonly Array<Pixel> pixels;

        public readonly USpan<Pixel> Pixels => pixels.AsSpan();

        public readonly ref Pixel this[uint index] => ref pixels[index];

        public LoadedImage(uint width, uint height)
        {
            this.width = width;
            this.height = height;
            length = width * height;
            pixels = new(length);
        }

        public readonly void Dispose()
        {
            pixels.Dispose();
        }

        public readonly ref Pixel GetAt(uint x, uint y)
        {
            return ref pixels[(y * width) + x];
        }
    }
}