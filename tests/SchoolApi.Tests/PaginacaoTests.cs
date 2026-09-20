using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;

namespace SchoolApi.Tests;

[Collection(ColecaoDaApi.NomeDaColecao)]
public class PaginacaoTests
{
    private readonly FabricaDaApi _api;

    public PaginacaoTests(FabricaDaApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task Listagem_respeita_o_tamanho_da_pagina()
    {
        var cliente = await _api.ClienteAdminAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDoTeste<AlunoDoTeste>>("/api/alunos?pagina=1&tamanho=10");

        pagina!.Itens.Should().HaveCount(10);
        pagina.Total.Should().BeGreaterThan(10);
        pagina.TemProxima.Should().BeTrue();
    }

    [Fact]
    public async Task Paginas_diferentes_trazem_alunos_diferentes()
    {
        var cliente = await _api.ClienteAdminAsync();

        var primeira = await cliente.GetFromJsonAsync<PaginaDoTeste<AlunoDoTeste>>("/api/alunos?pagina=1&tamanho=5");
        var segunda = await cliente.GetFromJsonAsync<PaginaDoTeste<AlunoDoTeste>>("/api/alunos?pagina=2&tamanho=5");

        primeira!.Itens.Select(a => a.Id).Should().NotIntersectWith(segunda!.Itens.Select(a => a.Id));
    }

    [Fact]
    public async Task Tamanho_de_pagina_absurdo_e_recusado()
    {
        var cliente = await _api.ClienteAdminAsync();

        // sem teto, o cliente pediria a tabela inteira numa requisição só
        var resposta = await cliente.GetAsync("/api/alunos?pagina=1&tamanho=100000");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Busca_filtra_por_nome()
    {
        var cliente = await _api.ClienteAdminAsync();

        var alvo = await _api.ComContextoAsync(c => c.Alunos.OrderBy(a => a.Id).Select(a => a.Nome).FirstAsync());
        var primeiroNome = alvo.Split(' ')[0];

        var pagina = await cliente.GetFromJsonAsync<PaginaDoTeste<AlunoDoTeste>>(
            $"/api/alunos?busca={Uri.EscapeDataString(primeiroNome)}");

        pagina!.Itens.Should().NotBeEmpty();
        pagina.Itens.Should().OnlyContain(a => a.Nome.Contains(primeiroNome, StringComparison.OrdinalIgnoreCase)
                                               || a.Email.Contains(primeiroNome, StringComparison.OrdinalIgnoreCase));
    }
}

public record PaginaDoTeste<T>(List<T> Itens, int Pagina, int Tamanho, int Total, int TotalDePaginas, bool TemProxima);
