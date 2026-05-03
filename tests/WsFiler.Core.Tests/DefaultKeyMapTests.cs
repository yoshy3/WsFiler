using WsFiler.Core.Commands;
using WsFiler.Core.KeyMap;

namespace WsFiler.Core.Tests;

public sealed class DefaultKeyMapTests
{
    [Fact]
    public void DefaultKeyMap_ContainsRequiredArrowAndEnterBindings()
    {
        Assert.Contains(DefaultKeyMap.Bindings, binding => binding.CommandId == ApplicationCommandId.CursorUp && binding.Gesture.Key == "Up");
        Assert.Contains(DefaultKeyMap.Bindings, binding => binding.CommandId == ApplicationCommandId.CursorDown && binding.Gesture.Key == "Down");
        Assert.Contains(DefaultKeyMap.Bindings, binding => binding.CommandId == ApplicationCommandId.CursorLeft && binding.Gesture.Key == "Left");
        Assert.Contains(DefaultKeyMap.Bindings, binding => binding.CommandId == ApplicationCommandId.CursorRight && binding.Gesture.Key == "Right");
        Assert.Contains(DefaultKeyMap.Bindings, binding => binding.CommandId == ApplicationCommandId.DirectoryOpen && binding.Gesture.Key == "Enter");
        Assert.Contains(DefaultKeyMap.Bindings, binding => binding.CommandId == ApplicationCommandId.FilePreview && binding.Gesture.Key == "Enter");
    }
}
