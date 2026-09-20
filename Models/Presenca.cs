namespace SchoolApi.Models;

/// <summary>
/// Chamada de um dia letivo. Uma linha por matrícula e dia — é o que permite
/// calcular frequência por aluno e por turma sem guardar total agregado, que
/// ficaria desatualizado à primeira correção.
/// </summary>
public class Presenca
{
    public int Id { get; set; }

    public int MatriculaId { get; set; }

    public Matricula? Matricula { get; set; }

    public DateOnly Data { get; set; }

    public bool Presente { get; set; }
}
