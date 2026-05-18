using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Finama.Infrastructure.Services;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string codeOtp);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendOtpEmailAsync(string toEmail, string codeOtp)
    {
        var smtpHost = "smtp.gmail.com";
        var smtpPort = 587;

        var emailEmetteur = _configuration["EmailSettings:Username"];
        var passwordApp = _configuration["EmailSettings:Password"];

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            Credentials = new NetworkCredential(emailEmetteur, passwordApp),
            EnableSsl = true
        };

        // 🎨 UI/UX Template Moderne et Épuré pour FINAMA
        var htmlBody = $@"
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

        var mailMessage = new MailMessage
        {
            From = new MailAddress(emailEmetteur!, "FINAMA Sécurité"),
            Subject = $"[{codeOtp}] Votre code de vérification FINAMA", // On met le code dans l'objet pour un accès rapide sans ouvrir le mail !
            Body = htmlBody,
            IsBodyHtml = true
        };

        mailMessage.To.Add(toEmail);

        await client.SendMailAsync(mailMessage);
    }
}
