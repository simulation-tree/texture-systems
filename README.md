# Texture Systems

Implements the `texture` project.

### Updating

For textures to be loaded from an address:
```cs
simulator.Add(new DataImportSystem(simulator, world));
simulator.Add(new TextureImportSystem(simulator, world));

//create the image and get it loaded
Texture texture = new(world, "C:/image.png");
simulator.Broadcast(new DataUpdate());

//do work with the loaded image
Assert.That(texture.IsLoaded, Is.True);
Assert.That(texture.Width, Is.GreaterThan(0));
Assert.That(texture.Height, Is.GreaterThan(0));
Span<Pixel> pixels = texture.Pixels;

simulator.Remove<TextureImportSystem>();
simulator.Remove<DataImportSystem>();
```