#!/usr/bin/env bash
set -Eeuo pipefail

script_directory="${BASH_SOURCE[0]%/*}"
if [[ "$script_directory" == "${BASH_SOURCE[0]}" ]]; then
  script_directory="."
fi
repository_root="$(cd "${script_directory}/../.." && pwd)"
migrations_directory="${MIGRATIONS_DIRECTORY:-${repository_root}/zombie_servival-3Dgame_Server/Data/Migrations}"
violations=""

for required_command in awk find sort; do
  if ! command -v "$required_command" >/dev/null 2>&1; then
    echo "EF Migration 정책 검사에 ${required_command} 명령이 필요합니다." >&2
    exit 1
  fi
done
find_version="$(find --version 2>/dev/null || true)"
if [[ "$find_version" != *"GNU findutils"* ]]; then
  echo "EF Migration 정책 검사에는 GNU findutils가 필요합니다." >&2
  exit 1
fi

if [[ ! -d "$migrations_directory" ]]; then
  echo "EF Migration 디렉터리를 찾을 수 없습니다: ${migrations_directory}" >&2
  exit 1
fi

migration_file_list="$(find "$migrations_directory" -maxdepth 1 -type f -name '*.cs' \
  ! -name '*.Designer.cs' ! -name '*ModelSnapshot.cs' -print | sort)"
if [[ -z "$migration_file_list" ]]; then
  echo "검사할 EF Migration 소스 파일이 없습니다: ${migrations_directory}" >&2
  exit 1
fi

while IFS= read -r migration_file; do
  file_violations="$(awk '
    function inspect_statement(    compact_statement) {
      compact_statement = statement
      gsub(/[[:space:]]/, "", compact_statement)
      sub(/^[{}]+/, "", compact_statement)

      if (compact_statement ~ /^[A-Za-z_][A-Za-z0-9_.]*(<[^>]+>)?\(/ \
          && compact_statement !~ /^migrationBuilder\./ \
          && compact_statement !~ /^table\.(Column|PrimaryKey|ForeignKey|UniqueConstraint|CheckConstraint)(<[^>]+>)?\(/ \
          && helper_call == "") {
        helper_call = compact_statement
        helper_line = statement_line
      }

      statement = ""
      statement_line = 0
    }

    function inspect_migration_builder_calls(    remaining, call) {
      remaining = up_method
      gsub(/[[:space:]]/, "", remaining)

      while (match(remaining, /migrationBuilder\.[A-Za-z_][A-Za-z0-9_]*(<[^<>]+>)?\(/)) {
        call = substr(remaining, RSTART, RLENGTH)

        if (call ~ /^migrationBuilder\.(Drop[A-Za-z0-9_]*|Rename[A-Za-z0-9_]*|AlterColumn|Sql|DeleteData|UpdateData)(<[^<>]+>)?\($/) {
          printf "%s: Up()에 축소형 또는 임의 SQL 작업이 있습니다: %s\n", FILENAME, call
        } else if (call ~ /^migrationBuilder\.AlterDatabase\($/ && FILENAME !~ /_InitialCreate\.cs$/) {
          printf "%s: AlterDatabase는 최초 Migration에서만 허용합니다: %s\n", FILENAME, call
        } else if (call !~ /^migrationBuilder\.(AddColumn|AddForeignKey|AddPrimaryKey|AddUniqueConstraint|AddCheckConstraint|CreateTable|CreateIndex|CreateSequence|EnsureSchema|InsertData|AlterDatabase)(<[^<>]+>)?\($/) {
          printf "%s: Up()에서 검증할 수 없는 MigrationBuilder 호출이 있습니다: %s\n", FILENAME, call
        }

        remaining = substr(remaining, RSTART + RLENGTH)
      }
    }

    function inspect_up_method() {
      if (statement != "") {
        inspect_statement()
      }

      inspect_migration_builder_calls()

      if (helper_call != "") {
        printf "%s:%d: Up()에서 검증할 수 없는 helper 호출이 있습니다: %s\n", FILENAME, helper_line, helper_call
      }

      up_method = ""
      statement = ""
      statement_line = 0
      helper_call = ""
      helper_line = 0
    }

    {
      source_line = $0
      if (in_up && source_line ~ /\/\*|\*\// && helper_call == "") {
        helper_call = "block-comment"
        helper_line = NR
      }
      sub(/\/\/.*/, "", source_line)

      compact_line = source_line
      gsub(/[[:space:]]/, "", compact_line)

      if (!in_up) {
        signature_buffer = signature_buffer compact_line
        if (length(signature_buffer) > 512) {
          signature_buffer = substr(signature_buffer, length(signature_buffer) - 511)
        }

        if (signature_buffer ~ /protectedoverridevoidUp\(MigrationBuildermigrationBuilder\)/) {
          in_up = 1
          saw_up = 1
          up_method = ""
          statement = ""
          statement_line = 0
          helper_call = ""
          helper_line = 0
          down_signature_buffer = ""
          signature_buffer = ""
        }
        next
      }

      if (down_signature_buffer != "") {
        down_signature_buffer = down_signature_buffer compact_line
        if (down_signature_buffer ~ /^protectedoverridevoidDown\(MigrationBuildermigrationBuilder\)/) {
          inspect_up_method()
          in_up = 0
          signature_buffer = ""
          down_signature_buffer = ""
          next
        }
        if (length(down_signature_buffer) > 512 || compact_line ~ /[;{}]/) {
          if (helper_call == "") {
            helper_call = down_signature_buffer
            helper_line = NR
          }
          down_signature_buffer = ""
        }
        next
      }

      if (compact_line ~ /^protected/) {
        down_signature_buffer = compact_line
        if (down_signature_buffer ~ /^protectedoverridevoidDown\(MigrationBuildermigrationBuilder\)/) {
          inspect_up_method()
          in_up = 0
          signature_buffer = ""
          down_signature_buffer = ""
        }
        next
      }

      up_method = up_method "\n" source_line
      candidate = source_line
      sub(/^[[:space:]]*/, "", candidate)

      if (statement == "" && candidate != "" && candidate !~ /^[{}]+$/) {
        statement_line = NR
      }
      if (candidate != "") {
        statement = statement " " candidate
      }
      if (candidate ~ /;/) {
        inspect_statement()
      }
    }

    END {
      if (in_up) {
        inspect_up_method()
      }
      if (!saw_up) {
        printf "%s: protected override void Up(MigrationBuilder migrationBuilder) 시그니처를 해석할 수 없습니다.\n", FILENAME
      }
    }
  ' "$migration_file")"

  if [[ -n "$file_violations" ]]; then
    violations+="${file_violations}"$'\n'
  fi
done <<< "$migration_file_list"

if [[ -n "$violations" ]]; then
  echo "컨테이너 롤백과 호환되지 않는 EF Migration Up 작업을 찾았습니다:" >&2
  printf '%s' "$violations" >&2
  echo "삭제·이름 변경·축소 변경은 별도 후속 배포로 분리하고 이번 배포에는 확장형 변경만 포함하세요." >&2
  exit 1
fi

echo "EF Migration이 expand-first 배포 규칙을 충족합니다."
