using System.Net;
using System.Net.Http.Json;

namespace SchoolApi.Tests;

[Collection(ColecaoDaApi.NomeDaColecao)]
public class AutenticacaoTests
{
    private readonly FabricaDaApi _api;

    public AutenticacaoTests(FabricaDaApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task Login_com_credenciais_validas_devolve_par_de_tokens()
    {
        var cliente = _api.CreateClient();

        var resposta = await cliente.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@escola.test", senha = FabricaDaApi.SenhaPadrao });

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessao = await resposta.Content.ReadFromJsonAsync<RespostaDeSessao>();
        sessao!.AccessToken.Should().NotBeNullOrWhiteSpace();
        sessao.RefreshToken.Should().NotBeNullOrWhiteSpace();
        sessao.Usuario.Papel.Should().Be("Admin");
    }

    [Theory]
    [InlineData("admin@escola.test", "senha-errada-123")]
    [InlineData("naoexiste@escola.test", "escola@2026")]
    public async Task Login_invalido_responde_sempre_igual(string email, string senha)
    {
        var cliente = _api.CreateClient();

        var resposta = await cliente.PostAsJsonAsync("/api/auth/login", new { email, senha });

        // mesma resposta para senha errada e conta inexistente: diferenciar
        // entregaria quais e-mails existem na base
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_protegido_sem_token_responde_401()
    {
        var cliente = _api.CreateClient();

        var resposta = await cliente.GetAsync("/api/turmas");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_rotaciona_o_token()
    {
        var cliente = _api.CreateClient();
        var sessao = await LogarAsync(cliente, "professor@escola.test");

        var resposta = await cliente.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = sessao.RefreshToken });

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var nova = await resposta.Content.ReadFromJsonAsync<RespostaDeSessao>();
        nova!.RefreshToken.Should().NotBe(sessao.RefreshToken);
    }

    [Fact]
    public async Task Refresh_reapresentado_derruba_a_familia_de_tokens()
    {
        var cliente = _api.CreateClient();
        var sessao = await LogarAsync(cliente, "aluno@escola.test");

        var primeira = await cliente.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = sessao.RefreshToken });
        var nova = await primeira.Content.ReadFromJsonAsync<RespostaDeSessao>();

        // reapresentar o token já usado é sinal de cópia
        var reuso = await cliente.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = sessao.RefreshToken });
        reuso.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // e a sessão inteira cai junto, inclusive o token recém-emitido
        var depois = await cliente.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = nova!.RefreshToken });
        depois.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_simultaneo_com_o_mesmo_token_deixa_so_um_passar()
    {
        var cliente = _api.CreateClient();
        var sessao = await LogarAsync(cliente, "admin@escola.test");

        // duas requisições disparadas juntas: ler o estado e revogar depois
        // deixava as duas passarem, cada uma com um par válido, e o alarme de
        // reuso nunca disparava
        var tarefas = Enumerable.Range(0, 2)
            .Select(_ => cliente.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = sessao.RefreshToken }))
            .ToArray();

        var respostas = await Task.WhenAll(tarefas);
        var codigos = respostas.Select(r => r.StatusCode).OrderBy(c => c).ToArray();

        codigos.Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Unauthorized]);
    }

    [Fact]
    public async Task Token_com_assinatura_adulterada_responde_401()
    {
        var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Add("Authorization",
            "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIiwicm9sZSI6IkFkbWluIn0.assinatura-falsa");

        var resposta = await cliente.GetAsync("/api/turmas");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<RespostaDeSessao> LogarAsync(HttpClient cliente, string email)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/auth/login",
            new { email, senha = FabricaDaApi.SenhaPadrao });
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<RespostaDeSessao>())!;
    }
}
