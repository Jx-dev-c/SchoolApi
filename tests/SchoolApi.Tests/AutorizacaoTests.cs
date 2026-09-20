using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;

using SchoolApi.Models;

namespace SchoolApi.Tests;

/// <summary>
/// Papel diz quem a pessoa é; posse diz o que é dela. Os dois precisam de
/// teste: numa primeira versão os claims de professor e aluno eram emitidos
/// e nunca lidos, e todo mundo enxergava todas as turmas.
/// </summary>
[Collection(ColecaoDaApi.NomeDaColecao)]
public class AutorizacaoTests
{
    private readonly FabricaDaApi _api;

    public AutorizacaoTests(FabricaDaApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task Aluno_nao_cria_turma()
    {
        var cliente = await _api.ClienteAlunoAsync();

        var resposta = await cliente.PostAsJsonAsync("/api/turmas",
            new { nome = "Turma do aluno", anoLetivo = 2026, capacidade = 10, professorId = 1 });

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Professor_nao_acessa_area_de_contas()
    {
        var cliente = await _api.ClienteProfessorAsync();

        var resposta = await cliente.GetAsync("/api/usuarios");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Professor_ve_apenas_as_proprias_turmas()
    {
        var cliente = await _api.ClienteProfessorAsync();

        var turmas = await cliente.GetFromJsonAsync<List<TurmaDoTeste>>("/api/turmas");

        turmas.Should().NotBeEmpty();
        var professorId = await _api.ComContextoAsync(async c =>
            (await c.Usuarios.FirstAsync(u => u.Email == "professor@escola.test")).ProfessorId!.Value);

        var idsDasTurmasDele = await _api.ComContextoAsync(c =>
            c.Turmas.Where(t => t.ProfessorId == professorId).Select(t => t.Id).ToListAsync());

        turmas!.Select(t => t.Id).Should().BeEquivalentTo(idsDasTurmasDele);
    }

    [Fact]
    public async Task Admin_ve_todas_as_turmas()
    {
        var cliente = await _api.ClienteAdminAsync();

        var turmas = await cliente.GetFromJsonAsync<List<TurmaDoTeste>>("/api/turmas");
        var total = await _api.ComContextoAsync(c => c.Turmas.CountAsync());

        turmas!.Count.Should().Be(total);
    }

    [Fact]
    public async Task Professor_nao_abre_turma_de_outro_professor()
    {
        var cliente = await _api.ClienteProfessorAsync();

        var professorId = await _api.ComContextoAsync(async c =>
            (await c.Usuarios.FirstAsync(u => u.Email == "professor@escola.test")).ProfessorId!.Value);

        var turmaAlheia = await _api.ComContextoAsync(c =>
            c.Turmas.Where(t => t.ProfessorId != professorId).Select(t => t.Id).FirstAsync());

        var resposta = await cliente.GetAsync($"/api/turmas/{turmaAlheia}");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Aluno_nao_le_boletim_de_outro_aluno()
    {
        var cliente = await _api.ClienteAlunoAsync();

        var alunoId = await _api.ComContextoAsync(async c =>
            (await c.Usuarios.FirstAsync(u => u.Email == "aluno@escola.test")).AlunoId!.Value);

        var matriculaAlheia = await _api.ComContextoAsync(c =>
            c.Matriculas.Where(m => m.AlunoId != alunoId).Select(m => m.Id).FirstAsync());

        var resposta = await cliente.GetAsync($"/api/matriculas/{matriculaAlheia}/boletim");

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Aluno_le_o_proprio_boletim()
    {
        var cliente = await _api.ClienteAlunoAsync();

        var alunoId = await _api.ComContextoAsync(async c =>
            (await c.Usuarios.FirstAsync(u => u.Email == "aluno@escola.test")).AlunoId!.Value);

        var minhaMatricula = await _api.ComContextoAsync(c =>
            c.Matriculas.Where(m => m.AlunoId == alunoId).Select(m => m.Id).FirstAsync());

        var boletim = await cliente.GetFromJsonAsync<BoletimDoTeste>($"/api/matriculas/{minhaMatricula}/boletim");

        boletim!.Notas.Should().NotBeEmpty();
        boletim.Media.Should().BeInRange(0, 10);
        boletim.FrequenciaPercentual.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task Professor_nao_lanca_nota_em_turma_alheia()
    {
        var cliente = await _api.ClienteProfessorAsync();

        var professorId = await _api.ComContextoAsync(async c =>
            (await c.Usuarios.FirstAsync(u => u.Email == "professor@escola.test")).ProfessorId!.Value);

        var matriculaAlheia = await _api.ComContextoAsync(c =>
            c.Matriculas.Include(m => m.Turma)
                        .Where(m => m.Turma!.ProfessorId != professorId)
                        .Select(m => m.Id)
                        .FirstAsync());

        var resposta = await cliente.PutAsJsonAsync($"/api/matriculas/{matriculaAlheia}/notas",
            new { bimestre = 4, valor = 10m });

        resposta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Professor_lanca_nota_na_propria_turma()
    {
        var cliente = await _api.ClienteProfessorAsync();

        var professorId = await _api.ComContextoAsync(async c =>
            (await c.Usuarios.FirstAsync(u => u.Email == "professor@escola.test")).ProfessorId!.Value);

        var minhaMatricula = await _api.ComContextoAsync(c =>
            c.Matriculas.Include(m => m.Turma)
                        .Where(m => m.Turma!.ProfessorId == professorId)
                        .Select(m => m.Id)
                        .FirstAsync());

        var resposta = await cliente.PutAsJsonAsync($"/api/matriculas/{minhaMatricula}/notas",
            new { bimestre = 4, valor = 9.5m });

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);

        // relançar o mesmo bimestre atualiza, não duplica
        var correcao = await cliente.PutAsJsonAsync($"/api/matriculas/{minhaMatricula}/notas",
            new { bimestre = 4, valor = 8m });
        correcao.StatusCode.Should().Be(HttpStatusCode.OK);

        var quantidade = await _api.ComContextoAsync(c =>
            c.Notas.CountAsync(n => n.MatriculaId == minhaMatricula && n.Bimestre == 4));
        quantidade.Should().Be(1);
    }
}

public record TurmaDoTeste(int Id, string Nome, int AnoLetivo, int Capacidade, int Matriculados, int VagasDisponiveis, string ProfessorNome, string Disciplina);

public record BoletimDoTeste(int MatriculaId, string AlunoNome, string TurmaNome, List<NotaDoTeste> Notas, decimal? Media, double? FrequenciaPercentual);

public record NotaDoTeste(int Bimestre, decimal Valor, DateTime DataLancamento);
