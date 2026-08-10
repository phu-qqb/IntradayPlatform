using System.Text.Json.Nodes;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

internal sealed class Arch7bPendingBrokerExecutionState
{
    private Task<Arch7bCoreBrokerCommandResult>? pmsImport;
    private CancellationTokenSource? pmsImportCancellation;

    public string? PmsImportCommandSha256 { get; private set; }
    public bool HasPmsImport => pmsImport is not null;
    public Task<Arch7bCoreBrokerCommandResult> PmsImport => pmsImport ??
        throw new Arch7bQualificationException(
            Arch7bCoreRdsSecretBrokerBlockers.StateInvalid, "PMS_IMPORT_NOT_STARTED");

    public void StartPmsImport(string commandSha256,
        Func<CancellationToken, Task<Arch7bCoreBrokerCommandResult>> execute,
        CancellationToken cancellationToken)
    {
        if (pmsImport is not null)
            throw new Arch7bQualificationException(
                Arch7bCoreRdsSecretBrokerBlockers.StateInvalid, "PMS_IMPORT_ALREADY_STARTED");
        PmsImportCommandSha256 = commandSha256;
        pmsImportCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        pmsImport = execute(pmsImportCancellation.Token);
    }

    public async Task<Arch7bCoreBrokerCommandResult> CompletePmsImportAsync()
    {
        var execution = PmsImport;
        try
        {
            return await execution.ConfigureAwait(false);
        }
        finally
        {
            pmsImport = null;
            pmsImportCancellation?.Dispose();
            pmsImportCancellation = null;
        }
    }

    public void CancelPending() => pmsImportCancellation?.Cancel();
}

public sealed partial class Arch7bOneShotLiveExecutionRuntimeV2
{
    private static readonly IReadOnlyDictionary<string, string> BrokerResponseFacts =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["POSITION_APPLY"] = "broker_position_response",
            ["PMS_IMPORT"] = "broker_pms_response",
            ["ARCH7A_QUALIFY_SHADOW"] = "broker_arch7a_response",
            ["REPORTING"] = "broker_reporting_response"
        };

    private async Task<bool> TryExecuteBrokerStageAsync(
        Arch7bOneShotStageContract stage,
        Arch7bOneShotLivePlanTemplate template,
        Arch7bOneShotLiveFactStore facts,
        Arch7bOneShotBudget budget,
        string runRoot,
        DateTimeOffset observedUtc,
        ICollection<string> produced,
        Arch7bPendingBrokerExecutionState pendingExecutions,
        Action<string> commandSha,
        Action<string> resultSha,
        Action<string> resultCode,
        CancellationToken cancellationToken)
    {
        if (brokerClient is null) return false;
        if (stage.StageId == "RDS_READ_2")
        {
            RequireBrokerStageFacts(stage, template, facts, observedUtc);
            budget.RecordRdsRead();
            var value = await brokerClient.MaterializeAndStartAsync(template, facts, runRoot,
                brokerClient.ExpectedRead1VersionId, cancellationToken).ConfigureAwait(false);
            var authority = value.Authority;
            commandSha(authority.EvidenceSha256);
            produced.Add(facts.Append("broker_config_path", stage.StageId,
                new { path = authority.ConfigPath }, authority.ConfigSha256, observedUtc).FactSha256);
            produced.Add(facts.Append("broker_config_sha", stage.StageId,
                new { sha256 = authority.ConfigSha256 }, authority.ConfigSha256, observedUtc).FactSha256);
            produced.Add(facts.Append("broker_plan_path", stage.StageId,
                new { path = authority.CommandPlanPath }, authority.CommandPlanSha256, observedUtc).FactSha256);
            produced.Add(facts.Append("broker_plan_sha", stage.StageId,
                new { sha256 = authority.CommandPlanSha256 }, authority.CommandPlanSha256, observedUtc).FactSha256);
            AppendProducedStageFacts(stage, facts, produced, new
            {
                process_authority = authority.EvidenceSha256,
                command_plan_sha256 = authority.CommandPlanSha256,
                secret_value_observed = false,
                no_order = true
            }, authority.EvidenceSha256, observedUtc);
            resultSha(authority.EvidenceSha256);
            return true;
        }
        if (stage.StageId == "PRELOADED_LEASE_READY")
        {
            RequireBrokerStageFacts(stage, template, facts, observedUtc);
            var ready = await brokerClient.ReadReadyAsync(cancellationToken).ConfigureAwait(false);
            var evidence = JsonString(ready, "EvidenceSha256");
            var secretVersion = JsonString(ready, "SecretVersionId");
            produced.Add(facts.Append("broker_ready_evidence", stage.StageId,
                Persistable(ready), evidence, observedUtc).FactSha256);
            produced.Add(facts.Append("broker_secret_version_id", stage.StageId,
                new { version_id = secretVersion, matches_read_1 = true }, evidence, observedUtc).FactSha256);
            AppendProducedStageFacts(stage, facts, produced, new
            {
                ready_evidence_sha256 = evidence,
                secret_version_id = secretVersion,
                phase = brokerClient.Phase,
                secret_value_observed = false
            }, evidence, observedUtc);
            resultSha(evidence);
            return true;
        }
        if (stage.StageId == "BRACKET_T0")
        {
            RequireBrokerStageFacts(stage, template, facts, observedUtc);
            if (!brokerClient.IsRunning || brokerClient.Phase != "PRE_BRACKET")
                throw new Arch7bQualificationException(
                    Arch7bCoreRdsSecretBrokerBlockers.StateInvalid, stage.StageId);
            var bracketEvidence = Arch7bOneShotContracts.Sha256(string.Join('\n', stage.StageId,
                brokerClient.Materialization!.Authority.RunId,
                brokerClient.Materialization.Authority.CommandPlanSha256));
            AppendProducedStageFacts(stage, facts, produced, new
            {
                bracket_t0_evidence_sha256 = bracketEvidence,
                broker_alive = true,
                no_order = true
            }, bracketEvidence, observedUtc);
            var bracketResponse = await brokerClient.MarkBracketStartedAsync(bracketEvidence,
                cancellationToken).ConfigureAwait(false);
            var responseEvidence = JsonString(bracketResponse, "EvidenceSha256");
            produced.Add(facts.Append("broker_bracket_transition_response", stage.StageId,
                Persistable(bracketResponse), responseEvidence, observedUtc).FactSha256);
            resultSha(responseEvidence);
            return true;
        }
        if (stage.StageId == "HANDOFF_V3")
        {
            RequireBrokerStageFacts(stage, template, facts, observedUtc);
            if (!brokerClient.IsRunning || brokerClient.Phase != "POST_BRACKET" ||
                brokerClient.Materialization is null)
                throw new Arch7bQualificationException(
                    Arch7bCoreRdsSecretBrokerBlockers.StateInvalid, stage.StageId);
            var authority = brokerClient.Materialization.Authority;
            AppendProducedStageFacts(stage, facts, produced, new
            {
                process_authority_sha256 = authority.EvidenceSha256,
                command_plan_sha256 = authority.CommandPlanSha256,
                broker_alive = true,
                phase = brokerClient.Phase,
                core_owned_preloaded_lease = true,
                secret_value_observed = false,
                no_order = true
            }, authority.EvidenceSha256, observedUtc);
            resultSha(authority.EvidenceSha256);
            return true;
        }

        if (!BrokerResponseFacts.ContainsKey(stage.StageId)) return false;

        RequireBrokerStageFacts(stage, template, facts, observedUtc);
        if (stage.StageId == "REPORTING")
        {
            var revision = facts.Require("revision_binding_evidence", "REVISION_BINDING",
                observedUtc, int.MaxValue);
            var arch7a = facts.Require("arch7a_qualify_shadow_evidence",
                "ARCH7A_QUALIFY_SHADOW", observedUtc, int.MaxValue);
            var reportingInput = Arch7bOneShotContracts.Sha256(string.Join('\n',
                stage.RequiredFactTypes.Select(value => facts.Require(value,
                    ProducerFor(template, value), observedUtc, int.MaxValue).FactSha256)));
            var transition = await brokerClient.MarkTerminalReadonlyAsync(
                revision.EvidenceSha256, arch7a.EvidenceSha256, reportingInput,
                cancellationToken).ConfigureAwait(false);
            var transitionEvidence = JsonString(transition, "EvidenceSha256");
            produced.Add(facts.Append("broker_terminal_transition_response", stage.StageId,
                Persistable(transition), transitionEvidence, observedUtc).FactSha256);
        }

        Arch7bCoreBrokerCommandResult execution;
        if (stage.StageId == "PMS_IMPORT" && pendingExecutions.HasPmsImport)
        {
            commandSha(pendingExecutions.PmsImportCommandSha256 ??
                throw new Arch7bQualificationException(
                    Arch7bCoreRdsSecretBrokerBlockers.StateInvalid, stage.StageId));
            execution = await pendingExecutions.CompletePmsImportAsync()
                .ConfigureAwait(false);
        }
        else
        {
            var commandTemplate = template.CommandTemplates.Single(value =>
                value.StageId == stage.StageId);
            if (commandTemplate.CausesRdsRead || commandTemplate.SecretVariableNames.Count != 0)
                throw new Arch7bQualificationException(
                    Arch7bCoreRdsSecretBrokerBlockers.PlanInvalid, stage.StageId);
            var command = await materializer.MaterializeAsync(commandTemplate, facts,
                template.FileAuthorities, runRoot, observedUtc, cancellationToken)
                .ConfigureAwait(false);
            commandSha(command.EvidenceSha256);
            var inputFactEvidence = Arch7bOneShotContracts.Sha256(string.Join('\n',
                stage.RequiredFactTypes.Select(value => facts.Require(value,
                    ProducerFor(template, value), observedUtc, int.MaxValue).FactSha256)));
            execution = await brokerClient.ExecuteAsync(command, inputFactEvidence, runRoot,
                cancellationToken).ConfigureAwait(false);
        }
        var response = execution.Response;
        resultSha(execution.AdaptedResult.EvidenceSha256);
        resultCode(execution.AdaptedResult.ResultCode);
        produced.Add(facts.Append(BrokerResponseFacts[stage.StageId], stage.StageId, new
        {
            response.ContractVersion,
            response.CommandId,
            response.StageId,
            response.SequenceNumber,
            response.Phase,
            response.ChildExitClassification,
            response.PreviousResponseEvidenceSha256,
            response.NativeOutputContract,
            response.NativeOutputSha256,
            response.NativeOutputByteCount,
            response.EvidenceSha256,
            transient_payload_persisted = false
        }, response.EvidenceSha256, observedUtc).FactSha256);
        AppendProducedStageFacts(stage, facts, produced, new
        {
            result = execution.AdaptedResult.ResultCode,
            evidence_sha256 = execution.AdaptedResult.EvidenceSha256,
            artifact_paths = execution.AdaptedResult.ArtifactPaths,
            broker_response_sha256 = response.EvidenceSha256,
            transient_payload_persisted = false
        }, execution.AdaptedResult.EvidenceSha256, observedUtc);

        if (stage.StageId == "REPORTING")
        {
            var terminalEvidence = await brokerClient.ShutdownAsync(cancellationToken)
                .ConfigureAwait(false);
            var terminalSha = JsonString(terminalEvidence, "EvidenceSha256");
            produced.Add(facts.Append("broker_terminal_evidence", stage.StageId,
                Persistable(terminalEvidence), terminalSha, observedUtc).FactSha256);
            produced.Add(facts.Append("broker_phase", stage.StageId,
                new { phase = brokerClient.Phase }, terminalSha, observedUtc).FactSha256);
            produced.Add(facts.Append("broker_previous_response_sha", stage.StageId,
                new { sha256 = brokerClient.PreviousResponseSha256 },
                brokerClient.PreviousResponseSha256, observedUtc).FactSha256);
            produced.Add(facts.Append("broker_last_sequence", stage.StageId,
                new { sequence = brokerClient.LastSequence }, terminalSha, observedUtc).FactSha256);
        }
        return true;
    }

    private async Task<string> StartPmsImportPrearmAsync(
        Arch7bOneShotLivePlanTemplate template,
        Arch7bOneShotLiveFactStore facts,
        Arch7bPendingBrokerExecutionState pendingExecutions,
        string runRoot,
        DateTimeOffset observedUtc,
        CancellationToken cancellationToken)
    {
        if (brokerClient is null || !brokerClient.IsRunning ||
            brokerClient.Phase != "POST_BRACKET")
            throw new Arch7bQualificationException(
                Arch7bCoreRdsSecretBrokerBlockers.StateInvalid,
                "POSITION_MARKET_DRAFT");
        var commandTemplate = template.CommandTemplates.Single(value =>
            value.StageId == "PMS_IMPORT");
        if (commandTemplate.CausesRdsRead || commandTemplate.SecretVariableNames.Count != 0)
            throw new Arch7bQualificationException(
                Arch7bCoreRdsSecretBrokerBlockers.PlanInvalid, "PMS_IMPORT");
        var command = await materializer.MaterializeAsync(commandTemplate, facts,
            template.FileAuthorities, runRoot, observedUtc, cancellationToken)
            .ConfigureAwait(false);
        var inputFacts = commandTemplate.ArgumentTemplates
            .Select(argument => (Argument: argument,
                Placeholder: Arch7bTypedPlaceholder.Parse(argument.Value)))
            .Where(value => value.Placeholder is { Scope: not "authority" })
            .Select(value => facts.Require(value.Placeholder!.Value.Name,
                value.Argument.ExpectedProducerStage ?? throw new Arch7bQualificationException(
                    Arch7bV2Blockers.FactProducerMismatch, value.Argument.Value),
                observedUtc, value.Argument.MaximumAgeSeconds).FactSha256)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        var inputFactEvidence = Arch7bOneShotContracts.Sha256(string.Join('\n', inputFacts));
        pendingExecutions.StartPmsImport(command.EvidenceSha256,
            token => brokerClient.ExecuteAsync(command, inputFactEvidence, runRoot, token),
            cancellationToken);
        var execution = pendingExecutions.PmsImport;

        var planned = facts.Require("position_market_draft_output_path",
            "ONE_SHOT_IDENTITIES_CREATED", observedUtc, int.MaxValue);
        using var document = System.Text.Json.JsonDocument.Parse(planned.ValueJson);
        var path = document.RootElement.GetProperty("path").GetString() ?? string.Empty;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(command.TimeoutSeconds);
        while (!File.Exists(path))
        {
            if (execution.IsCompleted || DateTimeOffset.UtcNow >= deadline)
            {
                if (execution.IsCompleted) _ = await execution.ConfigureAwait(false);
                pendingExecutions.CancelPending();
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.RequiredFactMissing,
                    "position_market_draft_artifact");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken)
                .ConfigureAwait(false);
        }
        return command.EvidenceSha256;
    }

    private static void RequireBrokerStageFacts(Arch7bOneShotStageContract stage,
        Arch7bOneShotLivePlanTemplate template, Arch7bOneShotLiveFactStore facts,
        DateTimeOffset observedUtc)
    {
        foreach (var factType in stage.RequiredFactTypes)
            _ = facts.Require(factType, ProducerFor(template, factType), observedUtc, int.MaxValue);
    }

    private static string ProducerFor(Arch7bOneShotLivePlanTemplate template, string factType) =>
        template.StageContracts.Single(value =>
            value.ProducedFactTypes.Contains(factType, StringComparer.Ordinal)).StageId;

    private static void AppendProducedStageFacts(Arch7bOneShotStageContract stage,
        Arch7bOneShotLiveFactStore facts, ICollection<string> produced, object value,
        string evidenceSha256, DateTimeOffset observedUtc)
    {
        foreach (var factType in stage.ProducedFactTypes)
            produced.Add(facts.Append(factType, stage.StageId, value,
                evidenceSha256, observedUtc).FactSha256);
    }

    private static string JsonString(JsonObject value, string name) =>
        value[name]?.GetValue<string>() ?? throw new Arch7bQualificationException(
            Arch7bCoreRdsSecretBrokerBlockers.FrameUnexpected, name);

    private static JsonObject Persistable(JsonObject value)
    {
        var clone = JsonNode.Parse(value.ToJsonString())!.AsObject();
        clone.Remove("NativeStdoutPayload");
        clone.Remove("NativeStderrPayload");
        return clone;
    }
}
