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
    // New response event; the expired 15:05 proof and its stop authority are retained unchanged.
    public const string ResumeApproval = "https://github.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-5732189009";
    public static readonly DateTimeOffset ResumeRecordedAt = DateTimeOffset.Parse("2026-09-18T15:24:37.2308012Z");
    public const string ResumeQuestion = "confirmes-tu que le compte Demo est toujours à plat, sans ordre actif ?";
    public const string ResumeResponse = "oui. Tu peux y aller";
    public const string StopReceiptHash = "3cd5dc6960ca93a0a3779f0478e9e9bf3d40e67dc40abf349378b3f11903301d";
    // Current post-duplicate-ID recovery declaration. Historical v2/v3 evidence remains valid and immutable.
    public const string CurrentApproval = "https://github.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-5733078786";
    public static readonly DateTimeOffset CurrentRecordedAt = DateTimeOffset.Parse("2026-09-18T16:37:58.7544526Z");
    public const string CurrentQuestion = "Stop 8512 and confirm current LMAX Demo account state";
    public const string CurrentResponse = "Stop 8512. Le compte LMAX Demo est flat et il n'y a aucun ordre actif.";
    public const string CurrentStopReceiptHash = "cbf39456d661aac10ad6814fb5f185a18e59dfac6c2fb735c26fede233b9fb85";
    public static bool IsApprovedReference(string value) => value is Approval or ResumeApproval or CurrentApproval;

    public static void ValidateResumeDeclaration(JsonElement proof, DateTimeOffset observedAt)
    {
        Require(proof.GetProperty("schema").GetString()=="lmax_demo_owner_confirmation_v3"
            && proof.GetProperty("source").GetString()==Source && proof.GetProperty("accountId").GetString()=="1754288005"
            && proof.GetProperty("owner").GetString()=="Philippe" && proof.GetProperty("ownerApprovalReference").GetString()==ResumeApproval
            && proof.GetProperty("approvalQuestion").GetString()==ResumeQuestion && proof.GetProperty("approvalResponse").GetString()==ResumeResponse
            && proof.GetProperty("recordedAtUtc").GetDateTimeOffset()==ResumeRecordedAt && observedAt==ResumeRecordedAt
            && proof.GetProperty("flat").GetBoolean() && proof.GetProperty("noWorkingOrders").GetBoolean()
            && proof.GetProperty("exclusiveOrderActivityDeclared").GetBoolean() && !proof.GetProperty("automatedBrokerObservation").GetBoolean()
            && proof.GetProperty("stopReceiptSha256").GetString()==StopReceiptHash, "EXACT_DATED_RESUMPTION_DECLARATION_REQUIRED");
    }

    public static void ValidateCurrentDeclaration(JsonElement proof, DateTimeOffset observedAt)
    {
        Require(proof.GetProperty("schema").GetString()=="lmax_demo_owner_confirmation_v4"
            && proof.GetProperty("source").GetString()==Source && proof.GetProperty("accountId").GetString()=="1754288005"
            && proof.GetProperty("owner").GetString()=="Philippe" && proof.GetProperty("ownerApprovalReference").GetString()==CurrentApproval
            && proof.GetProperty("approvalQuestion").GetString()==CurrentQuestion && proof.GetProperty("approvalResponse").GetString()==CurrentResponse
            && proof.GetProperty("recordedAtUtc").GetDateTimeOffset()==CurrentRecordedAt && observedAt==CurrentRecordedAt
            && proof.GetProperty("flat").GetBoolean() && proof.GetProperty("noWorkingOrders").GetBoolean()
            && proof.GetProperty("exclusiveOrderActivityDeclared").GetBoolean() && !proof.GetProperty("automatedBrokerObservation").GetBoolean()
            && proof.GetProperty("stopReceiptSha256").GetString()==CurrentStopReceiptHash, "EXACT_CURRENT_POST_RECOVERY_DECLARATION_REQUIRED");
    }

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
        if (document.RootElement.GetProperty("schema").GetString()=="lmax_demo_owner_confirmation_v4")
        {
            ValidateCurrentDeclaration(document.RootElement, observedAt);
            var stopPath=document.RootElement.GetProperty("stopReceiptPath").GetString()!;
            Require(File.Exists(stopPath) && (File.GetAttributes(stopPath)&FileAttributes.ReparsePoint)==0
                && LmaxDemoRecoveredSendRetirement.HashFile(stopPath)==CurrentStopReceiptHash,"CURRENT_AUTHORIZED_STOP_RECEIPT_REQUIRED");
            return;
        }
        if (document.RootElement.GetProperty("schema").GetString()=="lmax_demo_owner_confirmation_v3")
        {
            ValidateResumeDeclaration(document.RootElement, observedAt);
            var stopPath=document.RootElement.GetProperty("stopReceiptPath").GetString()!;
            Require(File.Exists(stopPath) && (File.GetAttributes(stopPath)&FileAttributes.ReparsePoint)==0
                && LmaxDemoRecoveredSendRetirement.HashFile(stopPath)==StopReceiptHash,"AUTHORIZED_STOP_RECEIPT_REQUIRED");
            return;
        }
        ValidateDeclaration(document.RootElement, observedAt);
        var receiptPath = document.RootElement.GetProperty("openingReceiptPath").GetString()!;
        Require(File.Exists(receiptPath) && (File.GetAttributes(receiptPath) & FileAttributes.ReparsePoint) == 0
            && LmaxDemoRecoveredSendRetirement.HashFile(receiptPath) == OpeningReceiptHash, "AUTHENTIC_OPENING_RECEIPT_REQUIRED");
    }
    private static void Require(bool ok, string code) { if (!ok) throw new InvalidOperationException(code); }
}
