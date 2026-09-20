using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SchoolApi.Auth;
using SchoolApi.Dtos;
using SchoolApi.Models;

namespace SchoolApi.Controllers;

/// <summary>
/// Números agregados para o painel. Só admin e professor — e o professor vê
/// apenas as turmas dele, pela mesma regra de posse do resto da API.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{nameof(PapelUsuario.Admin)},{nameof(PapelUsuario.Professor)}")]
public class RelatoriosController : ControllerBase
{
    /// <summary>Média abaixo disto conta como risco de reprovação.</summary>
    private const decimal MediaDeCorte = 6.0m;

    /// <summary>Frequência mínima exigida por lei no ensino básico.</summary>
    private const double FrequenciaMinima = 75.0;

    private readonly AppDbContext _contexto;

    public RelatoriosController(AppDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <summary>Desempenho de cada turma: média, extremos, frequência e risco.</summary>
    [HttpGet("turmas")]
    public async Task<ActionResult<IReadOnlyList<DesempenhoDaTurmaResponse>>> DesempenhoPorTurma(
        [FromQuery] int? anoLetivo,
        CancellationToken ct)
    {
        var turmas = TurmasVisiveis(anoLetivo);

        var linhas = await turmas
            .Select(t => new
            {
                t.Id,
                t.Nome,
                Professor = t.Professor!.Nome,
                t.Professor!.Disciplina,
                Matriculas = t.Matriculas
                    .Where(m => m.Situacao == SituacaoMatricula.Ativa)
                    .Select(m => new
                    {
                        // média por aluno primeiro: a média das médias é o que a
                        // escola chama de "média da turma", e não a média de
                        // todas as notas soltas (que pesaria mais quem tem mais
                        // notas lançadas)
                        Media = m.Notas.Any() ? (decimal?)m.Notas.Average(n => n.Valor) : null,
                        Notas = m.Notas.Count(),
                        Presencas = m.Presencas.Count(),
                        Comparecimentos = m.Presencas.Count(p => p.Presente),
                    })
                    .ToList(),
            })
            .ToListAsync(ct);

        var resposta = linhas.Select(t =>
        {
            var comMedia = t.Matriculas.Where(m => m.Media is not null).Select(m => m.Media!.Value).ToList();
            var presencas = t.Matriculas.Sum(m => m.Presencas);
            var comparecimentos = t.Matriculas.Sum(m => m.Comparecimentos);

            var emRisco = t.Matriculas.Count(m =>
                (m.Media is not null && m.Media < MediaDeCorte)
                || (m.Presencas > 0 && 100.0 * m.Comparecimentos / m.Presencas < FrequenciaMinima));

            return new DesempenhoDaTurmaResponse(
                t.Id,
                t.Nome,
                t.Professor,
                t.Disciplina,
                t.Matriculas.Count,
                comMedia.Count > 0 ? Math.Round(comMedia.Average(), 2) : null,
                comMedia.Count > 0 ? Math.Round(comMedia.Min(), 2) : null,
                comMedia.Count > 0 ? Math.Round(comMedia.Max(), 2) : null,
                presencas > 0 ? Math.Round(100.0 * comparecimentos / presencas, 1) : null,
                emRisco,
                t.Matriculas.Sum(m => m.Notas));
        }).ToList();

        return Ok(resposta);
    }

    /// <summary>Quem está abaixo da média de corte ou da frequência mínima.</summary>
    [HttpGet("alunos-em-risco")]
    public async Task<ActionResult<IReadOnlyList<AlunoEmRiscoResponse>>> AlunosEmRisco(
        [FromQuery] int? anoLetivo,
        CancellationToken ct)
    {
        var turmasVisiveis = TurmasVisiveis(anoLetivo).Select(t => t.Id);

        var matriculas = await _contexto.Matriculas
            .AsNoTracking()
            .Where(m => m.Situacao == SituacaoMatricula.Ativa && turmasVisiveis.Contains(m.TurmaId))
            .Select(m => new
            {
                m.Id,
                Aluno = m.Aluno!.Nome,
                Turma = m.Turma!.Nome,
                Media = m.Notas.Any() ? (decimal?)m.Notas.Average(n => n.Valor) : null,
                Presencas = m.Presencas.Count(),
                Comparecimentos = m.Presencas.Count(p => p.Presente),
            })
            .ToListAsync(ct);

        var emRisco = matriculas
            .Select(m => new
            {
                m.Id,
                m.Aluno,
                m.Turma,
                Media = m.Media is null ? null : (decimal?)Math.Round(m.Media.Value, 2),
                Frequencia = m.Presencas > 0 ? (double?)Math.Round(100.0 * m.Comparecimentos / m.Presencas, 1) : null,
            })
            .Select(m => new
            {
                m.Id,
                m.Aluno,
                m.Turma,
                m.Media,
                m.Frequencia,
                Motivo = Motivo(m.Media, m.Frequencia),
            })
            .Where(m => m.Motivo is not null)
            .OrderBy(m => m.Media ?? 0)
            .Select(m => new AlunoEmRiscoResponse(m.Id, m.Aluno, m.Turma, m.Media, m.Frequencia, m.Motivo!))
            .ToList();

        return Ok(emRisco);
    }

    private IQueryable<Turma> TurmasVisiveis(int? anoLetivo)
    {
        var consulta = _contexto.Turmas.AsNoTracking();

        if (anoLetivo is not null)
        {
            consulta = consulta.Where(t => t.AnoLetivo == anoLetivo);
        }

        if (!User.EhAdmin() && User.ObterProfessorId() is int professorId)
        {
            consulta = consulta.Where(t => t.ProfessorId == professorId);
        }

        return consulta;
    }

    private static string? Motivo(decimal? media, double? frequencia)
    {
        var abaixoDaMedia = media is not null && media < MediaDeCorte;
        var faltando = frequencia is not null && frequencia < FrequenciaMinima;

        return (abaixoDaMedia, faltando) switch
        {
            (true, true) => "Média e frequência abaixo do mínimo",
            (true, false) => "Média abaixo de 6,0",
            (false, true) => "Frequência abaixo de 75%",
            _ => null,
        };
    }
}
