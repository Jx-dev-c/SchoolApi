namespace SchoolApi.Models;

/// <summary>
/// Aluno da instituição. É uma pessoa do domínio, não uma conta de acesso:
/// login e senha ficam em <see cref="Usuario"/>, que pode apontar para um aluno.
/// </summary>
public class Aluno
{
    public int Id { get; set; }

    public required string Nome { get; set; }

    /// <summary>Único na base — há índice único configurado no AppDbContext.</summary>
    public required string Email { get; set; }

    public DateOnly DataNascimento { get; set; }

    public DateTime DataCriacao { get; set; }

    /// <summary>Turmas em que o aluno está (ou esteve) matriculado.</summary>
    public ICollection<Matricula> Matriculas { get; set; } = [];
}
