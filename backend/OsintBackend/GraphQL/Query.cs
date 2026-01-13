using HotChocolate.Authorization;

namespace OsintBackend.GraphQL
{
    public class Query
    {
        [Authorize]
        public string Status() => "OSINT backend ready";
    }
}
