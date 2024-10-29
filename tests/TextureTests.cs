using Data;
using Data.Systems;
using Simulation.Tests;
using System.Threading;
using System.Threading.Tasks;
using Textures.Systems;
using Unmanaged;

namespace Textures.Tests
{
    public class TextureTests : SimulationTests
    {
        protected override void SetUp()
        {
            base.SetUp();
            Simulator.AddSystem<DataImportSystem>();
            Simulator.AddSystem<TextureImportSystem>();
        }

        [Test]
        public void CreateEmptyTexture()
        {
            USpan<Pixel> pixels = stackalloc Pixel[16];
            for (uint i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Pixel(byte.MaxValue, 0, 0, byte.MaxValue);
            }

            Texture emptyTexture = new(World, 4, 4, pixels);
            Assert.That(emptyTexture.Width, Is.EqualTo(4));
            Assert.That(emptyTexture.Height, Is.EqualTo(4));
            Pixel[] pixelsArray = emptyTexture.Pixels.ToArray();
            Assert.That(pixelsArray.Length, Is.EqualTo(4 * 4));
            foreach (Pixel pixel in pixelsArray)
            {
                Assert.That(pixel.r, Is.EqualTo(byte.MaxValue));
                Assert.That(pixel.g, Is.EqualTo(0));
                Assert.That(pixel.b, Is.EqualTo(0));
                Assert.That(pixel.a, Is.EqualTo(byte.MaxValue));
            }
        }

        [Test, CancelAfter(1000)]
        public async Task ImportTexture(CancellationToken cancellation)
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

            DataSource testTextureFile = new(World, "testTexture", texturePngData);

            Texture texture = new(World, "testTexture");
            await texture.UntilCompliant(Simulate, cancellation);

            Assert.That(texture.Width, Is.EqualTo(16));
            Assert.That(texture.Height, Is.EqualTo(9));
            Assert.That(texture.Pixels.Length, Is.EqualTo(16 * 9));

            texture[0, 0] = new(1, 0, 0, 1);

            float hueThreshold = 3f; //compression

            //bottom left is yellow
            Assert.That(texture.Evaluate(0f, 0f).H, Is.EqualTo(0.25f).Within(hueThreshold));

            //bottom right is blue
            Assert.That(texture.Evaluate(1f, 0f).H, Is.EqualTo(0.6666f).Within(hueThreshold));

            //top left is green
            Assert.That(texture.Evaluate(0f, 1f).H, Is.EqualTo(0.3333f).Within(hueThreshold));

            //top right is red
            Assert.That(texture.Evaluate(1f, 1f).H, Is.EqualTo(0f).Within(hueThreshold));

            //center is cyan
            Assert.That(texture.Evaluate(0.5f, 0.5f).H, Is.EqualTo(0.5f).Within(hueThreshold));
        }

        [Test]
        public void CreateAtlasTextureFromSprites()
        {
            USpan<AtlasTexture.InputSprite> sprites = stackalloc AtlasTexture.InputSprite[4];
            AtlasTexture.InputSprite a = new("r", 32, 32);
            for (uint i = 0; i < a.Pixels.Length; i++)
            {
                a.Pixels[i] = new(byte.MaxValue, 0, 0, 0);
            }

            AtlasTexture.InputSprite b = new("g", 32, 32);
            for (uint i = 0; i < b.Pixels.Length; i++)
            {
                b.Pixels[i] = new(0, byte.MaxValue, 0, 0);
            }

            AtlasTexture.InputSprite c = new("b", 32, 32);
            for (uint i = 0; i < c.Pixels.Length; i++)
            {
                c.Pixels[i] = new(0, 0, byte.MaxValue, 0);
            }

            AtlasTexture.InputSprite d = new("y", 32, 32);
            for (uint i = 0; i < d.Pixels.Length; i++)
            {
                d.Pixels[i] = new(byte.MaxValue, byte.MaxValue, 0, 0);
            }

            sprites[0] = a;
            sprites[1] = b;
            sprites[2] = c;
            sprites[3] = d;

            AtlasTexture atlas = new(World, sprites);
            Assert.That(atlas.Width, Is.EqualTo(64));
            Assert.That(atlas.Height, Is.EqualTo(64));
            Assert.That(atlas.Sprites.Length, Is.EqualTo(4));
        }
    }
}
