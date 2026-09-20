using System.ComponentModel.DataAnnotations;

namespace SchoolApi.Auth;

/// <summary>
/// Configuração do JWT. A chave vem de variável de ambiente ou user-secrets —
/// nunca do appsettings versionado, que vai parar no GitHub.
/// </summary>
public class OpcoesJwt
{
    public const string Secao = "Jwt";

    [Required]
    public string Emissor { get; set; } = "SchoolApi";

    [Required]
    public string Audiencia { get; set; } = "school-app";

    /// <summary>
    /// Mínimo de 32 caracteres: HMAC-SHA256 usa chave de 256 bits, e chave
    /// curta é recusada pela própria biblioteca.
    /// </summary>
    [Required]
    [MinLength(32, ErrorMessage = "A chave do JWT precisa de pelo menos 32 caracteres")]
    public string Chave { get; set; } = string.Empty;

    /// <summary>
    /// Curto de propósito: enquanto o token vale, revogar acesso não tem efeito.
    /// Quem mantém a sessão viva é o refresh token, que dá para revogar.
    /// </summary>
    [Range(1, 120)]
    public int MinutosDoAccessToken { get; set; } = 15;

    [Range(1, 90)]
    public int DiasDoRefreshToken { get; set; } = 7;
}
