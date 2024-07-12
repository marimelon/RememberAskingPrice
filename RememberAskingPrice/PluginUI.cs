using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace RememberAskingPrice
{
    internal class PluginUI : Window
    {

        private short SortId = 0;
        private ImGuiSortDirection SortDirectionr = 0;

        public PluginUI() : base("RememberAskingPrice")
        {
            this.Size = new Vector2(525, 300);
            this.SizeCondition = ImGuiCond.FirstUseEver;
        }

        public override void Draw()
        {
            var enable1 = Service.Configuration.Enabled;
            if (ImGui.Checkbox("Enabled", ref enable1))
            {
                Service.Configuration.Enabled = enable1;
                Service.Configuration.Save();
            }


            #region Features

            if (!Service.Configuration.Enabled)
            {
                ImGui.BeginDisabled();
            }

            ImGui.Text("Features");
            var enable2 = Service.Configuration.EnabledAskingPrice;
            if (ImGui.Checkbox("Enabled AskingPrice", ref enable2))
            {
                Service.Configuration.EnabledAskingPrice = enable2;
                Service.Configuration.Save();
            }

            var enable3 = Service.Configuration.EnabledQuantity;
            if (ImGui.Checkbox("Enabled Quantity", ref enable3))
            {
                Service.Configuration.EnabledQuantity = enable3;
                Service.Configuration.Save();
            }

            if (!Service.Configuration.Enabled)
            {
                ImGui.EndDisabled();
            }

            #endregion


            #region Data
            if (ImGui.CollapsingHeader("Data", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.Button("Clear"))
                {
                    Service.Configuration.Data.Clear();
                    Service.Configuration.Save();
                }

                if (ImGui.BeginTable("items_table", 4, ImGuiTableFlags.Sortable))
                {
                    ImGui.TableSetupColumn("Name");
                    ImGui.TableSetupColumn("Price");
                    ImGui.TableSetupColumn("Quantity");
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort);
                    ImGui.TableHeadersRow();

                    unsafe
                    {
                        var sorts_specs = ImGui.TableGetSortSpecs();
                        if (sorts_specs.NativePtr != null && sorts_specs.SpecsDirty)
                        {
                            this.SortId = sorts_specs.Specs.ColumnIndex;
                            this.SortDirectionr = sorts_specs.Specs.SortDirection;
                            Service.PluginLog.Debug($"{sorts_specs.Specs.ColumnIndex} {sorts_specs.Specs.SortDirection}");
                            sorts_specs.SpecsDirty = false;
                        }
                    }

                    var items = (this.SortId, this.SortDirectionr) switch
                    {
                        (0, ImGuiSortDirection.Ascending) => Service.Configuration.Data.OrderBy(item => item.Key),
                        (1, ImGuiSortDirection.Ascending) => Service.Configuration.Data.OrderBy(item => item.Value.AskingPrice),
                        (2, ImGuiSortDirection.Ascending) => Service.Configuration.Data.OrderBy(item => item.Value.Quantity),
                        (0, ImGuiSortDirection.Descending) => Service.Configuration.Data.OrderByDescending(item => item.Key),
                        (1, ImGuiSortDirection.Descending) => Service.Configuration.Data.OrderByDescending(item => item.Value.AskingPrice),
                        (2, ImGuiSortDirection.Descending) => Service.Configuration.Data.OrderByDescending(item => item.Value.Quantity),
                        _ => Service.Configuration.Data.OrderBy(item => item.Key)
                    };


                    foreach (var item in items)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(item.Key);
                        ImGui.TableNextColumn();
                        ImGui.Text(string.Format("{0:#,0}", (int)item.Value.AskingPrice));
                        ImGui.TableNextColumn();
                        ImGui.Text(string.Format("{0:#,0}", (int)item.Value.Quantity));
                        ImGui.TableNextColumn();
                        if (ImGui.Button("Remove##" + item.Key))
                        {
                            Service.Configuration.Data.Remove(item.Key);
                            Service.Configuration.Save();
                        }
                    }
                }
                ImGui.EndTable();
            }
            #endregion
        }
    }
}
