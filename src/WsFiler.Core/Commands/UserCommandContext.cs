namespace WsFiler.Core.Commands;

public sealed record UserCommandItem(string Name, string FullPath);

public sealed record UserCommandContext(
    string CurrentDirectory,
    UserCommandItem? CurrentItem,
    IReadOnlyList<UserCommandItem> TargetItems)
{
    public IReadOnlyList<UserCommandItem> EffectiveItems =>
        TargetItems.Count > 0 ? TargetItems : CurrentItem is null ? [] : [CurrentItem];
}
