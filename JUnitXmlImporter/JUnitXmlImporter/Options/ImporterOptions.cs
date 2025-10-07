using System;

namespace JUnitXmlImporter3.Options;

/// <summary>
/// Groups importer-related option sets to keep Importer constructor concise.
/// </summary>
public sealed class ImporterOptions(BehaviorOptions behavior, RunOptions? run = null, AquaOptions? aqua = null)
{
    public BehaviorOptions Behavior { get; } = behavior ?? throw new ArgumentNullException(nameof(behavior));
    public RunOptions Run { get; } = run ?? new RunOptions();
    public AquaOptions Aqua { get; } = aqua ?? new AquaOptions();
}
