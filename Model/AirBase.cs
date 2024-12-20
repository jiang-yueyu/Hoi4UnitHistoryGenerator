namespace Hoi4UnitHistoryGenerator.Model
{
    class AirBase
    {
        public int Location { get; set; }
        public List<AirWing> AirWings { get; } = [];

        public void Print(TextWriter writer, string owner)
        {
            writer.WriteLine($"\t{Location} = {{");

            foreach (var airWing in AirWings)
            {
                airWing.Print(writer, owner);
            }

            writer.WriteLine("\t}");
        }
    }
}
