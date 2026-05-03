using WsFiler.Presentation.ViewModels;

namespace WsFiler.Presentation.Operations;

public sealed record DeleteRequest(IReadOnlyList<FileItemViewModel> Targets)
{
    public string RepresentativeName => Targets.Count == 0 ? "" : Targets[0].Name;
}
