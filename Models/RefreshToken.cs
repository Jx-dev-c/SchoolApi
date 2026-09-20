namespace SchoolApi.Models;

/// <summary>
/// Refresh token emitido para um usuário.
///
/// Guardamos o **hash** do token, não ele mesmo: se a base vazar, ninguém
/// consegue se passar por usuário com o que está lá — mesma razão de não
/// guardar senha em texto.
///
/// A rotação também é registrada (<see cref="SubstituidoPor"/>): cada uso gera
/// um token novo e invalida o antigo, então um token roubado e reutilizado
/// aparece como uso de token já substituído.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    public required string TokenHash { get; set; }

    public DateTime CriadoEm { get; set; }

    public DateTime ExpiraEm { get; set; }

    public DateTime? RevogadoEm { get; set; }

    /// <summary>Hash do token que substituiu este, quando houve rotação.</summary>
    public string? SubstituidoPor { get; set; }

    public bool Ativo => RevogadoEm is null && DateTime.UtcNow < ExpiraEm;
}
