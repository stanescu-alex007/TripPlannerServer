namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// Context about the caller, recorded on issued refresh tokens so a session can
    /// later be recognised in the "your devices" list.
    /// </summary>
    public sealed record RequestContext(string? IpAddress, string? UserAgent);
}
