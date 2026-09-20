using System.Text.Json.Serialization;

namespace SchoolApi.Models;

public enum PapelUsuario
{
    Admin = 0,
    Professor = 1,
    Aluno = 2,
}

/// <summary>
/// Conta de acesso. É separada de <see cref="Aluno"/> e <see cref="Professor"/>
/// de propósito: pessoa do domínio existe mesmo sem login (aluno cadastrado que
/// nunca acessou o sistema), e um dia a mesma pessoa pode ter mais de um papel.
///
/// O vínculo é opcional e é o que sustenta a autorização por posse: o professor
/// só mexe nas turmas ligadas ao <see cref="ProfessorId"/> dele.
/// </summary>
public class Usuario
{
    public int Id { get; set; }

    public required string Nome { get; set; }

    public required string Email { get; set; }

    /// <summary>
    /// Hash PBKDF2 gerado pelo PasswordHasher. Nunca sai numa resposta HTTP —
    /// o atributo abaixo é cinto de segurança para o caso de alguém serializar
    /// a entidade por engano em vez do DTO.
    /// </summary>
    [JsonIgnore]
    public required string SenhaHash { get; set; }

    public PapelUsuario Papel { get; set; }

    public int? ProfessorId { get; set; }

    public Professor? Professor { get; set; }

    public int? AlunoId { get; set; }

    public Aluno? Aluno { get; set; }

    public bool Ativo { get; set; } = true;

    public DateTime DataCriacao { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
