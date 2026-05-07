using NSwag;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace WebApp;

public class ApiPrefixDocumentProcessor : IDocumentProcessor
{
  public void Process(DocumentProcessorContext context)
  {
    context.Document.Servers.Add(new OpenApiServer { Url = "/api" });

    var paths = context.Document.Paths.ToList();
    context.Document.Paths.Clear();
    foreach (var (path, item) in paths)
      context.Document.Paths[path.Replace("/api/", "/", StringComparison.Ordinal)] = item;
  }
}
