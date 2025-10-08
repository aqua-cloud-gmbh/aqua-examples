using System;
using JUnitXmlImporter3.Aqua;
using Microsoft.Extensions.Logging;

namespace JUnitXmlImporter3.Services;

/// <summary>
/// Groups infrastructure dependencies for Importer to keep its constructor concise.
/// </summary>
public sealed class ImporterInfrastructure(IClock clock, IAquaClient aquaClient, ILogger<Importer> logger)
{
    public IClock Clock { get; } = clock ?? throw new ArgumentNullException(nameof(clock));
    public IAquaClient AquaClient { get; } = aquaClient ?? throw new ArgumentNullException(nameof(aquaClient));
    public ILogger<Importer> Logger { get; } = logger ?? throw new ArgumentNullException(nameof(logger));
}
