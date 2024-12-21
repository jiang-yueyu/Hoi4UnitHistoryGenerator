namespace Hoi4UnitHistoryGenerator.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    class LocalisationReferenceAttribute(string localisationKey) : Attribute
    {
        public string LocalisationKey { get; } = localisationKey;
    }
}
