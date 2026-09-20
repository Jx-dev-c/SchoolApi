using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;

using SchoolApi.Models;

namespace SchoolApi.Tests;

/// <summary>
/// As regras que o sistema promete: turma não estoura capacidade, aluno não
/// entra duas vezes, nota fica entre 0 e 10, e-mail não repete.
/// </summary>
[Collection(ColecaoDaApi.NomeDaColecao)]
public class RegrasDeNegocioTests
{
    private readonly FabricaDaApi _api;

    public RegrasDeNegocioTests(FabricaDaApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task Turma_sem_vaga_recusa_matricula()
    {
        var cliente = await _api.ClienteAdminAsync();

        var turma = await CriarTurmaAsync(cliente, capacidade: 1);
        var (primeiro, segundo) = await DoisAlunosNovosAsync(cliente);

        var entrou = await cliente.PostAsJsonAsync("/api/matriculas", new { alunoId = primeiro, turmaId = turma });
        entrou.StatusCode.Should().Be(HttpStatusCode.Created);

        var recusado = await cliente.PostAsJsonAsync("/api/matriculas", new { alunoId = segundo, turmaId = turma });
        recusado.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Matriculas_simultaneas_nao_estouram_a_capacidade()
    {
        var cliente = await _api.ClienteAdminAsync();

        var turma = await CriarTurmaAsync(cliente, capacidade: 1);
        var (primeiro, segundo) = await DoisAlunosNovosAsync(cliente);

        // contar e depois inserir é corrida: as duas requisições leem "0 de 1"
        // e as duas inserem. A trava é no banco (FOR UPDATE na linha da turma).
        var respostas = await Task.WhenAll(
            cliente.PostAsJsonAsync("/api/matriculas", new { alunoId = primeiro, turmaId = turma }),
            cliente.PostAsJsonAsync("/api/matriculas", new { alunoId = segundo, turmaId = turma }));

        respostas.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        respostas.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        var matriculados = await _api.ComContextoAsync(c => c.Matriculas.CountAsync(m => m.TurmaId == turma));
        matriculados.Should().Be(1);
    }

    [Fact]
    public async Task Aluno_nao_se_matricula_duas_vezes_na_mesma_turma()
    {
        var cliente = await _api.ClienteAdminAsync();

        var turma = await CriarTurmaAsync(cliente, capacidade: 5);
        var (aluno, _) = await DoisAlunosNovosAsync(cliente);

        await cliente.PostAsJsonAsync("/api/matriculas", new { alunoId = aluno, turmaId = turma });
        var repetida = await cliente.PostAsJsonAsync("/api/matriculas", new { alunoId = aluno, turmaId = turma });

        repetida.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(-1)]
    public async Task Nota_fora_da_faixa_e_recusada(decimal valor)
    {
        var cliente = await _api.ClienteAdminAsync();
        var matricula = await _api.ComContextoAsync(c => c.Matriculas.Select(m => m.Id).FirstAsync());

        var resposta = await cliente.PutAsJsonAsync($"/api/matriculas/{matricula}/notas",
            new { bimestre = 1, valor });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Bimestre_fora_da_faixa_e_recusado(int bimestre)
    {
        var cliente = await _api.ClienteAdminAsync();
        var matricula = await _api.ComContextoAsync(c => c.Matriculas.Select(m => m.Id).FirstAsync());

        var resposta = await cliente.PutAsJsonAsync($"/api/matriculas/{matricula}/notas",
            new { bimestre, valor = 7m });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Email_de_aluno_nao_se_repete()
    {
        var cliente = await _api.ClienteAdminAsync();
        var email = $"repetido-{Guid.NewGuid():N}@aluno.test";

        var primeiro = await cliente.PostAsJsonAsync("/api/alunos",
            new { nome = "Primeiro", email, dataNascimento = "2010-05-01" });
        primeiro.StatusCode.Should().Be(HttpStatusCode.Created);

        var segundo = await cliente.PostAsJsonAsync("/api/alunos",
            new { nome = "Segundo", email, dataNascimento = "2011-06-02" });
        segundo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Turma_com_professor_inexistente_e_recusada()
    {
        var cliente = await _api.ClienteAdminAsync();

        var resposta = await cliente.PostAsJsonAsync("/api/turmas",
            new { nome = $"Turma {Guid.NewGuid():N}", anoLetivo = 2026, capacidade = 10, professorId = 9999 });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<int> CriarTurmaAsync(HttpClient cliente, int capacidade)
    {
        var professorId = await _api.ComContextoAsync(c => c.Professores.Select(p => p.Id).FirstAsync());

        var resposta = await cliente.PostAsJsonAsync("/api/turmas", new
        {
            nome = $"Turma {Guid.NewGuid():N}"[..20],
            anoLetivo = 2026,
            capacidade,
            professorId,
        });
        resposta.EnsureSuccessStatusCode();

        var turma = await resposta.Content.ReadFromJsonAsync<TurmaDoTeste>();
        return turma!.Id;
    }

    private async Task<(int Primeiro, int Segundo)> DoisAlunosNovosAsync(HttpClient cliente)
    {
        var ids = new List<int>();
        for (var i = 0; i < 2; i++)
        {
            var resposta = await cliente.PostAsJsonAsync("/api/alunos", new
            {
                nome = $"Aluno Teste {i}",
                email = $"teste-{Guid.NewGuid():N}@aluno.test",
                dataNascimento = "2010-03-04",
            });
            resposta.EnsureSuccessStatusCode();
            var aluno = await resposta.Content.ReadFromJsonAsync<AlunoDoTeste>();
            ids.Add(aluno!.Id);
        }

        return (ids[0], ids[1]);
    }
}

public record AlunoDoTeste(int Id, string Nome, string Email, DateOnly DataNascimento, DateTime DataCriacao);
