using WsFiler.Presentation.ViewModels;

namespace WsFiler.Presentation.Operations;

public sealed record FileOperationRequest(
    IReadOnlyList<FileItemViewModel> Targets,
    string DestinationDirectory)
{
    public string RepresentativeName => Targets.Count == 0 ? "" : Targets[0].Name;
}
