using CarWashSaaS.Client.Prerender;

if (args.Length == 0)
{
    Console.Error.WriteLine("Uso: CarWashSaaS.Client.Prerender <caminho-wwwroot> [canonical-origin]");
    return 1;
}

var wwwrootPath = Path.GetFullPath(args[0]);
var canonicalOrigin = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])
    ? args[1]
    : StaticPrerenderer.DefaultCanonicalOrigin;

var count = await StaticPrerenderer.PrerenderAllAsync(wwwrootPath, canonicalOrigin);
Console.WriteLine($"[Prerender] Concluído: {count} rotas públicas pré-renderizadas em {wwwrootPath}");
return 0;
