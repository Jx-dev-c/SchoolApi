namespace SchoolApi.Models;

/// <summary>
/// Turma de um ano letivo, conduzida por um professor. A capacidade é regra de
/// negócio de verdade: matricular além dela é recusado no serviço.
/// </summary>
public class Turma
{
    public int Id { get; set; }

    public required string Nome { get; set; }

    public int AnoLetivo { get; set; }

    public int Capacidade { get; set; }

    public int ProfessorId { get; set; }

    public Professor? Professor { get; set; }

    public DateTime DataCriacao { get; set; }

    public ICollection<Matricula> Matriculas { get; set; } = [];
}
