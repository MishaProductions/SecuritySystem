using EmbedIO;
using EmbedIO.Routing;

namespace SecuritySystem
{
    internal class CorsFixer : WebModuleBase
    {
        public override bool IsFinalHandler => false;

        public CorsFixer(string baseRoute) : base(baseRoute)
        {

        }

        protected override Task OnRequestAsync(IHttpContext context)
        {
            context.Response.Headers.Set("Cross-Origin-Embedder-Policy", "require-corp");
            context.Response.Headers.Set("Cross-Origin-Opener-Policy", "same-origin");
            return Task.CompletedTask;
        }
    }
}