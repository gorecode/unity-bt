namespace Gorecode.Bt
{
    /// <summary>
    /// Result of one task step. <see cref="InProgress"/> while latent work runs;
    /// the terminal values match the Finish outcomes (Success, Failure, Aborted).
    /// </summary>
    public enum TaskStatus
    {
        InProgress = 0,
        Success = 1,
        Failure = 2,
        Aborted = 3,
    }
}
