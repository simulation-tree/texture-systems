using System.Numerics;

namespace Textures.Tests
{
    public class ColorTests
    {
        [Test]
        public void ConvertRGBToHSV()
        {
            Color a = Color.CreateFromRGB(1, 0, 0);
            Vector4 hsv = a.AsHSV();
            Assert.That(hsv.X, Is.EqualTo(0f).Within(0.04f));

            Color b = Color.CreateFromRGB(0, 1, 0);
            hsv = b.AsHSV();
            Assert.That(hsv.X, Is.EqualTo(0.3333f).Within(0.04f));

            Color c = Color.CreateFromRGB(0, 0, 1);
            hsv = c.AsHSV();
            Assert.That(hsv.X, Is.EqualTo(0.6666f).Within(0.04f));

            Assert.That(hsv.Z, Is.EqualTo(1f).Within(0.04f));
            c.Value *= 0.5f;
            hsv = c.AsHSV();
            Assert.That(hsv.Z, Is.EqualTo(0.5f).Within(0.04f));
            Assert.That(c.B, Is.EqualTo(127));
        }
    }
}
