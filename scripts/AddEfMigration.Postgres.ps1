param(
    [string]$MigrationName
)

if (-not $MigrationName) {
    if ($args.Count -gt 0) {
        $MigrationName = $args[0]
    }
}

if (-not $MigrationName) {
    throw "MigrationName parameter is required. Usage: .\AddEfMigration.Postgres.ps1 MigrationName"
}

$project = "src/OxyKit.Flow.Infrastructure.Postgres"
$startupProject = "src/OxyKit.Flow"
$context = "FlowDbContext"

$env:DatabaseProvider = "PostgreSQL"
$env:ConnectionStrings__PostgreSQLConnection = "Host=localhost;Database=oxykit_flow_mig;Username=oxykit;Password=oxykit"

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
