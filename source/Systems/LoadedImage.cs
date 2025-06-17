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
    }
}