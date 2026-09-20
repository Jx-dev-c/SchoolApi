namespace SchoolApi.Models;

/// <summary>
/// Professor responsável por turmas. Substitui o antigo campo `Cargo` em texto
/// livre: quem ensina é uma entidade, com turmas ligadas a ele.
/// </summary>
public class Professor
{
    public int Id { get; set; }

    public required string Nome { get; set; }

    public required string Email { get; set; }

    /// <summary>Área de atuação (ex.: "Matemática").</summary>
    public required string Disciplina { get; set; }

    public DateTime DataCriacao { get; set; }

    public ICollection<Turma> Turmas { get; set; } = [];
}
