using TUnit.Core;
using TUnit.Core.Interfaces;

namespace LibraFoto.Tests.Helpers
{
    /// <summary>
    /// Limits test parallelism to prevent resource contention issues
    /// when running many simultaneous SQLite connections, file I/O operations,
    /// and HTTP clients during parallel test execution.
    /// </summary>
    public record TestParallelLimit : IParallelLimit
    {
        public int Limit => Math.Max(Environment.ProcessorCount / 2, 4);
    }
}
