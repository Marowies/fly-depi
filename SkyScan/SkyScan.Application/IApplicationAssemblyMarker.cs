namespace SkyScan.Application
{
    /// <summary>
    /// Anchor type with no behavior of its own — used only so Program.cs can scan this
    /// assembly for MediatR handlers and FluentValidation validators via
    /// <c>typeof(IApplicationAssemblyMarker).Assembly</c> instead of pinning the scan to
    /// an arbitrary business type that might later be deleted or moved.
    /// </summary>
    public interface IApplicationAssemblyMarker
    {
    }
}
