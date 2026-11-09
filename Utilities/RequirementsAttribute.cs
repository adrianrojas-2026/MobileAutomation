namespace MobileAutomation.Tests.Reporting;

// The #10082 requirement IDs a test checks, e.g. [Requirements("NDT-1-P01", "NDT-1-N02")]. NearbyR1Report traces each
// ID to its tests and to the 'NDT-x-yyy not met' findings in their results. With no IDs (a control test) the test is
// still listed in the report, with no requirement of its own
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirementsAttribute : PropertyAttribute
{
    public const string PropertyName = "Requirement";
    public const string ReportPropertyName = "Report";
    public const string NearbyReport = "NearbyR1";

    public RequirementsAttribute(params string[] ids)
    {
        Properties.Set(ReportPropertyName, NearbyReport);
        foreach (string id in ids)
        {
            Properties.Add(PropertyName, id);
        }
    }
}
