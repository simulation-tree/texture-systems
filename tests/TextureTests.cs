using Data.Systems;
using Simulation;
using System;
using System.Numerics;
using Textures.Systems;
using Unmanaged;
using Unmanaged.Collections;

namespace Textures.Tests
{
    public class TextureTests
    {
        [TearDown]
        public void CleanUp()
        {
            Allocations.ThrowIfAny();
        }

        [Test]
        public void CreateEmptyTexture()
        {
            using World world = new();
            Span<Pixel> pixels = stackalloc Pixel[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Pixel(byte.MaxValue, 0, 0, byte.MaxValue);
            }

            Texture emptyTexture = new(world, 4, 4, pixels);
            Assert.That(emptyTexture.GetWidth(), Is.EqualTo(4));
            Assert.That(emptyTexture.GetHeight(), Is.EqualTo(4));
            Pixel[] pixelsArray = emptyTexture.GetPixels().ToArray();
            Assert.That(pixelsArray.Length, Is.EqualTo(4 * 4));
            foreach (Pixel pixel in pixelsArray)
            {
                Assert.That(pixel.r, Is.EqualTo(byte.MaxValue));
                Assert.That(pixel.g, Is.EqualTo(0));
                Assert.That(pixel.b, Is.EqualTo(0));
                Assert.That(pixel.a, Is.EqualTo(byte.MaxValue));
            }
        }

        [Test]
        public void ImportTexture()
        {
            byte[] texturePngData = [
                137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82,0,0,0,16,0,0,0,9,8,6,0,0,0,59,
                42,172,50,0,0,0,1,115,82,71,66,0,174,206,28,233,0,0,0,4,103,65,77,65,0,0,177,
                143,11,252,97,5,0,0,0,9,112,72,89,115,0,0,14,195,0,0,14,195,1,199,111,168,100,
                0,0,0,87,73,68,65,84,40,83,149,144,81,10,0,32,8,67,247,211,253,239,218,5,10,
                173,68,43,211,6,131,106,243,17,2,173,180,229,180,0,49,60,0,148,15,105,0,223,3,
                192,11,34,217,14,9,1,36,13,16,85,176,83,0,155,205,42,1,130,225,181,2,62,143,
                129,123,245,254,106,118,72,185,87,123,203,2,230,183,127,69,128,14,227,84,232,
                23,9,124,171,212,0,0,0,0,73,69,78,68,174,66,96,130
            ];

            using World world = new();
            using DataImportSystem dataImports = new(world);
            using TextureImportSystem textureImports = new(world);

            Data.DataSource testTextureFile = new(world, "testTexture", texturePngData);
            using Texture texture = new(world, "testTexture");
            Assert.That(texture.GetWidth(), Is.EqualTo(16));
            Assert.That(texture.GetHeight(), Is.EqualTo(9));
            Pixel[] pixels = texture.GetPixels().ToArray();
            Assert.That(pixels.Length, Is.EqualTo(16 * 9));

            float hueThreshold = 3f; //compression

            //bottom left is yellow
            Assert.That(Hue(texture.Evaluate(0f, 0f)), Is.EqualTo(0.25f).Within(hueThreshold));

            //bottom right is blue
            Assert.That(Hue(texture.Evaluate(1f, 0f)), Is.EqualTo(0.6666f).Within(hueThreshold));

            //top left is green
            Assert.That(Hue(texture.Evaluate(0f, 1f)), Is.EqualTo(0.3333f).Within(hueThreshold));

            //top right is red
            Assert.That(Hue(texture.Evaluate(1f, 1f)), Is.EqualTo(0f).Within(hueThreshold));

            //center is cyan
            Assert.That(Hue(texture.Evaluate(0.5f, 0.5f)), Is.EqualTo(0.5f).Within(hueThreshold));

            float Hue(Vector4 color)
            {
                float r = color.X;
                float g = color.Y;
                float b = color.Z;
                float max = Math.Max(r, Math.Max(g, b));
                float min = Math.Min(r, Math.Min(g, b));
                float delta = max - min;
                float hue = 0f;
                if (delta != 0f)
                {
                    if (max == r)
                    {
                        hue = (g - b) / delta;
                    }
                    else if (max == g)
                    {
                        hue = 2f + (b - r) / delta;
                    }
                    else
                    {
                        hue = 4f + (r - g) / delta;
                    }
                }

                hue /= 6f;
                if (hue < 0f)
                {
                    hue += 1f;
                }

                return hue;
            }
        }

        [Test]
        public void CreateAtlasTextureFromSprites()
        {
            using World world = new();
            using UnmanagedList<AtlasTexture.InputSprite> sprites = UnmanagedList<AtlasTexture.InputSprite>.Create();
            AtlasTexture.InputSprite a = new("r", 32, 32);
            for (int i = 0; i < a.Pixels.Length; i++)
            {
                a.Pixels[i] = new(byte.MaxValue, 0, 0, 0);
            }

            AtlasTexture.InputSprite b = new("g", 32, 32);
            for (int i = 0; i < b.Pixels.Length; i++)
            {
                b.Pixels[i] = new(0, byte.MaxValue, 0, 0);
            }

            AtlasTexture.InputSprite c = new("b", 32, 32);
            for (int i = 0; i < c.Pixels.Length; i++)
            {
                c.Pixels[i] = new(0, 0, byte.MaxValue, 0);
            }

            AtlasTexture.InputSprite d = new("y", 32, 32);
            for (int i = 0; i < d.Pixels.Length; i++)
            {
                d.Pixels[i] = new(byte.MaxValue, byte.MaxValue, 0, 0);
            }

            sprites.Add(a);
            sprites.Add(b);
            sprites.Add(c);
            sprites.Add(d);
            using AtlasTexture atlas = new(world, sprites.AsSpan());
            Assert.That(atlas.GetWidth(), Is.EqualTo(64));
            Assert.That(atlas.GetHeight(), Is.EqualTo(64));
            Assert.That(atlas.GetSprites().Length, Is.EqualTo(4));
        }
    }
}
