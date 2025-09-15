using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplicationPods.Models
{
    [Flags]
    public enum DiasSemanaFlags
    {
        Nenhum = 0,
        Domingo = 1 << 0,
        Segunda = 1 << 1,
        Terca = 1 << 2,
        Quarta = 1 << 3,
        Quinta = 1 << 4,
        Sexta = 1 << 5,
        Sabado = 1 << 6,
        Todos = Domingo | Segunda | Terca | Quarta | Quinta | Sexta | Sabado
    }

    public class LojaConfig
    {
        public int Id { get; set; }

        // se sua app tem multi-loja, guarde o Id do usuário lojista
        public string? LojistaUserId { get; set; }

        [Required, StringLength(120)]
        public string Nome { get; set; } = "Minha Loja";

        // você pode usar um ou outro (url ou arquivo). Guardamos o caminho final.
        [StringLength(300)]
        public string? LogoPath { get; set; } // ex: /img/loja/noemi.png

        [Required]
        public TimeSpan HorarioAbertura { get; set; } = new(18, 0, 0); // 18:00

        [Required]
        public TimeSpan HorarioFechamento { get; set; } = new(23, 59, 0);

        [Required]
        public DiasSemanaFlags DiasAbertos { get; set; } = DiasSemanaFlags.Todos;

        public bool Ativo { get; set; } = true;          // loja ativa
        public bool ForcarFechado { get; set; } = false; // override manual
        [StringLength(160)]
        public string? MensagemStatus { get; set; }      // “voltamos às 19h” etc.

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
