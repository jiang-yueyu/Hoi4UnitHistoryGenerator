namespace Hoi4UnitHistoryGenerator.Model
{
    class TaskForce
    {
        public string Name { get; set; } = "";
        public int Location { get; set; }
        public List<WarShip> WarShips { get; } = [];

        public void Print(TextWriter writer, string owner)
        {
            writer.WriteLine("\t\ttask_force = {");

            writer.WriteLine($"\t\t\tname = \"{Name}\"");
            writer.WriteLine($"\t\t\tlocation = {Location}");

            foreach (WarShip warShip in WarShips)
            {
                warShip.Print(writer, owner);
            }

            writer.WriteLine("\t\t}");
        }
    }
}
