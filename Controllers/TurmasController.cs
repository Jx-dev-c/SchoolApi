using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SchoolApi.Dtos;
using SchoolApi.Models;

namespace SchoolApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TurmasController : ControllerBase
{
    private readonly AppDbContext _contexto;

    public TurmasController(AppDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <summary>
    /// Lista as turmas com professor e ocupação.
    /// </summary>
    /// <remarks>
    /// A projeção direta para o DTO (<c>Select</c> antes do <c>ToListAsync</c>)
    /// é o ponto: o SQL sai com o JOIN e a contagem já resolvidos, uma consulta
    /// só. Carregar as entidades e contar em memória traria todas as matrículas
    /// para o processo — e sem <c>Include</c> viraria uma consulta por turma,
    /// que é o problema N+1.
    ///
    /// <c>AsNoTracking</c> porque nada aqui será alterado: evita o custo de o EF
    /// guardar snapshot de cada linha para detectar mudança.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TurmaResponse>>> Listar([FromQuery] int? anoLetivo)
    {
        var consulta = _contexto.Turmas.AsNoTracking();

        if (anoLetivo is not null)
        {
            consulta = consulta.Where(t => t.AnoLetivo == anoLetivo);
        }

        var turmas = await consulta
            .OrderBy(t => t.AnoLetivo)
            .ThenBy(t => t.Nome)
            .Select(t => new TurmaResponse(
                t.Id,
                t.Nome,
                t.AnoLetivo,
                t.Capacidade,
                t.Matriculas.Count(m => m.Situacao == SituacaoMatricula.Ativa),
                t.Capacidade - t.Matriculas.Count(m => m.Situacao == SituacaoMatricula.Ativa),
                t.Professor!.Nome,
                t.Professor!.Disciplina))
            .ToListAsync();

        return Ok(turmas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TurmaResponse>> BuscarPorId(int id)
    {
        var turma = await _contexto.Turmas
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TurmaResponse(
                t.Id,
                t.Nome,
                t.AnoLetivo,
                t.Capacidade,
                t.Matriculas.Count(m => m.Situacao == SituacaoMatricula.Ativa),
                t.Capacidade - t.Matriculas.Count(m => m.Situacao == SituacaoMatricula.Ativa),
                t.Professor!.Nome,
                t.Professor!.Disciplina))
            .FirstOrDefaultAsync();

        return turma is null ? NotFound() : Ok(turma);
    }

    [HttpPost]
    public async Task<ActionResult<TurmaResponse>> Criar(CriarTurmaRequest requisicao)
    {
        // o professor precisa existir: sem esta checagem o erro viria do banco
        // como violação de chave estrangeira, com status 500 em vez de 400
        var professor = await _contexto.Professores.FindAsync(requisicao.ProfessorId);
        if (professor is null)
        {
            ModelState.AddModelError(nameof(requisicao.ProfessorId), "Professor não encontrado");
            return ValidationProblem(ModelState);
        }

        var jaExiste = await _contexto.Turmas
            .AnyAsync(t => t.AnoLetivo == requisicao.AnoLetivo && t.Nome == requisicao.Nome);
        if (jaExiste)
        {
            ModelState.AddModelError(nameof(requisicao.Nome), "Já existe turma com esse nome neste ano letivo");
            return ValidationProblem(ModelState);
        }

        var turma = new Turma
        {
            Nome = requisicao.Nome,
            AnoLetivo = requisicao.AnoLetivo,
            Capacidade = requisicao.Capacidade,
            ProfessorId = requisicao.ProfessorId,
            DataCriacao = DateTime.UtcNow,
        };

        _contexto.Turmas.Add(turma);
        await _contexto.SaveChangesAsync();

        var resposta = new TurmaResponse(
            turma.Id, turma.Nome, turma.AnoLetivo, turma.Capacidade,
            0, turma.Capacidade, professor.Nome, professor.Disciplina);

        return CreatedAtAction(nameof(BuscarPorId), new { id = turma.Id }, resposta);
    }
}
