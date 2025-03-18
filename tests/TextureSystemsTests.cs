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
            TypeRegistry.Load<DataTypeBank>();
            TypeRegistry.Load<TexturesTypeBank>();
        }

        protected override void SetUp()
        {
            base.SetUp();
            simulator.AddSystem(new DataImportSystem());
            simulator.AddSystem(new TextureImportSystem());
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
