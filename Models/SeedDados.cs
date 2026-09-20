using Microsoft.EntityFrameworkCore;

namespace SchoolApi.Models;

/// <summary>
/// Popula a base com uma escola pequena, mas completa o bastante para os
/// relatórios fazerem sentido: 4 professores, 4 turmas, 48 alunos, notas dos
/// bimestres fechados e 40 dias de chamada.
///
/// Determinístico de propósito (semente fixa no <see cref="Random"/>): o mesmo
/// banco zerado gera sempre os mesmos números, então um teste pode afirmar
/// "a média da turma X é 7,3" sem ficar instável.
///
/// Só roda quando a base está vazia — subir a API de novo não duplica nada.
/// </summary>
public static class SeedDados
{
    private const int Semente = 20260920;

    private static readonly string[] Nomes =
    [
        "Ana", "Bruno", "Carla", "Diego", "Elisa", "Felipe", "Gabriela", "Henrique",
        "Isabela", "João", "Karina", "Lucas", "Mariana", "Nicolas", "Olívia", "Pedro",
        "Rafaela", "Samuel", "Tatiana", "Vinícius", "Yasmin", "Eduardo", "Letícia", "Murilo",
    ];

    private static readonly string[] Sobrenomes =
    [
        "Silva", "Santos", "Oliveira", "Souza", "Pereira", "Costa", "Almeida", "Ferreira",
    ];

    public static async Task AplicarAsync(AppDbContext contexto, CancellationToken ct = default)
    {
        if (await contexto.Alunos.AnyAsync(ct))
        {
            return;
        }

        var agora = DateTime.UtcNow;
        var aleatorio = new Random(Semente);

        var professores = new List<Professor>
        {
            new() { Nome = "Marcos Ribeiro", Email = "marcos.ribeiro@escola.test", Disciplina = "Matemática", DataCriacao = agora },
            new() { Nome = "Patrícia Lima", Email = "patricia.lima@escola.test", Disciplina = "Português", DataCriacao = agora },
            new() { Nome = "Sérgio Tavares", Email = "sergio.tavares@escola.test", Disciplina = "História", DataCriacao = agora },
            new() { Nome = "Denise Fontes", Email = "denise.fontes@escola.test", Disciplina = "Biologia", DataCriacao = agora },
        };
        contexto.Professores.AddRange(professores);
        await contexto.SaveChangesAsync(ct);

        var turmas = new List<Turma>
        {
            new() { Nome = "1º ano A", AnoLetivo = 2026, Capacidade = 15, ProfessorId = professores[0].Id, DataCriacao = agora },
            new() { Nome = "1º ano B", AnoLetivo = 2026, Capacidade = 15, ProfessorId = professores[1].Id, DataCriacao = agora },
            new() { Nome = "2º ano A", AnoLetivo = 2026, Capacidade = 12, ProfessorId = professores[2].Id, DataCriacao = agora },
            new() { Nome = "3º ano A", AnoLetivo = 2026, Capacidade = 12, ProfessorId = professores[3].Id, DataCriacao = agora },
        };
        contexto.Turmas.AddRange(turmas);
        await contexto.SaveChangesAsync(ct);

        var alunos = new List<Aluno>();
        for (var i = 0; i < 48; i++)
        {
            var nome = $"{Nomes[i % Nomes.Length]} {Sobrenomes[(i / Nomes.Length + i) % Sobrenomes.Length]}";
            alunos.Add(new Aluno
            {
                Nome = nome,
                // sufixo numérico garante e-mail único mesmo com nomes repetidos
                Email = $"{nome.ToLowerInvariant().Replace(' ', '.').Replace("í", "i").Replace("ã", "a").Replace("ç", "c").Replace("é", "e")}{i + 1}@aluno.test",
                DataNascimento = new DateOnly(2008 + (i % 3), 1 + (i % 12), 1 + (i % 28)),
                DataCriacao = agora,
            });
        }
        contexto.Alunos.AddRange(alunos);
        await contexto.SaveChangesAsync(ct);

        // distribui os alunos pelas turmas sem estourar a capacidade
        var matriculas = new List<Matricula>();
        var indiceAluno = 0;
        foreach (var turma in turmas)
        {
            var quantidade = Math.Min(turma.Capacidade - 2, alunos.Count - indiceAluno);
            for (var i = 0; i < quantidade; i++)
            {
                matriculas.Add(new Matricula
                {
                    AlunoId = alunos[indiceAluno++].Id,
                    TurmaId = turma.Id,
                    DataMatricula = new DateOnly(2026, 2, 5),
                    Situacao = SituacaoMatricula.Ativa,
                });
            }
        }
        contexto.Matriculas.AddRange(matriculas);
        await contexto.SaveChangesAsync(ct);

        var notas = new List<Nota>();
        var presencas = new List<Presenca>();
        foreach (var matricula in matriculas)
        {
            // cada aluno tem um "nível" próprio, para as médias variarem de
            // forma plausível em vez de todo mundo ficar perto de 7
            var nivel = 4.5 + aleatorio.NextDouble() * 5.0;

            for (var bimestre = 1; bimestre <= 3; bimestre++)
            {
                var valor = Math.Clamp(nivel + (aleatorio.NextDouble() - 0.4) * 2.0, 0, 10);
                notas.Add(new Nota
                {
                    MatriculaId = matricula.Id,
                    Bimestre = bimestre,
                    Valor = Math.Round((decimal)valor, 2),
                    DataLancamento = agora.AddDays(-90 + bimestre * 25),
                });
            }

            // 40 dias letivos de chamada; quem vai mal falta mais
            var chanceFalta = 0.04 + (10 - nivel) * 0.012;
            for (var dia = 0; dia < 40; dia++)
            {
                presencas.Add(new Presenca
                {
                    MatriculaId = matricula.Id,
                    Data = new DateOnly(2026, 3, 2).AddDays(dia * 2),
                    Presente = aleatorio.NextDouble() > chanceFalta,
                });
            }
        }

        contexto.Notas.AddRange(notas);
        contexto.Presencas.AddRange(presencas);
        await contexto.SaveChangesAsync(ct);
    }
}
