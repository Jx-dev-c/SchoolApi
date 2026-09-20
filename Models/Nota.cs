namespace SchoolApi.Models;

/// <summary>
/// Nota de um bimestre dentro de uma matrícula.
///
/// `decimal` e não `double`: nota é valor exato, e média de ponto flutuante
/// dá resultado que muda com a ordem da soma. No banco fica `numeric(4,2)`.
/// </summary>
public class Nota
{
    public int Id { get; set; }

    public int MatriculaId { get; set; }

    public Matricula? Matricula { get; set; }

    /// <summary>1 a 4. Cada bimestre aparece no máximo uma vez por matrícula.</summary>
    public int Bimestre { get; set; }

    /// <summary>De 0 a 10, com duas casas.</summary>
    public decimal Valor { get; set; }

    public DateTime DataLancamento { get; set; }
}
