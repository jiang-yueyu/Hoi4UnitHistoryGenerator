using Hoi4UnitHistoryGenerator.Attributes;

namespace Hoi4UnitHistoryGenerator.Model
{
    class WarShip
    {
        public string Name { get; set; } = "";
        public string Definition { get; set; } = "";

        [LocalisationReference("EquipmentVariant.Type")]
        public string Equipment { get; set; } = "";
        public string VersionName { get; set; } = "";

        public void Print(TextWriter writer, string owner)
        {
            writer.WriteLine($"\t\t\tship = {{ name = \"{Name}\" definition = {Definition} equipment = {{ {Equipment} = {{ amount = 1 owner = {owner} version_name = \"{VersionName}\" }} }} }}");
        }
    }
}
