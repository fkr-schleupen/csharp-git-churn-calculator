namespace GitChurnCalculator.Models;

/// <summary>
/// Reports a single progress milestone emitted during churn analysis.
/// </summary>
/// <param name="Description">Human-readable description of the current step.</param>
/// <param name="Completed">Number of steps completed so far (0-based).</param>
/// <param name="Total">Total number of steps. 0 indicates an indeterminate operation.</param>
public sealed record ChurnProgressUpdate(string Description, int Completed, int Total);
