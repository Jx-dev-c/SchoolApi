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
public class MatriculasController : ControllerBase
{
    private readonly AppDbContext _contexto;

    public MatriculasController(AppDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <summary>Matricula um aluno numa turma, respeitando a capacidade.</summary>
    /// <remarks>
    /// Contar as matrículas e depois inserir é corrida: duas requisições
    /// simultâneas leem "13 de 15" e ambas inserem, estourando a turma. Aqui a
    /// linha da turma é travada com `FOR UPDATE` antes da contagem, então a
    /// segunda requisição espera a primeira terminar e enxerga o total correto.
    ///
    /// É a mesma lição da rotação de refresh token: quando a decisão depende do
    /// estado atual, quem garante é o banco, não a aplicação.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = nameof(PapelUsuario.Admin))]
    public async Task<ActionResult<MatriculaResponse>> Criar(CriarMatriculaRequest requisicao, CancellationToken ct)
    {
        await using var transacao = await _contexto.Database.BeginTransactionAsync(ct);

        var turma = await _contexto.Turmas
            .FromSql($"SELECT * FROM \"Turmas\" WHERE \"Id\" = {requisicao.TurmaId} FOR UPDATE")
            .FirstOrDefaultAsync(ct);

        if (turma is null)
        {
            ModelState.AddModelError(nameof(requisicao.TurmaId), "Turma não encontrada");
            return ValidationProblem(ModelState);
        }

        var aluno = await _contexto.Alunos.FindAsync([requisicao.AlunoId], ct);
        if (aluno is null)
        {
            ModelState.AddModelError(nameof(requisicao.AlunoId), "Aluno não encontrado");
            return ValidationProblem(ModelState);
        }

        var jaMatriculado = await _contexto.Matriculas
            .AnyAsync(m => m.AlunoId == requisicao.AlunoId && m.TurmaId == requisicao.TurmaId, ct);
        if (jaMatriculado)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Aluno já matriculado nesta turma",
                Status = StatusCodes.Status409Conflict,
            });
        }

        var ocupadas = await _contexto.Matriculas
            .CountAsync(m => m.TurmaId == turma.Id && m.Situacao == SituacaoMatricula.Ativa, ct);

        if (ocupadas >= turma.Capacidade)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Turma sem vagas",
                Detail = $"A turma {turma.Nome} tem {turma.Capacidade} vagas e todas estão ocupadas.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        var matricula = new Matricula
        {
            AlunoId = requisicao.AlunoId,
            TurmaId = requisicao.TurmaId,
            DataMatricula = DateOnly.FromDateTime(DateTime.UtcNow),
            Situacao = SituacaoMatricula.Ativa,
        };

        _contexto.Matriculas.Add(matricula);
        await _contexto.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        return CreatedAtAction(
            nameof(BuscarBoletim),
            new { id = matricula.Id },
            new MatriculaResponse(matricula.Id, aluno.Id, aluno.Nome, turma.Id, turma.Nome, matricula.DataMatricula, matricula.Situacao));
    }

    /// <summary>Boletim da matrícula: notas, média e frequência.</summary>
    /// <remarks>
    /// Quem pode ver: o próprio aluno, o professor daquela turma e o admin.
    /// Sem a checagem de posse, qualquer pessoa autenticada leria a nota de
    /// qualquer aluno só trocando o id na URL.
    /// </remarks>
    [HttpGet("{id:int}/boletim")]
    public async Task<ActionResult<BoletimResponse>> BuscarBoletim(int id, CancellationToken ct)
    {
        var dados = await _contexto.Matriculas
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new
            {
                m.Id,
                m.AlunoId,
                AlunoNome = m.Aluno!.Nome,
                TurmaNome = m.Turma!.Nome,
                m.Turma!.ProfessorId,
                Notas = m.Notas
                    .OrderBy(n => n.Bimestre)
                    .Select(n => new NotaResponse(n.Bimestre, n.Valor, n.DataLancamento))
                    .ToList(),
                Presencas = m.Presencas.Count(),
                Comparecimentos = m.Presencas.Count(p => p.Presente),
            })
            .FirstOrDefaultAsync(ct);

        if (dados is null)
        {
            return NotFound();
        }

        if (!PodeVer(dados.AlunoId, dados.ProfessorId))
        {
            return Forbid();
        }

        decimal? media = dados.Notas.Count > 0
            ? Math.Round(dados.Notas.Sum(n => n.Valor) / dados.Notas.Count, 2)
            : null;

        double? frequencia = dados.Presencas > 0
            ? Math.Round(100.0 * dados.Comparecimentos / dados.Presencas, 1)
            : null;

        return Ok(new BoletimResponse(dados.Id, dados.AlunoNome, dados.TurmaNome, dados.Notas, media, frequencia));
    }

    /// <summary>Lança ou corrige a nota de um bimestre.</summary>
    /// <remarks>
    /// O banco garante uma nota por bimestre; aqui a operação é "criar ou
    /// atualizar", para o cliente não precisar saber se já existe.
    /// </remarks>
    [HttpPut("{id:int}/notas")]
    [Authorize(Roles = $"{nameof(PapelUsuario.Admin)},{nameof(PapelUsuario.Professor)}")]
    public async Task<ActionResult<NotaResponse>> LancarNota(int id, LancarNotaRequest requisicao, CancellationToken ct)
    {
        var matricula = await _contexto.Matriculas
            .Include(m => m.Turma)
            .Include(m => m.Notas.Where(n => n.Bimestre == requisicao.Bimestre))
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (matricula is null)
        {
            return NotFound();
        }

        // professor só lança nota na própria turma
        if (!User.EhAdmin() && User.ObterProfessorId() != matricula.Turma!.ProfessorId)
        {
            return Forbid();
        }

        if (matricula.Situacao != SituacaoMatricula.Ativa)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Matrícula não está ativa",
                Status = StatusCodes.Status409Conflict,
            });
        }

        var nota = matricula.Notas.FirstOrDefault();
        if (nota is null)
        {
            nota = new Nota { MatriculaId = matricula.Id, Bimestre = requisicao.Bimestre };
            _contexto.Notas.Add(nota);
        }

        nota.Valor = requisicao.Valor;
        nota.DataLancamento = DateTime.UtcNow;

        await _contexto.SaveChangesAsync(ct);

        return Ok(new NotaResponse(nota.Bimestre, nota.Valor, nota.DataLancamento));
    }

    private bool PodeVer(int alunoId, int professorId) =>
        User.EhAdmin()
        || User.ObterAlunoId() == alunoId
        || User.ObterProfessorId() == professorId;
}
