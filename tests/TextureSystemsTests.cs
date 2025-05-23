using Data;
using Data.Systems;
using Simulation.Tests;
using Textures.Systems;
using Types;
using Worlds;

namespace Textures.Tests
{
    public abstract class TextureSystemsTests : SimulationTests
    {
        static TextureSystemsTests()
        {
            MetadataRegistry.Load<DataMetadataBank>();
            MetadataRegistry.Load<TexturesMetadataBank>();
        }

        protected override void SetUp()
        {
            base.SetUp();
            simulator.Add(new DataImportSystem());
            simulator.Add(new TextureImportSystem());
        }

        protected override void TearDown()
        {
            simulator.Remove<TextureImportSystem>();
            simulator.Remove<DataImportSystem>();
            base.TearDown();
        }

        protected override Schema CreateSchema()
        {
            Schema schema = base.CreateSchema();
            schema.Load<DataSchemaBank>();
            schema.Load<TexturesSchemaBank>();
            return schema;
        }
    }
}
