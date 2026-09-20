using Microsoft.EntityFrameworkCore;
using SchoolApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add services to the container
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Migrations e seed na subida: em desenvolvimento, `docker compose up` seguido
// de `dotnet run` deixa a base pronta e povoada, sem passo manual. Em produção
// isso sairia daqui — migration na subida da aplicação é aceitável em projeto
// de uma pessoa, mas vira corrida quando há mais de uma instância.
using (var escopo = app.Services.CreateScope())
{
    var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
    await contexto.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        await SeedDados.AplicarAsync(contexto);
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // HTTPS obrigatorio apenas fora de desenvolvimento.
    // Em dev o certificado e auto-assinado e quebra chamadas server-side (SSR).
    app.UseHttpsRedirection();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();