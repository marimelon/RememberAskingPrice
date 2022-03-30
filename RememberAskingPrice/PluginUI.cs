using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using System.Linq;

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


            if (ImGui.CollapsingHeader("Saved", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.Button("Clear"))
                {
                    Service.Configuration.Data.Clear();
                    Service.Configuration.Save();
                }

                if (ImGui.BeginTable("items_table", 3, ImGuiTableFlags.Sortable))
                {
                    ImGui.TableSetupColumn("Name");
                    ImGui.TableSetupColumn("Price");
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort);
                    ImGui.TableHeadersRow();
                    
                    unsafe
                    {
                        var sorts_specs = ImGui.TableGetSortSpecs();
                        if (sorts_specs.NativePtr != null && sorts_specs.SpecsDirty)
                        {
                            this.SortId = sorts_specs.Specs.ColumnIndex;
                            this.SortDirectionr = sorts_specs.Specs.SortDirection;
                            PluginLog.Debug($"{sorts_specs.Specs.ColumnIndex} {sorts_specs.Specs.SortDirection}");
                            sorts_specs.SpecsDirty = false;
                        }
                    }

                    var items = (this.SortId, this.SortDirectionr) switch
                    {
                        (0, ImGuiSortDirection.Ascending) => Service.Configuration.Data.OrderBy(item => item.Key),
                        (1, ImGuiSortDirection.Ascending) => Service.Configuration.Data.OrderBy(item => item.Value),
                        (0, ImGuiSortDirection.Descending) => Service.Configuration.Data.OrderByDescending(item => item.Key),
                        (1, ImGuiSortDirection.Descending) => Service.Configuration.Data.OrderByDescending(item => item.Value),
                        _ => Service.Configuration.Data.OrderBy(item => item.Key)
                    };


                    foreach (var item in items)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(item.Key);
                        ImGui.TableNextColumn();
                        ImGui.Text(string.Format("{0:#,0}", (int)item.Value));
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
        }
    }
}
