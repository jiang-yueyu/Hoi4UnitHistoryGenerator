namespace Hoi4UnitHistoryGenerator.Model
{
    class DivisionTemplate
    {
        public List<List<string>> Support { get; set; } = [];
        public List<List<string>> Regiments { get; set; } = [];
        public string Name { get; set; } = "";
        public string DivisionNameGroup { get; set; } = "";
        public int IsLocked { get; set; }

        public void Print(TextWriter writer)
        {
            writer.WriteLine("division_template = {");

            writer.WriteLine($"\tname = \"{Name}\"");

            writer.WriteLine($"\tdivision_names_group = {DivisionNameGroup}");

            writer.WriteLine("\tregiments = {");
            {
                int x = 0;

                foreach (List<string> regiment in Regiments)
                {
                    int y = 0;
                    foreach(string s in regiment)
                    {
                        writer.WriteLine($"\t\t{s} = {{ x = {x} y = {y++} }}");
                    }
                    x++;
                }
            }
            writer.WriteLine("\t}");

            writer.WriteLine("\tsupport = {");
            {
                int x = 0;

                foreach (List<string> support0 in Support)
                {
                    int y = 0;
                    foreach (string s in support0)
                    {
                        writer.WriteLine($"\t\t{s} = {{ x = {x} y = {y++} }}");
                    }
                    x++;
                }
            }
            writer.WriteLine("\t}");

            if (IsLocked != 0)
            {
                writer.WriteLine("\tis_locked = yes");
            }

            writer.WriteLine("}");
        }
    }
}
