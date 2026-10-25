using Finama.Commerce.Application.Common;
using Finama.Commerce.Infrastructure.Persistence;
using Finama.Commerce.Infrastructure.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finama.Commerce.Infrastructure;

public static class CommerceDependencyInjection
{
    public static IServiceCollection AddFinamaCommerce(this IServiceCollection services, IConfiguration configuration)
    {
        // DbContext SQL Server
        services.AddDbContext<CommerceDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(CommerceDbContext).Assembly.FullName)));

        services.AddScoped<ICommerceDbContext>(sp => sp.GetRequiredService<CommerceDbContext>());

        // Enregistrement automatique des validateurs FluentValidation
        services.AddValidatorsFromAssembly(typeof(ICommerceDbContext).Assembly);

        // Enregistrement des Services Métiers
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IPosService, PosService>();
        services.AddScoped<IMagasinService, MagasinService>();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddSingleton<IPinHasher, Pbkdf2PinHasher>();
        services.AddScoped<IParametresCommerceService, ParametresCommerceService>();
        services.AddScoped<IReferentielCommerceService, ReferentielCommerceService>();
        services.AddScoped<ITableauBordCommerceService, TableauBordCommerceService>();
        services.AddScoped<IJournalVentesService, JournalVentesService>();

        return services;
    }
}