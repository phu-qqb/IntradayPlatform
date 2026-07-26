namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class InstitutionalSourceSnapshotContentAddress
{
    public static string ComputeSha256(InstitutionalSourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return InstitutionalCanonicalJson.FileSha256(snapshot);
    }
}
