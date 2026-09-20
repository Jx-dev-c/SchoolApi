using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SchoolApi.Auth;
using SchoolApi.Dtos;
using SchoolApi.Models;

namespace SchoolApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    public async Task<ActionResult<IReadOnlyList<TurmaResponse>>> Listar(
        [FromQuery] int? anoLetivo,
        CancellationToken ct)
    {
        var consulta = _contexto.Turmas.AsNoTracking();

        if (anoLetivo is not null)
        {
            consulta = consulta.Where(t => t.AnoLetivo == anoLetivo);
        }

        // Papel diz o que a pessoa é; posse diz o que é dela. O professor via
        // as turmas dos colegas porque só o papel era checado.
        if (!User.EhAdmin())
        {
            if (User.ObterProfessorId() is int professorId)
            {
                consulta = consulta.Where(t => t.ProfessorId == professorId);
            }
            else if (User.ObterAlunoId() is int alunoId)
            {
                consulta = consulta.Where(t => t.Matriculas.Any(m => m.AlunoId == alunoId));
            }
            else
            {
                return Forbid();
            }
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
                t.Professor!.Nome,
                t.Professor!.Disciplina))
            .ToListAsync(ct);

        return Ok(turmas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TurmaResponse>> BuscarPorId(int id, CancellationToken ct)
    {
        var turma = await _contexto.Turmas
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                Turma = new TurmaResponse(
                    t.Id,
                    t.Nome,
                    t.AnoLetivo,
                    t.Capacidade,
                    t.Matriculas.Count(m => m.Situacao == SituacaoMatricula.Ativa),
                    t.Professor!.Nome,
                    t.Professor!.Disciplina),
                t.ProfessorId,
                EhMinha = t.Matriculas.Any(m => m.AlunoId == User.ObterAlunoId()),
            })
            .FirstOrDefaultAsync(ct);

        if (turma is null)
        {
            return NotFound();
        }

        var podeVer = User.EhAdmin()
                      || User.ObterProfessorId() == turma.ProfessorId
                      || turma.EhMinha;

        return podeVer ? Ok(turma.Turma) : Forbid();
    }

    /// <summary>Criar turma é ato de secretaria, não de professor.</summary>
    [HttpPost]
    [Authorize(Roles = nameof(PapelUsuario.Admin))]
    public async Task<ActionResult<TurmaResponse>> Criar(CriarTurmaRequest requisicao, CancellationToken ct)
    {
        // o professor precisa existir: sem esta checagem o erro viria do banco
        // como violação de chave estrangeira, com status 500 em vez de 400
        var professor = await _contexto.Professores.FindAsync([requisicao.ProfessorId], ct);
        if (professor is null)
        {
            ModelState.AddModelError(nameof(requisicao.ProfessorId), "Professor não encontrado");
            return ValidationProblem(ModelState);
        }

        var jaExiste = await _contexto.Turmas
            .AnyAsync(t => t.AnoLetivo == requisicao.AnoLetivo && t.Nome == requisicao.Nome, ct);
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
        await _contexto.SaveChangesAsync(ct);

        var resposta = new TurmaResponse(
            turma.Id, turma.Nome, turma.AnoLetivo, turma.Capacidade,
            0, professor.Nome, professor.Disciplina);

        return CreatedAtAction(nameof(BuscarPorId), new { id = turma.Id }, resposta);
    }
}
