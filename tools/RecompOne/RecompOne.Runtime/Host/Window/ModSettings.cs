using ImGuiNET;

namespace RecompOne.Runtime.Host.Window;

/// <summary>Settings controls that fit the mod card's available width.</summary>
public static class ModSettings
{
    public static bool SliderInt(string label, ref int value, int minimum, int maximum,
        string format = "%d", ImGuiSliderFlags flags = ImGuiSliderFlags.AlwaysClamp, string? help = null)
    {
        ImGui.PushID(label);
        try
        {
            Label(label, help);
            ImGui.SetNextItemWidth(-float.Epsilon);
            return ImGui.SliderInt("##value", ref value, minimum, maximum, format, flags);
        }
        finally { ImGui.PopID(); }
    }

    public static bool SliderFloat(string label, ref float value, float minimum, float maximum,
        string format = "%.2f", ImGuiSliderFlags flags = ImGuiSliderFlags.AlwaysClamp, string? help = null)
    {
        ImGui.PushID(label);
        try
        {
            Label(label, help);
            ImGui.SetNextItemWidth(-float.Epsilon);
            return ImGui.SliderFloat("##value", ref value, minimum, maximum, format, flags);
        }
        finally { ImGui.PopID(); }
    }

    static void Label(string label, string? help)
    {
        float reserve = string.IsNullOrEmpty(help) ? 0 : ImGui.CalcTextSize("(?)").X + ImGui.GetStyle().ItemSpacing.X;
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Max(1, ImGui.GetContentRegionAvail().X - reserve));
        ImGui.TextUnformatted(label);
        ImGui.PopTextWrapPos();
        if (string.IsNullOrEmpty(help)) return;
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (!ImGui.IsItemHovered()) return;
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(Math.Max(1, Math.Min(ImGui.GetFontSize() * 24, ImGui.GetIO().DisplaySize.X - 40)));
        ImGui.TextUnformatted(help);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
}
