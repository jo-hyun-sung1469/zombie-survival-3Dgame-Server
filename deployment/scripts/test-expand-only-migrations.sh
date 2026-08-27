#!/usr/bin/env bash
set -Eeuo pipefail

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
temporary_directory="$(mktemp -d)"

cleanup() {
  find "$temporary_directory" -depth -type f -delete 2>/dev/null || true
  find "$temporary_directory" -depth -type d -empty -delete 2>/dev/null || true
}
trap cleanup EXIT

safe_migration="${temporary_directory}/20260819000000_SafeExpansion.cs"

cat > "$safe_migration" <<'EOF'
protected
    override
    void Up
    (
        MigrationBuilder migrationBuilder
    )
{
    migrationBuilder.AddColumn<int>(name: "Level", table: "Players", nullable: false, defaultValue: 0);

    migrationBuilder.CreateTable(
        name: "PlayerProfiles",
        columns: table => new
        {
            Id = table.Column<int>(nullable: false)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_PlayerProfiles", x => x.Id);
        });

    migrationBuilder.InsertData(
        table: "PlayerProfiles",
        column: "Id",
        value: 1);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(name: "Level", table: "Players");
}
EOF

MIGRATIONS_DIRECTORY="$temporary_directory" \
  bash "${script_directory}/check-expand-only-migrations.sh" >/dev/null

initial_migration="${temporary_directory}/20260818000000_InitialCreate.cs"
cat > "$initial_migration" <<'EOF'
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AlterDatabase();
}

protected override void Down(MigrationBuilder migrationBuilder)
{
}
EOF

MIGRATIONS_DIRECTORY="$temporary_directory" \
  bash "${script_directory}/check-expand-only-migrations.sh" >/dev/null

declare -a forbidden_cases=(
  'Drop*|migrationBuilder
        .DropColumn(name: "LegacyValue", table: "Players");'
  'Rename*|migrationBuilder.RenameColumn(name: "LegacyValue", table: "Players", newName: "Value");'
  'AlterColumn|migrationBuilder.AlterColumn<int>(name: "Level", table: "Players", nullable: false);'
  'Sql|migrationBuilder.Sql("DELETE FROM Players;");'
  'DeleteData|migrationBuilder.DeleteData(table: "PlayerProfiles", keyColumn: "Id", keyValue: 1);'
  'UpdateData|migrationBuilder.UpdateData(table: "PlayerProfiles", keyColumn: "Id", keyValue: 1, column: "Level", value: 2);'
  'CustomCreate|migrationBuilder.CreateUnsafeTable(name: "Players");'
  'UnknownAdd|migrationBuilder.AddUnsafeConstraint(name: "Unsafe");'
  'NonInitialAlterDatabase|migrationBuilder.AlterDatabase();'
)

unsafe_migration="${temporary_directory}/20260819000001_UnsafeOperation.cs"

for forbidden_case in "${forbidden_cases[@]}"; do
  case_name="${forbidden_case%%|*}"
  method_call="${forbidden_case#*|}"

  cat > "$unsafe_migration" <<EOF
protected override void Up(MigrationBuilder migrationBuilder)
{
    ${method_call}
}

protected override void Down(MigrationBuilder migrationBuilder)
{
}
EOF

  if MIGRATIONS_DIRECTORY="$temporary_directory" \
    bash "${script_directory}/check-expand-only-migrations.sh" >/dev/null 2>&1; then
    echo "금지 API ${case_name} EF Migration Up 작업이 허용되었습니다." >&2
    exit 1
  fi
done

rm -f "$unsafe_migration"
unsafe_helper_migration="${temporary_directory}/20260819000002_UnsafeHelper.cs"

cat > "$unsafe_helper_migration" <<'EOF'
protected override void Up(MigrationBuilder migrationBuilder)
{
    ApplyDestructiveSql
    (
        migrationBuilder
    );
}

protected override void Down(MigrationBuilder migrationBuilder)
{
}
EOF

if MIGRATIONS_DIRECTORY="$temporary_directory" \
  bash "${script_directory}/check-expand-only-migrations.sh" >/dev/null 2>&1; then
  echo "검증할 수 없는 EF Migration helper 호출이 허용되었습니다." >&2
  exit 1
fi

echo "EF Migration expand-first 정책 테스트가 통과했습니다."
