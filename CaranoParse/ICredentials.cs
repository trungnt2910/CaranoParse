namespace TimetableApp.Core
{
    public interface ICredentials
    {
        string Type { get; }
        bool SupportsOneClick { get; }
    }
}
