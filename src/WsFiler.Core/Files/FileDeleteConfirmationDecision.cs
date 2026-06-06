namespace WsFiler.Core.Files;

public sealed record FileDeleteConfirmationDecision(
    FileDeleteConfirmationAction Action,
    bool ApplyToAll);
