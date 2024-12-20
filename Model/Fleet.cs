namespace Hoi4UnitHistoryGenerator.Model
{
    class Fleet
    {
        public string Name { get; set; } = "";
        public int NavalBase { get; set; }
        public List<TaskForce> TaskForces { get; } = [];

        public void Print(TextWriter writer, string owner)
        {
            writer.WriteLine("\tfleet = {");

            writer.WriteLine($"\t\tname = \"{Name}\"");
            writer.WriteLine($"\t\tnaval_base = {NavalBase}");

            foreach (var taskForce in TaskForces)
            {
                taskForce.Print(writer, owner);
                writer.WriteLine();
            }

            writer.WriteLine("\t}");
        }
    }
}
