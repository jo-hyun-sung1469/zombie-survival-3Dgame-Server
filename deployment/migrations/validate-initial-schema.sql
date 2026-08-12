WITH expected_columns (table_name, column_name, column_type, is_nullable) AS (
    SELECT 'AuthVerificationCodes', 'Id', 'varchar(64)', 'NO'
    UNION ALL SELECT 'AuthVerificationCodes', 'Email', 'varchar(254)', 'NO'
    UNION ALL SELECT 'AuthVerificationCodes', 'CodeHash', 'varchar(500)', 'NO'
    UNION ALL SELECT 'AuthVerificationCodes', 'AttemptCount', 'int', 'NO'
    UNION ALL SELECT 'AuthVerificationCodes', 'CreatedAtUtc', 'datetime(6)', 'NO'
    UNION ALL SELECT 'AuthVerificationCodes', 'ExpiresAtUtc', 'datetime(6)', 'NO'
    UNION ALL SELECT 'AuthVerificationCodes', 'VerifiedAtUtc', 'datetime(6)', 'YES'
    UNION ALL SELECT 'AuthVerificationCodes', 'ConsumedAtUtc', 'datetime(6)', 'YES'
    UNION ALL SELECT 'FirearmDefinitions', 'Id', 'int', 'NO'
    UNION ALL SELECT 'FirearmDefinitions', 'Name', 'varchar(100)', 'NO'
    UNION ALL SELECT 'FirearmDefinitions', 'DisplayName', 'varchar(100)', 'NO'
    UNION ALL SELECT 'FirearmDefinitions', 'Category', 'varchar(50)', 'NO'
    UNION ALL SELECT 'FirearmDefinitions', 'Rarity', 'varchar(30)', 'NO'
    UNION ALL SELECT 'FirearmDefinitions', 'GachaProbability', 'double', 'NO'
    UNION ALL SELECT 'FirearmDefinitions', 'Damage', 'int', 'NO'
    UNION ALL SELECT 'FirearmDefinitions', 'FireRate', 'double', 'NO'
    UNION ALL SELECT 'FirearmDefinitions', 'MagazineSize', 'int', 'NO'
    UNION ALL SELECT 'FirearmDefinitions', 'ReloadTimeSeconds', 'double', 'NO'
    UNION ALL SELECT 'FirearmDefinitions', 'RangeMeters', 'double', 'NO'
    UNION ALL SELECT 'FirearmDefinitions', 'HeadshotDamageMultiplier', 'double', 'NO'
    UNION ALL SELECT 'PlayerSaveData', 'Id', 'int', 'NO'
    UNION ALL SELECT 'PlayerSaveData', 'PlayerId', 'varchar(64)', 'NO'
    UNION ALL SELECT 'PlayerSaveData', 'Gold', 'int', 'NO'
    UNION ALL SELECT 'PlayerSaveData', 'UpdatedAtUtc', 'datetime(6)', 'NO'
    UNION ALL SELECT 'Users', 'Id', 'varchar(64)', 'NO'
    UNION ALL SELECT 'Users', 'UserName', 'varchar(30)', 'NO'
    UNION ALL SELECT 'Users', 'Email', 'varchar(254)', 'NO'
    UNION ALL SELECT 'Users', 'PasswordHash', 'varchar(500)', 'NO'
    UNION ALL SELECT 'Users', 'Role', 'varchar(20)', 'NO'
    UNION ALL SELECT 'Users', 'CreatedAtUtc', 'datetime(6)', 'NO'
    UNION ALL SELECT 'PlayerStatUpgradeStates', 'Id', 'int', 'NO'
    UNION ALL SELECT 'PlayerStatUpgradeStates', 'PlayerSaveDataId', 'int', 'NO'
    UNION ALL SELECT 'PlayerStatUpgradeStates', 'StatName', 'varchar(50)', 'NO'
    UNION ALL SELECT 'PlayerStatUpgradeStates', 'UpgradeLevel', 'int', 'NO'
    UNION ALL SELECT 'PlayerWeaponStates', 'Id', 'int', 'NO'
    UNION ALL SELECT 'PlayerWeaponStates', 'PlayerSaveDataId', 'int', 'NO'
    UNION ALL SELECT 'PlayerWeaponStates', 'FirearmDefinitionId', 'int', 'NO'
    UNION ALL SELECT 'PlayerWeaponStates', 'WeaponName', 'varchar(100)', 'NO'
    UNION ALL SELECT 'PlayerWeaponStates', 'IsOwned', 'tinyint(1)', 'NO'
    UNION ALL SELECT 'PlayerWeaponStates', 'WeaponLevel', 'int', 'NO'
    UNION ALL SELECT 'PlayerWeaponStates', 'Damage', 'int', 'NO'
    UNION ALL SELECT 'PlayerWeaponStates', 'FireRate', 'double', 'NO'
    UNION ALL SELECT 'PlayerWeaponStates', 'MagazineSize', 'int', 'NO'
    UNION ALL SELECT 'PlayerWeaponStates', 'ReloadTimeSeconds', 'double', 'NO'
    UNION ALL SELECT 'PlayerWeaponStates', 'RangeMeters', 'double', 'NO'
    UNION ALL SELECT 'PlayerWeaponStates', 'HeadshotDamageMultiplier', 'double', 'NO'
),
actual_columns AS (
    SELECT table_name, column_name, column_type, is_nullable
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name <> '__EFMigrationsHistory'
),
expected_auto_increment (table_name, column_name) AS (
    SELECT 'FirearmDefinitions', 'Id'
    UNION ALL SELECT 'PlayerSaveData', 'Id'
    UNION ALL SELECT 'PlayerStatUpgradeStates', 'Id'
    UNION ALL SELECT 'PlayerWeaponStates', 'Id'
),
actual_auto_increment AS (
    SELECT table_name, column_name
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND extra LIKE '%auto_increment%'
),
expected_indexes (table_name, index_name, non_unique, seq_in_index, column_name) AS (
    SELECT 'AuthVerificationCodes', 'PRIMARY', 0, 1, 'Id'
    UNION ALL SELECT 'AuthVerificationCodes', 'IX_AuthVerificationCodes_Email', 1, 1, 'Email'
    UNION ALL SELECT 'FirearmDefinitions', 'PRIMARY', 0, 1, 'Id'
    UNION ALL SELECT 'FirearmDefinitions', 'IX_FirearmDefinitions_Name', 0, 1, 'Name'
    UNION ALL SELECT 'PlayerSaveData', 'PRIMARY', 0, 1, 'Id'
    UNION ALL SELECT 'PlayerSaveData', 'IX_PlayerSaveData_PlayerId', 0, 1, 'PlayerId'
    UNION ALL SELECT 'Users', 'PRIMARY', 0, 1, 'Id'
    UNION ALL SELECT 'Users', 'IX_Users_Email', 0, 1, 'Email'
    UNION ALL SELECT 'Users', 'IX_Users_UserName', 0, 1, 'UserName'
    UNION ALL SELECT 'PlayerStatUpgradeStates', 'PRIMARY', 0, 1, 'Id'
    UNION ALL SELECT 'PlayerStatUpgradeStates', 'IX_PlayerStatUpgradeStates_PlayerSaveDataId_StatName', 0, 1, 'PlayerSaveDataId'
    UNION ALL SELECT 'PlayerStatUpgradeStates', 'IX_PlayerStatUpgradeStates_PlayerSaveDataId_StatName', 0, 2, 'StatName'
    UNION ALL SELECT 'PlayerWeaponStates', 'PRIMARY', 0, 1, 'Id'
    UNION ALL SELECT 'PlayerWeaponStates', 'IX_PlayerWeaponStates_FirearmDefinitionId', 1, 1, 'FirearmDefinitionId'
    UNION ALL SELECT 'PlayerWeaponStates', 'IX_PlayerWeaponStates_PlayerSaveDataId_FirearmDefinitionId', 0, 1, 'PlayerSaveDataId'
    UNION ALL SELECT 'PlayerWeaponStates', 'IX_PlayerWeaponStates_PlayerSaveDataId_FirearmDefinitionId', 0, 2, 'FirearmDefinitionId'
),
actual_indexes AS (
    SELECT table_name, index_name, non_unique, seq_in_index, column_name
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name <> '__EFMigrationsHistory'
),
expected_foreign_keys (table_name, constraint_name, column_name, referenced_table_name, referenced_column_name, delete_rule) AS (
    SELECT
        'PlayerStatUpgradeStates',
        'FK_PlayerStatUpgradeStates_PlayerSaveData_PlayerSaveDataId',
        'PlayerSaveDataId',
        'PlayerSaveData',
        'Id',
        'CASCADE'
    UNION ALL SELECT
        'PlayerWeaponStates',
        'FK_PlayerWeaponStates_FirearmDefinitions_FirearmDefinitionId',
        'FirearmDefinitionId',
        'FirearmDefinitions',
        'Id',
        'RESTRICT'
    UNION ALL SELECT
        'PlayerWeaponStates',
        'FK_PlayerWeaponStates_PlayerSaveData_PlayerSaveDataId',
        'PlayerSaveDataId',
        'PlayerSaveData',
        'Id',
        'CASCADE'
),
actual_foreign_keys AS (
    SELECT
        kcu.table_name,
        kcu.constraint_name,
        kcu.column_name,
        kcu.referenced_table_name,
        kcu.referenced_column_name,
        rc.delete_rule
    FROM information_schema.key_column_usage AS kcu
    JOIN information_schema.referential_constraints AS rc
      ON rc.constraint_schema = kcu.constraint_schema
     AND rc.constraint_name = kcu.constraint_name
     AND rc.table_name = kcu.table_name
    WHERE kcu.table_schema = DATABASE()
      AND kcu.referenced_table_name IS NOT NULL
)
SELECT CONCAT(
    'COLUMN_MISMATCH expected ',
    expected.table_name, '.', expected.column_name, ' ',
    expected.column_type, ' nullable=', expected.is_nullable)
FROM expected_columns AS expected
LEFT JOIN actual_columns AS actual
  ON actual.table_name = expected.table_name
 AND actual.column_name = expected.column_name
 AND actual.column_type = expected.column_type
 AND actual.is_nullable = expected.is_nullable
WHERE actual.column_name IS NULL
UNION ALL
SELECT CONCAT(
    'COLUMN_UNEXPECTED actual ',
    actual.table_name, '.', actual.column_name, ' ',
    actual.column_type, ' nullable=', actual.is_nullable)
FROM actual_columns AS actual
LEFT JOIN expected_columns AS expected
  ON expected.table_name = actual.table_name
 AND expected.column_name = actual.column_name
 AND expected.column_type = actual.column_type
 AND expected.is_nullable = actual.is_nullable
WHERE expected.column_name IS NULL
UNION ALL
SELECT CONCAT(
    'AUTO_INCREMENT_MISMATCH expected ',
    expected.table_name, '.', expected.column_name)
FROM expected_auto_increment AS expected
LEFT JOIN actual_auto_increment AS actual
  ON actual.table_name = expected.table_name
 AND actual.column_name = expected.column_name
WHERE actual.column_name IS NULL
UNION ALL
SELECT CONCAT(
    'AUTO_INCREMENT_UNEXPECTED actual ',
    actual.table_name, '.', actual.column_name)
FROM actual_auto_increment AS actual
LEFT JOIN expected_auto_increment AS expected
  ON expected.table_name = actual.table_name
 AND expected.column_name = actual.column_name
WHERE expected.column_name IS NULL
  AND actual.table_name <> '__EFMigrationsHistory'
UNION ALL
SELECT CONCAT(
    'CHARSET_MISMATCH actual ',
    columns.table_name, '.', columns.column_name,
    ' charset=', COALESCE(columns.character_set_name, 'NULL'))
FROM information_schema.columns AS columns
WHERE columns.table_schema = DATABASE()
  AND columns.table_name <> '__EFMigrationsHistory'
  AND columns.data_type = 'varchar'
  AND COALESCE(columns.character_set_name, '') <> 'utf8mb4'
UNION ALL
SELECT CONCAT(
    'ENGINE_MISMATCH actual ',
    tables.table_name, ' engine=', COALESCE(tables.engine, 'NULL'))
FROM information_schema.tables AS tables
WHERE tables.table_schema = DATABASE()
  AND tables.table_name <> '__EFMigrationsHistory'
  AND tables.table_type = 'BASE TABLE'
  AND COALESCE(tables.engine, '') <> 'InnoDB'
UNION ALL
SELECT CONCAT(
    'INDEX_MISMATCH expected ',
    expected.table_name, '.', expected.index_name, ' sequence=', expected.seq_in_index,
    ' column=', expected.column_name, ' non_unique=', expected.non_unique)
FROM expected_indexes AS expected
LEFT JOIN actual_indexes AS actual
  ON actual.table_name = expected.table_name
 AND actual.index_name = expected.index_name
 AND actual.non_unique = expected.non_unique
 AND actual.seq_in_index = expected.seq_in_index
 AND actual.column_name = expected.column_name
WHERE actual.index_name IS NULL
UNION ALL
SELECT CONCAT(
    'INDEX_UNEXPECTED actual ',
    actual.table_name, '.', actual.index_name, ' sequence=', actual.seq_in_index,
    ' column=', actual.column_name, ' non_unique=', actual.non_unique)
FROM actual_indexes AS actual
LEFT JOIN expected_indexes AS expected
  ON expected.table_name = actual.table_name
 AND expected.index_name = actual.index_name
 AND expected.non_unique = actual.non_unique
 AND expected.seq_in_index = actual.seq_in_index
 AND expected.column_name = actual.column_name
WHERE expected.index_name IS NULL
UNION ALL
SELECT CONCAT(
    'FOREIGN_KEY_MISMATCH expected ',
    expected.table_name, '.', expected.constraint_name, ' column=', expected.column_name,
    ' references=', expected.referenced_table_name, '.', expected.referenced_column_name,
    ' delete=', expected.delete_rule)
FROM expected_foreign_keys AS expected
LEFT JOIN actual_foreign_keys AS actual
  ON actual.table_name = expected.table_name
 AND actual.constraint_name = expected.constraint_name
 AND actual.column_name = expected.column_name
 AND actual.referenced_table_name = expected.referenced_table_name
 AND actual.referenced_column_name = expected.referenced_column_name
 AND actual.delete_rule = expected.delete_rule
WHERE actual.constraint_name IS NULL
UNION ALL
SELECT CONCAT(
    'FOREIGN_KEY_UNEXPECTED actual ',
    actual.table_name, '.', actual.constraint_name, ' column=', actual.column_name,
    ' references=', actual.referenced_table_name, '.', actual.referenced_column_name,
    ' delete=', actual.delete_rule)
FROM actual_foreign_keys AS actual
LEFT JOIN expected_foreign_keys AS expected
  ON expected.table_name = actual.table_name
 AND expected.constraint_name = actual.constraint_name
 AND expected.column_name = actual.column_name
 AND expected.referenced_table_name = actual.referenced_table_name
 AND expected.referenced_column_name = actual.referenced_column_name
 AND expected.delete_rule = actual.delete_rule
WHERE expected.constraint_name IS NULL;
