using System.Text.Json;

namespace QQ.Production.Intraday.Application;

/// <summary>One dated owner declaration, never an automated broker observation.</summary>
public static class LmaxDemoOwnerConfirmedOpening
{
    public const string Source = "OWNER_CONFIRMED_ACCOUNT_STATE";
    public const string Approval = "https://github.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-5730453334";
    public const string Quote = "la position est flat et il n'y a aucun ordre. J'aimerais qu'on lance le trading pour la journée";
    public static readonly DateTimeOffset RecordedAt = DateTimeOffset.Parse("2026-09-18T13:08:41.1204102Z");
    public const string ApprovalQuestion = "Autorises-tu explicitement, pour ce démarrage Demo, que ta confirmation actuelle « flat, aucun ordre » remplace cette observation indépendante ?";
    public static readonly DateTimeOffset OriginalRecordedAt = DateTimeOffset.Parse("2026-09-18T12:52:03.0232487Z");
    public const string OriginalReference = "https://github.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-5730268654";
    public const string OpeningReceiptHash = "65fa6c3c2b4a7eb2a136fd7a2f90c83678e074fe6ddc989feeabd9bc524b5a7e";

    public static void ValidateDeclaration(JsonElement proof, DateTimeOffset observedAt)
    {
        Require(proof.GetProperty("schema").GetString() == "lmax_demo_owner_confirmation_v2"
            && proof.GetProperty("source").GetString() == Source
            && proof.GetProperty("accountId").GetString() == "1754288005"
            && proof.GetProperty("owner").GetString() == "Philippe"
            && proof.GetProperty("ownerApprovalReference").GetString() == Approval
            && proof.GetProperty("quote").GetString() == Quote
            && proof.GetProperty("originalRecordedAtUtc").GetDateTimeOffset() == OriginalRecordedAt
            && proof.GetProperty("originalReference").GetString() == OriginalReference
            && proof.GetProperty("approvalQuestion").GetString() == ApprovalQuestion
            && proof.GetProperty("approvalResponse").GetString() == "oui"
            && proof.GetProperty("recordedAtUtc").GetDateTimeOffset() == RecordedAt && observedAt == RecordedAt
            && proof.GetProperty("flat").GetBoolean() && proof.GetProperty("noWorkingOrders").GetBoolean()
            && !proof.GetProperty("automatedBrokerObservation").GetBoolean()
            && proof.GetProperty("openingReceiptSha256").GetString() == OpeningReceiptHash,
            "EXACT_DATED_OWNER_DECLARATION_REQUIRED");
    }
    public static void ValidateFile(string path, string hash, DateTimeOffset observedAt, DateTimeOffset now)
    {
        Require(now.Offset == TimeSpan.Zero && now >= observedAt && now - observedAt <= TimeSpan.FromSeconds(900), "OWNER_DECLARATION_EXPIRED");
        Require(File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0
            && new FileInfo(path).Length is > 0 and <= 16384 && LmaxDemoRecoveredSendRetirement.HashFile(path) == hash,
            "OWNER_DECLARATION_CHANGED");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        ValidateDeclaration(document.RootElement, observedAt);
        var receiptPath = document.RootElement.GetProperty("openingReceiptPath").GetString()!;
        Require(File.Exists(receiptPath) && (File.GetAttributes(receiptPath) & FileAttributes.ReparsePoint) == 0
            && LmaxDemoRecoveredSendRetirement.HashFile(receiptPath) == OpeningReceiptHash, "AUTHENTIC_OPENING_RECEIPT_REQUIRED");
    }
    private static void Require(bool ok, string code) { if (!ok) throw new InvalidOperationException(code); }
}
