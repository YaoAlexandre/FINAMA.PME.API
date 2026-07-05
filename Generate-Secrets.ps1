# Generate-Secrets.ps1
# Genération sécurisée des secrets pour Finama
# Usage: .\scripts\Generate-Secrets.ps1

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "       GENERATION DES SECRETS FINAMA" -ForegroundColor Cyan
Write-Host "       Secrets generes aleatoirement et securises" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. JWT Secret (Base64, 64+ caractères)
Write-Host "1. Generation JWT Secret..." -ForegroundColor Yellow
$jwtBytes = [System.Text.Encoding]::UTF8.GetBytes((New-Guid).ToString() + (New-Guid).ToString() + (Get-Random).ToString())
$jwtSecret = [Convert]::ToBase64String($jwtBytes)

Write-Host ""
Write-Host "JWT Secret:" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Gray
Write-Host $jwtSecret -ForegroundColor Yellow
Write-Host ""

# 2. PostgreSQL Password (20 caractères aléatoires sécurisés)
Write-Host "2. Generation PostgreSQL Password..." -ForegroundColor Yellow
$charArray = ((65..90) + (97..122) + (48..57) + (33, 35, 36, 37, 38, 42, 43, 45, 46, 61, 95))
$postgresPassword = -join ((Get-Random -InputObject $charArray -Count 20) | ForEach-Object {[char]$_})

Write-Host ""
Write-Host "PostgreSQL Password:" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Gray
Write-Host $postgresPassword -ForegroundColor Yellow
Write-Host ""

# 3. Redis Password (32 caractères hex)
Write-Host "3. Generation Redis Password..." -ForegroundColor Yellow
$redisBytes = [byte[]]::new(16)
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($redisBytes)
$redisPassword = -join ($redisBytes | ForEach-Object { "{0:x2}" -f $_ })

Write-Host ""
Write-Host "Redis Password:" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Gray
Write-Host $redisPassword -ForegroundColor Yellow
Write-Host ""

# 4. Information Resend (doit être manuel)
Write-Host "4. Resend API Key" -ForegroundColor Yellow
Write-Host ""
Write-Host "ATTENTION - API Key Resend:" -ForegroundColor Red
Write-Host "================================================================" -ForegroundColor Gray
Write-Host "Recuperer depuis : https://resend.com/api-keys" -ForegroundColor Cyan
Write-Host "Format: re_XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX" -ForegroundColor Gray
Write-Host ""

# 5. Créer le fichier .env.production
Write-Host "5. Creation du fichier .env.production..." -ForegroundColor Yellow

$envContent = @"
# ================================================================
# FINAMA - Configuration Production
# ATTENTION: NE JAMAIS COMMITER DANS GIT !
# ================================================================

# Environnement
ASPNETCORE_ENVIRONMENT=Production

# --- BASE DE DONNEES POSTGRESQL ---
POSTGRES_DATABASE=finamadb
POSTGRES_USER=finamadb_user
POSTGRES_PASSWORD=$postgresPassword

# --- SECURITE JWT ---
JWT_SECRET_KEY=$jwtSecret

# --- EMAILS RESEND ---
# Recuperer ta cle API depuis https://resend.com/api-keys
RESEND_API_KEY=re_REMPLACER_PAR_TA_VRAIE_CLE_ICI
EMAIL_FROM_ADDRESS=noreply@votredomaine.com

# --- REDIS (optionnel, pour cache) ---
REDIS_PASSWORD=$redisPassword
"@

# Sauvegarder le fichier
try {
    $envContent | Out-File -FilePath ".env.production" -Encoding UTF8
    Write-Host ""
    Write-Host "OK - Fichier .env.production cree avec succes !" -ForegroundColor Green
    Write-Host ""
    Write-Host "Chemin: $(Get-Item .env.production).FullName" -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host ""
    Write-Host "ERREUR lors de la creation du fichier :" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

# 6. Avertissements de sécurité
Write-Host "SECURITE - ETAPES CRITIQUES" -ForegroundColor Red
Write-Host "================================================================" -ForegroundColor Gray
Write-Host ""
Write-Host "1. Editer .env.production et remplacer RESEND_API_KEY" -ForegroundColor Yellow
Write-Host "   IMPORTANT: notepad .env.production (ou ton editeur)" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Verifier que .env.production est dans .gitignore" -ForegroundColor Yellow
Write-Host "   Command: type .gitignore | findstr .env.production" -ForegroundColor Gray
Write-Host ""
Write-Host "3. SI .env.production a ete commite avant :" -ForegroundColor Yellow
Write-Host "   git rm --cached .env.production" -ForegroundColor Red
Write-Host "   git commit -m 'Remove secrets from history'" -ForegroundColor Red
Write-Host ""
Write-Host "4. NE JAMAIS partager .env.production" -ForegroundColor Yellow
Write-Host "   Stocker dans gestionnaire de secrets (1Password, Vault)" -ForegroundColor Gray
Write-Host ""

Write-Host "OK - SECRETS GENERES AVEC SUCCES !" -ForegroundColor Green
Write-Host ""
Write-Host "Prochaines etapes :" -ForegroundColor Cyan
Write-Host "  1. Editer .env.production (ajouter ta cle Resend)" -ForegroundColor Gray
Write-Host "  2. Executer : .\Pre-Deploy-Check.ps1" -ForegroundColor Gray
Write-Host "  3. Tester : docker compose -f docker-compose.dev.yml up -d" -ForegroundColor Gray
Write-Host ""