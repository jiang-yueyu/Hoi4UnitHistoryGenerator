namespace Hoi4UnitHistoryGenerator.Model
{
    class EquipmentVariant
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string NameGroup { get; set; } = "";
        public string Icon { get; set; } = "";
        public int Obsolete { get; set; }
        public int ParentVersion { get; set; }
        public Dictionary<string, string> Modules { get; } = [];
        public Dictionary<string, int> Upgrades { get; } = [];


        public void Print(TextWriter writer)
        {
            writer.WriteLine("\tcreate_equipment_variant = {");

            writer.WriteLine($"\t\tname = \"{Name}\"");

            writer.WriteLine($"\t\ttype = {Type}");

            if (NameGroup.Length > 0)
            {
                writer.WriteLine($"\t\tname_group = {NameGroup}");
            }

            if (Icon.Length > 0)
            {
                writer.WriteLine($"\t\ticon = \"{Icon}\"");
            }

            writer.WriteLine($"\t\tparent_version = {ParentVersion}");

            if (Obsolete != 0)
            {

                writer.WriteLine("\t\tobsolete = yes");
            }

            writer.WriteLine("\t\tmodules = {");
            foreach (var (slot, equipment) in Modules)
            {
                writer.WriteLine($"\t\t\t{slot} = {equipment}");
            }
            writer.WriteLine("\t\t}");

            if (Upgrades.Count > 0)
            {
                writer.WriteLine("\t\tupgrades = {");
                foreach (var (item, level) in Upgrades)
                {
                    writer.WriteLine($"\t\t\t{item} = {level}");
                }
                writer.WriteLine("\t\t}");
            }

            writer.WriteLine("\t}");
        }
    }
}
