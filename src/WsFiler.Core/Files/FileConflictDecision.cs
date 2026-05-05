namespace WsFiler.Core.Files;

public sealed record FileConflictDecision(FileConflictAction Action, bool ApplyToAll);
