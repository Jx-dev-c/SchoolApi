namespace SchoolApi.Dtos;

/// <summary>
/// Uma linha do painel de desempenho por turma.
///
/// Tudo vem agregado do banco: média, frequência e contagem de alunos em risco
/// saem de um `GROUP BY`, não de somar no navegador. Com 46 matrículas daria
/// no mesmo; com 4 mil, a diferença é a tela abrir ou travar.
/// </summary>
public record DesempenhoDaTurmaResponse(
    int TurmaId,
    string Turma,
    string Professor,
    string Disciplina,
    int Alunos,
    decimal? MediaGeral,
    decimal? MenorMedia,
    decimal? MaiorMedia,
    double? FrequenciaPercentual,
    int AlunosEmRisco,
    int NotasLancadas);

/// <summary>Aluno com média abaixo do corte ou frequência insuficiente.</summary>
public record AlunoEmRiscoResponse(
    int MatriculaId,
    string Aluno,
    string Turma,
    decimal? Media,
    double? FrequenciaPercentual,
    string Motivo);
