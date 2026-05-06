namespace DdtManual.Web.Helpers;

public static class ServiceNavigationHelper
{
    public static bool IsNavItemActive(string? pathValue, string url)
    {
        var path = (pathValue ?? "/").TrimEnd('/');
        if (string.IsNullOrEmpty(path))
            path = "/";

        if (url == "/" || url == "")
            return path == "/" || path.Equals("/home", StringComparison.OrdinalIgnoreCase);

        var prefix = url.TrimEnd('/');
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && (path.Length == prefix.Length || path[prefix.Length] == '/');
    }
}
