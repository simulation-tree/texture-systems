using Collections.Generic;
using System;

namespace Textures.Systems
{
    public readonly struct CompiledImage : IDisposable
    {
        public readonly int width;
        public readonly int height;
        public readonly int length;
        private readonly Array<Pixel> pixels;

        public readonly Span<Pixel> Pixels => pixels.AsSpan();

        public readonly ref Pixel this[int index] => ref pixels[index];

        public CompiledImage(int width, int height)
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

        public readonly ref Pixel GetAt(int x, int y)
        {
            return ref pixels[(y * width) + x];
        }
    }
}