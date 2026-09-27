namespace ProductShop.Api.Services;

// Chobi kon folder e thakbe. Default: app folder er "uploads".
// Online (Azure) e "UploadsPath" setting diye app er baire rakha jay, jate notun deploy e muche na jay.
public static class UploadPaths
{
    public static string Root(IConfiguration config, IWebHostEnvironment env)
    {
        var path = config["UploadsPath"];
        return string.IsNullOrWhiteSpace(path)
            ? Path.Combine(env.ContentRootPath, "uploads")
            : Path.GetFullPath(path);
    }
}
