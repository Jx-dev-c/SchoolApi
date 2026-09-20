using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SchoolApi.Dtos;
using SchoolApi.Models;

namespace SchoolApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{nameof(PapelUsuario.Admin)},{nameof(PapelUsuario.Professor)}")]
public class AlunosController : ControllerBase
{
    private readonly AppDbContext _contexto;

    public AlunosController(AppDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <summary>Lista alunos, paginado, com busca opcional por nome ou e-mail.</summary>
    /// <remarks>
    /// A contagem é uma consulta separada de propósito: trazer o total junto
    /// com a página exigiria uma janela sobre o resultado inteiro, e o banco
    /// resolve `count(*)` com índice muito mais barato.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<PaginaResponse<AlunoResponse>>> Listar(
        // o nome do parâmetro não pode ser `pagina`: o binder trataria a chave
        // `?pagina=` como prefixo e iria procurar `pagina.Pagina`, deixando
        // tudo no valor padrão
        [FromQuery] ParametrosDePagina parametros,
        [FromQuery] string? busca,
        CancellationToken ct)
    {
        var consulta = _contexto.Alunos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = $"%{busca.Trim()}%";
            consulta = consulta.Where(a => EF.Functions.ILike(a.Nome, termo) || EF.Functions.ILike(a.Email, termo));
        }

        var total = await consulta.CountAsync(ct);

        var itens = await consulta
            .OrderBy(a => a.Nome)
            .ThenBy(a => a.Id)
            .Skip(parametros.Pular)
            .Take(parametros.Tamanho)
            .Select(a => new AlunoResponse(a.Id, a.Nome, a.Email, a.DataNascimento, a.DataCriacao))
            .ToListAsync(ct);

        return Ok(new PaginaResponse<AlunoResponse>(itens, parametros.Pagina, parametros.Tamanho, total));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AlunoResponse>> BuscarPorId(int id, CancellationToken ct)
    {
        var aluno = await _contexto.Alunos
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AlunoResponse(a.Id, a.Nome, a.Email, a.DataNascimento, a.DataCriacao))
            .FirstOrDefaultAsync(ct);

        return aluno is null ? NotFound() : Ok(aluno);
    }

    [HttpPost]
    [Authorize(Roles = nameof(PapelUsuario.Admin))]
    public async Task<ActionResult<AlunoResponse>> Criar(CriarAlunoRequest requisicao, CancellationToken ct)
    {
        if (await _contexto.Alunos.AnyAsync(a => a.Email == requisicao.Email, ct))
        {
            ModelState.AddModelError(nameof(requisicao.Email), "Já existe aluno com esse e-mail");
            return ValidationProblem(ModelState);
        }

        var aluno = new Aluno
        {
            Nome = requisicao.Nome,
            Email = requisicao.Email,
            DataNascimento = requisicao.DataNascimento,
            DataCriacao = DateTime.UtcNow,
        };

        _contexto.Alunos.Add(aluno);
        await _contexto.SaveChangesAsync(ct);

        var resposta = new AlunoResponse(aluno.Id, aluno.Nome, aluno.Email, aluno.DataNascimento, aluno.DataCriacao);
        return CreatedAtAction(nameof(BuscarPorId), new { id = aluno.Id }, resposta);
    }
}
