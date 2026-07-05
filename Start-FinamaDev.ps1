# Start-FinamaDev.ps1
# Demarrage des services Docker pour developpement local
# Usage: .\scripts\Start-FinamaDev.ps1

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "        DEMARRAGE ENV DE DEVELOPPEMENT FINAMA" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Verifier Docker
Write-Host "Verification de Docker..." -ForegroundColor Yellow
try {
    $dockerStatus = docker ps 2>$null
    Write-Host "OK - Docker est pret" -ForegroundColor Green
} catch {
    Write-Host "ERREUR - Docker n'est pas accessible !" -ForegroundColor Red
    Write-Host "         Demarrez Docker Desktop et reessayez" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Determiner quel fichier docker-compose utiliser
$composeFile = "docker-compose.dev.yml"
if (-not (Test-Path $composeFile)) {
    $composeFile = "docker-compose.prod.yml"
    Write-Host "ATTENTION - docker-compose.dev.yml non trouve, utilisation de docker-compose.prod.yml" -ForegroundColor Yellow
} else {
    Write-Host "Fichier de configuration: $composeFile" -ForegroundColor Gray
}

Write-Host ""

# Demarrer les services
Write-Host "Demarrage des services..." -ForegroundColor Cyan
Write-Host "   - PostgreSQL" -ForegroundColor Gray
Write-Host "   - Redis (optionnel)" -ForegroundColor Gray
Write-Host "   - Monitoring (optionnel)" -ForegroundColor Gray
Write-Host ""

docker compose -f $composeFile up -d

# Verifier que ca a marche
if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "OK - Services demarres avec succes !" -ForegroundColor Green
    Write-Host ""
    
    # Afficher les services
    Write-Host "Etat des services :" -ForegroundColor Cyan
    docker compose -f $composeFile ps
    
    Write-Host ""
    Write-Host "Connexions :" -ForegroundColor Cyan
    Write-Host "   PostgreSQL : localhost:5432" -ForegroundColor Gray
    Write-Host "   Redis      : localhost:6379" -ForegroundColor Gray
    Write-Host "   API        : http://localhost:10000" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "Logs en temps reel :" -ForegroundColor Yellow
    Write-Host "   docker compose -f $composeFile logs -f" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Pour arreter les services :" -ForegroundColor Yellow
    Write-Host "   docker compose -f $composeFile down" -ForegroundColor Gray
    Write-Host ""
    
} else {
    Write-Host ""
    Write-Host "ERREUR lors du demarrage des services !" -ForegroundColor Red
    Write-Host "   Verifiez les erreurs ci-dessus" -ForegroundColor Yellow
    exit 1
}