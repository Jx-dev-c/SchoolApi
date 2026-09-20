namespace SchoolApi.Models;

public enum SituacaoMatricula
{
    Ativa = 0,
    Trancada = 1,
    Concluida = 2,
    Cancelada = 3,
}

/// <summary>
/// Liga aluno e turma — é a tabela de junção do muitos-para-muitos, mas com
/// dados próprios (data, situação), por isso é entidade explícita e não uma
/// relação implícita do EF.
///
/// Notas e presenças penduram aqui, e não no aluno: uma nota só faz sentido
/// dentro de uma turma específica.
/// </summary>
public class Matricula
{
    public int Id { get; set; }

    public int AlunoId { get; set; }

    public Aluno? Aluno { get; set; }

    public int TurmaId { get; set; }

    public Turma? Turma { get; set; }

    public DateOnly DataMatricula { get; set; }

    public SituacaoMatricula Situacao { get; set; } = SituacaoMatricula.Ativa;

    public ICollection<Nota> Notas { get; set; } = [];

    public ICollection<Presenca> Presencas { get; set; } = [];
}
