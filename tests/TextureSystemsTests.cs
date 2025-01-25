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
            TypeRegistry.Load<Data.Core.TypeBank>();
            TypeRegistry.Load<Textures.TypeBank>();
        }

        protected override void SetUp()
        {
            base.SetUp();
            simulator.AddSystem<DataImportSystem>();
            simulator.AddSystem<TextureImportSystem>();
        }

        protected override Schema CreateSchema()
        {
            Schema schema = base.CreateSchema();
            schema.Load<Data.Core.SchemaBank>();
            schema.Load<Textures.SchemaBank>();
            return schema;
        }
    }
}
