using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bOneShotOperatorAuthorizationV2(
    string ContractVersion,
    string OperatorAuthorizationId,
    string TargetEnvironment,
    string AccountId,
    bool NoOrder,
    int MaximumSlots,
    int MaximumRdsReads,
    int MaximumCaptures,
    int MaximumRetries,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion, OperatorAuthorizationId,
        TargetEnvironment, AccountId, NoOrder, MaximumSlots, MaximumRdsReads, MaximumCaptures,
        MaximumRetries, IssuedAtUtc.ToUniversalTime().ToString("O"),
        ExpiresAtUtc.ToUniversalTime().ToString("O"));

    public void Validate(DateTimeOffset nowUtc)
    {
        if (ContractVersion != Arch7bV2Contracts.OperatorAuthorizationVersion ||
            EvidenceSha256 != Arch7bOneShotContracts.Sha256(Canonical()))
            throw new Arch7bQualificationException(Arch7bBlockers.OperatorAuthorizationMismatch);
        if (IssuedAtUtc > nowUtc || ExpiresAtUtc <= nowUtc)
            throw new Arch7bQualificationException(Arch7bV2Blockers.OperatorAuthorizationExpired);
        if (TargetEnvironment != "TEST" || AccountId != "1754288005" || !NoOrder ||
            MaximumSlots != 1 || MaximumRdsReads != 2 || MaximumCaptures != 1 || MaximumRetries != 0)
            throw new Arch7bQualificationException(Arch7bBlockers.OperatorAuthorizationMismatch);
    }
}

public sealed record Arch7bOneShotLiveExecutionAuthorityV2(
    string ContractVersion,
    string SupervisorCommit,
    string SupervisorTree,
    string CoreCommit,
    string CoreTree,
    string IntradayCommit,
    string IntradayTree,
    string FreezeManifestSha256,
    string FreezePacketSha256,
    string LivePlanTemplateSha256,
    string RuntimeInventorySha256,
    string CoreRepositoryAuthoritySha256,
    string CoreTrackedInventorySha256,
    string StaticAuthoritySetSha256,
    string CommandTemplateSetSha256,
    string AdapterSetSha256,
    string RootCaAuthoritySha256,
    string PrivilegeAuthoritySha256,
    string CalendarAuthoritySha256,
    string SloRegistrySha256,
    string ChronologySha256,
    string CleanupAuthoritySha256,
    string OperatorAuthorizationId,
    string TargetEnvironment,
    string AccountId,
    bool NoOrder,
    int MaximumSlots,
    int MaximumRdsReads,
    int MaximumCaptures,
    int MaximumRetries,
    IReadOnlyDictionary<string, Arch7bFileAuthority> FileAuthorities,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string EvidenceSha256)
{
    public string Canonical() => string.Join('\n', ContractVersion, SupervisorCommit, SupervisorTree,
        CoreCommit, CoreTree, IntradayCommit, IntradayTree, FreezeManifestSha256, FreezePacketSha256,
        LivePlanTemplateSha256, RuntimeInventorySha256, CoreRepositoryAuthoritySha256,
        CoreTrackedInventorySha256, StaticAuthoritySetSha256, CommandTemplateSetSha256, AdapterSetSha256,
        RootCaAuthoritySha256, PrivilegeAuthoritySha256, CalendarAuthoritySha256, SloRegistrySha256,
        ChronologySha256, CleanupAuthoritySha256, OperatorAuthorizationId, TargetEnvironment, AccountId,
        NoOrder, MaximumSlots, MaximumRdsReads, MaximumCaptures, MaximumRetries,
        string.Join('|', FileAuthorities.OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => $"{value.Key}:{value.Value.Path}:{value.Value.Sha256}")),
        IssuedAtUtc.ToUniversalTime().ToString("O"), ExpiresAtUtc.ToUniversalTime().ToString("O"));

    public void Validate(Arch7bOneShotLivePlanTemplate template,
        Arch7bOneShotOperatorAuthorizationV2 authorization, string templateFileSha256,
        DateTimeOffset nowUtc)
    {
        authorization.Validate(nowUtc);
        Require(ContractVersion == Arch7bV2Contracts.LiveExecutionAuthorityVersion,
            Arch7bBlockers.LiveAuthorityMissing);
        Require(EvidenceSha256 == Arch7bOneShotContracts.Sha256(Canonical()),
            Arch7bBlockers.CommandAuthorityMismatch);
        Require(IssuedAtUtc <= nowUtc && ExpiresAtUtc > nowUtc, Arch7bBlockers.LiveAuthorityExpired);
        Require(LivePlanTemplateSha256 == templateFileSha256, Arch7bBlockers.FreezeAuthorityMismatch);
        Require(OperatorAuthorizationId == authorization.OperatorAuthorizationId &&
            OperatorAuthorizationId == template.OperatorAuthorizationId,
            Arch7bBlockers.OperatorAuthorizationMismatch);
        Require(TargetEnvironment == "TEST" && template.TargetEnvironment == "TEST" &&
            authorization.TargetEnvironment == "TEST", Arch7bBlockers.TargetEnvironmentNotTest);
        Require(NoOrder && template.NoOrder && authorization.NoOrder, Arch7bBlockers.NoOrderRequired);
        Require(AccountId == "1754288005" && AccountId == template.AccountId &&
            AccountId == authorization.AccountId, Arch7bV2Blockers.AuthorityBindingMismatch);
        Require(MaximumSlots == 1 && MaximumRdsReads == 2 && MaximumCaptures == 1 && MaximumRetries == 0 &&
            MaximumSlots == template.MaximumSlots && MaximumRdsReads == template.MaximumRdsReads &&
            MaximumCaptures == template.MaximumCaptures && MaximumRetries == template.MaximumRetries,
            Arch7bBlockers.LiveCommandAuthorityIncomplete);
        Require(SupervisorCommit == template.SupervisorCommit && SupervisorTree == template.SupervisorTree &&
            CoreCommit == template.CoreCommit && CoreTree == template.CoreTree &&
            IntradayCommit == template.IntradayCommit && IntradayTree == template.IntradayTree,
            Arch7bBlockers.LiveAuthorityCommitMismatch);
        Require(FreezeManifestSha256 == template.FreezeManifestSha256 &&
            FreezePacketSha256 == template.FreezePacketSha256 &&
            RuntimeInventorySha256 == template.RuntimeInventorySha256 &&
            CoreRepositoryAuthoritySha256 == template.CoreRepositoryAuthoritySha256 &&
            CoreTrackedInventorySha256 == template.CoreTrackedInventorySha256 &&
            StaticAuthoritySetSha256 == template.StaticAuthoritySetSha256 &&
            CommandTemplateSetSha256 == template.CommandTemplateSetSha256 &&
            AdapterSetSha256 == template.AdapterSetSha256 &&
            RootCaAuthoritySha256 == template.RootCaAuthoritySha256 &&
            PrivilegeAuthoritySha256 == template.PrivilegeAuthoritySha256 &&
            CalendarAuthoritySha256 == template.CalendarAuthoritySha256 &&
            SloRegistrySha256 == template.SloRegistrySha256 &&
            ChronologySha256 == template.ChronologySha256 &&
            CleanupAuthoritySha256 == template.CleanupAuthoritySha256,
            Arch7bV2Blockers.AuthorityBindingMismatch);
        Require(FileAuthorities.Count == template.FileAuthorities.Count && FileAuthorities.All(value =>
            template.FileAuthorities.TryGetValue(value.Key, out var expected) && expected == value.Value),
            Arch7bV2Blockers.AuthorityBindingMismatch);
    }

    private static void Require(bool condition, string blocker)
    {
        if (!condition) throw new Arch7bQualificationException(blocker);
    }
}

public static class Arch7bLiveAuthorityLoaderV2
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Task<(Arch7bOneShotLiveExecutionAuthorityV2 Value, string FileSha256)> LoadAuthorityAsync(
        string path, string expectedSha256, CancellationToken cancellationToken = default) =>
        LoadAsync<Arch7bOneShotLiveExecutionAuthorityV2>(path, expectedSha256,
            Arch7bV2Blockers.AuthorityBindingMismatch, cancellationToken);

    public static Task<(Arch7bOneShotOperatorAuthorizationV2 Value, string FileSha256)> LoadOperatorAsync(
        string path, string expectedSha256, CancellationToken cancellationToken = default) =>
        LoadAsync<Arch7bOneShotOperatorAuthorizationV2>(path, expectedSha256,
            Arch7bV2Blockers.OperatorAuthorizationMissing, cancellationToken);

    public static Task<(Arch7bOneShotLivePlanTemplate Value, string FileSha256)> LoadTemplateAsync(
        string path, string expectedSha256, CancellationToken cancellationToken = default) =>
        LoadAsync<Arch7bOneShotLivePlanTemplate>(path, expectedSha256,
            Arch7bBlockers.FreezeAuthorityMismatch, cancellationToken);

    private static async Task<(T Value, string FileSha256)> LoadAsync<T>(string path, string expectedSha256,
        string blocker, CancellationToken cancellationToken)
    {
        Arch7bOneShotAuthorityLoader.RequireAbsolute(path);
        if (!File.Exists(path) || !Arch7bOneShotContracts.IsSha256(expectedSha256))
            throw new Arch7bQualificationException(blocker, path);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (sha != expectedSha256) throw new Arch7bQualificationException(blocker, path);
        var value = JsonSerializer.Deserialize<T>(bytes, Options) ??
            throw new Arch7bQualificationException(blocker, path);
        return (value, sha);
    }
}

public static class Arch7bLiveTemplateValidator
{
    public static void Validate(Arch7bOneShotLivePlanTemplate value,
        Arch7bRealCommandAdapterRegistry adapters)
    {
        if (value.ContractVersion != Arch7bV2Contracts.LivePlanTemplateVersion || !value.NoOrder ||
            value.TargetEnvironment != "TEST" || value.AccountId != "1754288005" ||
            value.StageContracts.Count != Arch7bStages.All.Count ||
            !value.StageContracts.Select(stage => stage.StageId).SequenceEqual(Arch7bStages.All,
                StringComparer.Ordinal))
            throw new Arch7bQualificationException(Arch7bBlockers.LiveCommandAuthorityIncomplete);
        if (value.CommandTemplates.Any(command => command.ExecutionKind is
                Arch7bExecutionKind.Internal or Arch7bExecutionKind.FilesystemGate or
                Arch7bExecutionKind.ExpectedBlockerGate))
            throw new Arch7bQualificationException(Arch7bV2Blockers.CommandTemplateInvalid);
        foreach (var command in value.CommandTemplates)
        {
            var stage = value.StageContracts.SingleOrDefault(item => item.StageId == command.StageId) ??
                throw new Arch7bQualificationException(Arch7bBlockers.ChronologyUnknownStage);
            if (stage.ExecutionKind != command.ExecutionKind)
                throw new Arch7bQualificationException(Arch7bV2Blockers.CommandTemplateInvalid,
                    command.CommandId);
            if (command.ExecutionKind is Arch7bExecutionKind.ChildInvoke or
                Arch7bExecutionKind.ChildStartLongLived or Arch7bExecutionKind.ChildStop)
                _ = adapters.Require(command.AdapterId);
        }
        var commandSet = Arch7bOneShotContracts.Sha256(string.Join('\n', value.CommandTemplates
            .Select(command => command.EvidenceSha256)));
        if (commandSet != value.CommandTemplateSetSha256 || adapters.EvidenceSha256 != value.AdapterSetSha256)
            throw new Arch7bQualificationException(Arch7bV2Blockers.AuthorityBindingMismatch);
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(
            value, Arch7bJson.CanonicalOptions));
        foreach (var property in new[] { "selected_slot", "run_id", "owner_id", "future_authorization_id",
                     "source_session_id", "market_capture_session_id", "secret_version_id" })
            if (document.RootElement.TryGetProperty(property, out _))
                throw new Arch7bQualificationException(Arch7bV2Blockers.CommandTemplateInvalid, property);
    }
}
