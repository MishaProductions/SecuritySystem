using EmbedIO;
using EmbedIO.Routing;

namespace SecuritySystem.Utils
{
    internal class CorsFixer(string baseRoute) : WebModuleBase(baseRoute)
    {
        public override bool IsFinalHandler => false;

        protected override Task OnRequestAsync(IHttpContext context)
        {
            context.Response.Headers.Set("Cross-Origin-Embedder-Policy", "require-corp");
            context.Response.Headers.Set("Cross-Origin-Opener-Policy", "same-origin");
            return Task.CompletedTask;
        }
    }
}