namespace SkyScan.Application.Common.Interfaces
{
    /// <summary>
    /// Generates absolute action URLs (e.g. for email links) without Application depending
    /// on IUrlHelper/HttpContext directly — same rationale as ICookieWriter and
    /// ICurrentLanguageProvider (see docs/ARCHITECTURE_DECISIONS.md).
    /// </summary>
    public interface IUrlBuilder
    {
        string ActionUrl(string action, string controller, object values);
    }
}
