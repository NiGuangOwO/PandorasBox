using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PandorasBox.Helpers
{
    public static class KeyHelper
    {
        private static readonly Dictionary<string, bool> InputState = [];

        private static readonly Vector4 DisabledButtonColor = new(0.5f, 0.5f, 0.5f, 1);

        public static bool KeyInput(string label, ref Keys key)
        {
            ImGui.PushID("###" + label);

            if (!InputState.TryGetValue(label, out var isInputting))
            {
                InputState[label] = false;
            }

            if (!isInputting)
            {
                if (ImGui.Button(key.ToString(), new Vector2(70, 20)))
                {
                    InputState[label] = true;
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, DisabledButtonColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, DisabledButtonColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, DisabledButtonColor);
                ImGui.Button("Input...", new Vector2(70, 20));
                ImGui.PopStyleColor(3);

                if (IsRightClickHovered() || (IsItemHoveredAndMouseClicked() && InputState[label]))
                {
                    InputState[label] = false;
                }
                else
                {
                    var inputKey = GetAnyKeyInput();
                    if (inputKey != Keys.None)
                    {
                        key = inputKey;
                        InputState[label] = false;
                        ImGui.PopID();
                        return true;
                    }
                }
            }

            ImGui.PopID();
            ImGui.SameLine();
            ImGui.Text(label);

            return false;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        private static readonly byte[] KeyboardState = new byte[256];

        public static Keys GetAnyKeyInput()
        {
            GetKeyboardState(KeyboardState);
            foreach (Keys key in Enum.GetValues(typeof(Keys)))
            {
                if (key >= Keys.None && key <= Keys.OemClear && (KeyboardState[(int)key] & 0x80) != 0)
                {
                    return key;
                }
            }
            return Keys.None;
        }

        private static bool IsRightClickHovered()
        {
            return ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && ImGui.IsMouseClicked(ImGuiMouseButton.Right);
        }

        private static bool IsItemHoveredAndMouseClicked()
        {
            return ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        }
    }
}
