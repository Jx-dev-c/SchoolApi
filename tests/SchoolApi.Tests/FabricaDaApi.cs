using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using SchoolApi.Models;

using Testcontainers.PostgreSql;

namespace SchoolApi.Tests;

/// <summary>
/// Sobe a API inteira contra um PostgreSQL de verdade, em container.
///
/// Por que não banco em memória: o provedor InMemory do EF não tem índice
/// único, nem transação, nem `FOR UPDATE` — exatamente as coisas que esta API
/// usa para garantir suas regras. Testar contra ele daria verde em bug real.
///
/// O container sobe uma vez por execução e é descartado no fim; cada teste
/// cria os dados de que precisa, para não depender da ordem de execução.
/// </summary>
public class FabricaDaApi : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("schooldb_teste")
        .WithUsername("teste")
        .WithPassword("teste")
        .Build();

    public const string SenhaPadrao = SeedDados.SenhaDemo;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development porque é o ambiente em que o seed roda — os testes
        // dependem dos dados determinísticos dele.
        builder.UseEnvironment("Development");
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        // Variável de ambiente, e não ConfigureAppConfiguration: o Program lê
        // a configuração enquanto monta os serviços, antes de a fábrica aplicar
        // as fontes dela. Com isso os testes chegavam a rodar contra o banco de
        // desenvolvimento (user-secrets), e o limite de login reprovava os
        // próprios testes. Variável de ambiente entra antes e tem precedência
        // sobre user-secrets.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Chave", "chave-de-teste-com-mais-de-32-caracteres-para-hmac");
        Environment.SetEnvironmentVariable("RateLimit__LoginPorMinuto", "1000");

        // primeira resolução do host: é aqui que migrations e seed rodam
        using var escopo = Services.CreateScope();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Cliente já autenticado com uma das contas do seed.</summary>
    public async Task<HttpClient> ClienteComoAsync(string email)
    {
        var cliente = CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new { email, senha = SenhaPadrao });
        resposta.EnsureSuccessStatusCode();

        var sessao = await resposta.Content.ReadFromJsonAsync<RespostaDeSessao>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao!.AccessToken);
        return cliente;
    }

    public Task<HttpClient> ClienteAdminAsync() => ClienteComoAsync("admin@escola.test");

    public Task<HttpClient> ClienteProfessorAsync() => ClienteComoAsync("professor@escola.test");

    public Task<HttpClient> ClienteAlunoAsync() => ClienteComoAsync("aluno@escola.test");

    /// <summary>Acesso direto ao banco, para montar cenário ou conferir efeito.</summary>
    public async Task<T> ComContextoAsync<T>(Func<AppDbContext, Task<T>> acao)
    {
        using var escopo = Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
        return await acao(contexto);
    }
}

public record RespostaDeSessao(string AccessToken, DateTime ExpiraEm, string RefreshToken, UsuarioDaSessao Usuario);

public record UsuarioDaSessao(int Id, string Nome, string Email, string Papel, int? ProfessorId, int? AlunoId);

[CollectionDefinition(NomeDaColecao)]
public class ColecaoDaApi : ICollectionFixture<FabricaDaApi>
{
    public const string NomeDaColecao = "api";
}
