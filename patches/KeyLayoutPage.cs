using ImGuiNET;
using RecompOne.Runtime.Host.Window;

namespace Kf3;

/// <summary>The same whole control choices as first-run and rerun setup.</summary>
public static class KeyLayoutPage
{
    public static void Draw()
    {
        ImGui.BeginDisabled(Setup.ControlProfile == 0);
        if (ImGui.Button(Localization.T("kf3.setup.bindings.classic"))) Setup.ApplyControlProfile(false);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Restores the upstream port's keyboard and controller bindings.");
        ImGui.SameLine();
        ImGui.BeginDisabled(Setup.ControlProfile == 1);
        if (ImGui.Button(Localization.T("kf3.setup.bindings.modern"))) Setup.ApplyControlProfile(true);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Shoulders attack and cast. Circle runs, Cross interacts, Triangle opens inventory and Options opens card/options.");
        if (Setup.ControlProfile < 0) ImGui.TextDisabled("Custom bindings");
        ImGui.Spacing();
    }
}
