using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using SchoolApi.Models;

namespace SchoolApi.Auth;

/// <summary>Emite access token (JWT) e refresh token.</summary>
public class ServicoDeToken
{
    private readonly OpcoesJwt _opcoes;

    public ServicoDeToken(IOptions<OpcoesJwt> opcoes)
    {
        _opcoes = opcoes.Value;
    }

    public (string Token, DateTime ExpiraEm) GerarAccessToken(Usuario usuario)
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.Chave));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);
        var expiraEm = DateTime.UtcNow.AddMinutes(_opcoes.MinutosDoAccessToken);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, usuario.Nome),
            new(ClaimTypes.Role, usuario.Papel.ToString()),
        };

        // o vínculo com a pessoa do domínio viaja no token: é o que permite
        // checar posse ("esta turma é sua?") sem ir ao banco buscar quem é quem
        if (usuario.ProfessorId is int professorId)
        {
            claims.Add(new Claim(ClaimsDoUsuario.ProfessorId, professorId.ToString()));
        }

        if (usuario.AlunoId is int alunoId)
        {
            claims.Add(new Claim(ClaimsDoUsuario.AlunoId, alunoId.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _opcoes.Emissor,
            audience: _opcoes.Audiencia,
            claims: claims,
            expires: expiraEm,
            signingCredentials: credenciais);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
    }

    /// <summary>
    /// Gera o refresh token e o hash que vai para o banco. O valor em claro só
    /// existe nesta resposta; depois dela, nem o banco sabe qual era.
    /// </summary>
    public (string Token, string Hash, DateTime ExpiraEm) GerarRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes);
        return (token, CalcularHash(token), DateTime.UtcNow.AddDays(_opcoes.DiasDoRefreshToken));
    }

    /// <summary>
    /// SHA-256 direto, sem sal, é adequado aqui (e não para senha): o token tem
    /// 64 bytes aleatórios, então não há dicionário nem força bruta viável, e a
    /// busca no banco precisa ser por igualdade.
    /// </summary>
    public static string CalcularHash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}

public static class ClaimsDoUsuario
{
    public const string ProfessorId = "professor_id";
    public const string AlunoId = "aluno_id";
}
