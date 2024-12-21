using Hoi4UnitHistoryGenerator.Attributes;

namespace Hoi4UnitHistoryGenerator.Model
{
    class AirWing
    {
        public int Location { get; set; }
        public int Amount { get; set; }

        [LocalisationReference("EquipmentVariant.Type")]
        public string Type { get; set; } = "";
        public string VersionName { get; set; } = "";

        public void Print(TextWriter writer, string owner)
        {
            writer.WriteLine($"\t\t{Type} = {{ owner = {owner} amount = {Amount} version_name = \"{VersionName}\" }}");
        }
    }
}
