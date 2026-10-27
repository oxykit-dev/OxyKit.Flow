param(
    [string]$MigrationName
)

if (-not $MigrationName) {
    if ($args.Count -gt 0) {
        $MigrationName = $args[0]
    }
}

if (-not $MigrationName) {
    throw "MigrationName parameter is required. Usage: .\AddEfMigration.Sqlite.ps1 MigrationName"
}

$project = "src/OxyKit.Flow.Infrastructure.Sqlite"
$startupProject = "src/OxyKit.Flow"
$context = "FlowDbContext"

$env:DatabaseProvider = "SQLite"
$env:ConnectionStrings__SQLiteConnection = "Data Source=data/oxykit-flow-mig.db"

dotnet ef migrations add $MigrationName `
    --project $project `
    --startup-project $startupProject `
    --context $context `
    --output-dir Migrations

if ($LASTEXITCODE -eq 0) {
    Write-Host "Migration '$MigrationName' created successfully in $project" -ForegroundColor Green
} else {
    Write-Host "Failed to create migration. Check the error above." -ForegroundColor Red
}
