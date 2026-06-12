namespace Auth.API.Services;

public sealed class EmailTemplateRenderer(IWebHostEnvironment env)
{
    private readonly string _templateDir = Path.Combine(env.WebRootPath, "email-templates");

    public async Task<string> RenderThemedAsync(
        string bodyTemplateFileName,
        EmailThemePalette palette,
        IReadOnlyDictionary<string, string> values)
    {
        var merged = new Dictionary<string, string>(palette.ToTemplateValues(), StringComparer.Ordinal);
        foreach (var (key, value) in values)
            merged[key] = value;

        var body = await RenderAsync(bodyTemplateFileName, merged);
        merged["content"] = body;
        return await RenderAsync("layout.html", merged);
    }

    public async Task<string> RenderAsync(string templateFileName, IReadOnlyDictionary<string, string> values)
    {
        var path = Path.Combine(_templateDir, templateFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"E-mail-skabelon mangler: {templateFileName}", path);

        var html = await File.ReadAllTextAsync(path);
        foreach (var (key, value) in values)
            html = html.Replace("{{" + key + "}}", value, StringComparison.Ordinal);

        return html;
    }
}
