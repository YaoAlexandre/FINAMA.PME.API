# Pre-Deploy-Check.ps1
# Verification pre-deploiement pour Finama sous Windows
# Usage: .\scripts\Pre-Deploy-Check.ps1

$passed = 0
$failed = 0

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "     VERIFICATION PRE-DEPLOIEMENT FINAMA" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# VERIFICATIONS FICHIERS
# ============================================================================
Write-Host "VERIFICATION DES FICHIERS" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Gray

$requiredFiles = @(
    @{ Name = "Dockerfile.prod"; Desc = "Dockerfile pour production" },
    @{ Name = "docker-compose.prod.yml"; Desc = "Docker Compose production" },
    @{ Name = ".env.production"; Desc = "Variables d'environnement" },
    @{ Name = "appsettings.Production.json"; Desc = "Config ASP.NET Core" },
    @{ Name = ".gitignore"; Desc = "Fichier .gitignore" }
)

foreach ($file in $requiredFiles) {
    if (Test-Path $file.Name) {
        Write-Host "OK - $($file.Desc)" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "ERREUR - $($file.Desc) (MANQUANT: $($file.Name))" -ForegroundColor Red
        $failed++
    }
}

Write-Host ""

# ============================================================================
# VERIFICATIONS GIT & SECURITE
# ============================================================================
Write-Host "VERIFICATIONS SECURITE GIT" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Gray

# Verifier que .env.production n'est pas trackee
try {
    $gitCheck = git ls-files 2>$null | Select-String "\.env\.production"
    if ($null -eq $gitCheck) {
        Write-Host "OK - .env.production n'est pas trackee dans Git" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "ERREUR: .env.production est trackee dans Git !" -ForegroundColor Red
        Write-Host "        Executez: git rm --cached .env.production" -ForegroundColor Yellow
        $failed++
    }
} catch {
    Write-Host "ATTENTION - Git non disponible, verification ignoree" -ForegroundColor Yellow
}

# Verifier .gitignore
if (Test-Path ".gitignore") {
    $gitignoreContent = Get-Content ".gitignore"
    if ($gitignoreContent -match "\.env\.production" -or $gitignoreContent -match "\.env\.\*") {
        Write-Host "OK - .env.production est dans .gitignore" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "ATTENTION - .env.production n'est pas dans .gitignore" -ForegroundColor Yellow
        Write-Host "            Ajoutez cette ligne a .gitignore :" -ForegroundColor Gray
        Write-Host "            .env.production" -ForegroundColor Gray
        $failed++
    }
}

Write-Host ""

# ============================================================================
# VERIFICATIONS DOCKER
# ============================================================================
Write-Host "VERIFICATIONS DOCKER" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Gray

# Verifier Docker
if (Get-Command docker -ErrorAction SilentlyContinue) {
    Write-Host "OK - Docker est installe" -ForegroundColor Green
    try {
        $dockerVersion = docker --version 2>$null
        Write-Host "    $dockerVersion" -ForegroundColor Gray
        $passed++
    } catch {
        Write-Host "    ATTENTION - Impossible de verifier la version" -ForegroundColor Yellow
    }
} else {
    Write-Host "ERREUR - Docker n'est pas installe ou pas dans le PATH" -ForegroundColor Red
    Write-Host "        https://www.docker.com/products/docker-desktop" -ForegroundColor Gray
    $failed++
}

# Verifier Docker Compose
if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
    Write-Host "OK - Docker Compose est installe" -ForegroundColor Green
    $passed++
} else {
    Write-Host "ERREUR - Docker Compose n'est pas trouve" -ForegroundColor Red
    $failed++
}

# Verifier que Docker est en cours d'execution
try {
    $dockerStatus = docker ps 2>$null
    Write-Host "OK - Docker est en cours d'execution" -ForegroundColor Green
    $passed++
} catch {
    Write-Host "ERREUR - Docker ne semble pas etre en cours d'execution" -ForegroundColor Red
    Write-Host "        Demarrez Docker Desktop" -ForegroundColor Yellow
    $failed++
}

Write-Host ""

# ============================================================================
# VERIFICATIONS CONFIGURATION
# ============================================================================
Write-Host "VERIFICATIONS CONFIGURATION" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Gray

if (Test-Path ".env.production") {
    $envContent = Get-Content ".env.production"
    
    $requiredVars = @(
        "ASPNETCORE_ENVIRONMENT",
        "POSTGRES_DATABASE",
        "POSTGRES_USER",
        "POSTGRES_PASSWORD",
        "JWT_SECRET_KEY",
        "RESEND_API_KEY",
        "EMAIL_FROM_ADDRESS"
    )
    
    foreach ($var in $requiredVars) {
        $varExists = $envContent | Select-String "^$var=" -Quiet
        
        if ($varExists) {
            $value = ($envContent | Select-String "^$var=").Line.Split("=")[1]
            
            if ($value -match "^(YOUR_|PLACEHOLDER|REMPLACER|CHANGE_ME|XXXXX)" -or [string]::IsNullOrWhiteSpace($value)) {
                Write-Host "ATTENTION - $var semble etre un placeholder" -ForegroundColor Yellow
                $failed++
            } else {
                Write-Host "OK - $var configuree" -ForegroundColor Green
                $passed++
            }
        } else {
            Write-Host "ERREUR - $var manquante" -ForegroundColor Red
            $failed++
        }
    }
}

Write-Host ""

# ============================================================================
# VERIFICATIONS DOCKERFILE
# ============================================================================
Write-Host "VERIFICATIONS DOCKERFILE" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Gray

if (Test-Path "Dockerfile.prod") {
    $dockerfileContent = Get-Content "Dockerfile.prod"
    
    # Verifier dependances locales
    if ($dockerfileContent -match "Finama\.Core" -and $dockerfileContent -match "Finama\.Infrastructure") {
        Write-Host "OK - Dependances locales incluses (Core, Infrastructure)" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "ERREUR - Dependances locales manquantes dans Dockerfile" -ForegroundColor Red
        $failed++
    }
    
    # Verifier Production
    if ($dockerfileContent -match "ASPNETCORE_ENVIRONMENT=Production") {
        Write-Host "OK - Environnement Production configure" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "ATTENTION - Environnement Production peut ne pas etre configure" -ForegroundColor Yellow
        $failed++
    }
    
    # Verifier HEALTHCHECK
    if ($dockerfileContent -match "HEALTHCHECK") {
        Write-Host "OK - HEALTHCHECK configure" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "ATTENTION - Pas de HEALTHCHECK dans Dockerfile" -ForegroundColor Yellow
        $failed++
    }
}

Write-Host ""

# ============================================================================
# VERIFICATIONS DOCKER COMPOSE
# ============================================================================
Write-Host "VERIFICATIONS DOCKER COMPOSE" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Gray

if (Test-Path "docker-compose.prod.yml") {
    $composeContent = Get-Content "docker-compose.prod.yml" -Raw
    
    # Verifier PostgreSQL
    if ($composeContent -match "postgres:") {
        Write-Host "OK - Service PostgreSQL configure" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "ERREUR - Service PostgreSQL manquant" -ForegroundColor Red
        $failed++
    }
    
    # Verifier volumes
    if ($composeContent -match "finama-uploads:" -and $composeContent -match "postgres-data:") {
        Write-Host "OK - Volumes persistants configures" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "ATTENTION - Verifiez les volumes persistants" -ForegroundColor Yellow
        $failed++
    }
}

Write-Host ""

# ============================================================================
# VERIFICATIONS PROJETS .NET
# ============================================================================
Write-Host "VERIFICATIONS PROJETS .NET" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Gray

$csprojFiles = @(
    @{ Name = "Finama.API/Finama.API.csproj"; Desc = "Finama.API" },
    @{ Name = "Finama.Core/Finama.Core.csproj"; Desc = "Finama.Core" },
    @{ Name = "Finama.Infrastructure/Finama.Infrastructure.csproj"; Desc = "Finama.Infrastructure" }
)

foreach ($proj in $csprojFiles) {
    if (Test-Path $proj.Name) {
        Write-Host "OK - $($proj.Desc) trouve" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "ERREUR - $($proj.Desc) n'existe pas" -ForegroundColor Red
        $failed++
    }
}

Write-Host ""

# ============================================================================
# RESUME
# ============================================================================
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "                     RESUME" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "OK Reussi: $passed" -ForegroundColor Green
Write-Host "ERREUR Echoue: $failed" -ForegroundColor Red
Write-Host ""

if ($failed -eq 0) {
    Write-Host "OK - TOUT EST PRET POUR LE DEPLOIEMENT !" -ForegroundColor Green
    Write-Host ""
    Write-Host "Prochaines etapes:" -ForegroundColor Cyan
    Write-Host "  1. Tester localement: docker compose -f docker-compose.prod.yml up -d" -ForegroundColor Gray
    Write-Host "  2. Verifier les logs: docker compose -f docker-compose.prod.yml logs -f" -ForegroundColor Gray
    Write-Host "  3. Deployer sur le serveur" -ForegroundColor Gray
    Write-Host ""
    exit 0
} else {
    Write-Host "ERREUR - PROBLEMES DETECCES - A CORRIGER AVANT DEPLOIEMENT" -ForegroundColor Red
    Write-Host ""
    exit 1
}