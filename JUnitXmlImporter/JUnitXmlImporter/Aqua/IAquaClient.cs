namespace JUnitXmlImporter3.Aqua;

/// <summary>
/// Aqua API client interface for batch submission of test executions.
/// Implementations are responsible for handling authentication, retries, rate limiting, and timeouts as required. HTTPS is required.
/// </summary>
public interface IAquaClient
{
    /// <summary>
    /// Submits a batch of test executions to Aqua in a single POST call.
    /// Returns a result indicating counts and success/failure. Implementations should be authenticated automatically.
    /// </summary>
    /// <param name="executions">The executions to submit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="AquaSubmitResult"/> containing the counts of successful and failed submissions, along with overall success or failure status.
    /// </returns>
    Task<AquaSubmitResult> SubmitExecutionsAsync(IReadOnlyList<AquaExecutionRequest> executions, CancellationToken cancellationToken);
}
