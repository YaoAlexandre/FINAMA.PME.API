using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// Namespaces pour MailKit (Nouveau)
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Finama.Infrastructure.Services;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string codeOtp);
    Task SendOtpEmailByApiAsync(string toEmail, string codeOtp);
    Task SendOtpEmailByMailKitAsync(string toEmail, string codeOtp); // La nouvelle méthode
    Task SendOtpEmailByResendAsync(string toEmail, string codeOtp); // La nouvelle méthode
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Option A : Envoi traditionnel via System.Net.Mail.SmtpClient (Port 465 / 587)
    /// </summary>
    public async Task SendOtpEmailAsync(string toEmail, string codeOtp)
    {
        var smtpHost = "smtp.gmail.com";
        var smtpPort = 465;

        var emailEmetteur = _configuration["EmailSettings:Username"]
                            ?? Environment.GetEnvironmentVariable("EmailSettings__Username");

        var passwordApp = _configuration["EmailSettings:Password"]
                          ?? Environment.GetEnvironmentVariable("EmailSettings__Password");

        if (string.IsNullOrEmpty(emailEmetteur) || string.IsNullOrEmpty(passwordApp))
        {
            throw new InvalidOperationException("[DIAGNOSTIC SMTP] Échec critique : Identifiants introuvables.");
        }

        using var client = new System.Net.Mail.SmtpClient(smtpHost, smtpPort)
        {
            Credentials = new NetworkCredential(emailEmetteur, passwordApp),
            EnableSsl = true
        };

        var htmlBody = ObtenirTemplateHtml(codeOtp);

        var mailMessage = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress(emailEmetteur!, "FINAMA Sécurité"),
            Subject = $"[{codeOtp}] Votre code de vérification FINAMA",
            Body = htmlBody,
            IsBodyHtml = true
        };
        mailMessage.To.Add(toEmail);

        try
        {
            Console.WriteLine($"[DIAGNOSTIC SMTP] Tentative via SmtpClient standard vers {toEmail}...");
            await client.SendMailAsync(mailMessage);
            Console.WriteLine($"[DIAGNOSTIC SMTP] Succès !");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CRITICAL OTP EMAIL ERROR] Échec SmtpClient :\n{ex.ToString()}");
            throw;
        }
    }

    /// <summary>
    /// Option B : Envoi sécurisé via l'API HTTP de Brevo (Port web 443 - Idéal pour Render)
    /// </summary>
    public async Task SendOtpEmailByApiAsync(string toEmail, string codeOtp)
    {
        var apiKey = _configuration["Brevo:ApiKey"]
                     ?? Environment.GetEnvironmentVariable("Brevo__ApiKey");

        var emailEmetteur = _configuration["EmailSettings:Username"]
                            ?? Environment.GetEnvironmentVariable("EmailSettings__Username");

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("[BREVO API] Clé API introuvable.");
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("api-key", apiKey);

        var htmlBody = ObtenirTemplateHtml(codeOtp);

        var emailData = new
        {
            sender = new { name = "FINAMA Sécurité", email = emailEmetteur },
            to = new[] { new { email = toEmail } },
            subject = $"[{codeOtp}] Votre code de vérification FINAMA",
            htmlContent = htmlBody
        };

        var json = JsonSerializer.Serialize(emailData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            Console.WriteLine($"[BREVO API] Envoi via HTTP à {toEmail}...");
            var response = await client.PostAsync("https://api.brevo.com/v3/smtp/email", content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[BREVO API] Succès !");
            }
            else
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[BREVO API ERROR] Détails : {errorResponse}");
                throw new HttpRequestException($"Erreur Brevo API: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CRITICAL OTP API ERROR] Échec API Brevo :\n{ex.ToString()}");
            throw;
        }
    }

    /// <summary>
    /// Option C : Envoi via MailKit (Port 465 SSL Direct - Parfait pour Gmail sous Linux/Render)
    /// </summary>
    public async Task SendOtpEmailByMailKitAsync(string toEmail, string codeOtp)
    {
        var smtpHost = "smtp.gmail.com";
        var smtpPort = 465; // SSL Implicite

        var emailEmetteur = _configuration["EmailSettings:Username"]
                            ?? Environment.GetEnvironmentVariable("EmailSettings__Username");

        var passwordApp = _configuration["EmailSettings:Password"]
                          ?? Environment.GetEnvironmentVariable("EmailSettings__Password");

        if (string.IsNullOrEmpty(emailEmetteur) || string.IsNullOrEmpty(passwordApp))
        {
            throw new InvalidOperationException("[MAILKIT] Échec : Identifiants introuvables.");
        }

        // Création du message au format MimeKit
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("FINAMA Sécurité", emailEmetteur));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = $"[{codeOtp}] Votre code de vérification FINAMA";

        var bodyBuilder = new BodyBuilder { HtmlBody = ObtenirTemplateHtml(codeOtp) };
        message.Body = bodyBuilder.ToMessageBody();

        // ⚠️ Utilise explicitement le client MailKit.Net.Smtp.SmtpClient
        using var client = new MailKit.Net.Smtp.SmtpClient();
        try
        {
            Console.WriteLine($"[MAILKIT] Connexion SSL Directe à {smtpHost}:{smtpPort}...");
            // SecureSocketOptions.SslOnConnect règle le problème de négociation du port 465
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.SslOnConnect);

            Console.WriteLine("[MAILKIT] Authentification en cours...");
            await client.AuthenticateAsync(emailEmetteur, passwordApp);

            Console.WriteLine($"[MAILKIT] Envoi de l'e-mail à {toEmail}...");
            await client.SendAsync(message);

            Console.WriteLine("[MAILKIT] Succès ! E-mail acheminé par Gmail.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MAILKIT CRITICAL ERROR] Échec de l'envoi :\n{ex.ToString()}");
            throw;
        }
        finally
        {
            // Déconnexion propre du protocole TCP
            await client.DisconnectAsync(true);
        }
    }

    public async Task SendOtpEmailByResendAsync(string toEmail, string codeOtp)
    {
        var apiKey = _configuration["Resend:ApiKey"] ?? Environment.GetEnvironmentVariable("Resend__ApiKey");
        var emailEmetteur = _configuration["EmailSettings:Username"] ?? Environment.GetEnvironmentVariable("EmailSettings__Username");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var htmlBody = ObtenirTemplateHtml(codeOtp);

        var emailData = new
        {
            from = $"FINAMA Sécurité <onboarding@resend.dev>", // Au début, Resend demande d'utiliser leur domaine de test
            to = new[] { toEmail },
            subject = $"[{codeOtp}] Votre code de vérification FINAMA",
            html_content = htmlBody
        };

        var json = JsonSerializer.Serialize(emailData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        Console.WriteLine($"[RESEND API] Envoi de l'OTP via HTTP à {toEmail}...");
        var response = await client.PostAsync("https://api.resend.com/emails", content);

        if (response.IsSuccessStatusCode)
            Console.WriteLine("[RESEND API] Succès !");
        else
            Console.WriteLine($"[RESEND API ERROR] : {await response.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// Centralisation du template HTML pour FINAMA
    /// </summary>
    private static string ObtenirTemplateHtml(string codeOtp)
    {
        return $@"
        <div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f8fafc; padding: 40px 10px; margin: 0; width: 100%;"">
            <div style=""max-width: 500px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05), 0 2px 4px -1px rgba(0,0,0,0.03); border: 1px solid #e2e8f0; overflow: hidden;"">
                <div style=""background-color: #1e293b; padding: 30px; text-align: center;"">
                    <h1 style=""color: #ffffff; margin: 0; font-size: 24px; font-weight: 700; letter-spacing: 1px;"">FINAMA</h1>
                    <p style=""color: #94a3b8; margin: 5px 0 0 0; font-size: 13px; font-weight: 500;"">Sécurité et Gestion Comptable</p>
                </div>
                <div style=""padding: 35px 30px;"">
                    <h2 style=""color: #0f172a; margin: 0 0 15px 0; font-size: 18px; font-weight: 600;"">Vérification de connexion</h2>
                    <p style=""color: #475569; font-size: 14px; line-height: 1.6; margin: 0 0 25px 0;"">
                        Bonjour,<br/><br/>
                        Pour finaliser votre authentification sur votre espace de gestion, veuillez utiliser le code de validation temporaire ci-dessous :
                    </p>
                    <div style=""background-color: #f1f5f9; border-radius: 8px; padding: 20px; text-align: center; margin-bottom: 25px; border: 1px dashed #cbd5e1;"">
                        <span style=""display: block; font-size: 11px; font-weight: 600; color: #64748b; text-transform: uppercase; letter-spacing: 1.5px; margin-bottom: 8px;"">Code de sécurité</span>
                        <span style=""font-size: 32px; font-weight: 700; color: #0284c7; letter-spacing: 6px; font-family: monospace;"">{codeOtp}</span>
                    </div>
                    <p style=""color: #64748b; font-size: 12px; line-height: 1.5; margin: 0;"">
                        ⏱️ Ce code est strictement confidentiel et expirera dans <strong>10 minutes</strong>.<br/>
                        Si vous n'êtes pas à l'origine de cette tentative, vous pouvez ignorer cet e-mail en toute sécurité.
                    </p>
                </div>
                <div style=""background-color: #f8fafc; padding: 20px 30px; text-align: center; border-top: 1px solid #e2e8f0;"">
                    <p style=""color: #94a3b8; font-size: 11px; margin: 0;"">
                        &copy; {DateTime.UtcNow.Year} FINAMA SaaS. Tous droits réservés.<br/>
                        Ceci est un message automatique, merci de ne pas y répondre.
                    </p>
                </div>
            </div>
        </div>";
    }
}