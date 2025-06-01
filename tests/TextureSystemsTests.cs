using Data;
using Data.Messages;
using Data.Systems;
using Simulation.Tests;
using Textures.Systems;
using Types;
using Worlds;

namespace Textures.Tests
{
    public abstract class TextureSystemsTests : SimulationTests
    {
        public World world;

        static TextureSystemsTests()
        {
            MetadataRegistry.Load<DataMetadataBank>();
            MetadataRegistry.Load<TexturesMetadataBank>();
        }

        protected override void SetUp()
        {
            base.SetUp();
            Schema schema = new();
            schema.Load<DataSchemaBank>();
            schema.Load<TexturesSchemaBank>();
            world = new(schema);
            Simulator.Add(new DataImportSystem(Simulator, world));
            Simulator.Add(new TextureImportSystem(Simulator, world));
        }

        protected override void TearDown()
        {
            Simulator.Remove<TextureImportSystem>();
            Simulator.Remove<DataImportSystem>();
            world.Dispose();
            base.TearDown();
        }

        protected override void Update(double deltaTime)
        {
            Simulator.Broadcast(new DataUpdate(deltaTime));
        }
    }
}
