using System.Text;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using SchoolApi.Auth;
using SchoolApi.Models;

var builder = WebApplication.CreateBuilder(args);

// CORS restrito às origens do app. AllowAnyOrigin abria a API para qualquer
// site na internet chamar em nome de quem estivesse logado.
var origensPermitidas = builder.Configuration.GetSection("Cors:Origens").Get<string[]>()
                        ?? ["http://localhost:4200"];

builder.Services.AddCors(opcoes =>
{
    opcoes.AddPolicy("AppAngular", politica =>
        politica.WithOrigins(origensPermitidas)
                .AllowAnyMethod()
                .AllowAnyHeader());
});

builder.Services.AddDbContext<AppDbContext>(opcoes =>
    opcoes.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Valida a configuração do JWT na subida: sem chave (ou com chave curta) a
// aplicação nem sobe, em vez de falhar no primeiro login em produção.
builder.Services.AddOptions<OpcoesJwt>()
    .Bind(builder.Configuration.GetSection(OpcoesJwt.Secao))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<ServicoDeToken>();
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

var opcoesJwt = builder.Configuration.GetSection(OpcoesJwt.Secao).Get<OpcoesJwt>() ?? new OpcoesJwt();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opcoes =>
    {
        opcoes.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = opcoesJwt.Emissor,
            ValidAudience = opcoesJwt.Audiencia,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opcoesJwt.Chave)),
            // sem tolerância: o padrão de 5 minutos faz um token expirado
            // continuar sendo aceito por bem mais tempo do que se imagina
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers()
    // enum como texto: "Professor" no JSON em vez de 1 — o front não precisa
    // conhecer a ordem do enum, e inserir um valor no meio não quebra o cliente
    .AddJsonOptions(opcoes =>
        opcoes.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(opcoes =>
{
    opcoes.SwaggerDoc("v1", new OpenApiInfo { Title = "SchoolApi", Version = "v1" });

    // botão "Authorize" no Swagger: sem isso não dá para testar endpoint
    // protegido pela interface
    opcoes.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Cole apenas o token, sem o prefixo Bearer.",
    });

    // No Microsoft.OpenApi 2.x o requisito aponta para o esquema por referencia
    // tipada, em vez do antigo objeto com Reference preenchida na mao.
    opcoes.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), [] },
    });
});

var app = builder.Build();

// Migrations e seed na subida: em desenvolvimento, `docker compose up` seguido
// de `dotnet run` deixa a base pronta e povoada, sem passo manual. Em produção
// isso sairia daqui — migration na subida é aceitável em projeto de uma pessoa,
// mas vira corrida quando há mais de uma instância.
using (var escopo = app.Services.CreateScope())
{
    var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
    await contexto.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        var hasher = escopo.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        await SeedDados.AplicarAsync(contexto, hasher);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // HTTPS obrigatório apenas fora de desenvolvimento: em dev o certificado é
    // auto-assinado e quebra chamadas server-side (SSR).
    app.UseHttpsRedirection();
}

app.UseCors("AppAngular");

// A ordem importa: autenticação descobre quem é, autorização decide o que pode.
// Antes havia UseAuthorization sozinho, o que não protegia nada.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

/// <summary>Exposto para os testes de integração poderem referenciar o host.</summary>
public partial class Program;
