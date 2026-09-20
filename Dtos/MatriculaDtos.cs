using System.ComponentModel.DataAnnotations;

using SchoolApi.Models;

namespace SchoolApi.Dtos;

public record CriarMatriculaRequest(
    [Range(1, int.MaxValue)] int AlunoId,
    [Range(1, int.MaxValue)] int TurmaId);

public record MatriculaResponse(
    int Id,
    int AlunoId,
    string AlunoNome,
    int TurmaId,
    string TurmaNome,
    DateOnly DataMatricula,
    SituacaoMatricula Situacao);

/// <summary>
/// Lançar nota é sempre "criar ou atualizar" o bimestre: o banco garante uma
/// nota por bimestre, então o cliente não precisa saber se já existe.
/// </summary>
public record LancarNotaRequest(
    [Range(1, 4, ErrorMessage = "Bimestre deve ser de 1 a 4")]
    int Bimestre,
    [Range(0, 10, ErrorMessage = "Nota deve ficar entre 0 e 10")]
    decimal Valor);

public record NotaResponse(int Bimestre, decimal Valor, DateTime DataLancamento);

/// <summary>Boletim de uma matrícula: o que a tela de relatórios vai consumir.</summary>
public record BoletimResponse(
    int MatriculaId,
    string AlunoNome,
    string TurmaNome,
    IReadOnlyList<NotaResponse> Notas,
    decimal? Media,
    double? FrequenciaPercentual);
