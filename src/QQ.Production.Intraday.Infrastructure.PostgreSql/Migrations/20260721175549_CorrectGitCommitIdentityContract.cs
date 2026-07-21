using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CorrectGitCommitIdentityContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_model_run_hashes",
                schema: "pms_shadow",
                table: "model_runs");

            migrationBuilder.RenameColumn(
                name: "core_master_sha256",
                schema: "pms_shadow",
                table: "model_runs",
                newName: "core_master_commit_id");

            migrationBuilder.AddColumn<string>(
                name: "core_master_object_format",
                schema: "pms_shadow",
                table: "model_runs",
                type: "character varying(6)",
                maxLength: 6,
                nullable: false,
                computedColumnSql: "CASE WHEN length(core_master_commit_id) = 40 THEN 'sha1' WHEN length(core_master_commit_id) = 64 THEN 'sha256' ELSE 'invalid' END",
                stored: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_model_run_artifact_hashes",
                schema: "pms_shadow",
                table: "model_runs",
                sql: "package_sha256 ~ '^[0-9a-f]{64}$' AND engine_sha256 ~ '^[0-9a-f]{64}$' AND output_sha256 ~ '^[0-9a-f]{64}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_model_run_core_master_commit_identity",
                schema: "pms_shadow",
                table: "model_runs",
                sql: "core_master_object_format IN ('sha1', 'sha256') AND ((core_master_object_format = 'sha1' AND core_master_commit_id ~ '^[0-9a-f]{40}$') OR (core_master_object_format = 'sha256' AND core_master_commit_id ~ '^[0-9a-f]{64}$'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_model_run_artifact_hashes",
                schema: "pms_shadow",
                table: "model_runs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_model_run_core_master_commit_identity",
                schema: "pms_shadow",
                table: "model_runs");

            migrationBuilder.DropColumn(
                name: "core_master_object_format",
                schema: "pms_shadow",
                table: "model_runs");

            migrationBuilder.RenameColumn(
                name: "core_master_commit_id",
                schema: "pms_shadow",
                table: "model_runs",
                newName: "core_master_sha256");

            migrationBuilder.AddCheckConstraint(
                name: "ck_model_run_hashes",
                schema: "pms_shadow",
                table: "model_runs",
                sql: "core_master_sha256 ~ '^[0-9a-f]{64}$' AND package_sha256 ~ '^[0-9a-f]{64}$' AND engine_sha256 ~ '^[0-9a-f]{64}$' AND output_sha256 ~ '^[0-9a-f]{64}$'");
        }
    }
}
