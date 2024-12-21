namespace Hoi4UnitHistoryGenerator.Model
{
    class DivisionEntity
    {
        public int Location { get; set; }
        public float StartExperienceFactor { get; set; }
        public float StartEquipmentFactor { get; set; }
        public int NameOrder { get; set; }
        public string FixedName { get; set; } = "";
        public string DivisionTemplate { get; set; } = "";

        public void Print(TextWriter writer)
        {
            writer.WriteLine("\tdivision = {");

            if (NameOrder > 0)
            {
                writer.WriteLine("\t\tdivision_name = {");
                writer.WriteLine("\t\t\tis_name_ordered = yes");
                writer.WriteLine($"\t\t\tname_order = {NameOrder}");
                writer.WriteLine("\t\t}");
            }

            if (FixedName.Length > 0)
            {
                writer.WriteLine($"\t\tname = \"{FixedName}\"");
            }

            writer.WriteLine($"\t\tlocation = {Location}");
            writer.WriteLine($"\t\tdivision_template = \"{DivisionTemplate}\"");
            writer.WriteLine($"\t\tstart_experience_factor = {StartExperienceFactor}");
            writer.WriteLine($"\t\tstart_equipment_factor = {StartEquipmentFactor}");

            writer.WriteLine("\t}");
        }
    }
}
